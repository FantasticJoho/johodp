namespace Johodp.Messaging.Mediator;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Lightweight mediator implementation for sending requests to handlers
/// </summary>
public class Sender : ISender
{
    private readonly IServiceProvider _serviceProvider;

    public Sender(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var responseType = typeof(TResponse);
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);

        var handler = _serviceProvider.GetRequiredService(handlerType);
        
        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type {requestType.Name}");
        }

        // Use reflection to invoke the Handle method
        var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle));
        
        if (handleMethod == null)
        {
            throw new InvalidOperationException(
                $"Handle method not found on handler for {requestType.Name}");
        }

        var task = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, cancellationToken })!;
        return await task;
    }
}
