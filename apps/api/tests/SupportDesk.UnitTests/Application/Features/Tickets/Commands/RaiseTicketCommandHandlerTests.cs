using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForcesCriticalCategoryAndAssignsSpecialist()
    {
        var context = new TicketCommandTestContext();
        var category = new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true);
        var agent = new Agent("Priya Nair", "priya@example.com", 8, context.Clock.UtcNow);
        agent.AddSpecialization(category.Id);
        context.Categories.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        context.Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(agent, 2)]);

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Equal(agent.Id, context.AddedTicket.AssignedAgentId);
        Assert.Equal(context.Clock.UtcNow.AddHours(2), context.AddedTicket.DueAtUtc);
        Assert.NotNull(result.Triage);
    }

    [Fact]
    public async Task HandleAsync_ForPremiumCustomer_UsesPremiumDueDate()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Medium), CancellationToken.None);

        Assert.Equal(context.Clock.UtcNow.AddHours(12), context.AddedTicket!.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsEligible_CreatesUnassignedWithReason()
    {
        var context = new TicketCommandTestContext();
        var category = new Category("General", false, false);
        var fullAgent = new Agent("Marcus Doyle", "marcus@example.com", 6, context.Clock.UtcNow);
        context.Categories.Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        context.Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(fullAgent, 6)]);

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket!.AssignedAgentId);
        Assert.Contains("capacity", result.Triage!.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }
}
