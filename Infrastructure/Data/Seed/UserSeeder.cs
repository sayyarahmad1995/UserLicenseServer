using System.Text.Json;
using System.Text.Json.Serialization;
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

        var jsonOpt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null
        };
        jsonOpt.Converters.Add(new JsonStringEnumConverter());

        var usersJson = await File.ReadAllTextAsync(usersFile);
        var users = JsonSerializer.Deserialize<List<User>>(usersJson, jsonOpt) ?? new();

        if (!users.Any())
        {
            logger.LogWarning("⚠️ No users found in users.json");
            return;
        }

        await context.AddRangeAsync(users);
        await context.SaveChangesAsync();
        logger.LogInformation("✅ Seeded {Count} users", users.Count);
    }
}
