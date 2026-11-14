using LeadDelivery.Domain.Enums;

namespace LeadDelivery.Domain.Entities;

public class DeliveryConfiguration
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution? Institution { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Version { get; set; } = 1;
    public ConfigurationPublishState PublishState { get; set; } = ConfigurationPublishState.Draft;
    public string RulesJson { get; set; } = "[]";
    public bool ContinueOnPrimaryFailure { get; set; }
    public string? EnrichmentPipelineJson { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public ICollection<DeliveryEndpoint> Endpoints { get; set; } = new List<DeliveryEndpoint>();
}

public class DeliveryEndpoint
{
    public Guid Id { get; set; }
    public Guid DeliveryConfigurationId { get; set; }
    public DeliveryConfiguration? DeliveryConfiguration { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; } = ChannelType.Http;
    public string ChannelConfigJson { get; set; } = "{}";
    public EndpointPriority Priority { get; set; } = EndpointPriority.Secondary;
    public int SortOrder { get; set; }
    public string? FieldMappingsJson { get; set; }
    public string? BodyTemplate { get; set; }
}

public class CredentialProfile
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AuthType { get; set; } = "None";
    public string ConfigJson { get; set; } = "{}";
}