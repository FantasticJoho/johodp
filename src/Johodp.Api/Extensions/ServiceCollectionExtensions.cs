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
        IConfiguration configuration,
        IWebHostEnvironment environment)
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

        // Email Service (pour l'envoi d'emails d'activation, etc.)
        services.AddScoped<IEmailService, Johodp.Infrastructure.Services.EmailService>();

        // User Activation Service (génère les tokens d'activation et envoie les emails)
        services.AddScoped<IUserActivationService, Johodp.Infrastructure.Services.UserActivationService>();

        // MFA Authentication Service (for client-specific MFA)
        services.AddScoped<IMfaAuthenticationService, Johodp.Infrastructure.Services.MfaAuthenticationService>();

        // Additional handlers not yet converted to IRequestHandler (will be auto-registered once converted)
        services.AddScoped<Johodp.Application.Users.Commands.AddUserToTenantCommandHandler>();
        services.AddScoped<Johodp.Application.Users.Commands.RemoveUserFromTenantCommandHandler>();

        // IdentityServer with custom client store (loads from database)
        services.AddScoped<Duende.IdentityServer.Stores.IClientStore, Johodp.Infrastructure.IdentityServer.CustomClientStore>();
        
        var idServerBuilder = services.AddIdentityServer(options =>
            {
                // Configure where IdentityServer redirects for user interactions
                options.UserInteraction.LoginUrl = "/api/auth/login";
                options.UserInteraction.LoginReturnUrlParameter = "returnUrl";
                options.UserInteraction.CreateAccountUrl = "/api/auth/register";
                options.UserInteraction.LogoutUrl = "/api/auth/logout";
                options.UserInteraction.DeviceVerificationUrl = "/api/auth/device";
                
                // Not needed since clients have RequireConsent = false
                // options.UserInteraction.ConsentUrl = "/consent";
                
                // Error handling can be done via API responses (no error page needed)
                options.UserInteraction.ErrorUrl = "/error";
            })
            .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())
            .AddInMemoryApiResources(IdentityServerConfig.GetApiResources())
            .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())
            // Store authorization codes, refresh tokens, and consents in PostgreSQL
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseNpgsql(connectionString,
                        sql => sql.MigrationsAssembly("Johodp.Infrastructure"));
                
                // Use 'dbo' schema for IdentityServer tables (consistent with JohodpDbContext)
                options.DefaultSchema = "dbo";
                
                // Automatic cleanup of expired tokens
                options.EnableTokenCleanup = true;
                options.TokenCleanupInterval = 3600; // 1 hour
            });
            // Clients are now loaded from database via CustomClientStore

        // Register a minimal IUserClaimsPrincipalFactory for the domain User so
        // IdentityServer can decorate it when wiring up ASP.NET Identity.
        services.AddScoped<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<Johodp.Domain.Users.Aggregates.User>, Johodp.Infrastructure.Identity.DomainUserClaimsPrincipalFactory>();

        // Wire IdentityServer to use ASP.NET Identity for user authentication
        idServerBuilder.AddAspNetIdentity<Johodp.Domain.Users.Aggregates.User>();
        
        // Signing credential configuration based on environment
        if (environment.IsDevelopment())
        {
            // Development: Use temporary signing credential (regenerated on each restart)
            idServerBuilder.AddDeveloperSigningCredential();
        }
        else
        {
            // Production: Use persistent signing credential
            var signingMethod = configuration["IdentityServer:SigningMethod"] ?? "Certificate";
            
            if (signingMethod == "Certificate")
            {
                // Option A: X.509 Certificate
                var signingKeyPath = configuration["IdentityServer:SigningKeyPath"];
                
                if (string.IsNullOrEmpty(signingKeyPath) || !File.Exists(signingKeyPath))
                {
                    throw new InvalidOperationException(
                        "IdentityServer:SigningKeyPath must be configured in production. " +
                        "Generate a key with: dotnet dev-certs https -ep path/to/key.pfx -p YourPassword");
                }
                
                var keyPassword = configuration["IdentityServer:SigningKeyPassword"];
                idServerBuilder.AddSigningCredential(
                    new System.Security.Cryptography.X509Certificates.X509Certificate2(
                        signingKeyPath, 
                        keyPassword));
            }
            else if (signingMethod == "JWK")
            {
                // Option B: JSON Web Key (RFC 7517)
                // Load from Vault JSON or file
                var currentKeyJson = configuration["IdentityServer:CurrentKeyJson"];
                
                if (!string.IsNullOrEmpty(currentKeyJson))
                {
                    // Load from Vault (injected via Program.cs)
                    var currentKey = Johodp.Infrastructure.IdentityServer.SigningKeyHelper.LoadJwkFromJson(currentKeyJson);
                    idServerBuilder.AddSigningCredential(currentKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256);
                    
                    // Support rotation: load previous key for validation
                    var previousKeyJson = configuration["IdentityServer:PreviousKeyJson"];
                    if (!string.IsNullOrEmpty(previousKeyJson))
                    {
                        var previousKey = Johodp.Infrastructure.IdentityServer.SigningKeyHelper.LoadJwkFromJson(previousKeyJson);
                        idServerBuilder.AddValidationKey(previousKey);
                    }
                }
                else
                {
                    // Fallback: load from file
                    var signingKeyPath = configuration["IdentityServer:SigningKeyPath"];
                    
                    if (string.IsNullOrEmpty(signingKeyPath) || !File.Exists(signingKeyPath))
                    {
                        throw new InvalidOperationException(
                            "IdentityServer:SigningKeyPath must be configured in production. " +
                            "Generate a JWK with: dotnet run --project tools/KeyGenerator");
                    }
                    
                    var jwkKey = Johodp.Infrastructure.IdentityServer.SigningKeyHelper.LoadJwkFromFile(signingKeyPath);
                    idServerBuilder.AddSigningCredential(jwkKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256);
                    
                    // Support rotation: load previous key if configured
                    var previousKeyPath = configuration["IdentityServer:PreviousSigningKeyPath"];
                    if (!string.IsNullOrEmpty(previousKeyPath) && File.Exists(previousKeyPath))
                    {
                        var previousKey = Johodp.Infrastructure.IdentityServer.SigningKeyHelper.LoadJwkFromFile(previousKeyPath);
                        idServerBuilder.AddValidationKey(previousKey);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown signing method: {signingMethod}. Valid values: Certificate, JWK");
            }
        }

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
