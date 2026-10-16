using System.Reflection;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain;

public sealed class BusinessRulesTests
{
    private const int GeneralCategoryId = 1;
    private const int SecurityCategoryId = 5;
    private const int StandardLowHours = 72;
    private const int StandardMediumHours = 24;
    private const int StandardHighHours = 8;
    private const int StandardCriticalHours = 4;
    private const double PremiumMultiplier = 0.5;
    private const double MinimumWindowHours = 1;
    private const double AtRiskPercent = 25;

    private static readonly DateTime Now = FixedClock.DefaultNow;
    private static readonly SlaSettings Sla = new()
    {
        BaseWindowHours = new()
        {
            [TicketPriority.Low] = StandardLowHours,
            [TicketPriority.Medium] = StandardMediumHours,
            [TicketPriority.High] = StandardHighHours,
            [TicketPriority.Critical] = StandardCriticalHours
        },
        PremiumCustomerMultiplier = PremiumMultiplier,
        MinimumWindowHours = MinimumWindowHours,
        AtRiskThresholdPercent = AtRiskPercent
    };

    [Theory]
    [InlineData(TicketPriority.Low, StandardLowHours)]
    [InlineData(TicketPriority.Medium, StandardMediumHours)]
    [InlineData(TicketPriority.High, StandardHighHours)]
    [InlineData(TicketPriority.Critical, StandardCriticalHours)]
    public void WindowFor_StandardCustomer_UsesBaseWindow(TicketPriority priority, int hours)
    {
        var policy = new SlaPolicy(Sla);

        Assert.Equal(TimeSpan.FromHours(hours), policy.WindowFor(priority, CustomerTier.Standard));
    }

    [Theory]
    [InlineData(TicketPriority.Low, StandardLowHours / 2)]
    [InlineData(TicketPriority.Medium, StandardMediumHours / 2)]
    [InlineData(TicketPriority.High, StandardHighHours / 2)]
    [InlineData(TicketPriority.Critical, StandardCriticalHours / 2)]
    public void WindowFor_PremiumCustomer_AppliesMultiplier(TicketPriority priority, int hours)
    {
        var policy = new SlaPolicy(Sla);

        Assert.Equal(TimeSpan.FromHours(hours), policy.WindowFor(priority, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_PremiumCustomerBelowFloor_UsesOneHour()
    {
        var settings = new SlaSettings
        {
            BaseWindowHours = new()
            {
                [TicketPriority.Low] = 1,
                [TicketPriority.Medium] = 1,
                [TicketPriority.High] = 1,
                [TicketPriority.Critical] = 1
            },
            PremiumCustomerMultiplier = PremiumMultiplier,
            MinimumWindowHours = MinimumWindowHours,
            AtRiskThresholdPercent = AtRiskPercent
        };

        Assert.Equal(TimeSpan.FromHours(MinimumWindowHours),
            new SlaPolicy(settings).WindowFor(TicketPriority.Low, CustomerTier.Premium));
    }

    [Fact]
    public void DueAt_FromStart_AddsSelectedWindow()
    {
        var policy = new SlaPolicy(Sla);

        Assert.Equal(Now.AddHours(StandardHighHours),
            policy.DueAt(TicketPriority.High, CustomerTier.Standard, Now));
    }

    [Fact]
    public void Evaluate_WithoutDueDate_IsNotApplicable()
    {
        Assert.Equal(SlaStatus.NotApplicable,
            new SlaPolicy(Sla).Evaluate(null, null, TicketPriority.Medium, CustomerTier.Standard, Now));
    }

    [Theory]
    [InlineData(-1, SlaStatus.Met)]
    [InlineData(0, SlaStatus.Met)]
    [InlineData(1, SlaStatus.Breached)]
    public void Evaluate_ResolvedAroundDue_UsesExpectedBoundary(int minutesAfterDue, SlaStatus expected)
    {
        var due = Now.AddHours(StandardCriticalHours);

        Assert.Equal(expected,
            new SlaPolicy(Sla).Evaluate(due, due.AddMinutes(minutesAfterDue),
                TicketPriority.Critical, CustomerTier.Standard, Now));
    }

    [Theory]
    [InlineData(-1, SlaStatus.AtRisk)]
    [InlineData(0, SlaStatus.AtRisk)]
    [InlineData(1, SlaStatus.Breached)]
    public void Evaluate_OpenAroundDue_UsesExpectedBoundary(int minutesAfterDue, SlaStatus expected)
    {
        var due = Now.AddHours(StandardCriticalHours);

        Assert.Equal(expected,
            new SlaPolicy(Sla).Evaluate(due, null, TicketPriority.Critical, CustomerTier.Standard,
                due.AddMinutes(minutesAfterDue)));
    }

    [Theory]
    [InlineData(61, SlaStatus.WithinSla)]
    [InlineData(60, SlaStatus.AtRisk)]
    [InlineData(59, SlaStatus.AtRisk)]
    public void Evaluate_RemainingAroundAtRiskThreshold_UsesExpectedBoundary(
        int remainingMinutes, SlaStatus expected)
    {
        var due = Now.AddHours(StandardCriticalHours);

        Assert.Equal(expected,
            new SlaPolicy(Sla).Evaluate(due, null, TicketPriority.Critical, CustomerTier.Standard,
                due.AddMinutes(-remainingMinutes)));
    }

    [Fact]
    public void Evaluate_AtRisk_UsesCurrentPriorityAndCustomerTierWindow()
    {
        var policy = new SlaPolicy(Sla);
        var due = Now.AddHours(1);

        Assert.Equal(SlaStatus.AtRisk,
            policy.Evaluate(due, null, TicketPriority.Critical, CustomerTier.Standard, Now));
        Assert.Equal(SlaStatus.WithinSla,
            policy.Evaluate(due, null, TicketPriority.Critical, CustomerTier.Premium, Now));
        Assert.Equal(SlaStatus.AtRisk,
            policy.Evaluate(due, null, TicketPriority.Low, CustomerTier.Premium, Now));
    }

    [Fact]
    public void Resolve_RequestedPriority_IsKept()
    {
        var decision = new PriorityPolicy().Resolve(TicketPriority.High, GeneralCategory());

        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Contains("High", decision.Reason);
    }

    [Fact]
    public void Resolve_NoRequestedPriority_DefaultsToMedium()
    {
        var decision = new PriorityPolicy().Resolve(null, GeneralCategory());

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Contains("defaulted to Medium", decision.Reason);
    }

    [Fact]
    public void Resolve_ForcedCategory_OverridesRequestedLow()
    {
        var decision = new PriorityPolicy().Resolve(TicketPriority.Low, SecurityCategory());

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("Security", decision.Reason);
    }

    [Fact]
    public void Select_FewestOpenTickets_Wins()
    {
        var first = Agent(1, "Alex", 10);
        var second = Agent(2, "Ben", 10);

        var decision = new AgentSelector().Select(GeneralCategory(),
            [new AgentWorkload(first, 5), new AgentWorkload(second, 3)]);

        Assert.Equal(second.Id, decision.AgentId);
        Assert.Contains("fewest open tickets (3) among 2 eligible agents", decision.Reason);
    }

    [Fact]
    public void Select_SpecialistCategory_RequiresMatchingSpecialization()
    {
        var specialist = Agent(1, "Priya", 8, SecurityCategoryId);
        var generalist = Agent(2, "Alex", 10);

        var decision = new AgentSelector().Select(SecurityCategory(),
            [new AgentWorkload(generalist, 0), new AgentWorkload(specialist, 4)]);

        Assert.Equal(specialist.Id, decision.AgentId);
    }

    [Fact]
    public void Select_AtLimitIsIneligible_JustBelowLimitIsEligible()
    {
        var full = Agent(1, "Full", 3);
        var available = Agent(2, "Available", 3);

        var decision = new AgentSelector().Select(GeneralCategory(),
            [new AgentWorkload(full, 3), new AgentWorkload(available, 2)]);

        Assert.Equal(available.Id, decision.AgentId);
    }

    [Fact]
    public void Select_InactiveAgent_IsIgnored()
    {
        var inactive = Agent(1, "Inactive", 10, isActive: false);
        var active = Agent(2, "Active", 10);

        var decision = new AgentSelector().Select(GeneralCategory(),
            [new AgentWorkload(inactive, 0), new AgentWorkload(active, 4)]);

        Assert.Equal(active.Id, decision.AgentId);
    }

    [Fact]
    public void Select_Tie_UsesLowestIdRegardlessOfInputOrder()
    {
        var lowerId = Agent(3, "Lower", 10);
        var higherId = Agent(8, "Higher", 10);
        var selector = new AgentSelector();

        var first = selector.Select(GeneralCategory(),
            [new AgentWorkload(higherId, 2), new AgentWorkload(lowerId, 2)]);
        var second = selector.Select(GeneralCategory(),
            [new AgentWorkload(lowerId, 2), new AgentWorkload(higherId, 2)]);

        Assert.Equal(lowerId.Id, first.AgentId);
        Assert.Equal(first.AgentId, second.AgentId);
    }

    [Fact]
    public void Select_NoActiveAgents_ReturnsActiveAgentReason()
    {
        var decision = new AgentSelector().Select(GeneralCategory(),
            [new AgentWorkload(Agent(1, "Inactive", 10, isActive: false), 0)]);

        Assert.Null(decision.AgentId);
        Assert.Contains("No active agents", decision.Reason);
    }

    [Fact]
    public void Select_NoActiveSpecialist_ReturnsSpecialistReason()
    {
        var decision = new AgentSelector().Select(SecurityCategory(),
            [new AgentWorkload(Agent(1, "Generalist", 10), 0)]);

        Assert.Null(decision.AgentId);
        Assert.Contains("No active specialist for Security", decision.Reason);
    }

    [Fact]
    public void Select_AllEligibleAtCapacity_ReturnsCapacityReason()
    {
        var decision = new AgentSelector().Select(GeneralCategory(),
            [new AgentWorkload(Agent(1, "Full", 3), 3)]);

        Assert.Null(decision.AgentId);
        Assert.Contains("at capacity", decision.Reason);
    }

    [Fact]
    public void Select_SeedAgents_ChoosesLowestWorkloadSpecialist()
    {
        var agents = new[]
        {
            Workload(1, "Alex", 10, 5, GeneralCategoryId, SecurityCategoryId),
            Workload(2, "Ben", 8, 3, GeneralCategoryId),
            Workload(3, "Priya", 8, 4, SecurityCategoryId),
            Workload(4, "Sara", 6, 2, GeneralCategoryId),
            Workload(5, "Marcus", 6, 6, SecurityCategoryId),
            Workload(6, "Yuki", 8, 0, false, SecurityCategoryId)
        };

        var decision = new AgentSelector().Select(SecurityCategory(), agents);

        Assert.Equal(3, decision.AgentId);
    }

    [Fact]
    public void Reevaluate_CurrentAgentAtLimit_KeepsCurrentAgent()
    {
        var current = Agent(7, "Current", 3);

        var decision = new AgentSelector().Reevaluate(GeneralCategory(), current.Id,
            [new AgentWorkload(current, 3)]);

        Assert.Equal(current.Id, decision.AgentId);
    }

    [Fact]
    public void Reevaluate_InactiveCurrentAgent_Reassigns()
    {
        var current = Agent(1, "Inactive", 10, isActive: false);
        var replacement = Agent(2, "Replacement", 10);

        var decision = new AgentSelector().Reevaluate(GeneralCategory(), current.Id,
            [new AgentWorkload(current, 0), new AgentWorkload(replacement, 3)]);

        Assert.Equal(replacement.Id, decision.AgentId);
    }

    [Fact]
    public void Reevaluate_CurrentAgentWithoutSpecialization_Reassigns()
    {
        var current = Agent(1, "Current", 10);
        var specialist = Agent(2, "Specialist", 10, SecurityCategoryId);

        var decision = new AgentSelector().Reevaluate(SecurityCategory(), current.Id,
            [new AgentWorkload(current, 0), new AgentWorkload(specialist, 2)]);

        Assert.Equal(specialist.Id, decision.AgentId);
    }

    [Fact]
    public void Reevaluate_WhenNobodyQualifies_ReturnsUnassignedReason()
    {
        var current = Agent(1, "Inactive", 10, isActive: false);

        var decision = new AgentSelector().Reevaluate(SecurityCategory(), current.Id,
            [new AgentWorkload(current, 0)]);

        Assert.Null(decision.AgentId);
        Assert.Contains("No active agents", decision.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_NonCriticalPriority_GoesUpExactlyOneLevel(
        TicketPriority startingPriority, TicketPriority expectedPriority)
    {
        var ticket = new TicketBuilder().WithPriority(startingPriority).Build();

        ticket.Escalate(expectedPriority, null, null, "Impact increased", "lead", Now);

        Assert.Equal(expectedPriority, ticket.Priority);
    }

    [Fact]
    public void Escalate_CriticalTicket_IsRejectedWithCriticalMessage()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.Critical, null, null, "Urgent issue", "lead", Now));

        Assert.Contains("already Critical", exception.Message);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved, "Resolved")]
    [InlineData(TicketStatus.Closed, "Closed")]
    public void Escalate_TerminalTicket_IsRejectedWithStatusMessage(
        TicketStatus status, string statusText)
    {
        var ticket = new TicketBuilder().WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.High, null, null, "Urgent issue", "lead", Now));

        Assert.Contains(statusText, exception.Message);
        Assert.Contains("cannot be escalated", exception.Message);
    }

    [Theory]
    [InlineData("four")]
    [InlineData("one")]
    public void Escalate_ReasonShorterThanFiveCharacters_IsRejected(string reason)
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<ArgumentException>(() =>
            ticket.Escalate(TicketPriority.High, null, null, reason, "lead", Now));
    }

    [Fact]
    public void Escalate_ReasonLongerThanFiveHundredCharacters_IsRejected()
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<ArgumentException>(() =>
            ticket.Escalate(TicketPriority.High, null, null, new string('x', 501), "lead", Now));
    }

    [Fact]
    public void Escalate_HistoryCapturesBeforeAndAfterValues()
    {
        var originalDueAt = Now.AddHours(StandardMediumHours);
        var newDueAt = Now.AddHours(StandardHighHours);
        var ticket = new TicketBuilder()
            .WithReference("TCK-0042")
            .WithCategoryId(GeneralCategoryId)
            .WithPriority(TicketPriority.Medium)
            .DueAt(originalDueAt)
            .AssignedTo(7)
            .Build();
        var escalatedAt = Now.AddMinutes(10);

        var history = ticket.Escalate(TicketPriority.High, newDueAt, 8,
            "Production impact", "lead@example.com", escalatedAt);

        Assert.Equal(TicketPriority.Medium, history.FromPriority);
        Assert.Equal(TicketPriority.High, history.ToPriority);
        Assert.Equal(7, history.FromAgentId);
        Assert.Equal(8, history.ToAgentId);
        Assert.Equal(originalDueAt, history.FromDueAtUtc);
        Assert.Equal(newDueAt, history.ToDueAtUtc);
        Assert.Equal("Production impact", history.Reason);
        Assert.Equal("lead@example.com", history.EscalatedBy);
        Assert.Equal(escalatedAt, history.EscalatedAtUtc);
    }

    [Fact]
    public void Triage_PremiumSecurity_UsesForcedCriticalAndSpecialist()
    {
        var category = SecurityCategory();
        var specialist = Agent(1, "Priya", 8, SecurityCategoryId);
        var triage = NewTriage();

        var result = triage.Triage(category, CustomerTier.Premium, TicketPriority.Low,
            [new AgentWorkload(specialist, 0)], Now);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Equal(Now.AddHours(StandardCriticalHours * PremiumMultiplier), result.DueAtUtc);
        Assert.Equal(specialist.Id, result.AssignedAgentId);
    }

    [Fact]
    public void Triage_StandardGeneralWithoutRequest_DefaultsToMedium()
    {
        var result = NewTriage().Triage(GeneralCategory(), CustomerTier.Standard, null, [], Now);

        Assert.Equal(TicketPriority.Medium, result.Priority);
        Assert.Equal(Now.AddHours(StandardMediumHours), result.DueAtUtc);
        Assert.Null(result.AssignedAgentId);
    }

    [Fact]
    public void PlanEscalation_ComputesDueDateFromEscalationMomentAndNewPriority()
    {
        var creationTime = Now;
        var escalationTime = Now.AddHours(2);
        var ticket = new TicketBuilder()
            .WithReference("TCK-0043")
            .WithCategoryId(GeneralCategoryId)
            .WithPriority(TicketPriority.Medium)
            .CreatedAt(creationTime)
            .DueAt(creationTime.AddHours(StandardMediumHours))
            .Build();

        var result = NewTriage().PlanEscalation(ticket, GeneralCategory(), CustomerTier.Standard,
            [], escalationTime);

        Assert.Equal(TicketPriority.High, result.Priority);
        Assert.Equal(escalationTime.AddHours(StandardHighHours), result.DueAtUtc);
    }

    private static Category GeneralCategory() => CategoryWithId("General", GeneralCategoryId, false, false);

    private static Category SecurityCategory() => CategoryWithId("Security", SecurityCategoryId, true, true);

    private static Category CategoryWithId(string name, int id, bool requiresSpecialist,
        bool forcesCriticalPriority)
    {
        var category = new Category(name, requiresSpecialist, forcesCriticalPriority);
        typeof(Category).GetProperty(nameof(Category.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(category, id);
        return category;
    }

    private static Agent Agent(int id, string name, int maxOpenTickets,
        int? specialization = null, bool isActive = true)
    {
        var agent = new Agent(name, $"{name.ToLowerInvariant()}@example.com", maxOpenTickets, Now);
        SetId(agent, id);
        if (specialization is { } categoryId)
        {
            agent.AddSpecialization(categoryId);
        }

        if (!isActive)
        {
            agent.Deactivate();
        }

        return agent;
    }

    private static AgentWorkload Workload(int id, string name, int maxOpenTickets, int openTickets,
        params int[] specializations) =>
        Workload(id, name, maxOpenTickets, openTickets, true, specializations);

    private static AgentWorkload Workload(int id, string name, int maxOpenTickets, int openTickets,
        bool isActive, params int[] specializations)
    {
        var agent = Agent(id, name, maxOpenTickets, isActive: isActive);
        foreach (var categoryId in specializations)
        {
            agent.AddSpecialization(categoryId);
        }

        return new(agent, openTickets);
    }

    private static TicketTriage NewTriage() =>
        new(new PriorityPolicy(), new SlaPolicy(Sla), new AgentSelector());

    private static void SetId(Agent agent, int id) =>
        typeof(SupportDesk.Domain.Aggregates.Agents.Agent)
            .GetProperty(nameof(SupportDesk.Domain.Aggregates.Agents.Agent.Id),
                BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(agent, id);
}
