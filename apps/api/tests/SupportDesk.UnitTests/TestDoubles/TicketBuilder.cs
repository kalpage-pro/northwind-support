using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// Builds tickets for tests, so each test only has to say what actually matters to it. The
/// ticket is driven into the requested state through its own behaviour, so a test can never
/// start from a state the domain would not allow.
/// </summary>
public sealed class TicketBuilder
{
    private string _reference = "TCK-0001";
    private string _title = "Something is not working";
    private string _description = "A description long enough to be realistic.";
    private TicketPriority _priority = TicketPriority.Medium;
    private TicketStatus _status = TicketStatus.New;
    private int _customerId = 1;
    private int _categoryId = 1;
    private int? _assignedAgentId;
    private DateTime _createdAtUtc = FixedClock.DefaultNow;
    private DateTime? _dueAtUtc;
    private DateTime? _resolvedAtUtc;

    public TicketBuilder WithReference(string reference)
    {
        _reference = reference;
        return this;
    }

    public TicketBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TicketBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public TicketBuilder WithPriority(TicketPriority priority)
    {
        _priority = priority;
        return this;
    }

    public TicketBuilder WithStatus(TicketStatus status)
    {
        _status = status;
        return this;
    }

    public TicketBuilder WithCustomerId(int customerId)
    {
        _customerId = customerId;
        return this;
    }

    public TicketBuilder WithCategoryId(int categoryId)
    {
        _categoryId = categoryId;
        return this;
    }

    public TicketBuilder AssignedTo(int? agentId)
    {
        _assignedAgentId = agentId;
        return this;
    }

    public TicketBuilder CreatedAt(DateTime createdAtUtc)
    {
        _createdAtUtc = createdAtUtc;
        return this;
    }

    public TicketBuilder DueAt(DateTime? dueAtUtc)
    {
        _dueAtUtc = dueAtUtc;
        return this;
    }

    /// <summary>When the ticket reached its status; for Resolved and Closed, the resolution time.</summary>
    public TicketBuilder ResolvedAt(DateTime? resolvedAtUtc)
    {
        _resolvedAtUtc = resolvedAtUtc;
        return this;
    }

    public Ticket Build()
    {
        var ticket = Ticket.Raise(
            _reference,
            _title,
            _description,
            customerId: _customerId,
            categoryId: _categoryId,
            _priority,
            _createdAtUtc,
            _dueAtUtc);

        if (_assignedAgentId is not null)
        {
            ticket.AssignTo(_assignedAgentId, _createdAtUtc);
        }

        if (_status != TicketStatus.New)
        {
            ticket.ChangeStatus(_status, _resolvedAtUtc ?? _createdAtUtc);
        }

        return ticket;
    }
}
