using Api.Errors;
using Core.Helpers;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Infrastructure.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Api.Filters;

public class ValidateSessionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // If endpoint explicitly allows anonymous, skip
        var endpointMetadata = context.ActionDescriptor.EndpointMetadata;
        if (endpointMetadata.OfType<AllowAnonymousAttribute>().Any())
        {
            await next();
            return;
        }

        // If endpoint does not require authorization, skip
        // Check for any IAuthorizeData (AuthorizeAttribute or policy/roles)
        var requiresAuth = endpointMetadata.OfType<IAuthorizeData>().Any();
        if (!requiresAuth)
        {
            await next();
            return;
        }

        // If the controller is "Auth", skip (so login/refresh endpoints are unaffected)
        if (context.ActionDescriptor is ControllerActionDescriptor cad &&
           string.Equals(cad.ControllerName, "Auth", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // If user not authenticated, let the regular authorization pipeline handle it.
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        // Perform session validation
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var jti = user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(jti))
        {
            var cache = context.HttpContext.RequestServices.GetRequiredService<ICacheRepository>();
            var authHelper = context.HttpContext.RequestServices.GetRequiredService<IAuthHelper>();

            var key = CacheKeys.Session(int.Parse(userId), jti);
            var session = await cache.GetAsync<RefreshToken>(key);

            if (session == null || session.Revoked)
            {
                // clear cookies to avoid stale client state
                authHelper.ClearAuthCookies(context.HttpContext.Response);

                context.Result = new ObjectResult(new ApiResponse(401, "Session expired or revoked. Please log in again."))
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }
        }

        await next();
    }
}