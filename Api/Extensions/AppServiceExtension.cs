using Infrastructure.Data;
using Infrastructure.Data.Options;
using Microsoft.EntityFrameworkCore;

namespace Api.Extensions;

public static class AppServiceExtension
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseNpgsql(config.GetConnectionString("DefaultConnection"));
        });
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        services.Configure<AdminUserSeedOptions>(config.GetSection("SeedData:AdminUser"));
        
        return services;
    }
}
