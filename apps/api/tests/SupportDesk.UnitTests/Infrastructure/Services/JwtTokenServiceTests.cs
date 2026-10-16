using System.Reflection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Configuration;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class JwtTokenServiceTests
{
    private const string Key = "a-valid-development-key-that-is-at-least-32-bytes";

    [Fact]
    public async Task Issue_CreatesValidTokenWithExpectedClaimsAndExpiry()
    {
        var clock = new FixedClock(DateTime.UtcNow);
        var settings = Settings(Key);
        var agent = new Agent("Alex Turner", "alex@example.com", 10, clock.UtcNow);
        SetId(agent, 7);

        var issued = new JwtTokenService(settings, clock).Issue(agent);
        var validation = await Validate(issued.Token, Key, settings);

        Assert.True(validation.IsValid);
        Assert.Equal(clock.UtcNow.AddMinutes(settings.LifetimeMinutes), issued.ExpiresAtUtc);
        Assert.Equal("7", validation.Claims["sub"]?.ToString());
        Assert.Equal(agent.FullName, validation.Claims["name"]?.ToString());
        Assert.Equal(agent.Email, validation.Claims["email"]?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(validation.Claims["jti"]?.ToString()));
    }

    [Fact]
    public async Task Issue_CannotBeValidatedWithAnotherKey()
    {
        var clock = new FixedClock();
        var settings = Settings(Key);
        var agent = new Agent("Alex Turner", "alex@example.com", 10, clock.UtcNow);
        var issued = new JwtTokenService(settings, clock).Issue(agent);

        var validation = await Validate(issued.Token, "another-key-that-is-also-long-enough-12345", settings);

        Assert.False(validation.IsValid);
    }

    private static Task<TokenValidationResult> Validate(string token, string key, JwtSettings settings) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key))
        });

    private static JwtSettings Settings(string key) => new()
    {
        Key = key,
        Issuer = "NorthwindSupport",
        Audience = "NorthwindSupportWeb",
        LifetimeMinutes = 60
    };

    private static void SetId(Agent agent, int id) =>
        typeof(Agent).BaseType!
            .GetProperty(nameof(agent.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(agent, id);
}
