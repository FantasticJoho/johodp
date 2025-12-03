namespace Johodp.Messaging.Mediator;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Johodp.Messaging.Validation;

/// <summary>
/// Base handler providing common cross-cutting concerns:
/// - Validation (automatic validation before handling)
/// - Logging (before/after execution)
/// - Timing (execution duration)
/// - Error handling (structured logging)
/// 
/// Handlers can override hooks to customize behavior.
/// </summary>
public abstract class BaseHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    protected readonly ILogger _logger;
    protected readonly IValidator<TRequest>? _validator;

    protected BaseHandler(ILogger logger, IValidator<TRequest>? validator = null)
    {
        _logger = logger;
        _validator = validator;
    }

    /// <summary>
    /// Main Handle method - orchestrates hooks and core logic
    /// </summary>
    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        
        await OnBeforeHandle(request);
        
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await HandleCore(request, cancellationToken);
            sw.Stop();
            
            await OnAfterHandle(request, response, sw.Elapsed);
            
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await OnError(request, ex, sw.Elapsed);
            throw;
        }
    }

    /// <summary>
    /// Core business logic - must be implemented by derived handlers
    /// </summary>
    protected abstract Task<TResponse> HandleCore(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Hook executed before handling the request
    /// Automatically validates the request if a validator is provided
    /// Override to add custom pre-processing logic (call base.OnBeforeHandle to keep validation)
    /// </summary>
    protected virtual async Task OnBeforeHandle(TRequest request)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);
        
        // Automatic validation
        if (_validator != null)
        {
            var errors = await _validator.ValidateAsync(request);
            if (errors.Any())
            {
                _logger.LogWarning("Validation failed for {RequestName}: {ErrorCount} error(s)", 
                    requestName, errors.Count);
                throw new ValidationException(errors);
            }
        }
    }

    /// <summary>
    /// Hook executed after successful handling
    /// Override to add custom post-processing logic
    /// </summary>
    protected virtual Task OnAfterHandle(TRequest request, TResponse response, TimeSpan elapsed)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation(
            "Successfully handled {RequestName} in {ElapsedMs}ms", 
            requestName, 
            elapsed.TotalMilliseconds);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook executed when an error occurs
    /// Override to add custom error handling logic
    /// </summary>
    protected virtual Task OnError(TRequest request, Exception exception, TimeSpan elapsed)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogError(
            exception,
            "Error handling {RequestName} after {ElapsedMs}ms",
            requestName,
            elapsed.TotalMilliseconds);
        return Task.CompletedTask;
    }
}
