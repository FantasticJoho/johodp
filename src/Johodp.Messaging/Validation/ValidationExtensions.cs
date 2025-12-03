namespace Johodp.Messaging.Validation;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

public static class ValidationExtensions
{
    /// <summary>
    /// Registers all validators from the specified assembly
    /// </summary>
    public static IServiceCollection AddValidators(this IServiceCollection services, Assembly assembly)
    {
        var validatorType = typeof(IValidator<>);

        var validators = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorType)
                .Select(i => new
                {
                    Interface = i,
                    Implementation = t
                }))
            .ToList();

        foreach (var validator in validators)
        {
            services.AddScoped(validator.Interface, validator.Implementation);
        }

        return services;
    }

    /// <summary>
    /// Registers all validators from the assembly containing the specified type
    /// </summary>
    public static IServiceCollection AddValidatorsFromAssemblyContaining<T>(this IServiceCollection services)
    {
        return services.AddValidators(typeof(T).Assembly);
    }
}
