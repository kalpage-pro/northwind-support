using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower(), ct);

    public async Task<IReadOnlyList<AgentWorkload>> GetWorkloadsAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .Include(a => a.Specializations)
            .Select(agent => new AgentWorkload(
                agent,
                db.Set<Ticket>().Count(ticket =>
                    ticket.AssignedAgentId == agent.Id &&
                    ticket.Status != TicketStatus.Resolved &&
                    ticket.Status != TicketStatus.Closed)))
            .ToListAsync(ct);
}
