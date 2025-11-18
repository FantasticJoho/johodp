using Johodp.Api.Extensions;
using Johodp.Api.Middleware;
using Serilog;
using Scalar.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog with enrichment for production readiness
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Johodp")
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddAuthentication(options =>
    {
        // Ensure the Identity cookie is used as the default authentication/sign-in scheme
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
        // Development-friendly cookie settings so the cookie is visible on localhost HTTP
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

builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Add request logging middleware for production monitoring
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserEmail", httpContext.User.FindFirst("email")?.Value);
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
        }
    };
});

// Global exception handler for production-ready error handling
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Ensure HTTPS redirection
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
 app.UseSwagger(opt => opt.RouteTemplate = "openapi/{documentName}.json");
   app.MapScalarApiReference(
    opt => {
        opt.Title = "WebApi with Scalar Example";
        opt.Theme = ScalarTheme.BluePlanet;
        opt.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.Http11);
        opt.HideModels = true;
        opt.ShowSidebar = true;
        opt.CustomCss = @"
            /* Hide Share and Generate SDK buttons - even on localhost */
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
            /* Additional selector to target sidebar items */
            [class*='generate'] button,
            [class*='share'] button {
                display: none !important;
            }
            /* Hide OpenAPI Client sidebar button */
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
    }
);
}

app.UseRouting();

// Allow SPA origin to send credentials (cookies) during local development
app.UseCors("AllowSpa");

// Authentication must run before IdentityServer so its endpoints can see the
// authenticated user (cookie) and avoid redirecting to login unnecessarily.
app.UseAuthentication();

// IdentityServer middleware exposes the OIDC endpoints (discovery, authorize, token...)
app.UseIdentityServer();

app.UseAuthorization();


// Top-level route registrations (recommended)
app.MapControllers();
app.MapDefaultControllerRoute();



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
