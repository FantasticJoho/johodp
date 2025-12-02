using Johodp.Api.Extensions;
using Johodp.Api.Middleware;
using Serilog;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// LOGGING CONFIGURATION
// ============================================================================
ConfigureLogging(builder);

// ============================================================================
// AUTHENTICATION & AUTHORIZATION
// ============================================================================
ConfigureAuthentication(builder.Services);

// ============================================================================
// MVC & API CONFIGURATION
// ============================================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new Johodp.Api.Extensions.TenantIdJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new Johodp.Api.Extensions.ClientIdJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new Johodp.Api.Extensions.CustomConfigurationIdJsonConverter());
    });
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================================
// HEALTH CHECKS
// ============================================================================
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: new[] { "db", "ready" })
    .AddCheck<Johodp.Api.HealthChecks.IdentityServerHealthCheck>(
        "identityserver",
        tags: new[] { "identityserver", "ready" });

// ============================================================================
// INFRASTRUCTURE SERVICES (Database, Repositories, IdentityServer, etc.)
// ============================================================================
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

// ============================================================================
// BUILD APPLICATION
// ============================================================================
var app = builder.Build();

// ============================================================================
// STATIC FILES
// ============================================================================
app.UseStaticFiles();
app.UseDefaultFiles();

// ============================================================================
// DATABASE MIGRATIONS (Development Only)
// ============================================================================
if (app.Environment.IsDevelopment())
{
    ApplyDatabaseMigrations(app);
}

// ============================================================================
// MIDDLEWARE PIPELINE
// ============================================================================
ConfigureMiddlewarePipeline(app);

// ============================================================================
// SWAGGER / SCALAR UI (Development Only)
// ============================================================================
if (app.Environment.IsDevelopment())
{
    ConfigureSwaggerUI(app);
}

// ============================================================================
// ROUTING & CORS
// ============================================================================
app.UseRouting();
app.UseCors("AllowSpa");

// ============================================================================
// AUTHENTICATION & AUTHORIZATION
// ============================================================================
app.UseAuthentication();
app.UseIdentityServer();
app.UseAuthorization();

// ============================================================================
// HEALTH CHECK ENDPOINTS
// ============================================================================
ConfigureHealthCheckEndpoints(app);

// ============================================================================
// CONTROLLER ENDPOINTS
// ============================================================================
app.MapControllers();
app.MapDefaultControllerRoute();


// ============================================================================
// APPLICATION STARTUP
// ============================================================================
try
{
    Log.Information("Starting Johodp Identity Provider application");
    Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
    Log.Information("Application URLs: {Urls}", string.Join(", ", builder.Configuration["urls"] ?? "not configured"));
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly. Error: {ErrorMessage}", ex.Message);
    throw;
}
finally
{
    Log.Information("Shutting down Johodp Identity Provider");
    Log.CloseAndFlush();
}

// ============================================================================
// LOCAL FUNCTIONS (Configuration Helpers)
// ============================================================================

static void ConfigureLogging(WebApplicationBuilder builder)
{
    // Bootstrap logger for startup
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Johodp")
        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    builder.Services.AddHttpContextAccessor();
    
    // Configure Serilog with tenant/client enrichment
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .MinimumLevel.Information()
           .Enrich.FromLogContext()
           .Enrich.WithProperty("Application", "Johodp")
           .Enrich.With(new Johodp.Api.Logging.TenantClientEnricher(services.GetRequiredService<IHttpContextAccessor>()))
           .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {TenantId} {ClientId} [{SourceContext}] {Message:lj}{NewLine}{Exception}");
    });
}

static void ConfigureAuthentication(IServiceCollection services)
{
    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Identity.Application";
        options.DefaultSignInScheme = "Identity.Application";
        options.DefaultChallengeScheme = "Identity.Application";
    })
    .AddCookie("Identity.Application", options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/accessdenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = ".AspNetCore.Identity.Application";
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    })
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/accessdenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
}

static void ApplyDatabaseMigrations(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Apply JohodpDbContext migrations
        var johodpDb = scope.ServiceProvider.GetRequiredService<Johodp.Infrastructure.Persistence.DbContext.JohodpDbContext>();
        logger.LogInformation("Applying JohodpDbContext migrations...");
        johodpDb.Database.Migrate();
        logger.LogInformation("✅ JohodpDbContext migrations applied successfully");
        
        // Apply PersistedGrantDbContext migrations (IdentityServer)
        var persistedGrantDb = scope.ServiceProvider.GetRequiredService<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>();
        logger.LogInformation("Applying PersistedGrantDbContext migrations...");
        persistedGrantDb.Database.Migrate();
        logger.LogInformation("✅ PersistedGrantDbContext migrations applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ An error occurred while migrating the database");
        throw;
    }
}

static void ConfigureMiddlewarePipeline(WebApplication app)
{
    // Custom request logging middleware (captures all requests with timing)
    app.UseRequestLogging();

    // Serilog request logging for detailed diagnostics
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                diagnosticContext.Set("UserEmail", httpContext.User.FindFirst("email")?.Value);
                diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
            }
        };
    });

    // Global exception handler
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    // HTTPS redirection
    app.UseHttpsRedirection();
}

static void ConfigureSwaggerUI(WebApplication app)
{
    app.UseSwagger(opt => opt.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference(opt =>
    {
        opt.Title = "Johodp Identity Provider API";
        opt.Theme = ScalarTheme.BluePlanet;
        opt.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.Http11);
        opt.HideModels = true;
        opt.ShowSidebar = true;
        opt.CustomCss = @"
            /* Hide Share and Generate SDK buttons */
            button:has-text('Generate SDKs'),
            button:has-text('Share'),
            [data-testid='share-button'],
            [data-testid='generate-sdk-button'],
            button[title*='Share'],
            button[title*='SDK'],
            button[aria-label*='Generate'],
            button[aria-label*='Share'],
            .scalar-card-button,
            .scalar-api-client-button {
                display: none !important;
                visibility: hidden !important;
                opacity: 0 !important;
                pointer-events: none !important;
            }
            [class*='generate'] button,
            [class*='share'] button {
                display: none !important;
            }
            button[class*='sidebar']:has-text('Client'),
            [class*='sidebar'] button:has-text('Client'),
            [class*='sidebar'] [class*='client'],
            nav button[aria-label*='Client'],
            aside button[aria-label*='Client'],
            .sidebar-button,
            button[data-sidebar-trigger] {
                display: none !important;
                visibility: hidden !important;
            }
        ";
    });
}

static void ConfigureHealthCheckEndpoints(WebApplication app)
{
    // Liveness probe - just checks if the app is running
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = "Healthy",
                    timestamp = DateTime.UtcNow,
                    description = "Application is alive"
                }));
        }
    });

    // Readiness probe - checks database and IdentityServer
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                duration = report.TotalDuration,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration,
                    description = e.Value.Description,
                    exception = e.Value.Exception?.Message
                })
            });
            await context.Response.WriteAsync(result);
        }
    });

    // General health endpoint
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            });
            await context.Response.WriteAsync(result);
        }
    });
}
