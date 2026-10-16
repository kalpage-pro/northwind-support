using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokenService)
{
    private const string DummyPasswordHash = "not-a-real-password-hash";

    public async Task<LoginResult> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);
        var hash = agent?.PasswordHash ?? DummyPasswordHash;
        var passwordMatches = passwordHasher.Verify(hash, request.Password);

        if (agent is null || !agent.CanSignIn || !passwordMatches)
        {
            return LoginResult.Failure();
        }

        return new(
            true,
            tokenService.Issue(agent),
            new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
