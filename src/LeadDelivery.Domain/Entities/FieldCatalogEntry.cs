using LeadDelivery.Domain.Enums;

namespace LeadDelivery.Domain.Entities;

public class FieldCatalogEntry
{
    public Guid Id { get; set; }
    public Guid? InstitutionId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public string Path { get; set; } = string.Empty;
    public FieldCatalogSource Source { get; set; }
    public bool IsPii { get; set; }
    public DateTimeOffset? DeprecatedAt { get; set; }
    public string? ParentKey { get; set; }
}

public class FieldCatalogInference
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string SuggestedKey { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public string SampleValueRedacted { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;
}