using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain;

/// <summary>Resolves requested priority against category rules (BR-1).</summary>
public sealed class PriorityPolicy
{
    public PriorityDecision Resolve(TicketPriority? requested, Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (category.ForcesCriticalPriority)
        {
            return new(TicketPriority.Critical, $"Category {category.Name} forces Critical priority");
        }

        if (requested is { } priority)
        {
            return new(priority, $"Requested priority {priority}");
        }

        return new(TicketPriority.Medium, "No priority requested, defaulted to Medium");
    }
}

public sealed record PriorityDecision(TicketPriority Priority, string Reason);
