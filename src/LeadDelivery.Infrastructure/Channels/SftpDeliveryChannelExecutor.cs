using LeadDelivery.Application.Abstractions;
using LeadDelivery.Domain.Enums;

namespace LeadDelivery.Infrastructure.Channels;

public sealed class SftpDeliveryChannelExecutor : IDeliveryChannelExecutor
{
    public ChannelType ChannelType => ChannelType.Sftp;

    public Task<DeliveryAttemptResult> ExecuteAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(new DeliveryAttemptResult
        {
            Status = DeliveryAttemptStatus.Skipped,
            ErrorMessage = "SFTP channel is scaffolded only; configure Http for production delivery.",
            DurationMs = 0
        });
    }
}