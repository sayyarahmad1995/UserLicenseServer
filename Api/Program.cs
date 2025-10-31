using System.Text.Json.Serialization;
using Api.Extensions;
using Api.Middlewares;
using Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
   options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
   options.JsonSerializerOptions.PropertyNamingPolicy = null;
   options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseStatusCodePagesWithReExecute("/error/{0}");

if (app.Environment.IsDevelopment())
{

}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await DbInitializer.InitializeAsync(app.Services);

app.Run();

