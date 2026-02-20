using Api;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using System.Net;
using Xunit;

namespace Tests.Integration;

public class UsersControllerIntegrationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private AppDbContext? _dbContext;

    public UsersControllerIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Jwt:Key", "this_is_a_valid_jwt_key_for_hmacsha512_it_must_be_at_least_64_bytes_long_padded_to_128_bytes_for_safety_to_pass_key_size_checks!!!!" },
                        { "Jwt:Issuer", "https://yourdomain.com" },
                        { "Jwt:Audience", "YourAppAudience" },
                        { "Jwt:Roles:0", "Admin" },
                        { "Jwt:Roles:1", "User" },
                        { "Jwt:Roles:2", "Manager" }
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Remove the in-memory DbContext to use production PostgreSQL instead
                    var servicestoRemove = services
                        .Where(d => d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                            d.ServiceType.FullName?.Contains("DbContext") == true ||
                            (d.ServiceType.IsGenericType && 
                            d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                        .ToList();

                    foreach (var service in servicestoRemove)
                    {
                        services.Remove(service);
                    }

                    // Configure real PostgreSQL connection for testing
                    services.AddDbContext<AppDbContext>(opt =>
                    {
                        opt.UseNpgsql("Server=localhost;port=5432;Database=eazecad_users_test_db;User Id=postgres;Password=Admin@123;TrustServerCertificate=True;");
                    });
                });

                builder.UseEnvironment("Testing");
            });

        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            // Clear throttle keys from Redis
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var db = redis.GetDatabase();

            try
            {
                var server = redis.GetServer(redis.GetEndPoints().First());
                var keys = server.Keys(pattern: "throttle:*").ToArray();
                if (keys.Any())
                {
                    db.KeyDelete(keys);
                }
            }
            catch
            {
                // Silently ignore if we can't clear keys
            }

            _dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            // Drop and recreate database
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.Database.EnsureCreatedAsync();
            
            // Seed test data
            if (!_dbContext.Users.Any())
            {
                var testUser = new User
                {
                    Username = "testuser",
                    Email = "testuser@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPass@123"),
                    Status = UserStatus.Active
                };
                
                _dbContext.Users.Add(testUser);
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
        _factory.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async Task GetUsers_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnSuccess()
    {
        var response = await _client.GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TestEndpoint_ShouldReturnSuccess()
    {
        var response = await _client.GetAsync("/api/v1/test/ok");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Everything is working fine");
    }

    [Fact]
    public async Task TestBadRequest_ShouldReturn400()
    {
        var response = await _client.GetAsync("/api/v1/test/badrequest");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TestUnauthorized_ShouldReturn401()
    {
        var response = await _client.GetAsync("/api/v1/test/unauthorized");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestNotFound_ShouldReturn404()
    {
        var response = await _client.GetAsync("/api/v1/test/notfound");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestServerError_ShouldReturn500()
    {
        var response = await _client.GetAsync("/api/v1/test/servererror");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    internal class TestCacheRepository : ICacheRepository
    {
        private readonly Dictionary<string, (object? value, DateTime expiry)> _cache = new();

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : DateTime.MaxValue;
            _cache[key] = (value, expiryTime);
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(key, out var item) && DateTime.UtcNow < item.expiry)
                return Task.FromResult((T?)item.value);
            return Task.FromResult((T?)default);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_cache.ContainsKey(key) && DateTime.UtcNow < _cache[key].expiry);

        public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        
        public Task<IEnumerable<string>> SearchKeysAsync(string pattern) => Task.FromResult(_cache.Keys.AsEnumerable());
        
        public Task PublishInvalidationAsync(string key) => Task.CompletedTask;
        
        public void SubscribeToInvalidations(Func<string, Task> onInvalidation) { }
        
        public Task RefreshAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<long> IncrementAsync(string key, TimeSpan? expiryOnCreate = null, CancellationToken cancellationToken = default)
        {
            long newValue = 1;
            if (_cache.TryGetValue(key, out var item) && DateTime.UtcNow < item.expiry && item.value is long current)
                newValue = current + 1;
            var expiryTime = newValue == 1 && expiryOnCreate.HasValue ? DateTime.UtcNow.Add(expiryOnCreate.Value) : (newValue == 1 ? DateTime.MaxValue : item.expiry);
            _cache[key] = (newValue, expiryTime);
            return Task.FromResult(newValue);
        }
    }
}