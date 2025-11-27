using Serilog.Core;
using Serilog.Events;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace Johodp.Api.Logging;

// Dynamically enrich log events with TenantId and ClientId derived from the current HttpContext.
// Extraction rules (priority order):
// 1. TenantId: 
//    - acr_values query param (format: tenant:xxx)
//    - authenticated user claim "tenant_id" (first if multiple)
//    - query string "tenant"
//    - header "X-Tenant-Id"
// 2. ClientId: 
//    - authenticated user claim "client_id"
//    - query string "client_id"
//    - header "X-Client-Id"
// This avoids having to push LogContext properties manually in each controller/middleware.
public sealed class TenantClientEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantClientEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return; // Non-request log (startup/shutdown)

        string? tenantId = null;
        string? clientId = null;

        // Tenant extraction from acr_values (OIDC standard for contextual parameters)
        // Format: acr_values=tenant:xxx or acr_values=tenant:xxx%20other:yyy
        if (ctx.Request.Query.TryGetValue("acr_values", out var acrValues) && !string.IsNullOrWhiteSpace(acrValues))
        {
            var acrParts = acrValues.ToString().Split(new[] { ' ', '%', '2', '0' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in acrParts)
            {
                if (part.StartsWith("tenant:", StringComparison.OrdinalIgnoreCase))
                {
                    tenantId = part.Substring(7); // Extract after "tenant:"
                    break;
                }
            }
        }

        // Fallback: tenant_id claim (issued after successful authentication)
        if (string.IsNullOrWhiteSpace(tenantId) && ctx.User.Identity?.IsAuthenticated == true)
        {
            tenantId = ctx.User.FindFirst("tenant_id")?.Value;
        }

        // Fallback: query string or header
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            if (ctx.Request.Query.TryGetValue("tenant", out var tenantQuery) && !string.IsNullOrWhiteSpace(tenantQuery))
                tenantId = tenantQuery.ToString();
            else if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) && !string.IsNullOrWhiteSpace(tenantHeader))
                tenantId = tenantHeader.ToString();
        }

        // Client extraction (from claims, query, or header)
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            clientId = ctx.User.FindFirst("client_id")?.Value;
        }
        
        if (string.IsNullOrWhiteSpace(clientId))
        {
            if (ctx.Request.Query.TryGetValue("client_id", out var clientQuery) && !string.IsNullOrWhiteSpace(clientQuery))
                clientId = clientQuery.ToString();
            else if (ctx.Request.Headers.TryGetValue("X-Client-Id", out var clientHeader) && !string.IsNullOrWhiteSpace(clientHeader))
                clientId = clientHeader.ToString();
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var prop = propertyFactory.CreateProperty("TenantId", tenantId);
            logEvent.AddPropertyIfAbsent(prop);
        }
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var prop = propertyFactory.CreateProperty("ClientId", clientId);
            logEvent.AddPropertyIfAbsent(prop);
        }
    }
}