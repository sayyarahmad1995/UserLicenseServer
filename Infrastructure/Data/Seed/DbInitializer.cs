using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seed;

public class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbInitializer>>();

        logger.LogInformation("Starting database initialization...");

        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            logger.LogInformation("Applying pending migrations...");
            await context.Database.MigrateAsync();
        }

        // Seed the admin user (only run once, or comment out if already seeded)
        await AdminSeeder.SeedAsync(context, serviceProvider, logger);

        // Seed development data (users, licenses, etc.)
        // ⚠️ Comment out these lines in production
        await UserSeeder.SeedAsync(context, logger);
        await LicenseSeeder.SeedAsync(context, logger);

        logger.LogInformation("✅ Database initialization complete.");
    }
}
