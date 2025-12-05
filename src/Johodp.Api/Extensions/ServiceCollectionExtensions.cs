namespace Johodp.Api.Extensions;

using Duende.IdentityServer.Services;
using Johodp.Messaging.Events;
using Johodp.Messaging.Mediator;
using Johodp.Messaging.Validation;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Users;
using Johodp.Application.Users.Commands;
using Johodp.Application.Users.EventHandlers;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.Events;
using Johodp.Infrastructure.Identity;
using Johodp.Infrastructure.IdentityServer;
using Johodp.Infrastructure.Persistence;
using Johodp.Infrastructure.Persistence.DbContext;
using Johodp.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Extension methods for configuring infrastructure services.
/// Optimized for readability and performance with extracted helper methods.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string InfrastructureAssemblyName = "Johodp.Infrastructure";

    /// <summary>
    /// Registers all infrastructure services including database, repositories, 
    /// IdentityServer, ASP.NET Identity, and application services.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.AddDatabase(connectionString);
        services.AddRepositories();
        services.AddMediator();
        services.AddValidatorsFromAssemblyContaining<RegisterUserCommand>();
        services.AddDomainEvents();
        services.AddApplicationServices();
        services.AddIdentityServerConfiguration(configuration, environment, connectionString);
        services.AddIdentityConfiguration();
        services.AddCorsPolicy();

        return services;
    }

    /// <summary>
    /// Configures PostgreSQL database with dynamic JSON support.
    /// Uses Npgsql data source for better connection pooling and performance.
    /// </summary>
    private static void AddDatabase(this IServiceCollection services, string connectionString)
    {
        // Enable dynamic JSON serialization for PostgreSQL JSONB columns
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<JohodpDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MigrationsAssembly(InfrastructureAssemblyName);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
            }));
    }

    /// <summary>
    /// Registers all domain repositories and Unit of Work pattern.
    /// Repositories provide data access abstraction following DDD principles.
    /// </summary>
    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, Johodp.Infrastructure.Persistence.Repositories.UserRepository>();
        services.AddScoped<IClientRepository, Johodp.Infrastructure.Persistence.Repositories.ClientRepository>();
        services.AddScoped<ITenantRepository, Johodp.Infrastructure.Persistence.Repositories.TenantRepository>();
        services.AddScoped<ICustomConfigurationRepository, Johodp.Infrastructure.Persistence.Repositories.CustomConfigurationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    /// <summary>
    /// Configures CQRS Mediator pattern.
    /// Auto-registers all Command/Query handlers from Application assembly.
    /// </summary>
    private static void AddMediator(this IServiceCollection services)
    {
        services.AddMediator(typeof(RegisterUserCommand).Assembly);
    }

    /// <summary>
    /// Configures event-driven architecture with simple event aggregator pattern.
    /// Registers domain event handlers for user lifecycle events.
    /// </summary>
    private static void AddDomainEvents(this IServiceCollection services)
    {
        services.AddScoped<IEventBus, EventAggregator>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

        // User lifecycle event handlers
        services.AddScoped<IEventHandler<UserPendingActivationEvent>, SendActivationEmailHandler>();
        services.AddScoped<IEventHandler<UserActivatedEvent>, UserActivatedEventHandler>();
    }

    /// <summary>
    /// Registers application-layer services for business logic and infrastructure concerns.
    /// Includes notification, email, user activation, and MFA services.
    /// </summary>
    private static void AddApplicationServices(this IServiceCollection services)
    {
        // HTTP client for webhook notifications with connection pooling
        services.AddHttpClient<INotificationService, NotificationService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserActivationService, UserActivationService>();
        services.AddScoped<IMfaAuthenticationService, MfaAuthenticationService>();
        services.AddScoped<IMfaService, MfaService>();
    }

    /// <summary>
    /// Configures Duende IdentityServer for OAuth2/OIDC with PostgreSQL storage.
    /// Uses custom client store for dynamic database-driven client configuration.
    /// </summary>
    private static void AddIdentityServerConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        string connectionString)
    {
        // Dynamic client loading from database
        services.AddScoped<Duende.IdentityServer.Stores.IClientStore, CustomClientStore>();

        var idServerBuilder = services.AddIdentityServer(options =>
        {
            options.UserInteraction.LoginUrl = "/api/auth/login";
            options.UserInteraction.LoginReturnUrlParameter = "returnUrl";
            options.UserInteraction.CreateAccountUrl = "/api/auth/register";
            options.UserInteraction.LogoutUrl = "/api/auth/logout";
            options.UserInteraction.DeviceVerificationUrl = "/api/auth/device";
            options.UserInteraction.ErrorUrl = "/error";

            // Configure event raising based on environment
            var eventsConfig = configuration.GetSection("IdentityServer:Events");
            options.Events.RaiseSuccessEvents = eventsConfig.GetValue("RaiseSuccessEvents", false);
            options.Events.RaiseFailureEvents = eventsConfig.GetValue("RaiseFailureEvents", true);
            options.Events.RaiseInformationEvents = eventsConfig.GetValue("RaiseInformationEvents", false);
            options.Events.RaiseErrorEvents = eventsConfig.GetValue("RaiseErrorEvents", true);
        })
        .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())
        .AddInMemoryApiResources(IdentityServerConfig.GetApiResources())
        .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())
        .AddOperationalStore(options =>
        {
            options.ConfigureDbContext = b => b.UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsAssembly(InfrastructureAssemblyName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
            });

            options.DefaultSchema = "dbo";
            options.EnableTokenCleanup = true;
            options.TokenCleanupInterval = 3600; // 1 hour
            
            // Standardize table names to snake_case for consistency:
            // 1. Cohérence: Les tables de l'application (clients, users, tenants) sont déjà en snake_case
            // 2. Pas de quotes: PostgreSQL ne nécessite pas de double-quotes pour les noms en minuscules
            //    Exemple: SELECT * FROM persisted_grants au lieu de SELECT * FROM "PersistedGrants"
            // 3. Case-insensitive: PostgreSQL convertit automatiquement en minuscules les identifiants non quotés
            // 4. Standard SQL: Convention largement adoptée dans PostgreSQL et les bases de données relationnelles
            // 5. Lisibilité pgAdmin: Requêtes plus propres et uniformes sans mélange de conventions
            options.PersistedGrants.Name = "persisted_grants";
            options.DeviceFlowCodes.Name = "device_codes";
            options.Keys.Name = "keys";
            options.ServerSideSessions.Name = "server_side_sessions";
            options.PushedAuthorizationRequests.Name = "pushed_authorization_requests";
        });

        services.AddScoped<IUserClaimsPrincipalFactory<User>, DomainUserClaimsPrincipalFactory>();
        idServerBuilder.AddAspNetIdentity<User>();

        ConfigureSigningCredential(idServerBuilder, configuration, environment);

        services.AddScoped<IProfileService, IdentityServerProfileService>();
        services.AddSingleton<Duende.IdentityServer.Services.IEventSink, IdentityServerEventSink>();
    }

    /// <summary>
    /// Configures token signing credential based on environment.
    /// Development: temporary key (regenerated on restart).
    /// Production: persistent X.509 certificate or JSON Web Key.
    /// </summary>
    private static void ConfigureSigningCredential(
        IIdentityServerBuilder idServerBuilder,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // Temporary signing key for development (not persisted)
            idServerBuilder.AddDeveloperSigningCredential();
            return;
        }

        var signingKeyPath = configuration["IdentityServer:SigningKeyPath"];

        if (string.IsNullOrEmpty(signingKeyPath) || !File.Exists(signingKeyPath))
        {
            throw new InvalidOperationException(
                "IdentityServer:SigningKeyPath must be configured in production. " +
                "Generate a certificate with: dotnet dev-certs https -ep path/to/key.pfx -p YourPassword " +
                "Or generate a JWK with: dotnet run --project tools/KeyGenerator");
        }

        var extension = Path.GetExtension(signingKeyPath).ToLowerInvariant();

        switch (extension)
        {
            case ".pfx" or ".p12":
                var keyPassword = configuration["IdentityServer:SigningKeyPassword"];
                var cert = new X509Certificate2(signingKeyPath, keyPassword);
                idServerBuilder.AddSigningCredential(cert);
                break;

            case ".json" or ".jwk":
                var key = SigningKeyHelper.LoadJwkFromFile(signingKeyPath);
                idServerBuilder.AddSigningCredential(key, SecurityAlgorithms.RsaSha256);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported signing key file format: {extension}. " +
                    "Supported formats: .pfx, .p12 (certificate), .json, .jwk (JSON Web Key)");
        }
    }

    /// <summary>
    /// Configures ASP.NET Core Identity with custom domain User entity.
    /// Uses relaxed password requirements for development convenience.
    /// </summary>
    private static void AddIdentityConfiguration(this IServiceCollection services)
    {
        services.AddIdentityCore<User>(options =>
        {
            // Relaxed password policy for development
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireDigit = false;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddSignInManager<CustomSignInManager>()
        .AddUserStore<UserStore>()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
        });

        services.ConfigureApplicationCookie(opts =>
        {
            opts.Cookie.Name = ".AspNetCore.Identity.Application";
            opts.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            opts.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        });
    }

    /// <summary>
    /// Configures CORS policy for development (allows any origin).
    /// TODO: Restrict origins in production for security.
    /// </summary>
    private static void AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowSpa", policy =>
            {
                // Development: allow any origin with credentials
                policy.SetIsOriginAllowed(_ => true)
                      .AllowCredentials()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
    }
}
