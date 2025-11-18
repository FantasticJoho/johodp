#!/bin/bash

# Script pour initialiser les migrations Entity Framework

echo "Création de la première migration..."
dotnet ef migrations add InitialCreate \
  --project src/Johodp.Infrastructure \
  --startup-project src/Johodp.Api \
  --output-dir Persistence/Migrations

echo "Application des migrations..."
dotnet ef database update \
  --project src/Johodp.Infrastructure \
  --startup-project src/Johodp.Api

echo "✅ Base de données initialisée avec succès!"
