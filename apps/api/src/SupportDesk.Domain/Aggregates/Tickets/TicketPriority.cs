namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// How urgent a ticket is. The numeric values are ordered, lowest urgency first, and are
/// persisted as integers - do not renumber them.
/// </summary>
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public static class TicketPriorityExtensions
{
    public static TicketPriority Next(this TicketPriority priority) => priority switch
    {
        TicketPriority.Low => TicketPriority.Medium,
        TicketPriority.Medium => TicketPriority.High,
        TicketPriority.High => TicketPriority.Critical,
        TicketPriority.Critical => throw new InvalidOperationException("Critical has no next priority."),
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null)
    };
}
