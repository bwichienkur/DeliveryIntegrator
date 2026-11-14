namespace LeadDelivery.Domain.Enums;

public enum DeliveryRunStatus
{
    Pending,
    InProgress,
    Success,
    PartialSuccess,
    Failed,
    Cancelled
}

public enum DeliveryAttemptStatus
{
    Pending,
    InProgress,
    Succeeded,
    Failed,
    Skipped
}

public enum ChannelType
{
    Http,
    Email,
    Sftp
}

public enum EndpointPriority
{
    Primary,
    Secondary,
    Optional
}

public enum ConfigurationPublishState
{
    Draft,
    Published,
    Archived
}

public enum FieldCatalogSource
{
    Column,
    JsonInferred,
    XmlInferred,
    FormImport,
    Enrichment,
    Manual
}

public enum EnrichmentStepType
{
    Lookup,
    StoredProcedure,
    Template,
    Plugin
}

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}