namespace Johodp.Api.Extensions;

using Microsoft.EntityFrameworkCore;
using MediatR;
using Johodp.Infrastructure.Persistence.DbContext;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence;
using Johodp.Infrastructure.Services;
using Johodp.Infrastructure.IdentityServer;
using IdentityServer4.Stores;
using IdentityServer4.Models;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<JohodpDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly("Johodp.Infrastructure")));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Domain Event Publisher
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Johodp.Application.Users.Commands.RegisterUserCommand).Assembly));

        // IdentityServer (in-memory for dev)
        services.AddIdentityServer()
            .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())
            .AddInMemoryApiResources(IdentityServerConfig.GetApiResources())
            .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())
            .AddInMemoryClients(IdentityServerConfig.GetClients())
            .AddDeveloperSigningCredential();

        // Profile service: map domain user -> token/userinfo claims
        services.AddScoped<IdentityServer4.Services.IProfileService, IdentityServerProfileService>();

        return services;
    }
}
