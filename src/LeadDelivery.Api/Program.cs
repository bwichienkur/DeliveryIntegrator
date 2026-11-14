using System.Text.Json;
using LeadDelivery.Application.Models;
using LeadDelivery.Domain.Enums;
using FieldCatalogEntry = LeadDelivery.Domain.Entities.FieldCatalogEntry;
using LeadDelivery.Infrastructure;
using LeadDelivery.Infrastructure.Persistence;
using LeadDelivery.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddLeadDeliveryInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/api/v1/leads", async (
    HttpRequest http,
    [FromBody] IngestLeadRequest request,
    LeadIngestionService ingestion,
    CancellationToken ct) =>
{
    var idempotency = http.Headers["Idempotency-Key"].FirstOrDefault();
    var result = await ingestion.IngestAsync(request, idempotency, ct);
    return Results.Accepted($"/api/v1/leads/{result.LeadId}", result);
});

app.MapGet("/api/v1/leads/{id:guid}", async (Guid id, LeadDeliveryDbContext db, CancellationToken ct) =>
{
    var lead = await db.Leads.AsNoTracking().Include(l => l.DeliveryRuns).FirstOrDefaultAsync(l => l.Id == id, ct);
    return lead is null ? Results.NotFound() : Results.Ok(lead);
});

app.MapGet("/api/v1/field-catalog", async (
    [FromQuery] Guid? institutionId,
    [FromQuery] string? search,
    FieldCatalogService catalog,
    CancellationToken ct) =>
{
    var entries = await catalog.GetCatalogAsync(institutionId, search, ct);
    return Results.Ok(new { items = entries, tree = BuildTree(entries) });
});

app.MapPost("/api/v1/field-catalog/refresh", async (
    HttpRequest http,
    FieldCatalogService catalog,
    LeadDeliveryDbContext db,
    CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Results.Unauthorized();
    await catalog.SyncColumnMetadataAsync(ct);

    var pending = await db.FieldCatalogInferences
        .Where(i => !i.IsApproved && !i.IsIgnored)
        .Take(100)
        .ToListAsync(ct);

    return Results.Ok(new { synced = true, pendingCount = pending.Count, pending });
});

app.MapPost("/api/v1/field-catalog/inferences/{id:guid}/approve", async (
    Guid id,
    HttpRequest http,
    FieldCatalogService catalog,
    CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Results.Unauthorized();
    await catalog.ApproveInferenceAsync(id, ct);
    return Results.Ok();
});

app.MapPost("/api/v1/simulate/match", async (
    [FromBody] SimulateMatchRequest body,
    RuleMatchingService rules,
    CancellationToken ct) =>
{
    var matched = await rules.SimulateMatchAsync(body.RulesJson, body.LeadDocumentJson, ct);
    return Results.Ok(new { matched });
});

app.MapPost("/api/v1/simulate/enrichment", async (
    [FromBody] SimulateEnrichmentRequest body,
    EnrichmentPipelineService enrichment,
    CancellationToken ct) =>
{
    var result = await enrichment.RunAsync(body.InstitutionId, body.LeadDocumentJson, body.PipelineJson, ct);
    return Results.Ok(new { enrichedLeadDocumentJson = result });
});

app.MapPost("/api/v1/simulate/transform", ([FromBody] SimulateTransformRequest body) =>
{
    return Results.Ok(new { rendered = body.Template, note = "Use enrichment or endpoint templates at runtime." });
});

app.MapGet("/api/v1/delivery-runs", async (
    [FromQuery] Guid? institutionId,
    [FromQuery] string? status,
    LeadDeliveryDbContext db,
    CancellationToken ct) =>
{
    var q = db.DeliveryRuns.AsNoTracking().Include(r => r.Attempts).AsQueryable();
    if (institutionId.HasValue) q = q.Where(r => r.InstitutionId == institutionId);
    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeliveryRunStatus>(status, true, out var st))
    {
        q = q.Where(r => r.Status == st);
    }

    var runs = await q.OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
    return Results.Ok(runs);
});

app.MapGet("/api/v1/delivery-runs/{id:guid}/timeline", async (Guid id, LeadDeliveryDbContext db, CancellationToken ct) =>
{
    var run = await db.DeliveryRuns.AsNoTracking()
        .Include(r => r.Attempts)
        .Include(r => r.Lead)
        .FirstOrDefaultAsync(r => r.Id == id, ct);
    return run is null ? Results.NotFound() : Results.Ok(run);
});

app.MapPost("/api/v1/delivery-runs/{id:guid}/retry", async (
    Guid id,
    [FromBody] RetryRequest? body,
    HttpRequest http,
    DeliveryOrchestrator orchestrator,
    CancellationToken ct) =>
{
    if (!IsAdmin(http)) return Results.Unauthorized();
    await orchestrator.RetryFailedAsync(id, body?.EndpointIds, body?.UseLatestConfig ?? false, ct);
    return Results.Ok();
});

app.MapGet("/api/v1/admin/institutions", async (LeadDeliveryDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Institutions.AsNoTracking().ToListAsync(ct)));

app.MapGet("/api/v1/admin/configurations", async (
    [FromQuery] Guid? institutionId,
    LeadDeliveryDbContext db,
    CancellationToken ct) =>
{
    var q = db.DeliveryConfigurations.AsNoTracking().Include(c => c.Endpoints).AsQueryable();
    if (institutionId.HasValue) q = q.Where(c => c.InstitutionId == institutionId);
    return Results.Ok(await q.ToListAsync(ct));
});

app.MapGet("/api/v1/admin/failures/export", async (
    [FromQuery] Guid? institutionId,
    LeadDeliveryDbContext db,
    CancellationToken ct) =>
{
    var q = db.DeliveryRuns.AsNoTracking()
        .Where(r => r.Status == DeliveryRunStatus.Failed || r.Status == DeliveryRunStatus.PartialSuccess);
    if (institutionId.HasValue) q = q.Where(r => r.InstitutionId == institutionId);
    var rows = await q.OrderByDescending(r => r.CreatedAt).Take(500).Select(r => new
    {
        r.Id,
        r.InstitutionId,
        r.LeadId,
        r.Status,
        r.CreatedAt
    }).ToListAsync(ct);

    var csv = "RunId,InstitutionId,LeadId,Status,CreatedAt\n" +
              string.Join('\n', rows.Select(r => $"{r.Id},{r.InstitutionId},{r.LeadId},{r.Status},{r.CreatedAt:O}"));
    return Results.Text(csv, "text/csv");
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeadDeliveryDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(db);
    var catalog = scope.ServiceProvider.GetRequiredService<FieldCatalogService>();
    await catalog.SyncColumnMetadataAsync(CancellationToken.None);
}

app.Run();

static bool IsAdmin(HttpRequest http)
{
    var key = http.Headers["X-Admin-Api-Key"].FirstOrDefault();
    var expected = Environment.GetEnvironmentVariable("ADMIN_API_KEY") ?? "dev-admin-key";
    return string.Equals(key, expected, StringComparison.Ordinal);
}

static object BuildTree(IReadOnlyList<FieldCatalogEntry> entries)
{
    return entries
        .GroupBy(e => e.ParentKey ?? e.Key.Split('.').First())
        .Select(g => new { group = g.Key, fields = g.Select(f => new { f.Key, f.DisplayName, f.DataType, f.IsPii }) });
}

public sealed class SimulateMatchRequest
{
    public string RulesJson { get; set; } = "[]";
    public string LeadDocumentJson { get; set; } = "{}";
}

public sealed class SimulateEnrichmentRequest
{
    public Guid InstitutionId { get; set; }
    public string LeadDocumentJson { get; set; } = "{}";
    public string? PipelineJson { get; set; }
}

public sealed class SimulateTransformRequest
{
    public string Template { get; set; } = "";
    public string LeadDocumentJson { get; set; } = "{}";
}

public sealed class RetryRequest
{
    public List<Guid>? EndpointIds { get; set; }
    public bool UseLatestConfig { get; set; }
}

public partial class Program;