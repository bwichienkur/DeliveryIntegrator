using LeadDelivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeadDelivery.Worker;

/// <summary>
/// Scaffold for SQL-backed ingestion: polls Leads flagged for external source sync.
/// </summary>
public sealed class LeadSourcePoller(LeadDeliveryDbContext db, ILogger<LeadSourcePoller> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pending = await db.Leads
                .Where(l => l.ExternalSourceId != null && l.UpdatedAt > l.CreatedAt)
                .OrderBy(l => l.UpdatedAt)
                .Take(10)
                .AsNoTracking()
                .CountAsync(stoppingToken);

            if (pending > 0)
            {
                logger.LogInformation("Lead source poller found {Count} candidate rows (wire to outbox in production).", pending);
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}