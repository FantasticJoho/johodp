#!/bin/bash
# Script d'initialisation hybride : SQL + EF Core Migrations
# 1. Cree le schema dbo et __EFMigrationsHistory via SQL
# 2. Applique les migrations EF Core normalement

set -e

echo "====================================================="
echo "Initialisation de la base de donnees Johodp (Hybride)"
echo "====================================================="
echo ""

# Verification du conteneur PostgreSQL
echo "[0/3] Verification du conteneur PostgreSQL..."
if ! docker inspect -f '{{.State.Running}}' johodp-postgres 2>/dev/null | grep -q true; then
    echo "ERREUR: Le conteneur johodp-postgres n'est pas actif"
    echo "Lancez 'docker-compose up -d' pour demarrer le conteneur"
    exit 1
fi
echo "OK: Conteneur PostgreSQL actif"
echo ""

# 1. Creation du schema dbo et __EFMigrationsHistory via SQL
echo "[1/3] Creation du schema dbo et __EFMigrationsHistory via SQL..."
cat init-schema.sql | docker exec -i johodp-postgres psql -U postgres -d johodp
echo "OK: Schema dbo et __EFMigrationsHistory crees"
echo ""

# 2. Migration JohodpDbContext
echo "[2/3] Application de la migration JohodpDbContext..."
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context JohodpDbContext
echo "OK: Migration JohodpDbContext appliquee"
echo ""

# 3. Migration PersistedGrantDbContext
echo "[3/3] Application de la migration PersistedGrantDbContext..."
dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext
echo "OK: Migration PersistedGrantDbContext appliquee"
echo ""

# Verification finale
echo "====================================================="
echo "OK Base de donnees initialisee avec succes!"
echo "   - Schema dbo cree via SQL"
echo "   - __EFMigrationsHistory cree via SQL"
echo "   - Migrations EF Core appliquees"
echo "====================================================="
echo ""

echo "Verification finale..."
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, COUNT(*) as tables FROM pg_tables WHERE schemaname = 'dbo' GROUP BY schemaname;"
echo ""
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, tablename FROM pg_tables WHERE schemaname = 'dbo' ORDER BY tablename;"
