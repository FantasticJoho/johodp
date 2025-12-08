using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Johodp.Infrastructure.IdentityServer
{
    using Microsoft.Extensions.Logging;
    /// <summary>
    /// Middleware pour forcer la reconnexion si le tenant demandé (acr_values) diffère du tenant courant.
    /// Place ce middleware avant IdentityServer dans le pipeline.
    /// </summary>
    public class TenantSwitchMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantSwitchMiddleware> _logger;

        public TenantSwitchMiddleware(RequestDelegate next, ILogger<TenantSwitchMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Intercepter uniquement les requêtes /connect/authorize
            if (!context.Request.Path.StartsWithSegments("/connect/authorize"))
            {
                await _next(context);
                return;
            }


            // Comparer directement acr_values de la query et du claim
            var requestedAcrValues = context.Request.Query["acr_values"].ToString();
            var currentAcrValues = context.User?.FindFirst("acr_values")?.Value;

            bool hasRequestedAcr = !string.IsNullOrWhiteSpace(requestedAcrValues);
            bool hasCurrentAcr = !string.IsNullOrWhiteSpace(currentAcrValues);
            bool acrMismatch = hasRequestedAcr && hasCurrentAcr && !requestedAcrValues.Equals(currentAcrValues, StringComparison.Ordinal);

            if (acrMismatch)
            {
                _logger.LogInformation(
                    "TenantSwitchMiddleware: Forced logout due to tenant change (current acr_values: {CurrentAcrValues}, requested acr_values: {RequestedAcrValues})",
                    currentAcrValues, requestedAcrValues);
                await context.SignOutAsync();
                context.Response.Redirect(context.Request.Path + context.Request.QueryString);
                return;
            }

            await _next(context);
        }
    }
}
