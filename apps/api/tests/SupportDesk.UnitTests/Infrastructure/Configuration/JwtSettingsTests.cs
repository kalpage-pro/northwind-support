using SupportDesk.Infrastructure.Configuration;

namespace SupportDesk.UnitTests.Infrastructure.Configuration;

public sealed class JwtSettingsTests
{
    [Fact]
    public void Validate_RejectsAShortKey()
    {
        var settings = new JwtSettings
        {
            Key = "too-short",
            Issuer = "NorthwindSupport",
            Audience = "NorthwindSupportWeb",
            LifetimeMinutes = 60
        };

        Assert.Throws<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void Validate_AcceptsAValidConfiguration()
    {
        var settings = ValidSettings();

        settings.Validate();
    }

    private static JwtSettings ValidSettings() => new()
    {
        Key = "a-valid-development-key-that-is-at-least-32-bytes",
        Issuer = "NorthwindSupport",
        Audience = "NorthwindSupportWeb",
        LifetimeMinutes = 60
    };
}
