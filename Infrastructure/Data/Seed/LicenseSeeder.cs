using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Data.Seed;

public static class LicenseSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Licenses.AnyAsync())
        {
            logger.LogInformation("⚠️ Licenses already exist. Skipping seeding.");
            return;
        }

        var file = Path.Combine("..", "Infrastructure", "Data", "Seed", "SeedData", "licenses.json");
        if (!File.Exists(file))
        {
            logger.LogWarning("⚠️ License seed file not found: {Path}", file);
            return;
        }

        var json = File.ReadAllText(file);

        var jsonOpt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null,
        };

        jsonOpt.Converters.Add(new JsonStringEnumConverter());

        var licenses = JsonSerializer.Deserialize<List<License>>(json, jsonOpt);
        await context.AddRangeAsync(licenses!);

        await context.SaveChangesAsync();
        logger.LogInformation("✅ Seeded {Count} licenses", licenses!.Count);
    }
}
