using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Johodp.Infrastructure.Persistence.DbContext;
using Xunit;

namespace Johodp.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration tests with in-memory SQLite database.
/// Provides isolated database for each test run with automatic cleanup.
/// Optimized for CI/CD pipelines - no external dependencies required.
/// </summary>
public class JohodpWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"InMemoryTest_{Guid.NewGuid()}";
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Development for testing (enables temporary signing credentials)
        builder.UseEnvironment("Development");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for testing
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databaseName};Mode=Memory;Cache=Shared",
                // Ensure environment is Development
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                // Signal to Program.cs to skip migrations (in-memory DB uses EnsureCreated instead)
                ["Testing:SkipMigrations"] = "true",
                // Disable IdentityServer TokenCleanupHost for tests
                ["IdentityServer:TokenCleanup:EnableTokenCleanup"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext registrations
            services.RemoveAll<DbContextOptions<JohodpDbContext>>();
            services.RemoveAll<JohodpDbContext>();
            services.RemoveAll<DbContextOptions<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>>();
            services.RemoveAll<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>();

            // Add test DbContext with in-memory SQLite for main database
            services.AddDbContext<JohodpDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_databaseName};Mode=Memory;Cache=Shared");
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });
            
            // Add IdentityServer DbContext with in-memory SQLite
            services.AddDbContext<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_databaseName};Mode=Memory;Cache=Shared");
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Create and keep open a SQLite connection for in-memory database
        _connection = new SqliteConnection($"Data Source={_databaseName};Mode=Memory;Cache=Shared");
        await _connection.OpenAsync();
        
        // Create database schema
        using var scope = Services.CreateScope();
        
        // Create main application database
        var johodpDb = scope.ServiceProvider.GetRequiredService<JohodpDbContext>();
        await johodpDb.Database.EnsureCreatedAsync();
        
        // Create IdentityServer databases - they share the same connection string
        var persistedGrantDb = scope.ServiceProvider.GetRequiredService<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>();
        await persistedGrantDb.Database.EnsureCreatedAsync();
        
        // Log successful initialization
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<JohodpWebApplicationFactory>>();
        logger.LogInformation("Test database initialized successfully with SQLite in-memory");
    }

    public new async Task DisposeAsync()
    {
        // Close and dispose database connection
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
        
        await base.DisposeAsync();
    }
}
