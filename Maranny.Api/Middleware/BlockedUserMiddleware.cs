using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Maranny.Api.Middleware
{
    public class BlockedUserMiddleware
    {
        private readonly RequestDelegate _next;

        public BlockedUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
        {
            // Skip middleware if user is not authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            // Get userId from JWT claims
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                await _next(context);
                return;
            }

            // Check if user is blocked in database (real-time check)
            var user = await dbContext.Users.FindAsync(userId);

            if (user?.IsBlocked == true)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    error = "AccountBlocked",
                    message = "Your account has been suspended.",
                    reason = user.BlockReason,
                    blockedAt = user.BlockedAt
                };

                var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(jsonResponse);
                return;

            }

            // User is not blocked, continue to next middleware
            await _next(context);
        }
    }
}