using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Integration;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "integration-test-key-that-is-long-enough-123456789";
    public const string Issuer = "NorthwindSupport";
    public const string Audience = "NorthwindSupportWeb";
    public const string Email = "alex.turner@northwind-support.example";
    public const string Password = "Dev-Only-Passw0rd!";

    private readonly SqliteConnection connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "DataSource=ignored");
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:LifetimeMinutes", "60");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "DataSource=ignored",
                ["Jwt:Key"] = JwtKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:LifetimeMinutes"] = "60"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<SupportDbContext>();
            services.AddSingleton(connection);
            services.AddDbContext<SupportDbContext>((provider, options) =>
                options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        connection.Open();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        db.Database.EnsureCreated();

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var customer = new Customer("Contoso", "contoso@example.com", null, CustomerTier.Standard, clock.UtcNow);
        var category = new Category("General", false, false);
        var agent = new Agent("Alex Turner", Email, 10, clock.UtcNow);
        agent.SetPasswordHash(hasher.Hash(Password));
        var inactive = new Agent("Yuki Tanaka", "yuki.tanaka@northwind-support.example", 10, clock.UtcNow);
        inactive.Deactivate();
        db.AddRange(customer, category, agent, inactive);
        db.SaveChanges();

        var ticket = new TicketBuilder()
            .WithReference("TCK-TEST-1")
            .WithCustomerId(customer.Id)
            .WithCategoryId(category.Id)
            .WithPriority(TicketPriority.Medium)
            .CreatedAt(clock.UtcNow)
            .Build();
        db.Add(ticket);
        db.SaveChanges();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}
