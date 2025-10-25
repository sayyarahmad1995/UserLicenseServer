using Api.Errors;
using Api.Services;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Data.Options;
using Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
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

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        services.Configure<AdminUserSeedOptions>(config.GetSection("SeedData:AdminUser"));

        services.AddSingleton<HealthService>();

        services.Configure<ApiBehaviorOptions>(opt =>
        {
            opt.InvalidModelStateResponseFactory = ActionContext =>
            {
                var errors = ActionContext.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors)
                    .Select(x => x.ErrorMessage)
                    .ToArray();

                var errorResponse = new ApiValidationErrorResponse { Errors = errors };
                return new BadRequestObjectResult(errorResponse);
            };
        });

        return services;
    }
}
