using Api;
using Core.DTOs;
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
using System.Text;
using System.Text.Json;
using Xunit;

namespace Tests.Integration;

public class AuthControllerIntegrationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private AppDbContext? _dbContext;

    public AuthControllerIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Jwt:Key", "this_is_a_valid_jwt_key_with_32_bytes!" },
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
                        opt.UseNpgsql("Server=localhost;port=5432;Database=eazecad_auth_test_db;User Id=postgres;Password=Admin@123;TrustServerCertificate=True;");
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

    #region Register Tests

    [Fact]
    public async Task Register_WithValidCredentials_ShouldReturnSuccess()
    {
        var registerDto = new RegisterDto
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "ValidPass@123"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync("/api/v1/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Registered successfully");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        var registerDto1 = new RegisterDto
        {
            Username = "user1",
            Email = "duplicate@example.com",
            Password = "ValidPass@123"
        };

        var content1 = new StringContent(
            JsonSerializer.Serialize(registerDto1),
            Encoding.UTF8,
            "application/json"
        );

        await _client.PostAsync("/api/v1/auth/register", content1);

        var registerDto2 = new RegisterDto
        {
            Username = "user2",
            Email = "duplicate@example.com",
            Password = "ValidPass@123"
        };

        var content2 = new StringContent(
            JsonSerializer.Serialize(registerDto2),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync("/api/v1/auth/register", content2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Email already in use");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReturnBadRequest()
    {
        var registerDto1 = new RegisterDto
        {
            Username = "duplicateuser",
            Email = "user1@example.com",
            Password = "ValidPass@123"
        };

        var content1 = new StringContent(
            JsonSerializer.Serialize(registerDto1),
            Encoding.UTF8,
            "application/json"
        );

        await _client.PostAsync("/api/v1/auth/register", content1);

        var registerDto2 = new RegisterDto
        {
            Username = "duplicateuser",
            Email = "user2@example.com",
            Password = "ValidPass@123"
        };

        var content2 = new StringContent(
            JsonSerializer.Serialize(registerDto2),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync("/api/v1/auth/register", content2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Username already taken");
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldReturnBadRequest()
    {
        var registerDto = new RegisterDto
        {
            Username = "weakpassuser",
            Email = "weak@example.com",
            Password = "weak"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync("/api/v1/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Password");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnSuccess()
    {
        var registerDto = new RegisterDto
        {
            Username = "loginuser",
            Email = "login@example.com",
            Password = "ValidPass@123"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        await _client.PostAsync("/api/v1/auth/register", registerContent);

        var loginDto = new LoginDto
        {
            Username = "loginuser",
            Password = "ValidPass@123"
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync("/api/v1/auth/login", loginContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Login successful");
        response.Headers.Should().ContainKey("Set-Cookie");
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ShouldReturnUnauthorized()
    {
        var loginDto = new LoginDto
        {
            Username = "nonexistentuser",
            Password = "SomePassword@123"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync("/api/v1/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        var registerDto = new RegisterDto
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "CorrectPass@123"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerDto),
            Encoding.UTF8,
            "application/json"
        );

        await _client.PostAsync("/api/v1/auth/register", registerContent);

        var loginDto = new LoginDto
        {
            Username = "testuser",
            Password = "WrongPass@123"
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync("/api/v1/auth/login", loginContent);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUser_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
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
}