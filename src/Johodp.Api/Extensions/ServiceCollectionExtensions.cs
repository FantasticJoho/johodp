namespace Johodp.Api.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Johodp.Infrastructure.Persistence.DbContext;
using Johodp.Application.Common.Interfaces;
using Johodp.Infrastructure.Persistence;
using Johodp.Infrastructure.Services;
using Johodp.Infrastructure.IdentityServer;
using Duende.IdentityServer.Services;
using Johodp.Application.Common.Mediator;

/// <summary>
/// Extension methods for configuring infrastructure services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services including database, repositories, 
    /// IdentityServer, ASP.NET Identity, and application services
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // ====================================================================
        // DATABASE CONFIGURATION
        // ====================================================================
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        
        services.AddDbContext<JohodpDbContext>(options =>
            options.UseNpgsql(dataSource,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("Johodp.Infrastructure");
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                }));

        // ====================================================================
        // REPOSITORIES & DATA ACCESS
        // ====================================================================
        services.AddScoped<IUserRepository, Johodp.Infrastructure.Persistence.Repositories.UserRepository>();
        services.AddScoped<IClientRepository, Johodp.Infrastructure.Persistence.Repositories.ClientRepository>();
        services.AddScoped<ITenantRepository, Johodp.Infrastructure.Persistence.Repositories.TenantRepository>();
        services.AddScoped<ICustomConfigurationRepository, Johodp.Infrastructure.Persistence.Repositories.CustomConfigurationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ====================================================================
        // MEDIATOR (CQRS Pattern)
        // ====================================================================
        // Auto-register all command/query handlers from Application assembly
        services.AddMediator(typeof(Johodp.Application.Users.Commands.RegisterUserCommand).Assembly);

        // ====================================================================
        // DOMAIN EVENTS (Event-Driven Architecture)
        // ====================================================================
        services.AddSingleton<Johodp.Application.Common.Events.IEventBus, Johodp.Infrastructure.Services.ChannelEventBus>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddHostedService<Johodp.Infrastructure.Services.DomainEventProcessor>();
        
        // Event Handlers
        services.AddScoped<Johodp.Application.Common.Events.IEventHandler<Johodp.Domain.Users.Events.UserPendingActivationEvent>, 
            Johodp.Application.Users.EventHandlers.SendActivationEmailHandler>();
        services.AddScoped<Johodp.Application.Common.Events.IEventHandler<Johodp.Domain.Users.Events.UserActivatedEvent>, 
            Johodp.Application.Users.EventHandlers.UserActivatedEventHandler>();

        // ====================================================================
        // APPLICATION SERVICES
        // ====================================================================
        // Notification Service (webhooks to external applications)
        services.AddHttpClient<INotificationService, Johodp.Infrastructure.Services.NotificationService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // Email Service (activation emails, password reset, etc.)
        services.AddScoped<IEmailService, Johodp.Infrastructure.Services.EmailService>();

        // User Activation Service (token generation and email sending)
        services.AddScoped<IUserActivationService, Johodp.Infrastructure.Services.UserActivationService>();

        // MFA Authentication Service (client-specific multi-factor authentication)
        services.AddScoped<IMfaAuthenticationService, Johodp.Infrastructure.Services.MfaAuthenticationService>();

        // ====================================================================
        // IDENTITYSERVER CONFIGURATION
        // ====================================================================
        // Custom client store (loads clients dynamically from database)
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
                        sql =>
                        {
                            sql.MigrationsAssembly("Johodp.Infrastructure");
                            sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                        });
                
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
            // Production: Use persistent signing credential (single key, no rotation)
            var signingKeyPath = configuration["IdentityServer:SigningKeyPath"];
            
            if (string.IsNullOrEmpty(signingKeyPath) || !File.Exists(signingKeyPath))
            {
                throw new InvalidOperationException(
                    "IdentityServer:SigningKeyPath must be configured in production. " +
                    "Generate a certificate with: dotnet dev-certs https -ep path/to/key.pfx -p YourPassword " +
                    "Or generate a JWK with: dotnet run --project tools/KeyGenerator");
            }
            
            // Auto-detect file type based on extension
            var extension = Path.GetExtension(signingKeyPath).ToLowerInvariant();
            
            if (extension == ".pfx" || extension == ".p12")
            {
                // X.509 Certificate (PFX/PKCS#12)
                var keyPassword = configuration["IdentityServer:SigningKeyPassword"];
                var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    signingKeyPath, 
                    keyPassword);
                
                idServerBuilder.AddSigningCredential(cert);
            }
            else if (extension == ".json" || extension == ".jwk")
            {
                // JSON Web Key (RFC 7517)
                var key = Johodp.Infrastructure.IdentityServer.SigningKeyHelper.LoadJwkFromFile(signingKeyPath);
                idServerBuilder.AddSigningCredential(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported signing key file format: {extension}. " +
                    "Supported formats: .pfx, .p12 (certificate), .json, .jwk (JSON Web Key)");
            }
        }

        // Profile service: map domain user to token/userinfo claims
        services.AddScoped<IProfileService, IdentityServerProfileService>();

        // ====================================================================
        // ASP.NET IDENTITY CONFIGURATION
        // ====================================================================
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

        // ====================================================================
        // CORS CONFIGURATION (Development)
        // ====================================================================
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
