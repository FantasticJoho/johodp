# Script pour initialiser la base de données avec toutes les migrations
# Ce script applique les migrations pour les 2 DbContext:
# 1. JohodpDbContext (1 migration InitialCreate: users, clients, tenants, custom_configurations + dbo schema)
# 2. PersistedGrantDbContext (IdentityServer operational store + dbo schema)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

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
Write-Host "OK Base de donnees initialisee avec succes!" -ForegroundColor Green
Write-Host "   - Migration JohodpDbContext appliquee (InitialCreate)" -ForegroundColor Green
Write-Host "   - Migrations PersistedGrantDbContext appliquees (IdentityServer)" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green

Write-Host ""
Write-Host "[3/3] Deplacement de __EFMigrationsHistory vers schema dbo..." -ForegroundColor Yellow
docker exec -i johodp-postgres psql -U postgres -d johodp -c 'ALTER TABLE IF EXISTS public.\"__EFMigrationsHistory\" SET SCHEMA dbo;' 2>$null

Write-Host "" 
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "OK Toutes les tables sont dans le schema dbo!" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
