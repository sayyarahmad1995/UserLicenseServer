using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

        var json = File.ReadAllText("../Infrastructure/Data/Seed/SeedData/licenses.json");

        var licenses = JsonSerializer.Deserialize<List<License>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? new();

        foreach (var license in licenses)
        {
            if (license?.CreatedAt != null)
                license.CreatedAt = DateTime.SpecifyKind(license.CreatedAt, DateTimeKind.Utc);

            if (license?.ExpiresAt != null)
                license.ExpiresAt = DateTime.SpecifyKind(license.ExpiresAt, DateTimeKind.Utc);

            if (license?.RevokedAt != null)
                license.RevokedAt = DateTime.SpecifyKind((DateTime)license.RevokedAt, DateTimeKind.Utc);

            if (!context.Licenses.Any(u => u.Id == license!.Id))
            {
                await context.Licenses.AddAsync(license!);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("✅ Seeded {Count} licenses", licenses!.Count);
    }
}
