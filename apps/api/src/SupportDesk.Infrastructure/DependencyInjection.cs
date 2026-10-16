using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Configuration;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();

        var jwt = BindJwtSettings(configuration);
        services.AddSingleton(jwt);

        services.AddSingleton(sp =>
        {
            var sla = configuration.GetSection("Sla");
            var settings = new SlaSettings
            {
                BaseWindowHours = Enum.GetValues<TicketPriority>()
                    .ToDictionary(
                        priority => priority,
                        priority => double.Parse(
                            sla[$"BaseWindowHours:{priority}"]
                                ?? throw new InvalidOperationException($"SLA window for {priority} is missing."),
                            System.Globalization.CultureInfo.InvariantCulture)),
                PremiumCustomerMultiplier = double.Parse(
                    sla["PremiumCustomerMultiplier"]
                        ?? throw new InvalidOperationException("Premium customer multiplier is missing."),
                    System.Globalization.CultureInfo.InvariantCulture),
                MinimumWindowHours = double.Parse(
                    sla["MinimumWindowHours"]
                        ?? throw new InvalidOperationException("Minimum SLA window is missing."),
                    System.Globalization.CultureInfo.InvariantCulture),
                AtRiskThresholdPercent = double.Parse(
                    sla["AtRiskThresholdPercent"]
                        ?? throw new InvalidOperationException("At-risk threshold is missing."),
                    System.Globalization.CultureInfo.InvariantCulture)
            };
            settings.Validate();
            return settings;
        });
        services.AddSingleton<SlaPolicy>();

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketEscalationRepository, TicketEscalationRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }

    private static JwtSettings BindJwtSettings(IConfiguration configuration)
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
}
