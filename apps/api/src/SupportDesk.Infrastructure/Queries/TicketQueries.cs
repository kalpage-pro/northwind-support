using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaPolicy slaPolicy) : ITicketQueries
{
    /// <remarks>
    /// Applies every filter in <paramref name="query"/>, then sorts and pages the filtered
    /// tickets. Counting and paging therefore reflect the filtered result set.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tickets = ApplyFilters(TicketsWithLabels(), query, now);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.SlaStatus = slaPolicy.Evaluate(
                item.DueAtUtc,
                item.ResolvedAtUtc,
                item.Priority,
                item.Customer.Tier,
                now);
        }

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;

        var item = await TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            })
            .FirstOrDefaultAsync(ct);

        if (item is not null)
        {
            item.SlaStatus = slaPolicy.Evaluate(
                item.DueAtUtc,
                item.ResolvedAtUtc,
                item.Priority,
                item.Customer.Tier,
                now);
        }

        return item;
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        var items = await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.SlaStatus = slaPolicy.Evaluate(
                item.DueAtUtc,
                item.ResolvedAtUtc,
                item.Priority,
                item.Customer.Tier,
                now);
        }

        return items;
    }

    public async Task<IReadOnlyList<TicketEscalationDto>> GetEscalationsAsync(int ticketId, CancellationToken ct) =>
        await (from escalation in db.Set<TicketEscalation>().AsNoTracking()
               join fromAgent in db.Set<Agent>() on escalation.FromAgentId equals fromAgent.Id into fromAgents
               from fromAgent in fromAgents.DefaultIfEmpty()
               join toAgent in db.Set<Agent>() on escalation.ToAgentId equals toAgent.Id into toAgents
               from toAgent in toAgents.DefaultIfEmpty()
               where escalation.TicketId == ticketId
               orderby escalation.EscalatedAtUtc descending, escalation.Id descending
               select new TicketEscalationDto(
                   escalation.Id,
                   escalation.FromPriority,
                   escalation.ToPriority,
                   fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                   toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                   escalation.FromDueAtUtc,
                   escalation.ToDueAtUtc,
                   escalation.Reason,
                   escalation.EscalatedBy,
                   escalation.EscalatedAtUtc))
            .ToListAsync(ct);

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    private IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        DateTime nowUtc)
    {
        source = FilterByStatus(source, query.Status);
        source = FilterByPriority(source, query.Priority);
        source = FilterBySlaStatus(source, query.SlaStatus, nowUtc);
        source = FilterByCategory(source, query.CategoryId);
        source = FilterByCustomer(source, query.CustomerId);
        source = FilterByAssignedAgent(source, query.AssignedAgentId);
        source = FilterUnassignedOnly(source, query.UnassignedOnly);
        source = FilterBySearch(source, query.Search);

        return source;
    }

    private IQueryable<TicketWithLabels> FilterBySlaStatus(
        IQueryable<TicketWithLabels> source,
        SlaStatus? status,
        DateTime nowUtc)
    {
        if (status is null)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(TicketWithLabels), "x");
        var ticket = Expression.Property(parameter, nameof(TicketWithLabels.Ticket));
        var dueAt = Expression.Property(ticket, nameof(Ticket.DueAtUtc));
        var resolvedAt = Expression.Property(ticket, nameof(Ticket.ResolvedAtUtc));
        var dueValue = Expression.Property(dueAt, nameof(Nullable<DateTime>.Value));
        var resolvedValue = Expression.Property(resolvedAt, nameof(Nullable<DateTime>.Value));
        var dueExists = Expression.NotEqual(dueAt, Expression.Constant(null, typeof(DateTime?)));
        var resolvedExists = Expression.NotEqual(resolvedAt, Expression.Constant(null, typeof(DateTime?)));
        var now = Expression.Constant(nowUtc);
        var boundaryMatches = BuildBoundaryMatches(parameter, dueValue, nowUtc);

        Expression predicate = status.Value switch
        {
            SlaStatus.NotApplicable => Expression.Not(dueExists),
            SlaStatus.Met => Expression.AndAlso(
                dueExists,
                Expression.AndAlso(resolvedExists, Expression.LessThanOrEqual(resolvedValue, dueValue))),
            SlaStatus.Breached => Expression.AndAlso(
                dueExists,
                Expression.OrElse(
                    Expression.AndAlso(resolvedExists, Expression.GreaterThan(resolvedValue, dueValue)),
                    Expression.AndAlso(Expression.Not(resolvedExists), Expression.GreaterThan(now, dueValue)))),
            SlaStatus.AtRisk => Expression.AndAlso(
                dueExists,
                Expression.AndAlso(
                    Expression.Not(resolvedExists),
                    Expression.AndAlso(
                        Expression.LessThanOrEqual(now, dueValue),
                        boundaryMatches))),
            SlaStatus.WithinSla => Expression.AndAlso(
                dueExists,
                Expression.AndAlso(
                    Expression.Not(resolvedExists),
                    Expression.AndAlso(
                        Expression.LessThanOrEqual(now, dueValue),
                        Expression.Not(boundaryMatches)))),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        return source.Where(Expression.Lambda<Func<TicketWithLabels, bool>>(predicate, parameter));
    }

    // Build the enum cross-product explicitly so each provider compares dates to captured constants.
    private Expression BuildBoundaryMatches(ParameterExpression parameter, MemberExpression dueValue, DateTime nowUtc)
    {
        var ticket = Expression.Property(parameter, nameof(TicketWithLabels.Ticket));
        var priority = Expression.Property(ticket, nameof(Ticket.Priority));
        var tier = Expression.Property(Expression.Property(parameter, nameof(TicketWithLabels.Customer)), nameof(Customer.Tier));
        Expression? matches = null;

        foreach (var priorityValue in Enum.GetValues<TicketPriority>())
        {
            foreach (var tierValue in Enum.GetValues<CustomerTier>())
            {
                var boundary = Expression.LessThanOrEqual(
                    dueValue,
                    Expression.Constant(slaPolicy.AtRiskBoundaryUtc(priorityValue, tierValue, nowUtc)));
                var pair = Expression.AndAlso(
                    Expression.Equal(priority, Expression.Constant(priorityValue)),
                    Expression.Equal(tier, Expression.Constant(tierValue)));
                var match = Expression.AndAlso(pair, boundary);
                matches = matches is null ? match : Expression.OrElse(matches, match);
            }
        }

        return matches!;
    }

    private static IQueryable<TicketWithLabels> FilterByStatus(
        IQueryable<TicketWithLabels> source,
        TicketStatus? status) => status is { } value ? source.Where(x => x.Ticket.Status == value) : source;

    private static IQueryable<TicketWithLabels> FilterByPriority(
        IQueryable<TicketWithLabels> source,
        TicketPriority? priority) => priority is { } value ? source.Where(x => x.Ticket.Priority == value) : source;

    private static IQueryable<TicketWithLabels> FilterByCategory(
        IQueryable<TicketWithLabels> source,
        int? categoryId) => categoryId is { } value ? source.Where(x => x.Ticket.CategoryId == value) : source;

    private static IQueryable<TicketWithLabels> FilterByCustomer(
        IQueryable<TicketWithLabels> source,
        int? customerId) => customerId is { } value ? source.Where(x => x.Ticket.CustomerId == value) : source;

    private static IQueryable<TicketWithLabels> FilterByAssignedAgent(
        IQueryable<TicketWithLabels> source,
        int? assignedAgentId) => assignedAgentId is { } value
        ? source.Where(x => x.Ticket.AssignedAgentId == value)
        : source;

    // Combined with an agent filter this matches nothing, deliberately.
    private static IQueryable<TicketWithLabels> FilterUnassignedOnly(
        IQueryable<TicketWithLabels> source,
        bool unassignedOnly) => unassignedOnly ? source.Where(x => x.Ticket.AssignedAgentId == null) : source;

    // Trimmed, blank search is ignored; Contains is translated to escaped SQL and case sensitivity follows the DB collation.
    private static IQueryable<TicketWithLabels> FilterBySearch(
        IQueryable<TicketWithLabels> source,
        string? search)
    {
        var text = search?.Trim();

        return string.IsNullOrEmpty(text)
            ? source
            : source.Where(x =>
                x.Ticket.Title.Contains(text) ||
                x.Ticket.Reference.Contains(text) ||
                x.Customer.Name.Contains(text));
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
