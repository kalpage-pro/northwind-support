namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Configuration for the response windows and at-risk boundary (BR-6).</summary>
public sealed class SlaSettings
{
    public Dictionary<TicketPriority, double> BaseWindowHours { get; init; } = [];

    public double PremiumCustomerMultiplier { get; init; }

    public double MinimumWindowHours { get; init; }

    public double AtRiskThresholdPercent { get; init; }

    public void Validate()
    {
        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!BaseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                throw new InvalidOperationException($"SLA window for {priority} must be greater than zero.");
            }
        }

        if (PremiumCustomerMultiplier is <= 0 or > 1)
        {
            throw new InvalidOperationException("Premium customer multiplier must be greater than zero and at most one.");
        }

        if (MinimumWindowHours < 0)
        {
            throw new InvalidOperationException("Minimum SLA window cannot be negative.");
        }

        if (AtRiskThresholdPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("At-risk threshold must be between zero and one hundred percent.");
        }
    }
}
