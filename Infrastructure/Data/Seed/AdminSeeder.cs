using Core.Entities;
using Infrastructure.Data.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Data.Seed;

public static class AdminSeeder
{
    public static async Task SeedAsync(AppDbContext context, IServiceProvider serviceProvider, ILogger logger)
    {
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminUserSeedOptions>>().Value;

        if (await context.Users.AnyAsync(u => u.Email == adminOptions.Email))
        {
            logger.LogInformation("⚠️ Admin user already exists. Skipping seeding.");
            return;
        }

        var adminUser = new User
        {
            Username = adminOptions.Username,
            Email = adminOptions.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminOptions.Password),
            Role = adminOptions.Role,
            CreatedAt = DateTime.UtcNow,
            VerifiedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(adminUser);
        await context.SaveChangesAsync();

        logger.LogInformation("✅ Admin user created: {Email}", adminOptions.Email);
    }
}
