using SupportDesk.Domain;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>Coordinates priority, SLA and assignment rules for ticket creation and escalation.</summary>
public sealed class TicketTriage(
    PriorityPolicy priorityPolicy,
    SlaPolicy slaPolicy,
    AgentSelector agentSelector)
{
    public TriageResult Triage(
        Category category,
        CustomerTier tier,
        TicketPriority? requested,
        IReadOnlyList<AgentWorkload> workloads,
        DateTime nowUtc)
    {
        var priority = priorityPolicy.Resolve(requested, category);
        var dueAtUtc = slaPolicy.DueAt(priority.Priority, tier, nowUtc);
        var assignment = agentSelector.Select(category, workloads);

        return new(
            priority.Priority,
            dueAtUtc,
            assignment.AgentId,
            priority.Reason,
            DescribeSla(priority.Priority, tier, dueAtUtc - nowUtc),
            assignment.Reason);
    }

    public EscalationOutcome PlanEscalation(
        Ticket ticket,
        Category category,
        CustomerTier tier,
        IReadOnlyList<AgentWorkload> workloads,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var priority = ticket.Priority.Next();
        var dueAtUtc = slaPolicy.DueAt(priority, tier, nowUtc);
        var assignment = agentSelector.Reevaluate(category, ticket.AssignedAgentId, workloads);

        return new(
            priority,
            dueAtUtc,
            assignment.AgentId,
            $"Escalated from {ticket.Priority} to {priority}",
            DescribeSla(priority, tier, dueAtUtc - nowUtc),
            assignment.Reason);
    }

    private string DescribeSla(TicketPriority priority, CustomerTier tier, TimeSpan window) =>
        $"Due in {window.TotalHours:0.##} hours: {priority} window, {tier.ToString().ToLowerInvariant()} customer";
}

public sealed record TriageResult(
    TicketPriority Priority,
    DateTime DueAtUtc,
    int? AssignedAgentId,
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);

public sealed record EscalationOutcome(
    TicketPriority Priority,
    DateTime DueAtUtc,
    int? AssignedAgentId,
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);
