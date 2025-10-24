using Core.Entities;
using Infrastructure.Data.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Data.Seed;

public class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbInitializer>>();
        var adminOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminUserSeedOptions>>().Value;

        logger.LogInformation("Starting database initialization...");

        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            logger.LogInformation("Applying pending migrations...");
            await context.Database.MigrateAsync();
        }

        if (!await context.Users.AnyAsync())
        {
            logger.LogInformation("Seeding initial data...");

            var adminUser = new User
            {
                Username = adminOptions.Username,
                Email = adminOptions.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminOptions.Password),
                Role = adminOptions.Role,
                CreatedAt = DateTime.UtcNow
            };

            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();

            logger.LogInformation("✅ Admin user created: admin@yourapp.com");
        }

        logger.LogInformation("✅ Database initialization complete.");
    }
}
