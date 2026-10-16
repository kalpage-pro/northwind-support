using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public interface IJwtTokenService
{
    IssuedToken Issue(Agent agent);
}

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);
