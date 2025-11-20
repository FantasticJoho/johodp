namespace Johodp.Application.Common.Mediator;

/// <summary>
/// Interface for sending requests to handlers
/// </summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
