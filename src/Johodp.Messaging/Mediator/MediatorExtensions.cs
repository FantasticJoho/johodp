namespace Johodp.Messaging.Mediator;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>
/// Extension methods for registering the lightweight mediator
/// </summary>
public static class MediatorExtensions
{
    /// <summary>
    /// Registers the mediator and all handlers from specified assemblies
    /// </summary>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Register the sender
        services.AddScoped<ISender, Sender>();

        // If no assemblies specified, use calling assembly
        assemblies = assemblies.Any() 
            ? assemblies 
            : new[] { Assembly.GetCallingAssembly() };

        // Register all handlers
        RegisterHandlers(services, assemblies);

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var handlerTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .Select(type => new
            {
                Implementation = type,
                Interface = type.GetInterfaces()
                    .First(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            });

        foreach (var handler in handlerTypes)
        {
            services.AddScoped(handler.Interface, handler.Implementation);
        }
    }
}
