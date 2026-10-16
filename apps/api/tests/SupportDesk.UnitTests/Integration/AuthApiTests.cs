using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Infrastructure.Configuration;
using SupportDesk.Infrastructure.Services;
using SupportDesk.Presentation.Controllers;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Integration;

public sealed class AuthApiTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient client;
    private readonly AuthApiFactory factory;

    public AuthApiTests(AuthApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task TicketsWithoutToken_ReturnsUnauthorized()
    {
        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TicketsWithValidToken_ReturnsOk()
    {
        var token = await LoginAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TicketsWithExpiredOrWronglySignedToken_ReturnsUnauthorized()
    {
        var expired = IssueToken(DateTime.UtcNow.AddMinutes(-120), AuthApiFactory.JwtKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);

        var wronglySigned = IssueToken(DateTime.UtcNow.AddMinutes(60), "another-integration-key-that-is-long-enough-123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wronglySigned);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task LoginWithWrongPassword_ReturnsUnauthorizedWithoutToken()
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AuthApiFactory.Email, "wrong"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Invalid email or password.", body);
    }

    [Fact]
    public async Task SeededAgentCanLoginAndUseMe()
    {
        var token = await LoginAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");
        var user = await response.Content.ReadFromJsonAsync<AuthenticatedAgentDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(user);
        Assert.Equal(AuthApiFactory.Email, user.Email);
    }

    [Fact]
    public async Task EscalationUsesTokenUserInsteadOfBodyActor()
    {
        var token = await LoginAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/api/tickets/1/escalate",
            new { reason = "Production impact requires escalation.", escalatedBy = "spoofed actor" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Alex Turner", body);
        Assert.DoesNotContain("spoofed actor", body);
    }

    [Fact]
    public void EveryControllerActionRequiresAuthorizationExceptLogin()
    {
        var actions = factory.Services
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>();

        Assert.NotEmpty(actions);
        var fallbackPolicy = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;
        Assert.NotNull(fallbackPolicy);
        Assert.Contains(fallbackPolicy.Requirements, requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);

        foreach (var action in actions)
        {
            var allowsAnonymous = action.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
            var isLogin = action.ControllerTypeInfo == typeof(AuthController) && action.ActionName == "Login";
            Assert.Equal(isLogin, allowsAnonymous);
        }
    }

    private async Task<string> LoginAsync()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthApiFactory.Email, AuthApiFactory.Password));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        return payload?.Token ?? throw new InvalidOperationException("Login response did not contain a token.");
    }

    private static string IssueToken(DateTime expiresAtUtc, string key)
    {
        var clock = new FixedClock(expiresAtUtc.AddMinutes(-60));
        var settings = new JwtSettings
        {
            Key = key,
            Issuer = AuthApiFactory.Issuer,
            Audience = AuthApiFactory.Audience,
            LifetimeMinutes = 60
        };
        var agent = new SupportDesk.Domain.Aggregates.Agents.Agent("Test", "test@example.com", 1, clock.UtcNow);
        return new JwtTokenService(settings, clock).Issue(agent).Token;
    }

    private sealed record LoginPayload(string Token, DateTime ExpiresAtUtc, AuthenticatedAgentDto User);
}
