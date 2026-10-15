using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TicketTriageDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason,
    double SlaWindowHours);

public sealed record TicketEscalationDto(
    int Id,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime? ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

public sealed record EscalationResultDto(
    TicketDetailDto Ticket,
    TicketEscalationDto Escalation);
