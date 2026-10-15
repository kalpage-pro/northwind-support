using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>Plans and records a ticket escalation as one unit of work.</summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    TicketTriage triage,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<EscalationResultDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var workloads = await agents.GetWorkloadsAsync(ct);
        var nowUtc = clock.UtcNow;
        var plan = triage.PlanEscalation(ticket, category, customer.Tier, workloads, nowUtc);
        var escalation = ticket.Escalate(
            plan.Priority,
            plan.DueAtUtc,
            plan.AssignedAgentId,
            request.Reason,
            request.EscalatedBy,
            nowUtc);

        await escalations.AddAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority}.",
            ticket.Reference,
            escalation.FromPriority,
            escalation.ToPriority);

        var fromAgent = workloads.FirstOrDefault(x => x.Agent.Id == escalation.FromAgentId)?.Agent;
        var toAgent = workloads.FirstOrDefault(x => x.Agent.Id == escalation.ToAgentId)?.Agent;
        return new(
            await getTicket.HandleAsync(id, ct),
            new TicketEscalationDto(
                escalation.Id,
                escalation.FromPriority,
                escalation.ToPriority,
                fromAgent is null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                toAgent is null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                escalation.FromDueAtUtc,
                escalation.ToDueAtUtc,
                escalation.Reason,
                escalation.EscalatedBy,
                escalation.EscalatedAtUtc));
    }
}
