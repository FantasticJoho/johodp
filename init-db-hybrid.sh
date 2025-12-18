#!/bin/bash
# Script d'initialisation hybride : SQL + EF Core Migrations
# 1. Cree le schema dbo et __EFMigrationsHistory via SQL
# 2. Applique les migrations EF Core normalement

set -e

# -----------------------------------------------------------------------------
# Defaults (can be overridden by environment variables or CLI args)
#   CONTAINER_NAME : Docker container name running Postgres (default: johodp-postgres)
#   DB_NAME        : Database name to initialize (default: johodp)
#   DB_USER        : Postgres user (default: postgres)
#   DB_PASSWORD    : Password for DB_USER (default: postgres) - prefer using env var
#
# Usage examples:
#   ./init-db-hybrid.sh                         # use defaults
#   ./init-db-hybrid.sh --db mydb --user myuser --password s3cret
#   ./init-db-hybrid.sh --what-if               # simulation only
# -----------------------------------------------------------------------------

CONTAINER_NAME="${CONTAINER_NAME:-johodp-postgres}"
DB_NAME="${DB_NAME:-johodp}"
DB_USER="${DB_USER:-postgres}"
DB_PASSWORD="${DB_PASSWORD:-postgres}"
WHAT_IF=false

print_usage() {
  cat <<EOF
Usage: $0 [--container name] [--db name] [--user user] [--password pass] [--what-if] [--help]

Options:
  --container NAME   Docker container name running Postgres (default: ${CONTAINER_NAME})
  --db NAME          Database name to initialize (default: ${DB_NAME})
  --user USER        Postgres user (default: ${DB_USER})
  --password PASS    Password for the DB user (default: from env or 'postgres')
  --what-if          Dry-run mode: do not execute destructive commands
  --help             Show this help and exit

Examples:
  $0 --db mydb --user myuser --password s3cret
  $0 --what-if

Note: For security, prefer setting DB_PASSWORD via environment variable rather than CLI.
EOF
}

# Parse CLI args
while [[ $# -gt 0 ]]; do
  case "$1" in
    --container) CONTAINER_NAME="$2"; shift 2 ;;
    --db) DB_NAME="$2"; shift 2 ;;
    --user) DB_USER="$2"; shift 2 ;;
    --password) DB_PASSWORD="$2"; shift 2 ;;
    --what-if) WHAT_IF=true; shift 1 ;;
    -h|--help) print_usage; exit 0 ;;
    *) echo "Unknown argument: $1"; print_usage; exit 1 ;;
  esac
done

echo "====================================================="
echo "Initialisation de la base de donnees Johodp (Hybride)"
echo "====================================================="
echo ""

# Verification du conteneur PostgreSQL
echo "[0/3] Verification du conteneur PostgreSQL..."
if [ "$WHAT_IF" = true ]; then
  echo "WhatIf: would check if container '$CONTAINER_NAME' is running"
else
  if ! docker inspect -f '{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null | grep -q true; then
      echo "ERREUR: Le conteneur $CONTAINER_NAME n'est pas actif"
      echo "Lancez 'docker-compose up -d' pour demarrer le conteneur"
      exit 1
  fi
  echo "OK: Conteneur PostgreSQL actif"
fi

echo ""

# 1. Creation du schema dbo et __EFMigrationsHistory via SQL
echo "[1/3] Creation du schema dbo et __EFMigrationsHistory via SQL..."
if [ "$WHAT_IF" = true ]; then
  echo "WhatIf: would execute init-schema.sql in container '$CONTAINER_NAME' against database '$DB_NAME' as user '$DB_USER'"
else
  cat init-schema.sql | docker exec -i "$CONTAINER_NAME" bash -lc "PGPASSWORD='$DB_PASSWORD' psql -U $DB_USER -d $DB_NAME -f -"
  echo "OK: Schema dbo et __EFMigrationsHistory crees"
fi

echo ""

# 2. Migration JohodpDbContext
echo "[2/3] Application de la migration JohodpDbContext..."
if [ "$WHAT_IF" = true ]; then
  echo "WhatIf: would run 'dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context JohodpDbContext'"
else
  dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context JohodpDbContext
  echo "OK: Migration JohodpDbContext appliquee"
fi

echo ""

# 3. Migration PersistedGrantDbContext
echo "[3/3] Application de la migration PersistedGrantDbContext..."
if [ "$WHAT_IF" = true ]; then
  echo "WhatIf: would run 'dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext'"
else
  dotnet ef database update --project src/Johodp.Infrastructure --startup-project src/Johodp.Api --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext
  echo "OK: Migration PersistedGrantDbContext appliquee"
fi

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
if [ "$WHAT_IF" = true ]; then
  echo "WhatIf: would run verification queries against database '$DB_NAME' in container '$CONTAINER_NAME'"
else
  docker exec -i "$CONTAINER_NAME" bash -lc "PGPASSWORD='$DB_PASSWORD' psql -U $DB_USER -d $DB_NAME -c \"SELECT schemaname, COUNT(*) as tables FROM pg_tables WHERE schemaname = 'dbo' GROUP BY schemaname;\""
  echo ""
  docker exec -i "$CONTAINER_NAME" bash -lc "PGPASSWORD='$DB_PASSWORD' psql -U $DB_USER -d $DB_NAME -c \"SELECT schemaname, tablename FROM pg_tables WHERE schemaname = 'dbo' ORDER BY tablename;\""
fi
