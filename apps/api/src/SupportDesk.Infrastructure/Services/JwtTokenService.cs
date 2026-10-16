using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Configuration;

namespace SupportDesk.Infrastructure.Services;

public sealed class JwtTokenService(JwtSettings settings, IClock clock) : IJwtTokenService
{
    public IssuedToken Issue(Agent agent)
    {
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(settings.LifetimeMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            NotBefore = now,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", agent.Id.ToString()),
                new Claim("name", agent.FullName),
                new Claim("email", agent.Email),
                new Claim("jti", Guid.NewGuid().ToString())
            ]),
            SigningCredentials = credentials
        });

        return new(token, expiresAtUtc);
    }
}
