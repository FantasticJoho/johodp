# Script pour initialiser les migrations Entity Framework sur Windows

Write-Host "Application des migrations..." -ForegroundColor Green
dotnet ef database update `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api

Write-Host "✅ Base de données initialisée avec succès!" -ForegroundColor Green
