using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

/// <summary>Stores ticket escalation history.</summary>
public interface ITicketEscalationRepository
{
    Task AddAsync(TicketEscalation escalation, CancellationToken ct);
}
