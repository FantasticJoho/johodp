<#
.SYNOPSIS
Initialise la base de données Johodp (hybride : SQL + EF Core Migrations).

.DESCRIPTION
Ce script :
 - crée le schéma `dbo` et la table `__EFMigrationsHistory` via le fichier SQL `init-schema.sql`,
 - puis applique les migrations EF Core pour `JohodpDbContext` et le PersistedGrantDbContext.
Utilisez-le pour initialiser une base de développement ou automatiser l'initialisation dans CI.

.PARAMETER ContainerName
Nom du conteneur Docker qui exécute PostgreSQL. Par défaut : 'johodp-postgres'.

.PARAMETER DbName
Nom de la base PostgreSQL à initialiser. Par défaut : 'johodp'.

.PARAMETER DbUser
Utilisateur PostgreSQL utilisé pour la connexion dans le conteneur. Par défaut : 'postgres'.

.PARAMETER DbPassword
Mot de passe pour $DbUser. Pour la sécurité, préférez fournir ce secret via des variables d'environnement ou un store de secrets.

.PARAMETER Help
Afficher l'aide complète de ce script (équivalent de Get-Help) et quitter.

.PARAMETER WhatIf
Mode simulation : si indiqué, le script n'exécutera pas les commandes destructrices et affichera ce qu'il ferait.

.EXAMPLE
# Utiliser les valeurs par défaut
.\init-db-hybrid.ps1

.EXAMPLE
# Fournir un nom de base et des identifiants personnalisés
.\init-db-hybrid.ps1 -DbName mydb -DbUser myuser -DbPassword s3cret

.NOTES
- Le mot de passe est passé à `psql` via `PGPASSWORD` dans le conteneur pour éviter les invites interactives.
- Ce script est destiné aux environnements de développement et de test. Ne l'exécutez pas en production sans revue.
#>

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# -----------------------------------------------------------------------------
# Script parameters (can be overridden at invocation)
# -----------------------------------------------------------------------------
# $ContainerName : Name of the Docker container that runs PostgreSQL.
#                  Default: 'johodp-postgres'
# $DbName        : Name of the PostgreSQL database to initialize.
#                  Default: 'johodp'
# $DbUser        : Postgres user name to connect with inside the container.
#                  Default: 'postgres'
# $DbPassword    : Password for $DbUser. For security, prefer passing this via
#                  environment variables or your CI secret store instead of
#                  committing secrets to source control. Default: 'postgres'
#
# Usage examples:
#   .\init-db-hybrid.ps1                         # use defaults
#   .\init-db-hybrid.ps1 -DbName mydb -DbUser myuser -DbPassword s3cret
#
# Note: values are injected into the psql calls inside the container using
#       PGPASSWORD to avoid interactive prompts. Keep secrets safe.
# -----------------------------------------------------------------------------
param(
    [string]$ContainerName = 'johodp-postgres',
    [string]$DbName = 'johodp',
    [string]$DbUser = 'postgres',
    [string]$DbPassword = 'postgres',
    [switch]$Help,
    [switch]$WhatIf
)

if ($Help) {
    Get-Help -Full -ErrorAction SilentlyContinue $MyInvocation.MyCommand.Path
    return
}

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Initialisation de la base de donnees Johodp (Hybride)" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Verification du conteneur PostgreSQL
Write-Host "[0/3] Verification du conteneur PostgreSQL..." -ForegroundColor Yellow
if ($WhatIf) {
    Write-Host "WhatIf: would check if container '$ContainerName' is running"
    $containerStatus = "true"
} else {
    $containerStatus = docker inspect -f '{{.State.Running}}' $ContainerName 2>$null
}
if ($containerStatus -ne "true") {
    Write-Host "ERREUR: Le conteneur $ContainerName n'est pas actif" -ForegroundColor Red
    Write-Host "Lancez 'docker-compose up -d' pour demarrer le conteneur" -ForegroundColor Red
    exit 1
}
Write-Host "OK: Conteneur PostgreSQL actif" -ForegroundColor Green
Write-Host ""

# 1. Creation du schema dbo et __EFMigrationsHistory via SQL
Write-Host "[1/3] Creation du schema dbo et __EFMigrationsHistory via SQL..." -ForegroundColor Yellow
if ($WhatIf) {
    Write-Host "WhatIf: would execute init-schema.sql in container '$ContainerName' against database '$DbName' as user '$DbUser'"
} else {
    Get-Content init-schema.sql | docker exec -i $ContainerName bash -lc "PGPASSWORD='$DbPassword' psql -U $DbUser -d $DbName -f -"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERREUR: Echec de la creation du schema" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: Schema dbo et __EFMigrationsHistory crees" -ForegroundColor Green
}
Write-Host ""

# 2. Migration JohodpDbContext
Write-Host "[2/3] Application de la migration JohodpDbContext..." -ForegroundColor Yellow
if ($WhatIf) {
    Write-Host "WhatIf: would run 'dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context JohodpDbContext'"
} else {
    dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context JohodpDbContext
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERREUR: Echec de la migration JohodpDbContext" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: Migration JohodpDbContext appliquee" -ForegroundColor Green
}
Write-Host ""

# 3. Migration PersistedGrantDbContext
Write-Host "[3/3] Application de la migration PersistedGrantDbContext..." -ForegroundColor Yellow
if ($WhatIf) {
    Write-Host "WhatIf: would run 'dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext'"
} else {
    dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERREUR: Echec de la migration PersistedGrantDbContext" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: Migration PersistedGrantDbContext appliquee" -ForegroundColor Green
}
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
if ($WhatIf) {
    Write-Host "WhatIf: would run verification queries against database '$DbName' in container '$ContainerName'"
} else {
    docker exec -i $ContainerName bash -lc "PGPASSWORD='$DbPassword' psql -U $DbUser -d $DbName -c \"SELECT schemaname, COUNT(*) as tables FROM pg_tables WHERE schemaname = 'dbo' GROUP BY schemaname;\""
    Write-Host ""
    docker exec -i $ContainerName bash -lc "PGPASSWORD='$DbPassword' psql -U $DbUser -d $DbName -c \"SELECT schemaname, tablename FROM pg_tables WHERE schemaname = 'dbo' ORDER BY tablename;\""
}
