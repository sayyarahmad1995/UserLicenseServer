using System.Text.Json;
using Api.Errors;
using Api.Extensions;
using Api.Middlewares;
using Infrastructure.Data.Seed;

var _jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.StatusCode > 400)
    {
        var apiResponse = new ApiResponse(response.StatusCode);
        var json = JsonSerializer.Serialize(apiResponse, _jsonOptions);

        response.ContentType = "application/json";
        await response.WriteAsync(json);
    }
});

if (app.Environment.IsDevelopment())
{

}

app.MapControllers();

app.UseHttpsRedirection();

await DbInitializer.InitializeAsync(app.Services);

app.Run();

