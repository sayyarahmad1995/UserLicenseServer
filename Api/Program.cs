using Api.Extensions;
using Api.Middlewares;
using Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ThrottlingMiddleware>();
app.UseMiddleware<HttpLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

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

app.MapSwagger().RequireAuthorization();

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Public Program class for testing purposes
/// </summary>
public partial class Program { }

