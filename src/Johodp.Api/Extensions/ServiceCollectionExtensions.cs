namespace Johodp.Api.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Johodp.Infrastructure.Persistence.DbContext;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence;
using Johodp.Infrastructure.Services;
using Johodp.Infrastructure.IdentityServer;
using Duende.IdentityServer.Services;
using Johodp.Application.Common.Mediator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database - Configure Npgsql for dynamic JSON serialization
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        
        services.AddDbContext<JohodpDbContext>(options =>
            options.UseNpgsql(dataSource,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly("Johodp.Infrastructure")));

        // User Repository (for profile service)
        services.AddScoped<IUserRepository, Johodp.Infrastructure.Persistence.Repositories.UserRepository>();

        // Client Repository
        services.AddScoped<IClientRepository, Johodp.Infrastructure.Persistence.Repositories.ClientRepository>();

        // Tenant Repository
        services.AddScoped<ITenantRepository, Johodp.Infrastructure.Persistence.Repositories.TenantRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Mini-MediatR: Auto-register all handlers from Application assembly
        services.AddMediator(typeof(Johodp.Application.Users.Commands.RegisterUserCommand).Assembly);

        // Domain Event Publisher with Channel-based Event Bus
        services.AddSingleton<Johodp.Application.Common.Events.IEventBus, Johodp.Infrastructure.Services.ChannelEventBus>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        
        // Domain Event Processor (Background Service)
        services.AddHostedService<Johodp.Infrastructure.Services.DomainEventProcessor>();
        
        // Event Handlers
        services.AddScoped<Johodp.Application.Common.Events.IEventHandler<Johodp.Domain.Users.Events.UserPendingActivationEvent>, 
            Johodp.Application.Users.EventHandlers.SendActivationEmailHandler>();
        services.AddScoped<Johodp.Application.Common.Events.IEventHandler<Johodp.Domain.Users.Events.UserActivatedEvent>, 
            Johodp.Application.Users.EventHandlers.UserActivatedEventHandler>();

        // Notification Service (fire-and-forget HTTP calls to external apps)
        services.AddHttpClient<INotificationService, Johodp.Infrastructure.Services.NotificationService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // Additional handlers not yet converted to IRequestHandler (will be auto-registered once converted)
        services.AddScoped<Johodp.Application.Users.Commands.AddUserToTenantCommandHandler>();
        services.AddScoped<Johodp.Application.Users.Commands.RemoveUserFromTenantCommandHandler>();

        // IdentityServer with custom client store (loads from database)
        services.AddScoped<Duende.IdentityServer.Stores.IClientStore, Johodp.Infrastructure.IdentityServer.CustomClientStore>();
        
        var idServerBuilder = services.AddIdentityServer()
            .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())
            .AddInMemoryApiResources(IdentityServerConfig.GetApiResources())
            .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())
            // Store authorization codes, refresh tokens, and consents in PostgreSQL
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseNpgsql(connectionString,
                        sql => sql.MigrationsAssembly("Johodp.Infrastructure"));
                
                // Automatic cleanup of expired tokens
                options.EnableTokenCleanup = true;
                options.TokenCleanupInterval = 3600; // 1 hour
            });
            // Clients are now loaded from database via CustomClientStore

        // Register a minimal IUserClaimsPrincipalFactory for the domain User so
        // IdentityServer can decorate it when wiring up ASP.NET Identity.
        services.AddScoped<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<Johodp.Domain.Users.Aggregates.User>, Johodp.Infrastructure.Identity.DomainUserClaimsPrincipalFactory>();

        // Wire IdentityServer to use ASP.NET Identity for user authentication
        idServerBuilder.AddAspNetIdentity<Johodp.Domain.Users.Aggregates.User>()
                   .AddDeveloperSigningCredential();

        // Profile service: map domain user -> token/userinfo claims
        services.AddScoped<IProfileService, IdentityServerProfileService>();

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

        // Configure token lifespan for email confirmation tokens (activation)
        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
        });

        // TODO: Add Tenant API Key Authentication for external applications later
        // services.AddAuthentication()
        //     .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Johodp.Infrastructure.Identity.TenantApiKeyAuthenticationHandler>(
        //         "TenantApiKey",
        //         options => { });

        // Ensure the application cookie used by ASP.NET Identity has dev-friendly settings
        // so browsers accept it on localhost HTTP. For production, revisit these settings.
        services.ConfigureApplicationCookie(opts =>
        {
            opts.Cookie.Name = ".AspNetCore.Identity.Application";
            opts.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            opts.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        });

        // CORS policy for development (allow any origin with credentials)
        services.AddCors(options =>
        {
            options.AddPolicy("AllowSpa", policy =>
            {
                policy.SetIsOriginAllowed(origin => true) // Allow any origin in development
                      .AllowCredentials()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
