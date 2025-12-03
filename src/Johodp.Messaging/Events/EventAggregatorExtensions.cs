namespace Johodp.Messaging.Events;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>
/// Extension methods for registering the event aggregator
/// </summary>
public static class EventAggregatorExtensions
{
    /// <summary>
    /// Registers the event aggregator and all event handlers from specified assemblies
    /// </summary>
    public static IServiceCollection AddEventAggregator(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Register the event bus
        services.AddSingleton<IEventBus, EventAggregator>();

        // If no assemblies specified, use calling assembly
        assemblies = assemblies.Any() 
            ? assemblies 
            : new[] { Assembly.GetCallingAssembly() };

        // Register all event handlers
        RegisterEventHandlers(services, assemblies);

        return services;
    }

    private static void RegisterEventHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var handlerTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEventHandler<>)))
            .SelectMany(type => type.GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .Select(i => new
                {
                    Implementation = type,
                    Interface = i
                }));

        foreach (var handler in handlerTypes)
        {
            services.AddScoped(handler.Interface, handler.Implementation);
        }
    }
}
