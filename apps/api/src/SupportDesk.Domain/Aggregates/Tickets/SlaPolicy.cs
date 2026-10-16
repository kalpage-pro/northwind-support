using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Calculates response windows and SLA state according to BR-2, BR-3, BR-6 and BR-8.</summary>
public sealed class SlaPolicy(SlaSettings settings)
{
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier)
    {
        settings.Validate();
        var hours = settings.BaseWindowHours[priority];
        if (tier == CustomerTier.Premium)
        {
            hours = Math.Max(hours * settings.PremiumCustomerMultiplier, settings.MinimumWindowHours);
        }

        return TimeSpan.FromHours(hours);
    }

    public DateTime DueAt(TicketPriority priority, CustomerTier tier, DateTime fromUtc) =>
        fromUtc + WindowFor(priority, tier);

    public DateTime AtRiskBoundaryUtc(TicketPriority priority, CustomerTier tier, DateTime nowUtc) =>
        nowUtc + (WindowFor(priority, tier) * (settings.AtRiskThresholdPercent / 100d));

    public SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        TicketPriority priority,
        CustomerTier tier,
        DateTime nowUtc)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is { } resolvedAtUtcValue)
        {
            return resolvedAtUtcValue <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        return dueAtUtc.Value <= AtRiskBoundaryUtc(priority, tier, nowUtc)
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
