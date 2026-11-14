using System.Text.Json;
using System.Text.Json.Nodes;
using LeadDelivery.Domain.Entities;

namespace LeadDelivery.Infrastructure.Services;

public sealed class LeadDocumentService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public string BuildDocument(Lead lead, JsonElement payload)
    {
        var root = new JsonObject
        {
            ["id"] = lead.Id.ToString(),
            ["institutionId"] = lead.InstitutionId.ToString(),
            ["email"] = lead.Email,
            ["campus"] = lead.Campus,
            ["program"] = lead.Program,
            ["sourceSite"] = lead.SourceSite,
            ["createdAt"] = lead.CreatedAt.ToString("O")
        };

        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in payload.EnumerateObject())
            {
                root[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
            }
        }
        else
        {
            root["payload"] = JsonNode.Parse(payload.GetRawText());
        }

        return root.ToJsonString(JsonOptions);
    }

    public string WithEnrichment(string leadDocumentJson, string stepKey, JsonObject enrichmentData)
    {
        var root = JsonNode.Parse(leadDocumentJson)!.AsObject();
        var enriched = root["enriched"] as JsonObject ?? new JsonObject();
        enriched[stepKey] = enrichmentData;
        root["enriched"] = enriched;
        return root.ToJsonString(JsonOptions);
    }
}