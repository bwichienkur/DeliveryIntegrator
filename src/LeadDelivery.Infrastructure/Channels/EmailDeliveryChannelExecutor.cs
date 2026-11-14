using LeadDelivery.Application.Abstractions;
using LeadDelivery.Domain.Enums;

namespace LeadDelivery.Infrastructure.Channels;

public sealed class EmailDeliveryChannelExecutor : IDeliveryChannelExecutor
{
    public ChannelType ChannelType => ChannelType.Email;

    public Task<DeliveryAttemptResult> ExecuteAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(new DeliveryAttemptResult
        {
            Status = DeliveryAttemptStatus.Skipped,
            ErrorMessage = "Email channel is scaffolded only; configure Http for production delivery.",
            DurationMs = 0
        });
    }
}