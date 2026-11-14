using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeadDelivery.Application.Models;

public sealed class IngestLeadRequest
{
    public string? InstitutionCode { get; set; }
    public Guid? InstitutionId { get; set; }
    public JsonElement? Payload { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class IngestLeadResponse
{
    public Guid LeadId { get; set; }
    public Guid DeliveryRunId { get; set; }
    public string Status { get; set; } = "Accepted";
}