# Script d'initialisation de la base de données via SQL pur
# Cette stratégie garantit que toutes les tables sont créées dans le schéma 'dbo' dès le départ

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Initialisation de la base de donnees Johodp (SQL)" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Vérifier que Docker est démarré
Write-Host "[0/3] Verification du conteneur PostgreSQL..." -ForegroundColor Yellow
$containerRunning = docker ps --filter "name=johodp-postgres" --format "{{.Names}}"
if (-not $containerRunning) {
    Write-Host "" -ForegroundColor Red
    Write-Host "ERREUR: Le conteneur johodp-postgres n'est pas demarre" -ForegroundColor Red
    Write-Host "Executez: docker-compose up -d" -ForegroundColor Yellow
    exit 1
}
Write-Host "OK: Conteneur PostgreSQL actif" -ForegroundColor Green

Write-Host ""
Write-Host "[1/3] Creation du schema dbo..." -ForegroundColor Yellow
docker exec -i johodp-postgres psql -U postgres -d johodp -c "CREATE SCHEMA IF NOT EXISTS dbo;"
if ($LASTEXITCODE -ne 0) {
    Write-Host "" -ForegroundColor Red
    Write-Host "ERREUR lors de la creation du schema dbo" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[2/3] Application du script migration-johodp.sql..." -ForegroundColor Yellow
Get-Content migration-johodp.sql | docker exec -i johodp-postgres psql -U postgres -d johodp
if ($LASTEXITCODE -ne 0) {
    Write-Host "" -ForegroundColor Red
    Write-Host "ERREUR lors de l'application de migration-johodp.sql" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[3/3] Application du script migration-identityserver.sql..." -ForegroundColor Yellow
Get-Content migration-identityserver.sql | docker exec -i johodp-postgres psql -U postgres -d johodp
if ($LASTEXITCODE -ne 0) {
    Write-Host "" -ForegroundColor Red
    Write-Host "ERREUR lors de l'application de migration-identityserver.sql" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "OK Base de donnees initialisee avec succes!" -ForegroundColor Green
Write-Host "   - Schema dbo cree" -ForegroundColor Green
Write-Host "   - Tables JohodpDbContext creees dans dbo" -ForegroundColor Green
Write-Host "   - Tables IdentityServer creees dans dbo" -ForegroundColor Green
Write-Host "   - __EFMigrationsHistory dans dbo" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green

Write-Host ""
Write-Host "Verification finale..." -ForegroundColor Yellow
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, COUNT(*) as tables FROM pg_tables WHERE schemaname = 'dbo' GROUP BY schemaname;"
