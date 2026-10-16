using System.Security.Claims;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Security;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("The current user is unavailable outside an HTTP request.");

    public int AgentId => int.TryParse(FindRequired("sub"), out var id)
        ? id
        : throw new InvalidOperationException("The authenticated user has an invalid agent id claim.");

    public string FullName => FindRequired("name");

    public string Email => FindRequired("email");

    private string FindRequired(string claimType) =>
        Principal.FindFirst(claimType)?.Value
        ?? throw new InvalidOperationException(
            $"The authenticated user is missing the required '{claimType}' claim.");
}
