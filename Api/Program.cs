using Api.Extensions;
using Api.Middlewares;
using Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseStatusCodePagesWithReExecute("/error/{0}");

app.UseRedisCacheInvalidation();

if (app.Environment.IsDevelopment())
{

}

app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await DbInitializer.InitializeAsync(app.Services);

app.Run();

