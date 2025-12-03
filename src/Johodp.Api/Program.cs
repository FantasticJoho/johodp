using Johodp.Api.Extensions;
using Johodp.Api.Middleware;
using Serilog;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Logging
ConfigureLogging(builder);

// Authentication
ConfigureAuthentication(builder.Services);

// MVC & API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new TenantIdJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ClientIdJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new CustomConfigurationIdJsonConverter());
    });
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health Checks
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "db", "ready" })
    .AddCheck<Johodp.Api.HealthChecks.IdentityServerHealthCheck>("identityserver", tags: new[] { "identityserver", "ready" });

// Infrastructure Services
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);


var app = builder.Build();

// Static Files & Defaults
app.UseStaticFiles();
app.UseDefaultFiles();

// Database Migrations (Development)
if (app.Environment.IsDevelopment())
    ApplyDatabaseMigrations(app);

// Middleware Pipeline
ConfigureMiddlewarePipeline(app);

// Swagger UI (Development)
if (app.Environment.IsDevelopment())
    ConfigureSwaggerUI(app);

// Routing & CORS
app.UseRouting();
app.UseCors("AllowSpa");

// Authentication & Authorization
app.UseAuthentication();
app.UseIdentityServer();
app.UseAuthorization();

// Endpoints
ConfigureHealthCheckEndpoints(app);
app.MapControllers();
app.MapDefaultControllerRoute();

// Startup
try
{
    Log.Information("Starting Johodp Identity Provider - {Environment}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("Shutting down Johodp Identity Provider");
    await Log.CloseAndFlushAsync();
}


// Configuration Helpers

static void ConfigureLogging(WebApplicationBuilder builder)
{
    // Bootstrap logger
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Johodp")
        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    builder.Services.AddHttpContextAccessor();
    
    // Full logger with enrichment
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .MinimumLevel.Information()
           .Enrich.FromLogContext()
           .Enrich.WithProperty("Application", "Johodp")
           .Enrich.With(new Johodp.Api.Logging.TenantClientEnricher(services.GetRequiredService<IHttpContextAccessor>()))
           .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {TenantId} {ClientId} {Message:lj}{NewLine}{Exception}");
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
    });
}


static void ApplyDatabaseMigrations(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    // Skip migrations in test environment (uses EnsureCreated instead)
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Testing:SkipMigrations"))
    {
        logger.LogInformation("⚠️ Skipping migrations (test environment with in-memory database)");
        return;
    }
    
    try
    {
        var johodpDb = scope.ServiceProvider.GetRequiredService<Johodp.Infrastructure.Persistence.DbContext.JohodpDbContext>();
        logger.LogInformation("Applying JohodpDbContext migrations...");
        johodpDb.Database.Migrate();
        
        var persistedGrantDb = scope.ServiceProvider.GetRequiredService<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>();
        logger.LogInformation("Applying PersistedGrantDbContext migrations...");
        persistedGrantDb.Database.Migrate();
        
        logger.LogInformation("✅ All database migrations applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Database migration failed");
        throw;
    }
}

static void ConfigureMiddlewarePipeline(WebApplication app)
{
    app.UseRequestLogging();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} {StatusCode} in {Elapsed:0.0000}ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                diagnosticContext.Set("UserEmail", httpContext.User.FindFirst("email")?.Value);
                diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
            }
        };
    });

    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
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
            button:has-text('Generate SDKs'), button:has-text('Share'),
            [data-testid='share-button'], [data-testid='generate-sdk-button'],
            button[title*='Share'], button[title*='SDK'],
            .scalar-card-button, .scalar-api-client-button,
            button[data-sidebar-trigger] {
                display: none !important;
            }";
    });
}

static void ConfigureHealthCheckEndpoints(WebApplication app)
{
    var jsonOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                duration = report.TotalDuration,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration,
                    exception = e.Value.Exception?.Message
                })
            }));
        }
    };

    // Liveness: app is running
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = async (context, _) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow
            }));
        }
    });

    // Readiness: dependencies OK
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = jsonOptions.ResponseWriter
    });

    // General health
    app.MapHealthChecks("/health", jsonOptions);
}

// Make Program class accessible to tests
public partial class Program { }