# Script d'initialisation hybride : SQL + EF Core Migrations
# 1. Cree le schema dbo et __EFMigrationsHistory via SQL
# 2. Applique les migrations EF Core normalement

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Initialisation de la base de donnees Johodp (Hybride)" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Verification du conteneur PostgreSQL
Write-Host "[0/3] Verification du conteneur PostgreSQL..." -ForegroundColor Yellow
$containerStatus = docker inspect -f '{{.State.Running}}' johodp-postgres 2>$null
if ($containerStatus -ne "true") {
    Write-Host "ERREUR: Le conteneur johodp-postgres n'est pas actif" -ForegroundColor Red
    Write-Host "Lancez 'docker-compose up -d' pour demarrer le conteneur" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Conteneur PostgreSQL actif" -ForegroundColor Green
Write-Host ""

# 1. Creation du schema dbo et __EFMigrationsHistory via SQL
Write-Host "[1/3] Creation du schema dbo et __EFMigrationsHistory via SQL..." -ForegroundColor Yellow
Get-Content init-schema.sql | docker exec -i johodp-postgres psql -U postgres -d johodp
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERREUR: Echec de la creation du schema" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Schema dbo et __EFMigrationsHistory crees" -ForegroundColor Green
Write-Host ""

# 2. Migration JohodpDbContext
Write-Host "[2/3] Application de la migration JohodpDbContext..." -ForegroundColor Yellow
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context JohodpDbContext
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERREUR: Echec de la migration JohodpDbContext" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Migration JohodpDbContext appliquee" -ForegroundColor Green
Write-Host ""

# 3. Migration PersistedGrantDbContext
Write-Host "[3/3] Application de la migration PersistedGrantDbContext..." -ForegroundColor Yellow
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERREUR: Echec de la migration PersistedGrantDbContext" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Migration PersistedGrantDbContext appliquee" -ForegroundColor Green
Write-Host ""

# Verification finale
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "OK Base de donnees initialisee avec succes!" -ForegroundColor Green
Write-Host "   - Schema dbo cree via SQL" -ForegroundColor Green
Write-Host "   - __EFMigrationsHistory cree via SQL" -ForegroundColor Green
Write-Host "   - Migrations EF Core appliquees" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Verification finale..." -ForegroundColor Yellow
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, COUNT(*) as tables FROM pg_tables WHERE schemaname = 'dbo' GROUP BY schemaname;"
Write-Host ""
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, tablename FROM pg_tables WHERE schemaname = 'dbo' ORDER BY tablename;"
