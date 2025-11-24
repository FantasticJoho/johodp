#!/bin/bash

# Script pour initialiser la base de données avec toutes les migrations
# Ce script applique les migrations pour les 2 DbContext:
# 1. JohodpDbContext (12 migrations: users, clients, tenants, roles, permissions + dbo schema)
# 2. PersistedGrantDbContext (2 migrations: IdentityServer operational store + dbo schema)

echo "====================================================="
echo "Initialisation de la base de données Johodp"
echo "====================================================="
echo ""

echo "[1/2] Application des migrations JohodpDbContext..."
dotnet ef database update \
  --project src/Johodp.Infrastructure \
  --startup-project src/Johodp.Api \
  --context JohodpDbContext

if [ $? -ne 0 ]; then
    echo ""
    echo "❌ Erreur lors de l'application des migrations JohodpDbContext"
    exit 1
fi

echo ""
echo "[2/2] Application des migrations PersistedGrantDbContext (IdentityServer)..."
dotnet ef database update \
  --project src/Johodp.Infrastructure \
  --startup-project src/Johodp.Api \
  --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext

if [ $? -ne 0 ]; then
    echo ""
    echo "❌ Erreur lors de l'application des migrations PersistedGrantDbContext"
    exit 1
fi

echo ""
echo "====================================================="
echo "✅ Base de données initialisée avec succès!"
echo "   - 12 migrations JohodpDbContext appliquées"
echo "   - 2 migrations PersistedGrantDbContext appliquées"
echo "   - Total: 14 migrations"
echo "   - Toutes les tables sont dans le schéma 'dbo'"
echo "====================================================="
