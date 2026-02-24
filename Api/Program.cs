using Api.Extensions;
using Api.Middlewares;
using Infrastructure.Data.Seed;
using Microsoft.AspNetCore.HttpOverrides;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Replace default logging with Serilog
builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "EazeCad")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}");
});

builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

// Serilog request logging
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});

// Support reverse proxy (nginx, Caddy) — must be first
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// 1. Security headers on every response
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Exception handler must wrap everything below it
app.UseMiddleware<ExceptionMiddleware>();

// 3. Request/response logging (exceptions from here are caught above)
app.UseMiddleware<HttpLoggingMiddleware>();

app.UseStatusCodePagesWithReExecute("/error/{0}");

app.UseRedisCacheInvalidation();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EazeCad License Server");
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    await DbInitializer.InitializeAsync(app.Services, app.Environment.IsDevelopment());
}

// HTTPS redirection disabled in development to prevent CORS preflight issues
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors("DefaultPolicy");
app.UseAuthentication();
app.UseAuthorization();

// 4. Throttling runs after auth so context.User is populated for user-tier throttling
app.UseMiddleware<ThrottlingMiddleware>();

// 5. Prometheus request metrics (after routing so endpoint metadata is available)
app.UseMiddleware<PrometheusRequestMiddleware>();

app.MapControllers();

// Prometheus /metrics endpoint — unauthenticated for scraping
app.MapMetrics();

app.Run();

/// <summary>
/// Public Program class for testing purposes
/// </summary>
public partial class Program { }