using Api;
using Core.Entities;
using Core.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Tests.Helpers;

/// <summary>
/// Base class for integration tests. Provides WebApplicationFactory setup,
/// database lifecycle (create/seed/destroy), and Redis throttle key cleanup.
/// Subclasses provide a unique database name via the abstract property.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;

    /// <summary>Override to give each test class its own database.</summary>
    protected abstract string DatabaseName { get; }

    protected IntegrationTestBase()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Jwt:Key", "this_is_a_valid_jwt_key_for_hmacsha512_it_must_be_at_least_64_bytes_long_padded_to_128_bytes_for_safety_to_pass_key_size_checks!!!!" },
                        { "Jwt:Issuer", "https://yourdomain.com" },
                        { "Jwt:Audience", "YourAppAudience" },
                        { "Jwt:Roles:0", "Admin" },
                        { "Jwt:Roles:1", "User" },
                        { "Jwt:Roles:2", "Manager" }
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var servicesToRemove = services
                        .Where(d => d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                            d.ServiceType.FullName?.Contains("DbContext") == true ||
                            (d.ServiceType.IsGenericType &&
                             d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                        .ToList();

                    foreach (var service in servicesToRemove)
                        services.Remove(service);

                    services.AddDbContext<AppDbContext>(opt =>
                    {
                        opt.UseNpgsql($"Server=localhost;port=5432;Database={DatabaseName};User Id=postgres;Password=Admin@123;TrustServerCertificate=True;");
                    });
                });

                builder.UseEnvironment("Testing");
            });

        Client = Factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();

        // Clear throttle keys from Redis
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();

        try
        {
            var server = redis.GetServer(redis.GetEndPoints().First());
            var keys = server.Keys(pattern: "throttle:*").ToArray();
            if (keys.Any())
                db.KeyDelete(keys);
        }
        catch
        {
            // Silently ignore if we can't clear keys
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        if (!dbContext.Users.Any())
        {
            dbContext.Users.Add(new User
            {
                Username = "testuser",
                Email = "testuser@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPass@123"),
                Status = UserStatus.Active
            });
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
        Client.Dispose();
    }
}
