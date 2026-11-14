using System.Text.Json;
using LeadDelivery.Domain.Enums;
using LeadDelivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadDelivery.Infrastructure.Services;

public sealed class OutboxProcessor
{
    private readonly LeadDeliveryDbContext _db;
    private readonly DeliveryOrchestrator _orchestrator;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(LeadDeliveryDbContext db, DeliveryOrchestrator orchestrator, ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        var messages = await _db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            await _db.SaveChangesAsync(ct);

            try
            {
                if (message.MessageType == "ProcessDeliveryRun")
                {
                    var payload = JsonSerializer.Deserialize<OutboxPayload>(message.PayloadJson);
                    if (payload?.DeliveryRunId is Guid runId)
                    {
                        await _orchestrator.ProcessRunAsync(runId, ct);
                    }
                }

                message.Status = OutboxMessageStatus.Completed;
                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                message.RetryCount += 1;
                message.Status = message.RetryCount >= 5 ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
                _logger.LogError(ex, "Outbox message {MessageId} failed", message.Id);
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private sealed class OutboxPayload
    {
        public Guid? DeliveryRunId { get; set; }
    }
}