using Api.Errors;
using Api.Filters;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Interfaces;
using Infrastructure.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace Tests.Filters;

public class ValidateSessionFilterTests
{
    private readonly Mock<ICacheRepository> _cacheMock;
    private readonly Mock<IAuthHelper> _authHelperMock;
    private readonly ValidateSessionFilter _filter;

    public ValidateSessionFilterTests()
    {
        _cacheMock = new Mock<ICacheRepository>();
        _authHelperMock = new Mock<IAuthHelper>();
        _filter = new ValidateSessionFilter();
    }

    private ActionExecutingContext CreateContext(
        List<object>? endpointMetadata = null,
        ClaimsPrincipal? user = null,
        string controllerName = "Users",
        string actionName = "GetAll")
    {
        var httpContext = new DefaultHttpContext();

        // Register services
        var services = new ServiceCollection();
        services.AddSingleton(_cacheMock.Object);
        services.AddSingleton(_authHelperMock.Object);
        httpContext.RequestServices = services.BuildServiceProvider();

        if (user != null)
            httpContext.User = user;

        var actionDescriptor = new ControllerActionDescriptor
        {
            ControllerName = controllerName,
            ActionName = actionName,
            EndpointMetadata = endpointMetadata ?? new List<object>(),
            ControllerTypeInfo = typeof(object).GetTypeInfo()
        };

        var routeData = new RouteData();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(string userId = "1", string jti = "test-jti")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Jti, jti)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        return new ClaimsPrincipal(identity);
    }

    private static ActionExecutionDelegate CreateNextDelegate()
    {
        return () =>
        {
            var context = new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(),
                controller: null!);
            return Task.FromResult(context);
        };
    }

    #region Skip Scenarios

    [Fact]
    public async Task OnActionExecutionAsync_WithAllowAnonymous_ShouldSkipValidation()
    {
        // Arrange
        var metadata = new List<object> { new AllowAnonymousAttribute() };
        var context = CreateContext(endpointMetadata: metadata);
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));
        };

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutAuthorizeAttribute_ShouldSkipValidation()
    {
        // Arrange - no [Authorize] metadata
        var context = CreateContext(endpointMetadata: new List<object>());
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));
        };

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithAuthController_ShouldSkipValidation()
    {
        // Arrange
        var metadata = new List<object> { new AuthorizeAttribute() };
        var context = CreateContext(endpointMetadata: metadata, controllerName: "Auth");
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));
        };

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithUnauthenticatedUser_ShouldSkipValidation()
    {
        // Arrange
        var metadata = new List<object> { new AuthorizeAttribute() };
        var context = CreateContext(endpointMetadata: metadata);
        // Default HttpContext user is unauthenticated
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));
        };

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    #endregion

    #region Session Validation

    [Fact]
    public async Task OnActionExecutionAsync_WithValidSession_ShouldCallNext()
    {
        // Arrange
        var user = CreateAuthenticatedUser("1", "valid-jti");
        var metadata = new List<object> { new AuthorizeAttribute() };
        var context = CreateContext(endpointMetadata: metadata, user: user);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>("session:1:valid-jti", CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                Jti = "valid-jti",
                TokenHash = "hash",
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(30)
            });

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));
        };

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithRevokedSession_ShouldReturn401()
    {
        // Arrange
        var user = CreateAuthenticatedUser("1", "revoked-jti");
        var metadata = new List<object> { new AuthorizeAttribute() };
        var context = CreateContext(endpointMetadata: metadata, user: user);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>("session:1:revoked-jti", CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                Jti = "revoked-jti",
                TokenHash = "hash",
                Revoked = true,
                Expires = DateTime.UtcNow.AddDays(30)
            });

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));
        };

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithMissingSession_ShouldReturn401()
    {
        // Arrange
        var user = CreateAuthenticatedUser("1", "missing-jti");
        var metadata = new List<object> { new AuthorizeAttribute() };
        var context = CreateContext(endpointMetadata: metadata, user: user);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>("session:1:missing-jti", CancellationToken.None))
            .ReturnsAsync((RefreshToken?)null);

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));
        };

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithRevokedSession_ShouldClearCookies()
    {
        // Arrange
        var user = CreateAuthenticatedUser("1", "revoked-jti");
        var metadata = new List<object> { new AuthorizeAttribute() };
        var context = CreateContext(endpointMetadata: metadata, user: user);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>("session:1:revoked-jti", CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                Jti = "revoked-jti",
                TokenHash = "hash",
                Revoked = true,
                Expires = DateTime.UtcNow.AddDays(30)
            });

        ActionExecutionDelegate next = () =>
            Task.FromResult(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(), controller: null!));

        // Act
        await _filter.OnActionExecutionAsync(context, next);

        // Assert
        _authHelperMock.Verify(x => x.ClearAuthCookies(It.IsAny<HttpResponse>()), Times.Once);
    }

    #endregion
}
