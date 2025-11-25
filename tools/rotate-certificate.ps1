# rotate-certificate.ps1
# Script PowerShell pour rotation des certificats IdentityServer sans interruption

param(
    [string]$KeysDir = "C:\app\keys",
    [string]$VaultPath = "secret/johodp/identityserver",
    [int]$TokenLifetimeHours = 1,
    [switch]$SkipWait
)

$ErrorActionPreference = "Stop"
$GracePeriodHours = $TokenLifetimeHours + 1

Write-Host "🔄 IdentityServer Certificate Rotation" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# 1. Backup du certificat actuel
Write-Host "📦 Step 1: Backing up current certificate..." -ForegroundColor Yellow

try {
    $currentCertPath = vault kv get -field=cert-path "$VaultPath/current"
    $currentCertPassword = vault kv get -field=cert-password "$VaultPath/current"
    Write-Host "   Current cert: $currentCertPath" -ForegroundColor Gray
}
catch {
    Write-Host "   ❌ Failed to retrieve current certificate from Vault" -ForegroundColor Red
    exit 1
}

# 2. Déplacer vers "previous"
Write-Host "📝 Step 2: Moving current certificate to 'previous' slot..." -ForegroundColor Yellow

vault kv put "$VaultPath/previous" `
    cert-path="$currentCertPath" `
    cert-password="$currentCertPassword"

Write-Host "   ✅ Previous certificate saved" -ForegroundColor Green

# 3. Générer nouveau certificat
Write-Host "🔑 Step 3: Generating new certificate..." -ForegroundColor Yellow

$newCertName = "signing-key-$(Get-Date -Format 'yyyyMMdd-HHmmss').pfx"
$newCertPath = Join-Path $KeysDir $newCertName
$newCertPassword = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object {[char]$_})

# Générer avec dotnet dev-certs (plus simple sous Windows)
dotnet dev-certs https -ep $newCertPath -p $newCertPassword --trust

Write-Host "   ✅ New certificate generated: $newCertName" -ForegroundColor Green

# 4. Stocker dans Vault
Write-Host "☁️  Step 4: Uploading new certificate to Vault..." -ForegroundColor Yellow

vault kv put "$VaultPath/current" `
    cert-path="$newCertPath" `
    cert-password="$newCertPassword"

Write-Host "   ✅ New certificate uploaded to Vault" -ForegroundColor Green

# 5. Rolling restart
Write-Host "🔄 Step 5: Rolling restart of pods..." -ForegroundColor Yellow

if (Get-Command kubectl -ErrorAction SilentlyContinue) {
    kubectl rollout restart deployment/johodp-api -n production
    kubectl rollout status deployment/johodp-api -n production --timeout=5m
    Write-Host "   ✅ Pods restarted successfully" -ForegroundColor Green
}
else {
    Write-Host "   ⚠️  kubectl not found - manual restart required" -ForegroundColor Yellow
}

# 6. État après rotation
Write-Host ""
Write-Host "📊 Rotation Status:" -ForegroundColor Cyan
Write-Host "   - NEW certificate (current): Signs new tokens"
Write-Host "   - OLD certificate (previous): Validates existing tokens"
Write-Host "   - Grace period: $GracePeriodHours hours"
Write-Host ""

# 7. Attendre expiration
if (-not $SkipWait) {
    Write-Host "⏳ Step 6: Waiting for old tokens to expire..." -ForegroundColor Yellow
    Write-Host "   Sleeping for $GracePeriodHours hours..."
    Write-Host "   (Press Ctrl+C to skip and complete manually later)"
    
    Start-Sleep -Seconds ($GracePeriodHours * 3600)
    
    # 8. Supprimer l'ancien certificat
    Write-Host "🗑️  Step 7: Removing old certificate..." -ForegroundColor Yellow
    vault kv delete "$VaultPath/previous"
    Remove-Item -Path $currentCertPath -Force -ErrorAction SilentlyContinue
    
    Write-Host ""
    Write-Host "✅ Certificate rotation completed successfully!" -ForegroundColor Green
}
else {
    Write-Host "⏭️  Skipping wait period. Run this later to cleanup:" -ForegroundColor Yellow
    Write-Host "   vault kv delete $VaultPath/previous" -ForegroundColor Gray
    Write-Host "   Remove-Item '$currentCertPath'" -ForegroundColor Gray
}

Write-Host ""
$nextRotation = (Get-Date).AddDays(90).ToString("yyyy-MM-dd")
Write-Host "📝 Next rotation due: $nextRotation" -ForegroundColor Cyan
