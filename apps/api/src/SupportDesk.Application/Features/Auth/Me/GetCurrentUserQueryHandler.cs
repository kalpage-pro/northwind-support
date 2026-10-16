using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Me;

public sealed class GetCurrentUserQueryHandler(IAgentRepository agents, ICurrentUser currentUser)
{
    public async Task<CurrentUserResult> HandleAsync(CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(currentUser.AgentId, ct);
        if (agent is null || !agent.CanSignIn)
        {
            return CurrentUserResult.Unauthorized();
        }

        return new(true, new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
