using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public sealed class AgentCredentialTests
{
    [Fact]
    public void CanSignIn_RequiresAnActiveAgentWithAPasswordHash()
    {
        var active = new Agent("Alex Turner", "alex@example.com", 10, FixedClock.DefaultNow);
        var inactive = new Agent("Yuki Tanaka", "yuki@example.com", 10, FixedClock.DefaultNow);
        inactive.Deactivate();
        var noHash = new Agent("Ben Osei", "ben@example.com", 10, FixedClock.DefaultNow);

        active.SetPasswordHash("hash");
        inactive.SetPasswordHash("hash");

        Assert.True(active.CanSignIn);
        Assert.False(inactive.CanSignIn);
        Assert.False(noHash.CanSignIn);
    }
}
