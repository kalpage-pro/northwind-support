using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesFilteringTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SupportDbContext db;
    private readonly TicketQueries queries;
    private readonly int customerId;
    private readonly int otherCustomerId;
    private readonly int generalCategoryId;
    private readonly int billingCategoryId;
    private readonly int firstAgentId;
    private readonly int secondAgentId;

    public TicketQueriesFilteringTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;
        db = new SupportDbContext(options);
        db.Database.EnsureCreated();

        var customers = new[]
        {
            new Customer("Contoso Ltd", "contoso@example.com", null, CustomerTier.Premium, FixedClock.DefaultNow),
            new Customer("Fabrikam Inc", "fabrikam@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow)
        };
        var categories = new[]
        {
            new Category("General", false, false),
            new Category("Billing", false, false)
        };
        var agents = new[]
        {
            new Agent("Alex Turner", "alex@example.com", 10, FixedClock.DefaultNow),
            new Agent("Ben Osei", "ben@example.com", 10, FixedClock.DefaultNow)
        };
        db.AddRange(customers);
        db.AddRange(categories);
        db.AddRange(agents);
        db.SaveChanges();

        customerId = customers[0].Id;
        otherCustomerId = customers[1].Id;
        generalCategoryId = categories[0].Id;
        billingCategoryId = categories[1].Id;
        firstAgentId = agents[0].Id;
        secondAgentId = agents[1].Id;

        for (var index = 1; index <= 20; index++)
        {
            var builder = new TicketBuilder()
                .WithReference($"TCK-{index:0000}")
                .WithCustomerId(index <= 10 ? customerId : otherCustomerId)
                .WithCategoryId(index % 2 == 1 ? generalCategoryId : billingCategoryId)
                .WithPriority(index <= 7 ? TicketPriority.High : TicketPriority.Low)
                .WithTitle(index == 1 ? "Invoice access issue" : $"Ticket {index}")
                .CreatedAt(FixedClock.DefaultNow.AddMinutes(index));

            if (index <= 5)
            {
                builder.WithStatus(TicketStatus.InProgress);
            }

            if (index <= 4)
            {
                builder.AssignedTo(firstAgentId);
            }
            else if (index <= 8)
            {
                builder.AssignedTo(secondAgentId);
            }

            db.Add(builder.Build());
        }

        db.SaveChanges();
        queries = new TicketQueries(db, new FixedClock(), new SlaPolicy(DefaultSlaSettings()));
    }

    [Fact]
    public async Task NoFiltersReturnsAllTickets()
    {
        var result = await GetAsync(new TicketQuery());

        Assert.Equal(20, result.TotalCount);
        Assert.Equal(20, result.Items.Count);
    }

    [Fact]
    public async Task ExactMatchFiltersReturnOnlyMatchingTickets()
    {
        Assert.Equal(7, (await GetAsync(new TicketQuery { Priority = TicketPriority.High })).TotalCount);
        Assert.Equal(5, (await GetAsync(new TicketQuery { Status = TicketStatus.InProgress })).TotalCount);
        Assert.Equal(10, (await GetAsync(new TicketQuery { CategoryId = generalCategoryId })).TotalCount);
        Assert.Equal(10, (await GetAsync(new TicketQuery { CustomerId = customerId })).TotalCount);
        Assert.Equal(4, (await GetAsync(new TicketQuery { AssignedAgentId = firstAgentId })).TotalCount);
    }

    [Fact]
    public async Task FiltersCombineWithAnd()
    {
        var result = await GetAsync(new TicketQuery
        {
            Priority = TicketPriority.High,
            CustomerId = customerId,
            CategoryId = generalCategoryId
        });

        Assert.Equal(4, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task UnassignedOnlyWorksAloneAndWithPriority()
    {
        Assert.Equal(12, (await GetAsync(new TicketQuery { UnassignedOnly = true })).TotalCount);

        var result = await GetAsync(new TicketQuery
        {
            UnassignedOnly = true,
            Priority = TicketPriority.Low
        });

        Assert.Equal(12, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.Low, item.Priority));
    }

    [Fact]
    public async Task SearchMatchesCustomerTitleReferenceAndIgnoresBlankText()
    {
        Assert.Equal(10, (await GetAsync(new TicketQuery { Search = "Contoso" })).TotalCount);
        Assert.Equal(1, (await GetAsync(new TicketQuery { Search = "Invoice" })).TotalCount);
        Assert.Equal(1, (await GetAsync(new TicketQuery { Search = "0012" })).TotalCount);
        Assert.Equal(10, (await GetAsync(new TicketQuery { Search = "  Contoso  " })).TotalCount);
        Assert.Equal(20, (await GetAsync(new TicketQuery { Search = "   " })).TotalCount);
    }

    [Fact]
    public async Task SearchCombinesWithAnotherFilter()
    {
        var result = await GetAsync(new TicketQuery
        {
            Search = "Contoso",
            Priority = TicketPriority.High
        });

        Assert.Equal(7, result.TotalCount);
    }

    [Fact]
    public async Task TotalCountAndPagesReflectFilteredSet()
    {
        var result = await GetAsync(new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 2,
            PageSize = 5
        });

        Assert.Equal(7, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private Task<PagedResult<TicketListItemDto>> GetAsync(TicketQuery query) =>
        queries.GetPagedAsync(query.Normalized(), CancellationToken.None);

    private static SlaSettings DefaultSlaSettings() => new()
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
