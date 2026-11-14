using LeadDelivery.Domain.Entities;
using LeadDelivery.Domain.Enums;

namespace LeadDelivery.Application.Abstractions;

public sealed class DeliveryContext
{
    public required DeliveryRun Run { get; init; }
    public required DeliveryEndpoint Endpoint { get; init; }
    public required string LeadDocumentJson { get; init; }
    public required string CorrelationId { get; init; }
}

public sealed class DeliveryAttemptResult
{
    public DeliveryAttemptStatus Status { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? RequestSnapshotRedacted { get; init; }
    public string? ResponseSnapshotRedacted { get; init; }
    public string? ErrorMessage { get; init; }
    public long DurationMs { get; init; }
}

public interface IDeliveryChannelExecutor
{
    ChannelType ChannelType { get; }
    Task<DeliveryAttemptResult> ExecuteAsync(DeliveryContext context, CancellationToken cancellationToken);
}