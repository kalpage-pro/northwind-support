using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason => reason?.Trim().Length is >= 5 and <= 500)
            .WithMessage("Reason must be between 5 and 500 characters");

        RuleFor(x => x.EscalatedBy)
            .NotEmpty()
            .WithMessage("EscalatedBy is required")
            .Must(value => value.Trim().Length <= 100)
            .WithMessage("EscalatedBy must be 100 characters or fewer");
    }
}
