using LeadDelivery.Application.Services;
using LeadDelivery.Domain.Entities;
using LeadDelivery.Domain.Enums;

namespace LeadDelivery.UnitTests;

public class AggregateStatusCalculatorTests
{
    [Fact]
    public void Primary_failure_yields_failed_even_if_secondary_succeeded()
    {
        var attempts = new List<DeliveryAttempt>
        {
            new() { Priority = EndpointPriority.Primary, Status = DeliveryAttemptStatus.Failed },
            new() { Priority = EndpointPriority.Secondary, Status = DeliveryAttemptStatus.Succeeded }
        };

        var status = AggregateStatusCalculator.Compute(attempts);
        Assert.Equal(DeliveryRunStatus.Failed, status);
    }

    [Fact]
    public void Primary_success_secondary_failure_is_partial()
    {
        var attempts = new List<DeliveryAttempt>
        {
            new() { Priority = EndpointPriority.Primary, Status = DeliveryAttemptStatus.Succeeded },
            new() { Priority = EndpointPriority.Secondary, Status = DeliveryAttemptStatus.Failed }
        };

        Assert.Equal(DeliveryRunStatus.PartialSuccess, AggregateStatusCalculator.Compute(attempts));
    }
}