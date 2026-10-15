using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_IncreasesPriorityComputesDueDateAddsHistoryAndSavesOnce()
    {
        var context = new HandlerContext();
        var ticket = Ticket.Raise("TCK-0044", "Title", "Description", 1, 1,
            TicketPriority.Medium, context.Clock.UtcNow, context.Clock.UtcNow.AddHours(24));
        context.Tickets.Setup(t => t.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await context.Handler.HandleAsync(
            4, new EscalateTicketRequest("Production impact", "lead@example.com"), CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
        Assert.NotNull(context.AddedEscalation);
        Assert.Equal(TicketPriority.Medium, context.AddedEscalation!.FromPriority);
        Assert.Equal(TicketPriority.High, context.AddedEscalation.ToPriority);
        Assert.Equal(1, context.SaveCount);
        Assert.Equal(TicketPriority.High, result.Escalation.ToPriority);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentRemainsEligible_KeepsOwner()
    {
        var context = new HandlerContext();
        var current = Agent(7, "Current", 1);
        var ticket = Ticket.Raise("TCK-0045", "Title", "Description", 1, 1,
            TicketPriority.Medium, context.Clock.UtcNow);
        ticket.AssignTo(current.Id, context.Clock.UtcNow);
        context.Tickets.Setup(t => t.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        context.Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(current, 1)]);

        await context.Handler.HandleAsync(
            5, new EscalateTicketRequest("Still severe", "lead"), CancellationToken.None);

        Assert.Equal(current.Id, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentIsInactive_ReassignsOwner()
    {
        var context = new HandlerContext();
        var current = Agent(7, "Inactive", 10, isActive: false);
        var replacement = Agent(8, "Replacement", 10);
        var ticket = Ticket.Raise("TCK-0046", "Title", "Description", 1, 1,
            TicketPriority.Medium, context.Clock.UtcNow);
        ticket.AssignTo(current.Id, context.Clock.UtcNow);
        context.Tickets.Setup(t => t.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        context.Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(current, 1), new AgentWorkload(replacement, 2)]);

        await context.Handler.HandleAsync(
            6, new EscalateTicketRequest("Still severe", "lead"), CancellationToken.None);

        Assert.Equal(replacement.Id, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyQualifies_LeavesTicketUnassigned()
    {
        var context = new HandlerContext();
        var full = Agent(9, "Inactive", 1, isActive: false);
        var ticket = Ticket.Raise("TCK-0047", "Title", "Description", 1, 1,
            TicketPriority.Medium, context.Clock.UtcNow);
        ticket.AssignTo(full.Id, context.Clock.UtcNow);
        context.Tickets.Setup(t => t.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        context.Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(full, 1)]);

        await context.Handler.HandleAsync(
            7, new EscalateTicketRequest("Still severe", "lead"), CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsNotFound()
    {
        var context = new HandlerContext();
        context.Tickets.Setup(t => t.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => context.Handler.HandleAsync(
            99, new EscalateTicketRequest("Still severe", "lead"), CancellationToken.None));

        Assert.Equal(0, context.SaveCount);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New, "already Critical")]
    [InlineData(TicketPriority.High, TicketStatus.Resolved, "Resolved")]
    [InlineData(TicketPriority.High, TicketStatus.Closed, "Closed")]
    public async Task HandleAsync_WhenEscalationIsBlocked_ThrowsConflictWithoutSaving(
        TicketPriority priority, TicketStatus status, string expectedMessage)
    {
        var context = new HandlerContext();
        var ticket = new TicketBuilder().WithPriority(priority).WithStatus(status).Build();
        context.Tickets.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() => context.Handler.HandleAsync(
            10, new EscalateTicketRequest("Still severe", "lead"), CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
        Assert.Equal(0, context.SaveCount);
        Assert.Null(context.AddedEscalation);
    }

    [Theory]
    [InlineData("tiny", "lead")]
    [InlineData("", "lead")]
    [InlineData("valid reason", " ")]
    public async Task Validator_InvalidReasonOrEscalatedBy_ReturnsExpectedErrors(
        string reason, string escalatedBy)
    {
        var result = await new EscalateTicketRequestValidator()
            .ValidateAsync(new EscalateTicketRequest(reason, escalatedBy));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validator_ReasonLongerThanFiveHundred_ReturnsReasonError()
    {
        var result = await new EscalateTicketRequestValidator()
            .ValidateAsync(new EscalateTicketRequest(new string('x', 501), "lead"));

        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Reason must be between 5 and 500 characters");
    }

    private static Agent Agent(int id, string name, int maxOpenTickets, bool isActive = true)
    {
        var agent = new Agent(name, $"{name}@example.com", maxOpenTickets, Now);
        typeof(SupportDesk.Domain.Aggregates.Agents.Agent)
            .GetProperty(nameof(SupportDesk.Domain.Aggregates.Agents.Agent.Id),
                BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(agent, id);
        if (!isActive)
        {
            agent.Deactivate();
        }

        return agent;
    }

    private static DateTime Now => FixedClock.DefaultNow;

    private sealed class HandlerContext
    {
        public HandlerContext()
        {
            var queries = new Mock<ITicketQueries>();
            queries.Setup(q => q.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TicketDetailDto());
            var getTicket = new GetTicketQueryHandler(queries.Object);
            Handler = new EscalateTicketCommandHandler(
                Tickets.Object,
                Escalations.Object,
                Customers.Object,
                Categories.Object,
                Agents.Object,
                UnitOfWork.Object,
                Clock,
                new TicketTriage(new PriorityPolicy(), new SlaPolicy(Settings()), new AgentSelector()),
                getTicket,
                NullLogger<EscalateTicketCommandHandler>.Instance);

            Customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Customer("Contoso", "contoso@example.com", null, CustomerTier.Standard, Clock.UtcNow));
            Categories.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Category("General", false, false));
            Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AgentWorkload>());
            Escalations.Setup(e => e.AddAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()))
                .Callback<TicketEscalation, CancellationToken>((escalation, _) => AddedEscalation = escalation)
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => SaveCount++)
                .ReturnsAsync(1);
        }

        public Mock<ITicketRepository> Tickets { get; } = new();
        public Mock<ITicketEscalationRepository> Escalations { get; } = new();
        public Mock<ICustomerRepository> Customers { get; } = new();
        public Mock<ICategoryRepository> Categories { get; } = new();
        public Mock<IAgentRepository> Agents { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public FixedClock Clock { get; } = new();
        public EscalateTicketCommandHandler Handler { get; }
        public TicketEscalation? AddedEscalation { get; private set; }
        public int SaveCount { get; private set; }
    }

    private static SlaSettings Settings() => new()
    {
        BaseWindowHours = new()
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };
}
