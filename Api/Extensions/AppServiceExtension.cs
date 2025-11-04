using System.Security.Claims;
using System.Text;
using Api.Errors;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Data.Options;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;


namespace Api.Extensions;

public static class AppServiceExtension
{
	public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
	{
		services.AddDbContext<AppDbContext>(opt =>
		{
			opt.UseNpgsql(config.GetConnectionString("DefaultConnection"));
		});

		services.AddScoped<ICacheRepository, RedisCacheRepository>();

		services.AddScoped<IUnitOfWork, UnitOfWork>();

		services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

		services.Configure<AdminUserSeedOptions>(config.GetSection("SeedData:AdminUser"));

		services.AddSingleton<HealthService>();

		services.AddScoped<ITokenService, TokenService>();

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

		services.AddSingleton<IConnectionMultiplexer>(sp =>
		{
			var redisConfig = config.GetConnectionString("Redis");
			return ConnectionMultiplexer.Connect(redisConfig!);
		});

		var jwtKey = config["Jwt:Key"];
		var jwtIssuer = config["Jwt:Issuer"];
		var jwtAudience = config["Jwt:Audience"];

		services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
			options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
		})
		.AddJwtBearer(options =>
		{
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidIssuer = jwtIssuer,
				ValidateAudience = true,
				ValidAudience = jwtAudience,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
				ValidateLifetime = true,
				ClockSkew = TimeSpan.Zero,
				RoleClaimType = ClaimTypes.Role
			};

			options.Events = new JwtBearerEvents
			{
				OnMessageReceived = context =>
				{
					if (string.IsNullOrEmpty(context.Token) &&
						context.Request.Cookies.ContainsKey("accessToken"))
					{
						context.Token = context.Request.Cookies["accessToken"];
					}
					return Task.CompletedTask;
				}
			};
		});

		var roles = config.GetSection("Jwt:Roles").Get<string[]>();
		services.AddAuthorization(options =>
		{
			foreach (var role in roles!)
			{
				options.AddPolicy(role, policy =>
					policy.RequireClaim(ClaimTypes.Role, role));
			}
		});

		return services;
	}
}
