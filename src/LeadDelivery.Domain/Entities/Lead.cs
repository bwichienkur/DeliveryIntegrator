namespace LeadDelivery.Domain.Entities;

public class Lead
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution? Institution { get; set; }
    public string? RawPayload { get; set; }
    public string RawContentType { get; set; } = "application/json";
    public string LeadDocumentJson { get; set; } = "{}";
    public string? Campus { get; set; }
    public string? Program { get; set; }
    public string? SourceSite { get; set; }
    public string? Email { get; set; }
    public string? ExternalSourceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<DeliveryRun> DeliveryRuns { get; set; } = new List<DeliveryRun>();
}