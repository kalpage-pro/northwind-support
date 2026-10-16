using SupportDesk.Application.Abstractions;

namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedAgentDto(int Id, string FullName, string Email);

public sealed record LoginResult(
    bool Succeeded,
    IssuedToken? IssuedToken,
    AuthenticatedAgentDto? User)
{
    public static LoginResult Failure() => new(false, null, null);
}

public sealed record CurrentUserResult(bool Succeeded, AuthenticatedAgentDto? User)
{
    public static CurrentUserResult Unauthorized() => new(false, null);
}
