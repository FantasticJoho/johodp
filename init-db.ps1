# Script pour initialiser la base de données avec toutes les migrations
# Ce script applique les migrations pour les 2 DbContext:
# 1. JohodpDbContext (12 migrations: users, clients, tenants, roles, permissions + dbo schema)
# 2. PersistedGrantDbContext (2 migrations: IdentityServer operational store + dbo schema)

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Initialisation de la base de données Johodp" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/2] Application des migrations JohodpDbContext..." -ForegroundColor Yellow
dotnet ef database update `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext

if ($LASTEXITCODE -ne 0) {
    Write-Host "" -ForegroundColor Red
    Write-Host "❌ Erreur lors de l'application des migrations JohodpDbContext" -ForegroundColor Red
    exit 1
}

Write-Host "" 
Write-Host "[2/2] Application des migrations PersistedGrantDbContext (IdentityServer)..." -ForegroundColor Yellow
dotnet ef database update `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext

if ($LASTEXITCODE -ne 0) {
    Write-Host "" -ForegroundColor Red
    Write-Host "❌ Erreur lors de l'application des migrations PersistedGrantDbContext" -ForegroundColor Red
    exit 1
}

Write-Host "" 
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "✅ Base de données initialisée avec succès!" -ForegroundColor Green
Write-Host "   - 12 migrations JohodpDbContext appliquées" -ForegroundColor Green
Write-Host "   - 2 migrations PersistedGrantDbContext appliquées" -ForegroundColor Green
Write-Host "   - Total: 14 migrations" -ForegroundColor Green
Write-Host "   - Toutes les tables sont dans le schéma 'dbo'" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
