using Api.Errors;
using Api.Filters;
using Api.Helpers;
using Core.Helpers;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Data.Options;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Infrastructure.Services;
using Infrastructure.Services.Cache;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

namespace Api.Extensions;

public static class AppServiceExtension
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseNpgsql(config.GetConnectionString("DefaultConnection"));
        });

        services.AddControllers(options =>
        {
            options.Filters.Add<ValidateSessionFilter>();
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddScoped<ICacheRepository, RedisCacheRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

        services.Configure<AdminUserSeedOptions>(config.GetSection("SeedData:AdminUser"));
        services.Configure<CacheSettings>(config.GetSection("CacheSettings"));
        services.Configure<ThrottlingSettings>(config.GetSection("ThrottlingSettings"));

        services.AddSingleton<HealthService>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthHelper, AuthHelper>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IAuditService, AuditService>();

        // Email: use SMTP when configured, otherwise log-only for development
        services.Configure<EmailSettings>(config.GetSection("Email"));
        var smtpHost = config["Email:SmtpHost"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
            services.AddScoped<IEmailService, SmtpEmailService>();
        else
            services.AddScoped<IEmailService, ConsoleEmailService>();

        services.AddHostedService<LicenseExpirationJob>();

        services.Configure<ApiBehaviorOptions>(opt =>
        {
            opt.InvalidModelStateResponseFactory = context =>
          {
              var errors = context.ModelState
               .Where(e => e.Value?.Errors.Count > 0)
               .ToDictionary(
                  kvp => kvp.Key,
                  kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
               );

              var errorResponse = new ApiValidationErrorResponse { Errors = errors };
              return new BadRequestObjectResult(errorResponse);
          };
            opt.SuppressMapClientErrors = true;
        });

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisConfig = config.GetConnectionString("Redis");
            return ConnectionMultiplexer.Connect(redisConfig!);
        });
        services.AddScoped<IUserCacheVersionService, UserCacheVersionService>();
        services.AddScoped<IUserCacheService, UserCacheService>();

        // Bind and validate JWT settings
        var jwtSection = config.GetSection("Jwt");
        services.Configure<JwtSettings>(jwtSection);
        var jwtSettings = jwtSection.Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        if (string.IsNullOrEmpty(jwtSettings.Key) || jwtSettings.Key.Length < 64)
            throw new InvalidOperationException("JWT Key must be at least 64 characters long for HmacSha512.");
        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
            throw new InvalidOperationException("Jwt:Issuer must be configured.");
        if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
            throw new InvalidOperationException("Jwt:Audience must be configured.");
        if (jwtSettings.AccessTokenExpiryMinutes <= 0)
            throw new InvalidOperationException("Jwt:AccessTokenExpiryMinutes must be a positive integer.");
        if (jwtSettings.RefreshTokenExpiryDays <= 0)
            throw new InvalidOperationException("Jwt:RefreshTokenExpiryDays must be a positive integer.");

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
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha512 }
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
              {
                  if (string.IsNullOrEmpty(context.Token) &&
                   context.Request.Cookies.ContainsKey(CookieConstants.AccessToken))
                  {
                      context.Token = context.Request.Cookies[CookieConstants.AccessToken];
                  }
                  return Task.CompletedTask;
              }
            };
        });

        var roles = jwtSettings.Roles;
        services.AddAuthorization(options =>
        {
            if (roles != null)
            {
                foreach (var role in roles)
                {
                    options.AddPolicy(role, policy =>
                        policy.RequireClaim(ClaimTypes.Role, role));
                }
            }
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "EazeCad License Server API",
                Version = "v1",
                Description = "REST API for user management, authentication, and software license management.",
                Contact = new OpenApiContact
                {
                    Name = "EazeCad",
                    Email = "noreply@eazecad.com"
                }
            });

            // Include XML comments from controllers and DTOs
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);

            // Cookie-based auth description
            c.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = "access_token",
                Description = "JWT access token stored in HTTP-only cookie. Login via POST /api/v1/auth/login to obtain."
            });
        });

        // CORS: allow configurable origins (default: localhost dev ports)
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5173" };

        services.AddCors(options =>
        {
            options.AddPolicy("DefaultPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials(); // required for cookie-based auth
            });
        });

        return services;
    }
}
