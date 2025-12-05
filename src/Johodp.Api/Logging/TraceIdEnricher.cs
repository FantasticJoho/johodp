namespace Johodp.Api.Logging;

using Serilog.Core;
using Serilog.Events;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Enrichit les logs Serilog avec le TraceId de la requête HTTP courante
/// Permet de tracer toutes les opérations liées à une même requête
/// </summary>
public class TraceIdEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TraceIdEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        // Ajouter TraceId (GUID unique par requête)
        var traceId = httpContext.TraceIdentifier;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));

        // Ajouter IP réelle du client (après X-Forwarded-For processing)
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientIP", clientIp));

        // Ajouter méthode HTTP et path
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.ToString();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("HttpMethod", method));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("HttpPath", path));
    }
}
