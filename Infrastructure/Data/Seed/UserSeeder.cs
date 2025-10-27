using System.Text.Json;
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seed;

public static class UserSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Users.CountAsync() > 1)
        {
            logger.LogInformation("⚠️ Users already seeded (excluding admin). Skipping user seeding.");
            return;
        }

        var usersFile = Path.Combine("..", "Infrastructure", "Data", "Seed", "SeedData", "users.json");
        if (!File.Exists(usersFile))
        {
            logger.LogWarning("⚠️ User seed file not found: {Path}", usersFile);
            return;
        }

        var usersJson = await File.ReadAllTextAsync(usersFile);
        var users = JsonSerializer.Deserialize<List<User>>(usersJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new();

        if (!users.Any())
        {
            logger.LogWarning("⚠️ No users found in users.json");
            return;
        }

        foreach (var user in users)
        {
            if (await context.Users.AnyAsync(u => u.Email == user.Email))
                continue;

            if (!string.IsNullOrEmpty(user.PasswordHash))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

            user.CreatedAt = DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc);

            if (user?.CreatedAt != null)
                user.CreatedAt = DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc);

            if (user?.LastLogin != null)
                user.LastLogin = DateTime.SpecifyKind((DateTime)user.LastLogin, DateTimeKind.Utc);

            if (user?.UpdatedAt.HasValue == true)
                user.UpdatedAt = DateTime.SpecifyKind(user.UpdatedAt.Value, DateTimeKind.Utc);

            if (user?.VerifiedAt.HasValue == true)
                user.VerifiedAt = DateTime.SpecifyKind(user.VerifiedAt.Value, DateTimeKind.Utc);

            if (!context.Users.Any(u => u.Id == user!.Id))
            {
                await context.Users.AddAsync(user!);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("✅ Seeded {Count} users", users.Count);
    }
}
