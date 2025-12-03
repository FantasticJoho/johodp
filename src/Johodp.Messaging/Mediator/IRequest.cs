namespace Johodp.Messaging.Mediator;

/// <summary>
/// Marker interface for a request that returns a response
/// </summary>
public interface IRequest<out TResponse>
{
}
