using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketEscalationQueriesTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SupportDbContext db;
    private readonly TicketQueries queries;
    private readonly Ticket ticket;
    private readonly Ticket otherTicket;

    public TicketEscalationQueriesTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options);
        db.Database.EnsureCreated();

        var customer = new Customer("Contoso", "contoso@example.com", null,
            CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        var fromAgent = new Agent("Alex Turner", "alex@example.com", 10, FixedClock.DefaultNow);
        var toAgent = new Agent("Ben Osei", "ben@example.com", 10, FixedClock.DefaultNow);
        db.AddRange(customer, category, fromAgent, toAgent);
        db.SaveChanges();

        ticket = new TicketBuilder()
            .WithReference("TCK-0100")
            .WithCustomerId(customer.Id)
            .WithCategoryId(category.Id)
            .WithPriority(TicketPriority.Critical)
            .DueAt(FixedClock.DefaultNow.AddHours(1))
            .AssignedTo(fromAgent.Id)
            .Build();
        otherTicket = new TicketBuilder()
            .WithReference("TCK-0101")
            .WithCustomerId(customer.Id)
            .WithCategoryId(category.Id)
            .WithPriority(TicketPriority.High)
            .Build();
        db.AddRange(ticket, otherTicket);
        db.SaveChanges();

        db.AddRange(
            new TicketEscalation(ticket.Id, TicketPriority.High, TicketPriority.Critical,
                fromAgent.Id, toAgent.Id, FixedClock.DefaultNow, FixedClock.DefaultNow.AddHours(1),
                "First escalation", "lead", FixedClock.DefaultNow.AddMinutes(1)),
            new TicketEscalation(ticket.Id, TicketPriority.Medium, TicketPriority.High,
                null, fromAgent.Id, null, FixedClock.DefaultNow,
                "Older escalation", "manager", FixedClock.DefaultNow),
            new TicketEscalation(otherTicket.Id, TicketPriority.Medium, TicketPriority.High,
                null, fromAgent.Id, null, null,
                "Other ticket escalation", "manager", FixedClock.DefaultNow.AddMinutes(2)));
        db.SaveChanges();
        queries = new TicketQueries(db, new FixedClock(), new SlaPolicy(Settings()));
    }

    [Fact]
    public async Task GetEscalationsAsync_ReturnsOnlyTicketHistoryNewestFirstWithAgentNames()
    {
        var result = await queries.GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("First escalation", result[0].Reason);
        Assert.Equal("Older escalation", result[1].Reason);
        Assert.Equal("Alex Turner", result[0].FromAgent!.FullName);
        Assert.Equal("Ben Osei", result[0].ToAgent!.FullName);
        Assert.Equal(TicketPriority.High, result[0].FromPriority);
        Assert.Equal(TicketPriority.Critical, result[0].ToPriority);
    }

    [Fact]
    public async Task GetDetailAsync_UsesCurrentPriorityAndTierForAtRiskStatus()
    {
        var result = await queries.GetDetailAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(SlaStatus.AtRisk, result.SlaStatus);
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
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
