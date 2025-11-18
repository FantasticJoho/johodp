namespace Johodp.Api.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MediatR;
using Johodp.Infrastructure.Persistence.DbContext;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence;
using Johodp.Infrastructure.Services;
using Johodp.Infrastructure.IdentityServer;
using IdentityServer4.Stores;
using IdentityServer4.Models;
using IdentityServer4.AspNetIdentity;

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

        // User Repository (for profile service)
        services.AddScoped<IUserRepository, Johodp.Infrastructure.Persistence.Repositories.UserRepository>();

        // Client Repository
        services.AddScoped<IClientRepository, Johodp.Infrastructure.Persistence.Repositories.ClientRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Domain Event Publisher
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Johodp.Application.Users.Commands.RegisterUserCommand).Assembly));

        // IdentityServer (in-memory for dev) and integrate with ASP.NET Identity
        var idServerBuilder = services.AddIdentityServer()
            .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())
            .AddInMemoryApiResources(IdentityServerConfig.GetApiResources())
            .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())
            .AddInMemoryClients(IdentityServerConfig.GetClients());

        // Register a minimal IUserClaimsPrincipalFactory for the domain User so
        // IdentityServer can decorate it when wiring up ASP.NET Identity.
        services.AddScoped<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<Johodp.Domain.Users.Aggregates.User>, Johodp.Infrastructure.Identity.DomainUserClaimsPrincipalFactory>();

        // Wire IdentityServer to use ASP.NET Identity for user authentication
        idServerBuilder.AddAspNetIdentity<Johodp.Domain.Users.Aggregates.User>()
                   .AddDeveloperSigningCredential();

        // Profile service: map domain user -> token/userinfo claims
        services.AddScoped<IdentityServer4.Services.IProfileService, IdentityServerProfileService>();

        // ASP.NET Identity integration using domain User and custom stores
        services.AddIdentityCore<Johodp.Domain.Users.Aggregates.User>(options =>
        {
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireDigit = false;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddSignInManager<Johodp.Infrastructure.Identity.CustomSignInManager>()
        .AddUserStore<Johodp.Infrastructure.Identity.UserStore>()
        .AddDefaultTokenProviders();

        // Ensure the application cookie used by ASP.NET Identity has dev-friendly settings
        // so browsers accept it on localhost HTTP. For production, revisit these settings.
        services.ConfigureApplicationCookie(opts =>
        {
            opts.Cookie.Name = ".AspNetCore.Identity.Application";
            opts.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            opts.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        });

        // CORS policy for local SPA development (allow credentials)
        services.AddCors(options =>
        {
            options.AddPolicy("AllowSpa", policy =>
            {
                policy.WithOrigins("http://localhost:4200")
                      .AllowCredentials()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
