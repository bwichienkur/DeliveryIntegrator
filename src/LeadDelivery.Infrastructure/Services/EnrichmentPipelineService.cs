using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeadDelivery.Domain.Enums;
using LeadDelivery.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LeadDelivery.Infrastructure.Services;

public sealed class EnrichmentPipelineService
{
    private readonly LeadDeliveryDbContext _db;
    private readonly LeadDocumentService _documents;
    private readonly IConfiguration _configuration;

    public EnrichmentPipelineService(LeadDeliveryDbContext db, LeadDocumentService documents, IConfiguration configuration)
    {
        _db = db;
        _documents = documents;
        _configuration = configuration;
    }

    public async Task<string> RunAsync(Guid institutionId, string leadDocumentJson, string? pipelineJson, CancellationToken ct)
    {
        var doc = leadDocumentJson;
        var steps = await ResolveStepsAsync(institutionId, pipelineJson, ct);

        foreach (var step in steps)
        {
            if (!step.IsEnabled) continue;

            doc = step.StepType switch
            {
                EnrichmentStepType.StoredProcedure => await RunStoredProcedureAsync(step, doc, ct),
                EnrichmentStepType.Template => RunTemplate(step, doc),
                EnrichmentStepType.Lookup => doc,
                EnrichmentStepType.Plugin => doc,
                _ => doc
            };
        }

        return doc;
    }

    private async Task<List<Domain.Entities.EnrichmentStepDefinition>> ResolveStepsAsync(
        Guid institutionId, string? pipelineJson, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(pipelineJson))
        {
            var keys = JsonSerializer.Deserialize<List<string>>(pipelineJson);
            if (keys?.Count > 0)
            {
                return await _db.EnrichmentSteps
                    .Where(s => s.InstitutionId == institutionId && keys.Contains(s.StepKey))
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync(ct);
            }
        }

        return await _db.EnrichmentSteps
            .Where(s => s.InstitutionId == institutionId && s.IsEnabled)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
    }

    private async Task<string> RunStoredProcedureAsync(Domain.Entities.EnrichmentStepDefinition step, string doc, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<SpStepConfig>(step.ConfigJson) ?? new SpStepConfig();
        var allowed = await _db.AllowedStoredProcedures
            .AnyAsync(p => p.IsEnabled && p.ProcedureName == cfg.ProcedureName, ct);

        if (!allowed)
        {
            throw new InvalidOperationException($"Procedure {cfg.ProcedureName} is not allowlisted.");
        }

        var lead = JsonNode.Parse(doc)!.AsObject();
        var cs = _configuration.GetConnectionString("LeadDelivery")!;

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(cfg.ProcedureName, conn) { CommandType = CommandType.StoredProcedure };

        foreach (var map in cfg.Parameters ?? [])
        {
            var value = ResolvePath(lead, map.SourcePath);
            cmd.Parameters.AddWithValue(map.ParameterName, value ?? DBNull.Value);
        }

        var result = new JsonObject();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                result[reader.GetName(i)] = reader.GetValue(i)?.ToString();
            }
        }

        return _documents.WithEnrichment(doc, step.StepKey, result);
    }

    private static string RunTemplate(Domain.Entities.EnrichmentStepDefinition step, string doc)
    {
        var cfg = JsonSerializer.Deserialize<TemplateStepConfig>(step.ConfigJson) ?? new TemplateStepConfig();
        var output = CottleTemplateRenderer.Render(cfg.Template ?? "", doc);
        var merged = new JsonObject { ["output"] = output };
        var root = JsonNode.Parse(doc)!.AsObject();
        var enriched = root["enriched"] as JsonObject ?? new JsonObject();
        enriched[step.StepKey] = merged;
        root["enriched"] = enriched;
        return root.ToJsonString();
    }

    private static object? ResolvePath(JsonObject lead, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var parts = path.Replace("lead.", "", StringComparison.OrdinalIgnoreCase).Split('.');
        JsonNode? node = lead;
        foreach (var part in parts)
        {
            if (node is not JsonObject o || !o.TryGetPropertyValue(part, out node))
            {
                return null;
            }
        }

        return node?.ToString();
    }

    private sealed class SpStepConfig
    {
        public string ProcedureName { get; set; } = "ld_GetProgramDetails";
        public List<SpParameterMap>? Parameters { get; set; }
    }

    private sealed class SpParameterMap
    {
        public string ParameterName { get; set; } = "@InstitutionId";
        public string SourcePath { get; set; } = "lead.institutionId";
    }

    private sealed class TemplateStepConfig
    {
        public string? Template { get; set; }
    }
}