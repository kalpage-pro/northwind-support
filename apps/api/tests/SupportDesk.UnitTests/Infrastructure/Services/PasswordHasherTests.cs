using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashesAreSaltedAndCanVerifyPasswords()
    {
        var hasher = new PasswordHasherService();

        var first = hasher.Hash("correct password");
        var second = hasher.Hash("correct password");

        Assert.NotEqual("correct password", first);
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify(first, "correct password"));
        Assert.False(hasher.Verify(first, "wrong password"));
    }
}
