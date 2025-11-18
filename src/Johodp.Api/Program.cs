using Johodp.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable routing so authentication runs and populates HttpContext.User
app.UseRouting();

// Allow SPA origin to send credentials (cookies) during local development
app.UseCors("AllowSpa");

// Authentication must run before IdentityServer so its endpoints can see the
// authenticated user (cookie) and avoid redirecting to login unnecessarily.
app.UseAuthentication();

// IdentityServer middleware exposes the OIDC endpoints (discovery, authorize, token...)
app.UseIdentityServer();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapDefaultControllerRoute();
});

try
{
    Log.Information("Starting application...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
