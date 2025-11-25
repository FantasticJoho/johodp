using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Johodp.Infrastructure.Persistence.DbContext;

namespace Johodp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MigrationsController : ControllerBase
{
    private readonly JohodpDbContext _johodpDb;
    private readonly Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext _persistedGrantDb;
    private readonly ILogger<MigrationsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public MigrationsController(
        JohodpDbContext johodpDb,
        Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext persistedGrantDb,
        ILogger<MigrationsController> logger,
        IWebHostEnvironment environment)
    {
        _johodpDb = johodpDb;
        _persistedGrantDb = persistedGrantDb;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Applique toutes les migrations (UP) pour JohodpDbContext et PersistedGrantDbContext
    /// </summary>
    /// <remarks>
    /// ⚠️ ATTENTION : Cette opération est irréversible. 
    /// Utilisez uniquement en développement ou avec une sauvegarde de la base de données.
    /// </remarks>
    [HttpPost("up")]
    public async Task<IActionResult> MigrateUp()
    {
        // Sécurité : désactiver en production
        if (_environment.IsProduction())
        {
            _logger.LogWarning("Migration endpoint called in production - rejected");
            return StatusCode(403, new
            {
                error = "Forbidden",
                message = "Migration endpoints are disabled in production. Use init-db.ps1 script instead."
            });
        }

        try
        {
            _logger.LogInformation("Starting database migrations (UP)...");

            // Appliquer les migrations JohodpDbContext
            _logger.LogInformation("Applying JohodpDbContext migrations...");
            await _johodpDb.Database.MigrateAsync();
            var johodpMigrations = await _johodpDb.Database.GetAppliedMigrationsAsync();
            _logger.LogInformation("✅ JohodpDbContext migrations applied. Total: {Count}", johodpMigrations.Count());

            // Appliquer les migrations PersistedGrantDbContext
            _logger.LogInformation("Applying PersistedGrantDbContext migrations...");
            await _persistedGrantDb.Database.MigrateAsync();
            var persistedGrantMigrations = await _persistedGrantDb.Database.GetAppliedMigrationsAsync();
            _logger.LogInformation("✅ PersistedGrantDbContext migrations applied. Total: {Count}", persistedGrantMigrations.Count());

            var result = new
            {
                success = true,
                message = "All migrations applied successfully",
                johodpDbContext = new
                {
                    appliedMigrations = johodpMigrations.Count(),
                    migrations = johodpMigrations
                },
                persistedGrantDbContext = new
                {
                    appliedMigrations = persistedGrantMigrations.Count(),
                    migrations = persistedGrantMigrations
                }
            };
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Migration UP failed");
            return StatusCode(500, new
            {
                error = "Migration failed",
                message = ex.Message,
                stackTrace = _environment.IsDevelopment() ? ex.StackTrace : null
            });
        }
    }

    /// <summary>
    /// Rollback toutes les migrations (DOWN) - Supprime toutes les tables
    /// </summary>
    /// <remarks>
    /// ⚠️ DANGER : Cette opération SUPPRIME TOUTES LES DONNÉES.
    /// Utilisez uniquement en développement local avec des données de test.
    /// </remarks>
    [HttpPost("down")]
    public async Task<IActionResult> MigrateDown()
    {
        // Sécurité : désactiver en production
        if (_environment.IsProduction())
        {
            _logger.LogWarning("Migration DOWN endpoint called in production - rejected");
            return StatusCode(403, new
            {
                error = "Forbidden",
                message = "Migration DOWN is disabled in production for safety."
            });
        }

        try
        {
            _logger.LogWarning("Starting database migrations DOWN (DROPPING ALL TABLES)...");

            // Supprimer la base PersistedGrantDbContext
            _logger.LogWarning("Dropping PersistedGrantDbContext database...");
            await _persistedGrantDb.Database.EnsureDeletedAsync();
            _logger.LogWarning("✅ PersistedGrantDbContext database dropped");

            // Supprimer la base JohodpDbContext
            _logger.LogWarning("Dropping JohodpDbContext database...");
            await _johodpDb.Database.EnsureDeletedAsync();
            _logger.LogWarning("✅ JohodpDbContext database dropped");

            return Ok(new
            {
                success = true,
                message = "All databases dropped successfully. Run POST /api/migrations/up to recreate."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Migration DOWN failed");
            return StatusCode(500, new
            {
                error = "Migration DOWN failed",
                message = ex.Message,
                stackTrace = _environment.IsDevelopment() ? ex.StackTrace : null
            });
        }
    }

    /// <summary>
    /// Obtenir l'état actuel des migrations
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetMigrationStatus()
    {
        try
        {
            // JohodpDbContext
            var johodpApplied = await _johodpDb.Database.GetAppliedMigrationsAsync();
            var johodpPending = await _johodpDb.Database.GetPendingMigrationsAsync();
            var johodpCanConnect = await _johodpDb.Database.CanConnectAsync();

            // PersistedGrantDbContext
            var persistedGrantApplied = await _persistedGrantDb.Database.GetAppliedMigrationsAsync();
            var persistedGrantPending = await _persistedGrantDb.Database.GetPendingMigrationsAsync();
            var persistedGrantCanConnect = await _persistedGrantDb.Database.CanConnectAsync();

            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                environment = _environment.EnvironmentName,
                johodpDbContext = new
                {
                    canConnect = johodpCanConnect,
                    appliedMigrations = johodpApplied.Count(),
                    pendingMigrations = johodpPending.Count(),
                    applied = johodpApplied,
                    pending = johodpPending
                },
                persistedGrantDbContext = new
                {
                    canConnect = persistedGrantCanConnect,
                    appliedMigrations = persistedGrantApplied.Count(),
                    pendingMigrations = persistedGrantPending.Count(),
                    applied = persistedGrantApplied,
                    pending = persistedGrantPending
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get migration status");
            return StatusCode(500, new
            {
                error = "Failed to get migration status",
                message = ex.Message
            });
        }
    }
}
