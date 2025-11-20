namespace Johodp.Infrastructure.Services;

using System.Net.Http;
using System.Net.Http.Json;
using Johodp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service de notification fire-and-forget pour les applications tierces
/// </summary>
public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        HttpClient httpClient,
        ITenantRepository tenantRepository,
        ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task NotifyAccountRequestAsync(
        string tenantId,
        string email,
        string firstName,
        string lastName,
        string requestId)
    {
        var tenant = await _tenantRepository.GetByNameAsync(tenantId);

        if (tenant == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found, skipping notification", tenantId);
            return;
        }

        // Si pas de notification configurée, skip
        if (string.IsNullOrEmpty(tenant.NotificationUrl) || !tenant.NotifyOnAccountRequest)
        {
            _logger.LogInformation("No notification configured for tenant {TenantId}", tenantId);
            return;
        }

        var payload = new
        {
            eventType = "AccountCreationRequested",
            tenantId,
            email,
            firstName,
            lastName,
            requestedAt = DateTime.UtcNow,
            requestId
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, tenant.NotificationUrl)
            {
                Content = JsonContent.Create(payload)
            };

            // Optionnel : ajouter l'API key comme header si configurée
            if (!string.IsNullOrEmpty(tenant.ApiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {tenant.ApiKey}");
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Notification sent successfully to {Url} for request {RequestId}",
                    tenant.NotificationUrl,
                    requestId);
            }
            else
            {
                _logger.LogWarning(
                    "Notification failed with status {StatusCode} for request {RequestId}",
                    response.StatusCode,
                    requestId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Notification timeout for {Url} (request {RequestId})",
                tenant.NotificationUrl,
                requestId);
        }
        catch (Exception ex)
        {
            // Fire-and-forget : on log mais on ne throw pas
            _logger.LogError(
                ex,
                "Failed to send notification to {Url} for request {RequestId}",
                tenant.NotificationUrl,
                requestId);
        }
    }
}
