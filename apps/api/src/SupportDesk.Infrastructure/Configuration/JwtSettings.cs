using System.Text;
using Microsoft.Extensions.Configuration;

namespace SupportDesk.Infrastructure.Configuration;

public sealed class JwtSettings
{
    public string Key { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public int LifetimeMinutes { get; init; } = 60;

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        var settings = new JwtSettings
        {
            Key = section["Key"] ?? string.Empty,
            Issuer = section["Issuer"] ?? string.Empty,
            Audience = section["Audience"] ?? string.Empty,
            LifetimeMinutes = int.TryParse(section["LifetimeMinutes"], out var lifetimeMinutes)
                ? lifetimeMinutes
                : 0
        };
        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Jwt:Audience is required.");
        }

        if (LifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt:LifetimeMinutes must be greater than zero.");
        }
    }
}
