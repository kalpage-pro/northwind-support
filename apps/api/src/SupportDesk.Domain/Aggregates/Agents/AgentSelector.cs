using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>Chooses an eligible owner using the BR-4 and BR-5 assignment rules.</summary>
public sealed class AgentSelector
{
    public AssignmentDecision Select(Category category, IReadOnlyList<AgentWorkload> candidates)
    {
        var eligible = candidates
            .Where(candidate => IsEligible(category, candidate, ignoreOwnTicket: false))
            .OrderBy(candidate => candidate.OpenTickets)
            .ThenBy(candidate => candidate.Agent.Id)
            .ToList();

        if (eligible.Count > 0)
        {
            var selected = eligible[0];
            return new(
                selected.Agent.Id,
                $"Assigned to {selected.Agent.FullName}: fewest open tickets ({selected.OpenTickets}) among {eligible.Count} eligible agents");
        }

        return new(null, NoEligibleReason(category, candidates));
    }

    /// <summary>
    /// Keeps the current owner when possible. The escalating ticket is assumed to be included in
    /// the current owner's workload, so one slot is ignored while checking that owner.
    /// </summary>
    public AssignmentDecision Reevaluate(
        Category category,
        int? currentAgentId,
        IReadOnlyList<AgentWorkload> candidates)
    {
        if (currentAgentId is { } id)
        {
            var current = candidates.FirstOrDefault(candidate => candidate.Agent.Id == id);
            if (current is not null && IsEligible(category, current, ignoreOwnTicket: true))
            {
                return new(
                    id,
                    $"Kept assignment to {current.Agent.FullName}: current agent remains eligible");
            }
        }

        return Select(category, candidates);
    }

    private static bool IsEligible(Category category, AgentWorkload workload, bool ignoreOwnTicket) =>
        workload.Agent.IsActive &&
        (!category.RequiresSpecialist || workload.Agent.Specializations.Any(s => s.CategoryId == category.Id)) &&
        (ignoreOwnTicket ? workload.OpenTickets - 1 : workload.OpenTickets) < workload.Agent.MaxOpenTickets;

    private static string NoEligibleReason(Category category, IReadOnlyList<AgentWorkload> candidates)
    {
        var active = candidates.Where(candidate => candidate.Agent.IsActive).ToList();
        if (active.Count == 0)
        {
            return "No active agents";
        }

        if (category.RequiresSpecialist && !active.Any(candidate =>
                candidate.Agent.Specializations.Any(s => s.CategoryId == category.Id)))
        {
            return $"No active specialist for {category.Name}";
        }

        return "All eligible agents are at capacity";
    }
}

public sealed record AgentWorkload(Agent Agent, int OpenTickets);

public sealed record AssignmentDecision(int? AgentId, string Reason);
