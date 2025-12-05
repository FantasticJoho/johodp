namespace Johodp.Infrastructure.IdentityServer;

using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Custom event sink for logging critical IdentityServer events (failures, errors, security events).
/// Production-optimized: logs only actionable events to reduce noise.
/// </summary>
public class IdentityServerEventSink : IEventSink
{
    private readonly ILogger<IdentityServerEventSink> _logger;
    private readonly IConfiguration _configuration;

    public IdentityServerEventSink(
        ILogger<IdentityServerEventSink> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task PersistAsync(Event evt)
    {
        // Log only failures, errors, and security-relevant events
        switch (evt)
        {
            // CRITICAL: Authentication Failures
            case UserLoginFailureEvent loginFailure:
                _logger.LogWarning(
                    "Login failed: {Username} from {IP}, Reason: {Message}",
                    loginFailure.Username,
                    loginFailure.RemoteIpAddress,
                    loginFailure.Message);
                break;

            // CRITICAL: Client Authentication Failures
            case ClientAuthenticationFailureEvent clientFailure:
                _logger.LogWarning(
                    "Client auth failed: {ClientId}, Reason: {Message}",
                    clientFailure.ClientId,
                    clientFailure.Message);
                break;

            // ERROR: Unhandled Exceptions
            case UnhandledExceptionEvent exception:
                _logger.LogError(
                    "IdentityServer unhandled exception: {Details}",
                    exception.Details);
                break;

            // ERROR: Invalid Configuration
            case InvalidClientConfigurationEvent invalidConfig:
                _logger.LogError(
                    "Invalid client config: {ClientId}, Reason: {Message}",
                    invalidConfig.ClientId,
                    invalidConfig.Message);
                break;

            // INFO: Token Issued (for audit trail - can be disabled if too verbose)
            case TokenIssuedSuccessEvent tokenIssued when ShouldLogTokenIssuance():
                _logger.LogInformation(
                    "Token issued: {GrantType} for {ClientId}, Subject: {SubjectId}",
                    tokenIssued.GrantType,
                    tokenIssued.ClientId,
                    tokenIssued.SubjectId);
                break;

            // DEBUG: Success events (disabled by default in production)
            case UserLoginSuccessEvent loginSuccess:
                _logger.LogDebug(
                    "Login success: {Username} from {IP}",
                    loginSuccess.Username,
                    loginSuccess.RemoteIpAddress);
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines if token issuance should be logged (audit trail).
    /// Configurable via appsettings: IdentityServer:Events:LogTokenIssuance
    /// Modes: "All" (log all tokens), "Sampling" (10% random), "Disabled" (no logging)
    /// </summary>
    private bool ShouldLogTokenIssuance()
    {
        var mode = _configuration.GetValue<string>("IdentityServer:Events:LogTokenIssuance", "All");
        return mode switch
        {
            "All" => true,
            "Sampling" => Random.Shared.Next(100) < 10,
            "Disabled" => false,
            _ => true // Default to All for unknown values
        };
    }
}
