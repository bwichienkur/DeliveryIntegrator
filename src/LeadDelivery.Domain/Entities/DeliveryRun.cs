using LeadDelivery.Domain.Enums;

namespace LeadDelivery.Domain.Entities;

public class DeliveryRun
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public Lead? Lead { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid? DeliveryConfigurationId { get; set; }
    public int? DeliveryConfigurationVersion { get; set; }
    public DeliveryRunStatus Status { get; set; } = DeliveryRunStatus.Pending;
    public string? EnrichedLeadSnapshotJson { get; set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<DeliveryAttempt> Attempts { get; set; } = new List<DeliveryAttempt>();
}

public class DeliveryAttempt
{
    public Guid Id { get; set; }
    public Guid DeliveryRunId { get; set; }
    public DeliveryRun? DeliveryRun { get; set; }
    public Guid DeliveryEndpointId { get; set; }
    public string EndpointName { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; }
    public EndpointPriority Priority { get; set; }
    public DeliveryAttemptStatus Status { get; set; } = DeliveryAttemptStatus.Pending;
    public int AttemptNumber { get; set; } = 1;
    public int? HttpStatusCode { get; set; }
    public string? RequestSnapshotRedacted { get; set; }
    public string? ResponseSnapshotRedacted { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public Guid LeadId { get; set; }
    public Guid? DeliveryRunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public class AllowedStoredProcedure
{
    public Guid Id { get; set; }
    public string SchemaName { get; set; } = "ld";
    public string ProcedureName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public class EnrichmentStepDefinition
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public EnrichmentStepType StepType { get; set; }
    public int SortOrder { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
}

public class ValueLookupTable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MappingsJson { get; set; } = "{}";
}