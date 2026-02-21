using Api.Middlewares;
using FluentAssertions;
using Infrastructure.Services.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Tests.Middlewares;

public class ExceptionMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionMiddleware>> _loggerMock;

    public ExceptionMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionMiddleware>>();
    }

    private ExceptionMiddleware CreateMiddleware(RequestDelegate next, bool isDevelopment = false)
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new ExceptionMiddleware(next, _loggerMock.Object, envMock.Object);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    #region No Exception

    [Fact]
    public async Task InvokeAsync_WithNoException_ShouldPassThrough()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = CreateMiddleware(next);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    #endregion

    #region InvalidCredentialsException

    [Fact]
    public async Task InvokeAsync_WithInvalidCredentialsException_ShouldReturn401()
    {
        // Arrange
        RequestDelegate next = _ => throw new InvalidCredentialsException();
        var middleware = CreateMiddleware(next);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        context.Response.ContentType.Should().Be("application/json");

        var body = await ReadResponseBody(context);
        body.GetProperty("statusCode").GetInt32().Should().Be(401);
        body.GetProperty("message").GetString().Should().Be("Invalid credentials.");
    }

    #endregion

    #region TokenException

    [Fact]
    public async Task InvokeAsync_WithTokenException_ShouldReturn401WithMessage()
    {
        // Arrange
        RequestDelegate next = _ => throw new TokenException("Token has expired.");
        var middleware = CreateMiddleware(next);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        context.Response.ContentType.Should().Be("application/json");

        var body = await ReadResponseBody(context);
        body.GetProperty("statusCode").GetInt32().Should().Be(401);
        body.GetProperty("message").GetString().Should().Be("Token has expired.");
    }

    #endregion

    #region Unhandled Exception

    [Fact]
    public async Task InvokeAsync_WithUnhandledException_InDevelopment_ShouldReturn500WithStackTrace()
    {
        // Arrange
        RequestDelegate next = _ => throw new InvalidOperationException("Something broke");
        var middleware = CreateMiddleware(next, isDevelopment: true);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/json");

        var body = await ReadResponseBody(context);
        body.GetProperty("statusCode").GetInt32().Should().Be(500);
        body.GetProperty("message").GetString().Should().Be("Something broke");
        body.TryGetProperty("details", out var details).Should().BeTrue();
        details.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithUnhandledException_InProduction_ShouldReturn500WithGenericMessage()
    {
        // Arrange
        RequestDelegate next = _ => throw new InvalidOperationException("Sensitive error details");
        var middleware = CreateMiddleware(next, isDevelopment: false);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/json");

        var body = await ReadResponseBody(context);
        body.GetProperty("statusCode").GetInt32().Should().Be(500);
        body.GetProperty("message").GetString().Should().Be("An internal server error occurred");
        // Should NOT contain stack trace in production
        body.TryGetProperty("details", out _).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WithUnhandledException_ShouldSetJsonContentType()
    {
        // Arrange
        RequestDelegate next = _ => throw new Exception("test");
        var middleware = CreateMiddleware(next);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.ContentType.Should().Be("application/json");
    }

    #endregion
}
