using Api.Extensions;
using Api.Middlewares;
using Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UserLicenseServer");
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    await DbInitializer.InitializeAsync(app.Services);
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("DefaultPolicy");
app.UseAuthentication();
app.UseAuthorization();

// 4. Throttling runs after auth so context.User is populated for user-tier throttling
app.UseMiddleware<ThrottlingMiddleware>();

app.MapControllers();

app.Run();

/// <summary>
/// Public Program class for testing purposes
/// </summary>
public partial class Program { }

