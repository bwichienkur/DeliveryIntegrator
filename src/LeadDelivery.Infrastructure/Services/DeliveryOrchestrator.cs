using LeadDelivery.Application.Abstractions;
using LeadDelivery.Application.Services;
using LeadDelivery.Domain.Entities;
using LeadDelivery.Domain.Enums;
using LeadDelivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeadDelivery.Infrastructure.Services;

public sealed class DeliveryOrchestrator
{
    private readonly LeadDeliveryDbContext _db;
    private readonly RuleMatchingService _rules;
    private readonly EnrichmentPipelineService _enrichment;
    private readonly IEnumerable<IDeliveryChannelExecutor> _executors;

    public DeliveryOrchestrator(
        LeadDeliveryDbContext db,
        RuleMatchingService rules,
        EnrichmentPipelineService enrichment,
        IEnumerable<IDeliveryChannelExecutor> executors)
    {
        _db = db;
        _rules = rules;
        _enrichment = enrichment;
        _executors = executors;
    }

    public async Task ProcessRunAsync(Guid deliveryRunId, CancellationToken ct)
    {
        var run = await _db.DeliveryRuns
            .Include(r => r.Lead)
            .FirstOrDefaultAsync(r => r.Id == deliveryRunId, ct)
            ?? throw new KeyNotFoundException("Run not found");

        run.Status = DeliveryRunStatus.InProgress;
        await _db.SaveChangesAsync(ct);

        var lead = run.Lead!;
        var config = await _rules.MatchAsync(run.InstitutionId, lead.LeadDocumentJson, ct);
        if (config is null)
        {
            run.Status = DeliveryRunStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        run.DeliveryConfigurationId = config.Id;
        run.DeliveryConfigurationVersion = config.Version;

        var enriched = await _enrichment.RunAsync(run.InstitutionId, lead.LeadDocumentJson, config.EnrichmentPipelineJson, ct);
        run.EnrichedLeadSnapshotJson = enriched;

        var endpoints = config.Endpoints.OrderBy(e => e.SortOrder).ToList();
        var primaryFailed = false;

        foreach (var endpoint in endpoints)
        {
            if (primaryFailed && !config.ContinueOnPrimaryFailure && endpoint.Priority != EndpointPriority.Primary)
            {
                await AddSkippedAttempt(run, endpoint, ct);
                continue;
            }

            var executor = _executors.FirstOrDefault(e => e.ChannelType == endpoint.ChannelType);
            if (executor is null)
            {
                await AddFailedAttempt(run, endpoint, "No executor registered", ct);
                if (endpoint.Priority == EndpointPriority.Primary) primaryFailed = true;
                continue;
            }

            var context = new DeliveryContext
            {
                Run = run,
                Endpoint = endpoint,
                LeadDocumentJson = enriched,
                CorrelationId = run.CorrelationId
            };

            var result = await executor.ExecuteAsync(context, ct);
            var attempt = new DeliveryAttempt
            {
                Id = Guid.NewGuid(),
                DeliveryRunId = run.Id,
                DeliveryEndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                ChannelType = endpoint.ChannelType,
                Priority = endpoint.Priority,
                Status = result.Status,
                HttpStatusCode = result.HttpStatusCode,
                RequestSnapshotRedacted = result.RequestSnapshotRedacted,
                ResponseSnapshotRedacted = result.ResponseSnapshotRedacted,
                ErrorMessage = result.ErrorMessage,
                DurationMs = result.DurationMs
            };

            _db.DeliveryAttempts.Add(attempt);

            if (endpoint.Priority == EndpointPriority.Primary && result.Status == DeliveryAttemptStatus.Failed)
            {
                primaryFailed = true;
            }
        }

        await _db.SaveChangesAsync(ct);

        var attempts = await _db.DeliveryAttempts.Where(a => a.DeliveryRunId == run.Id).ToListAsync(ct);
        run.Status = AggregateStatusCalculator.Compute(attempts);
        run.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RetryFailedAsync(Guid runId, IReadOnlyList<Guid>? endpointIds, bool useLatestConfig, CancellationToken ct)
    {
        var run = await _db.DeliveryRuns
            .Include(r => r.Lead)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new KeyNotFoundException("Run not found");

        var failed = await _db.DeliveryAttempts
            .Where(a => a.DeliveryRunId == runId && a.Status == DeliveryAttemptStatus.Failed)
            .ToListAsync(ct);

        if (endpointIds is { Count: > 0 })
        {
            failed = failed.Where(a => endpointIds.Contains(a.DeliveryEndpointId)).ToList();
        }

        if (failed.Count == 0) return;

        DeliveryConfiguration? config = null;
        if (useLatestConfig || run.DeliveryConfigurationId is null)
        {
            config = await _rules.MatchAsync(run.InstitutionId, run.Lead!.LeadDocumentJson, ct);
        }
        else
        {
            config = await _db.DeliveryConfigurations
                .Include(c => c.Endpoints)
                .FirstOrDefaultAsync(c => c.Id == run.DeliveryConfigurationId, ct);
        }

        if (config is null) return;

        var enriched = run.EnrichedLeadSnapshotJson ?? run.Lead!.LeadDocumentJson;

        foreach (var fail in failed)
        {
            var endpoint = config.Endpoints.FirstOrDefault(e => e.Id == fail.DeliveryEndpointId);
            if (endpoint is null) continue;

            var executor = _executors.First(e => e.ChannelType == endpoint.ChannelType);
            var result = await executor.ExecuteAsync(new DeliveryContext
            {
                Run = run,
                Endpoint = endpoint,
                LeadDocumentJson = enriched,
                CorrelationId = run.CorrelationId
            }, ct);

            fail.Status = result.Status;
            fail.AttemptNumber += 1;
            fail.HttpStatusCode = result.HttpStatusCode;
            fail.ResponseSnapshotRedacted = result.ResponseSnapshotRedacted;
            fail.ErrorMessage = result.ErrorMessage;
            fail.DurationMs = result.DurationMs;
        }

        var attempts = await _db.DeliveryAttempts.Where(a => a.DeliveryRunId == run.Id).ToListAsync(ct);
        run.Status = AggregateStatusCalculator.Compute(attempts);
        await _db.SaveChangesAsync(ct);
    }

    private async Task AddSkippedAttempt(DeliveryRun run, DeliveryEndpoint endpoint, CancellationToken ct)
    {
        _db.DeliveryAttempts.Add(new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            DeliveryRunId = run.Id,
            DeliveryEndpointId = endpoint.Id,
            EndpointName = endpoint.Name,
            ChannelType = endpoint.ChannelType,
            Priority = endpoint.Priority,
            Status = DeliveryAttemptStatus.Skipped,
            ErrorMessage = "Skipped due to primary failure"
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task AddFailedAttempt(DeliveryRun run, DeliveryEndpoint endpoint, string error, CancellationToken ct)
    {
        _db.DeliveryAttempts.Add(new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            DeliveryRunId = run.Id,
            DeliveryEndpointId = endpoint.Id,
            EndpointName = endpoint.Name,
            ChannelType = endpoint.ChannelType,
            Priority = endpoint.Priority,
            Status = DeliveryAttemptStatus.Failed,
            ErrorMessage = error
        });
        await _db.SaveChangesAsync(ct);
    }
}