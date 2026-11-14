using System.Text.Json;
using LeadDelivery.Domain.Entities;
using LeadDelivery.Domain.Enums;
using LeadDelivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LeadDelivery.Infrastructure.Services;

public sealed class FieldCatalogService
{
    private readonly LeadDeliveryDbContext _db;

    public FieldCatalogService(LeadDeliveryDbContext db) => _db = db;

    public async Task SyncColumnMetadataAsync(CancellationToken ct)
    {
        var leadEntity = _db.Model.FindEntityType(typeof(Lead));
        if (leadEntity is null) return;

        foreach (var prop in leadEntity.GetProperties())
        {
            if (prop.Name is nameof(Lead.LeadDocumentJson) or nameof(Lead.RawPayload)) continue;

            var key = $"lead.{ToCamel(prop.Name)}";
            if (await _db.FieldCatalogEntries.AnyAsync(e => e.InstitutionId == null && e.Key == key, ct))
            {
                continue;
            }

            _db.FieldCatalogEntries.Add(new FieldCatalogEntry
            {
                Id = Guid.NewGuid(),
                Key = key,
                DisplayName = prop.Name,
                DataType = MapClrType(prop.ClrType),
                Path = key,
                Source = FieldCatalogSource.Column,
                ParentKey = "lead"
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task InferFromLeadDocumentAsync(Guid institutionId, string leadDocumentJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(leadDocumentJson);
        await WalkJson(doc.RootElement, institutionId, "lead", ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FieldCatalogEntry>> GetCatalogAsync(Guid? institutionId, string? search, CancellationToken ct)
    {
        var q = _db.FieldCatalogEntries.AsNoTracking()
            .Where(e => e.DeprecatedAt == null);

        if (institutionId.HasValue)
        {
            q = q.Where(e => e.InstitutionId == null || e.InstitutionId == institutionId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(e => e.Key.Contains(term) || e.DisplayName.Contains(term));
        }

        return await q.OrderBy(e => e.Key).ToListAsync(ct);
    }

    public async Task ApproveInferenceAsync(Guid inferenceId, CancellationToken ct)
    {
        var inf = await _db.FieldCatalogInferences.FirstOrDefaultAsync(i => i.Id == inferenceId, ct)
            ?? throw new KeyNotFoundException("Inference not found");

        inf.IsApproved = true;
        _db.FieldCatalogEntries.Add(new FieldCatalogEntry
        {
            Id = Guid.NewGuid(),
            InstitutionId = inf.InstitutionId,
            Key = inf.SuggestedKey,
            DisplayName = inf.SuggestedKey.Split('.').Last(),
            DataType = inf.DataType,
            Path = inf.SuggestedKey,
            Source = FieldCatalogSource.JsonInferred
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task WalkJson(JsonElement element, Guid institutionId, string prefix, CancellationToken ct)
    {
        if (element.ValueKind != JsonValueKind.Object) return;

        foreach (var prop in element.EnumerateObject())
        {
            var key = $"{prefix}.{ToCamel(prop.Name)}";
            var exists = await _db.FieldCatalogEntries.AnyAsync(
                e => e.Key == key && (e.InstitutionId == null || e.InstitutionId == institutionId), ct);
            var pending = await _db.FieldCatalogInferences.AnyAsync(
                i => i.SuggestedKey == key && i.InstitutionId == institutionId && !i.IsIgnored, ct);

            if (!exists && !pending)
            {
                _db.FieldCatalogInferences.Add(new FieldCatalogInference
                {
                    Id = Guid.NewGuid(),
                    InstitutionId = institutionId,
                    SuggestedKey = key,
                    DataType = prop.Value.ValueKind.ToString(),
                    SampleValueRedacted = "(redacted)"
                });
            }

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                await WalkJson(prop.Value, institutionId, key, ct);
            }
        }
    }

    private static string ToCamel(string name) =>
        char.ToLowerInvariant(name[0]) + name[1..];

    private static string MapClrType(Type type)
    {
        var u = Nullable.GetUnderlyingType(type) ?? type;
        return u.Name switch
        {
            nameof(String) => "string",
            nameof(Guid) => "string",
            nameof(Int32) => "number",
            nameof(Boolean) => "boolean",
            nameof(DateTimeOffset) => "datetime",
            _ => "string"
        };
    }
}