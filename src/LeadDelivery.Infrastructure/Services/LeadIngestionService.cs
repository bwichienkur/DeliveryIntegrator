using System.Text.Json;
using LeadDelivery.Application.Models;
using LeadDelivery.Domain.Entities;
using LeadDelivery.Domain.Enums;
using LeadDelivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeadDelivery.Infrastructure.Services;

public sealed class LeadIngestionService
{
    private readonly LeadDeliveryDbContext _db;
    private readonly LeadDocumentService _documents;
    private readonly FieldCatalogService _catalog;

    public LeadIngestionService(LeadDeliveryDbContext db, LeadDocumentService documents, FieldCatalogService catalog)
    {
        _db = db;
        _documents = documents;
        _catalog = catalog;
    }

    public async Task<IngestLeadResponse> IngestAsync(IngestLeadRequest request, string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(i => i.Key == idempotencyKey, ct);
            if (existing is not null)
            {
                return new IngestLeadResponse
                {
                    LeadId = existing.LeadId,
                    DeliveryRunId = existing.DeliveryRunId ?? Guid.Empty,
                    Status = "Duplicate"
                };
            }
        }

        var institution = await ResolveInstitutionAsync(request, ct)
            ?? throw new InvalidOperationException("Institution not found");

        var payload = ResolvePayload(request);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            InstitutionId = institution.Id,
            RawPayload = payload.GetRawText(),
            Email = TryGetString(payload, "email"),
            Campus = TryGetString(payload, "campus"),
            Program = TryGetString(payload, "program"),
            SourceSite = TryGetString(payload, "sourceSite"),
            ExternalSourceId = TryGetString(payload, "externalId")
        };

        lead.LeadDocumentJson = _documents.BuildDocument(lead, payload);
        _db.Leads.Add(lead);

        var run = new DeliveryRun
        {
            Id = Guid.NewGuid(),
            LeadId = lead.Id,
            InstitutionId = institution.Id,
            Status = DeliveryRunStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
        _db.DeliveryRuns.Add(run);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "ProcessDeliveryRun",
            PayloadJson = JsonSerializer.Serialize(new { deliveryRunId = run.Id }),
            Status = OutboxMessageStatus.Pending
        });

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = idempotencyKey,
                LeadId = lead.Id,
                DeliveryRunId = run.Id
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _catalog.InferFromLeadDocumentAsync(institution.Id, lead.LeadDocumentJson, ct);

        return new IngestLeadResponse { LeadId = lead.Id, DeliveryRunId = run.Id, Status = "Accepted" };
    }

    private async Task<Institution?> ResolveInstitutionAsync(IngestLeadRequest request, CancellationToken ct)
    {
        if (request.InstitutionId.HasValue)
        {
            return await _db.Institutions.FirstOrDefaultAsync(i => i.Id == request.InstitutionId, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.InstitutionCode))
        {
            return await _db.Institutions.FirstOrDefaultAsync(i => i.Code == request.InstitutionCode, ct);
        }

        return await _db.Institutions.OrderBy(i => i.Code).FirstOrDefaultAsync(ct);
    }

    private static JsonElement ResolvePayload(IngestLeadRequest request)
    {
        if (request.Payload is { } p && p.ValueKind != JsonValueKind.Undefined)
        {
            return p;
        }

        if (request.ExtensionData is not null)
        {
            var filtered = new Dictionary<string, JsonElement>(request.ExtensionData);
            filtered.Remove("institutionCode");
            filtered.Remove("institutionId");
            return JsonSerializer.SerializeToElement(filtered);
        }

        return JsonSerializer.SerializeToElement(new { });
    }

    private static string? TryGetString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }
}