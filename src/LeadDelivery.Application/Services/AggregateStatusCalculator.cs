using LeadDelivery.Domain.Entities;
using LeadDelivery.Domain.Enums;

namespace LeadDelivery.Application.Services;

public static class AggregateStatusCalculator
{
    public static DeliveryRunStatus Compute(IReadOnlyList<DeliveryAttempt> attempts)
    {
        var executed = attempts.Where(a => a.Status is not DeliveryAttemptStatus.Skipped and not DeliveryAttemptStatus.Pending).ToList();
        if (executed.Count == 0)
        {
            return DeliveryRunStatus.Pending;
        }

        if (executed.All(a => a.Status == DeliveryAttemptStatus.Succeeded))
        {
            return DeliveryRunStatus.Success;
        }

        var primary = executed.Where(a => a.Priority == EndpointPriority.Primary).ToList();
        if (primary.Any(a => a.Status == DeliveryAttemptStatus.Failed))
        {
            return DeliveryRunStatus.Failed;
        }

        var secondary = executed.Where(a => a.Priority == EndpointPriority.Secondary).ToList();
        var anySuccess = executed.Any(a => a.Status == DeliveryAttemptStatus.Succeeded);
        var anyFail = executed.Any(a => a.Status == DeliveryAttemptStatus.Failed);

        if (primary.All(a => a.Status == DeliveryAttemptStatus.Succeeded))
        {
            if (secondary.Any(a => a.Status == DeliveryAttemptStatus.Failed))
            {
                return DeliveryRunStatus.PartialSuccess;
            }

            if (anyFail && anySuccess)
            {
                return DeliveryRunStatus.PartialSuccess;
            }

            if (anySuccess && !anyFail)
            {
                return DeliveryRunStatus.Success;
            }
        }

        return anySuccess ? DeliveryRunStatus.PartialSuccess : DeliveryRunStatus.Failed;
    }
}