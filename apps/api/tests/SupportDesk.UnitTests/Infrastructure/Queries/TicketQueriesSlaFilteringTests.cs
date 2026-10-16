using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesSlaFilteringTests : IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly SupportDbContext db;
    private readonly TicketQueries queries;
    private readonly SlaPolicy policy = new(DefaultSlaSettings());
    private readonly DateTime now = FixedClock.DefaultNow;
    private int standardCustomerId;
    private int premiumCustomerId;
    private int categoryId;

    public TicketQueriesSlaFilteringTests()
    {
        connection.Open();
        db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options);
        db.Database.EnsureCreated();

        var customers = new[]
        {
            new Customer("Standard", "standard@example.com", null, CustomerTier.Standard, now),
            new Customer("Premium", "premium@example.com", null, CustomerTier.Premium, now)
        };
        var category = new Category("General", false, false);
        db.AddRange(customers);
        db.Add(category);
        db.SaveChanges();

        standardCustomerId = customers[0].Id;
        premiumCustomerId = customers[1].Id;
        categoryId = category.Id;
        queries = new TicketQueries(db, new FixedClock(), policy);
    }

    [Fact]
    public async Task EachStatusReturnsOnlyMatchingTickets()
    {
        AddTicket("not-applicable", TicketPriority.Low, CustomerTier.Standard, null);
        AddTicket("met", TicketPriority.Low, CustomerTier.Standard, now.AddHours(1), now.AddHours(1));
        AddTicket("breached", TicketPriority.Low, CustomerTier.Standard, now.AddHours(-1));
        AddTicket("at-risk", TicketPriority.High, CustomerTier.Standard, policy.AtRiskBoundaryUtc(TicketPriority.High, CustomerTier.Standard, now));
        AddTicket("within", TicketPriority.Critical, CustomerTier.Premium, policy.AtRiskBoundaryUtc(TicketPriority.Critical, CustomerTier.Premium, now).AddSeconds(1));
        db.SaveChanges();

        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            var result = await GetAsync(new TicketQuery { SlaStatus = status });

            Assert.Single(result.Items);
            Assert.Equal(status, result.Items[0].SlaStatus);
        }
    }

    [Fact]
    public async Task BoundaryValuesMatchTheSlaDefinitions()
    {
        AddTicket("resolved-at-due", TicketPriority.Low, CustomerTier.Standard, now.AddHours(1), now.AddHours(1));
        AddTicket("resolved-after-due", TicketPriority.Low, CustomerTier.Standard, now.AddHours(1), now.AddHours(1).AddSeconds(1));
        AddTicket("now-at-due", TicketPriority.Medium, CustomerTier.Standard, now);
        AddTicket("now-after-due", TicketPriority.Medium, CustomerTier.Standard, now.AddSeconds(-1));

        foreach (var tier in Enum.GetValues<CustomerTier>())
        {
            foreach (var priority in new[] { TicketPriority.High, TicketPriority.Critical })
            {
                var boundary = policy.AtRiskBoundaryUtc(priority, tier, now);
                AddTicket($"risk-{priority}-{tier}", priority, tier, boundary);
                AddTicket($"within-{priority}-{tier}", priority, tier, boundary.AddSeconds(1));
            }
        }

        db.SaveChanges();
        Assert.Equal(1, (await GetAsync(new TicketQuery { SlaStatus = SlaStatus.Met })).TotalCount);
        Assert.Equal(2, (await GetAsync(new TicketQuery { SlaStatus = SlaStatus.Breached })).TotalCount);
        Assert.Equal(5, (await GetAsync(new TicketQuery { SlaStatus = SlaStatus.AtRisk })).TotalCount);
        Assert.Equal(4, (await GetAsync(new TicketQuery { SlaStatus = SlaStatus.WithinSla })).TotalCount);
    }

    [Fact]
    public async Task SlaFilterCombinesWithPriorityAndPaging()
    {
        for (var index = 0; index < 7; index++)
        {
            AddTicket($"high-{index}", TicketPriority.High, CustomerTier.Standard, now.AddHours(-1));
        }

        AddTicket("low", TicketPriority.Low, CustomerTier.Standard, now.AddHours(-1));
        db.SaveChanges();

        var result = await GetAsync(new TicketQuery
        {
            SlaStatus = SlaStatus.Breached,
            Priority = TicketPriority.High,
            Page = 2,
            PageSize = 5
        });

        Assert.Equal(7, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task SqlFilterMatchesSlaPolicyAcrossPrioritiesTiersAndDates()
    {
        var expectedById = new Dictionary<string, SlaStatus>();
        var offsets = new[] { -1, 0, 1, 2, 3 };

        foreach (var tier in Enum.GetValues<CustomerTier>())
        {
            foreach (var priority in Enum.GetValues<TicketPriority>())
            {
                var boundary = policy.AtRiskBoundaryUtc(priority, tier, now);
                foreach (var offset in offsets)
                {
                    var due = boundary.AddSeconds(offset - 2);
                    var unresolvedReference = $"unresolved-{priority}-{tier}-{offset}";
                    var resolvedReference = $"resolved-{priority}-{tier}-{offset}";
                    AddTicket(unresolvedReference, priority, tier, due);
                    AddTicket(resolvedReference, priority, tier, due, due);
                    expectedById[unresolvedReference] = policy.Evaluate(due, null, priority, tier, now);
                    expectedById[resolvedReference] = policy.Evaluate(due, due, priority, tier, now);
                }
            }
        }

        db.SaveChanges();
        var actualByReference = new Dictionary<string, SlaStatus>();

        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            var result = await GetAsync(new TicketQuery { SlaStatus = status, PageSize = 100 });
            foreach (var item in result.Items)
            {
                Assert.True(actualByReference.TryAdd(item.Reference, status));
            }
        }

        Assert.Equal(expectedById.Count, actualByReference.Count);
        foreach (var expected in expectedById)
        {
            Assert.Equal(expected.Value, actualByReference[expected.Key]);
        }
    }

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }

    private void AddTicket(
        string reference,
        TicketPriority priority,
        CustomerTier tier,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc = null)
    {
        var customerId = tier == CustomerTier.Premium ? premiumCustomerId : standardCustomerId;
        var ticket = Ticket.Raise(
            reference, "SLA ticket", "Description", customerId, categoryId, priority, now, dueAtUtc);
        if (resolvedAtUtc is not null)
        {
            ticket.ChangeStatus(TicketStatus.Resolved, resolvedAtUtc.Value);
        }

        db.Add(ticket);
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
