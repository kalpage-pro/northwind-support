using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    private const string Password = "correct password";

    [Fact]
    public async Task HandleAsync_SuccessReturnsTokenExpiryAndUser()
    {
        var agent = Agent("Alex Turner", "alex@example.com");
        var agents = new Mock<IAgentRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var tokens = new Mock<IJwtTokenService>();
        var issued = new IssuedToken("token", FixedClock.DefaultNow.AddMinutes(60));
        agent.SetPasswordHash("stored-hash");
        agents.Setup(repository => repository.GetByEmailAsync("alex@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        hasher.Setup(value => value.Verify("stored-hash", Password)).Returns(true);
        tokens.Setup(value => value.Issue(agent)).Returns(issued);

        var result = await new LoginCommandHandler(agents.Object, hasher.Object, tokens.Object)
            .HandleAsync(new LoginRequest(" alex@example.com ", Password), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(issued, result.IssuedToken);
        Assert.Equal(new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email), result.User);
    }

    [Fact]
    public async Task HandleAsync_EmailLookupIsCaseInsensitive()
    {
        var agent = Agent("Alex Turner", "alex@example.com");
        agent.SetPasswordHash("stored-hash");
        var agents = new Mock<IAgentRepository>();
        agents.Setup(repository => repository.GetByEmailAsync("ALEX@EXAMPLE.COM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(value => value.Verify("stored-hash", Password)).Returns(true);

        var result = await new LoginCommandHandler(agents.Object, hasher.Object, new Mock<IJwtTokenService>().Object)
            .HandleAsync(new LoginRequest("ALEX@EXAMPLE.COM", Password), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("unknown@example.com", true)]
    [InlineData("alex@example.com", false)]
    [InlineData("inactive@example.com", true)]
    public async Task HandleAsync_InvalidCredentialsReturnIdenticalFailure(
        string email,
        bool agentIsMissingOrInactive)
    {
        var agents = new Mock<IAgentRepository>();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(value => value.Verify(It.IsAny<string>(), Password)).Returns(false);
        Agent? agent = null;
        if (email == "inactive@example.com")
        {
            agent = Agent("Inactive", email);
            agent.Deactivate();
        }

        agents.Setup(repository => repository.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var result = await new LoginCommandHandler(agents.Object, hasher.Object, new Mock<IJwtTokenService>().Object)
            .HandleAsync(new LoginRequest(email, Password), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.IssuedToken);
        Assert.Null(result.User);
        if (agentIsMissingOrInactive)
        {
            hasher.Verify(value => value.Verify(It.IsAny<string>(), Password), Times.Once);
        }
    }

    [Fact]
    public async Task HandleAsync_AgentWithoutHashReturnsIdenticalFailure()
    {
        var agent = Agent("No Hash", "nohash@example.com");
        var agents = new Mock<IAgentRepository>();
        agents.Setup(repository => repository.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(value => value.Verify(It.IsAny<string>(), Password)).Returns(true);

        var result = await new LoginCommandHandler(agents.Object, hasher.Object, new Mock<IJwtTokenService>().Object)
            .HandleAsync(new LoginRequest(agent.Email, Password), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.IssuedToken);
        Assert.Null(result.User);
    }

    private static Agent Agent(string name, string email) =>
        new(name, email, 10, FixedClock.DefaultNow);
}
