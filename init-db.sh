#!/bin/bash

# Script pour initialiser les migrations Entity Framework

echo "Application des migrations..."
dotnet ef database update \
  --project src/Johodp.Infrastructure \
  --startup-project src/Johodp.Api

echo "✅ Base de données initialisée avec succès!"
