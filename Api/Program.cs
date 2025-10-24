using Api.Extensions;
using Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{

}

app.MapControllers();

app.UseHttpsRedirection();

await DbInitializer.InitializeAsync(app.Services);

app.Run();

