#!/bin/bash
# Script d'initialisation de la base de données via SQL pur (Linux/Mac)
# Cette stratégie garantit que toutes les tables sont créées dans le schéma 'dbo' dès le départ

set -e

echo "====================================================="
echo "Initialisation de la base de donnees Johodp (SQL)"
echo "====================================================="
echo ""

# Vérifier que Docker est démarré
echo "[0/3] Verification du conteneur PostgreSQL..."
if ! docker ps --filter "name=johodp-postgres" --format "{{.Names}}" | grep -q johodp-postgres; then
    echo ""
    echo "ERREUR: Le conteneur johodp-postgres n'est pas demarre"
    echo "Executez: docker-compose up -d"
    exit 1
fi
echo "OK: Conteneur PostgreSQL actif"

echo ""
echo "[1/3] Creation du schema dbo..."
docker exec -i johodp-postgres psql -U postgres -d johodp -c "CREATE SCHEMA IF NOT EXISTS dbo;"

echo ""
echo "[2/3] Application du script migration-johodp.sql..."
docker exec -i johodp-postgres psql -U postgres -d johodp < migration-johodp.sql

echo ""
echo "[3/3] Application du script migration-identityserver.sql..."
docker exec -i johodp-postgres psql -U postgres -d johodp < migration-identityserver.sql

echo ""
echo "====================================================="
echo "OK Base de donnees initialisee avec succes!"
echo "   - Schema dbo cree"
echo "   - Tables JohodpDbContext creees dans dbo"
echo "   - Tables IdentityServer creees dans dbo"
echo "   - __EFMigrationsHistory dans dbo"
echo "====================================================="

echo ""
echo "Verification finale..."
docker exec -i johodp-postgres psql -U postgres -d johodp -c "SELECT schemaname, COUNT(*) as tables FROM pg_tables WHERE schemaname = 'dbo' GROUP BY schemaname;"
