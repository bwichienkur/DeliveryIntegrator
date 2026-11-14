using System.Text.Json;
using LeadDelivery.Domain.Entities;
using LeadDelivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RulesEngine.Models;

namespace LeadDelivery.Infrastructure.Services;

public sealed class RuleMatchingService
{
    private readonly LeadDeliveryDbContext _db;

    public RuleMatchingService(LeadDeliveryDbContext db) => _db = db;

    public async Task<DeliveryConfiguration?> MatchAsync(Guid institutionId, string leadDocumentJson, CancellationToken ct)
    {
        var configs = await _db.DeliveryConfigurations
            .AsNoTracking()
            .Include(c => c.Endpoints)
            .Where(c => c.InstitutionId == institutionId && c.PublishState == Domain.Enums.ConfigurationPublishState.Published)
            .OrderBy(c => c.Priority)
            .ToListAsync(ct);

        var lead = JsonSerializer.Deserialize<Dictionary<string, object?>>(leadDocumentJson) ?? new();

        foreach (var config in configs)
        {
            if (await EvaluateRulesAsync(config.RulesJson, lead, ct))
            {
                return config;
            }
        }

        return null;
    }

    public async Task<bool> SimulateMatchAsync(string rulesJson, string leadDocumentJson, CancellationToken ct)
    {
        var lead = JsonSerializer.Deserialize<Dictionary<string, object?>>(leadDocumentJson) ?? new();
        return await EvaluateRulesAsync(rulesJson, lead, ct);
    }

    private static async Task<bool> EvaluateRulesAsync(string rulesJson, Dictionary<string, object?> lead, CancellationToken ct)
    {
        await Task.Yield();
        if (string.IsNullOrWhiteSpace(rulesJson) || rulesJson == "[]")
        {
            return true;
        }

        List<Rule>? rules;
        try
        {
            rules = JsonSerializer.Deserialize<List<Rule>>(rulesJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return EvaluateSimpleRules(rulesJson, lead);
        }

        if (rules is null || rules.Count == 0)
        {
            return EvaluateSimpleRules(rulesJson, lead);
        }

        var workflowRules = new List<RulesEngine.Models.Rule>();
        foreach (var r in rules)
        {
            workflowRules.Add(new RulesEngine.Models.Rule
            {
                RuleName = r.RuleName ?? "Rule",
                Expression = r.Expression ?? "true",
                RuleExpressionType = RuleExpressionType.LambdaExpression
            });
        }

        var workflow = new Workflow { WorkflowName = "LeadMatch", Rules = workflowRules };
        var engine = new RulesEngine.RulesEngine([workflow]);
        var inputs = new RuleParameter("lead", lead);
        var results = await engine.ExecuteAllRulesAsync("LeadMatch", inputs);
        return results.Any(x => x.IsSuccess);
    }

    private static bool EvaluateSimpleRules(string rulesJson, Dictionary<string, object?> lead)
    {
        try
        {
            var simple = JsonSerializer.Deserialize<List<SimpleRule>>(rulesJson);
            if (simple is null || simple.Count == 0) return true;

            return simple.All(r =>
            {
                if (!lead.TryGetValue(r.Field ?? "", out var val) && !TryGetDotted(lead, r.Field ?? "", out val))
                {
                    return false;
                }

                var s = val?.ToString() ?? "";
                return r.Operator?.ToLowerInvariant() switch
                {
                    "equals" => string.Equals(s, r.Value, StringComparison.OrdinalIgnoreCase),
                    "in" => (r.Values ?? []).Any(v => string.Equals(v, s, StringComparison.OrdinalIgnoreCase)),
                    _ => string.Equals(s, r.Value, StringComparison.OrdinalIgnoreCase)
                };
            });
        }
        catch
        {
            return true;
        }
    }

    private static bool TryGetDotted(Dictionary<string, object?> lead, string field, out object? val)
    {
        val = null;
        if (lead.TryGetValue(field, out val)) return true;
        return false;
    }

    private sealed class Rule
    {
        public string? RuleName { get; set; }
        public string? Expression { get; set; }
    }

    private sealed class SimpleRule
    {
        public string? Field { get; set; }
        public string? Operator { get; set; }
        public string? Value { get; set; }
        public List<string>? Values { get; set; }
    }
}