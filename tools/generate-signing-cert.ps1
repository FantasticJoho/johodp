#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Génère un certificat X.509 pour signer les tokens IdentityServer

.DESCRIPTION
    Script d'installation simple pour créer un certificat de signature X.509.
    Utilise dotnet dev-certs par défaut, avec option OpenSSL pour plus de contrôle.

.PARAMETER OutputPath
    Chemin du fichier PFX à créer (défaut: src/Johodp.Api/keys/signing-key.pfx)

.PARAMETER Password
    Mot de passe du certificat (défaut: génération aléatoire)

.PARAMETER Days
    Durée de validité en jours (défaut: 365)

.PARAMETER UseOpenSSL
    Utiliser OpenSSL au lieu de dotnet dev-certs (requiert OpenSSL installé)

.EXAMPLE
    .\generate-signing-cert.ps1
    Génère un certificat avec les paramètres par défaut

.EXAMPLE
    .\generate-signing-cert.ps1 -Password "MonMotDePasse123!" -Days 730
    Génère un certificat valide 2 ans avec mot de passe spécifique

.EXAMPLE
    .\generate-signing-cert.ps1 -UseOpenSSL
    Génère un certificat RSA 4096 bits avec OpenSSL
#>

param(
    [string]$OutputPath = "src/Johodp.Api/keys/signing-key.pfx",
    [string]$Password = "",
    [int]$Days = 365,
    [switch]$UseOpenSSL
)

$ErrorActionPreference = "Stop"

# Couleurs pour l'affichage
function Write-Step { param($Message) Write-Host "🔹 $Message" -ForegroundColor Cyan }
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }

Write-Host ""
Write-Host "🔐 Générateur de Certificat de Signature IdentityServer" -ForegroundColor White
Write-Host "========================================================" -ForegroundColor White
Write-Host ""

# Générer un mot de passe aléatoire si non fourni
if ([string]::IsNullOrEmpty($Password)) {
    $Password = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object { [char]$_ })
    Write-Step "Mot de passe généré automatiquement (32 caractères)"
}

# Créer le dossier de destination
$OutputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $OutputDir)) {
    Write-Step "Création du dossier: $OutputDir"
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Vérifier si le fichier existe déjà
if (Test-Path $OutputPath) {
    Write-Warning "Le fichier $OutputPath existe déjà"
    $response = Read-Host "Voulez-vous l'écraser? (o/N)"
    if ($response -ne "o" -and $response -ne "O") {
        Write-Host "Opération annulée"
        exit 0
    }
    Remove-Item $OutputPath -Force
}

# Méthode OpenSSL
if ($UseOpenSSL) {
    Write-Step "Génération du certificat avec OpenSSL (RSA 4096 bits)..."
    
    # Vérifier qu'OpenSSL est disponible
    try {
        $null = & openssl version
    }
    catch {
        Write-Error "OpenSSL n'est pas installé ou pas dans le PATH"
        Write-Host "Installation: choco install openssl (Windows) ou apt install openssl (Linux)"
        exit 1
    }
    
    # Fichiers temporaires
    $tempKey = [System.IO.Path]::GetTempFileName()
    $tempCert = [System.IO.Path]::GetTempFileName()
    
    try {
        # Générer la clé privée et le certificat
        $subject = "/CN=Johodp IdentityServer/O=Johodp/C=FR"
        & openssl req -x509 -newkey rsa:4096 `
            -keyout $tempKey `
            -out $tempCert `
            -days $Days `
            -nodes `
            -subj $subject 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            throw "Erreur lors de la génération du certificat"
        }
        
        # Convertir en PFX
        & openssl pkcs12 -export `
            -out $OutputPath `
            -inkey $tempKey `
            -in $tempCert `
            -passout "pass:$Password" 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            throw "Erreur lors de la conversion en PFX"
        }
    }
    finally {
        # Nettoyer les fichiers temporaires
        Remove-Item $tempKey -Force -ErrorAction SilentlyContinue
        Remove-Item $tempCert -Force -ErrorAction SilentlyContinue
    }
}
# Méthode dotnet dev-certs
else {
    Write-Step "Génération du certificat avec dotnet dev-certs..."
    
    & dotnet dev-certs https -ep $OutputPath -p $Password --trust 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Erreur lors de la génération du certificat"
        exit 1
    }
}

# Vérifier la création
if (-not (Test-Path $OutputPath)) {
    Write-Error "Le certificat n'a pas été créé"
    exit 1
}

# Configurer les permissions (Windows uniquement)
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Write-Step "Configuration des permissions Windows..."
    icacls $OutputPath /inheritance:r | Out-Null
    icacls $OutputPath /grant:r "$env:USERNAME:(R)" | Out-Null
}

Write-Host ""
Write-Success "Certificat de signature créé avec succès!"
Write-Host ""
Write-Host "📋 Informations:" -ForegroundColor White
Write-Host "   Fichier       : $OutputPath"
Write-Host "   Validité      : $Days jours"
Write-Host "   Algorithme    : $(if ($UseOpenSSL) { 'RSA 4096' } else { 'RSA 2048' })"
Write-Host ""
Write-Host "🔑 Mot de passe:" -ForegroundColor Yellow
Write-Host "   $Password"
Write-Host ""
Write-Warning "Stockez ce mot de passe de manière sécurisée!"
Write-Host ""
Write-Host "📖 Prochaines étapes:" -ForegroundColor White
Write-Host ""
Write-Host "1️⃣  Configurer appsettings.Production.json:"
Write-Host '   {' -ForegroundColor Gray
Write-Host '     "IdentityServer": {' -ForegroundColor Gray
Write-Host '       "SigningMethod": "Certificate",' -ForegroundColor Gray
Write-Host "       `"SigningKeyPath`": `"keys/signing-key.pfx`"," -ForegroundColor Gray
Write-Host "       `"SigningKeyPassword`": `"$Password`"" -ForegroundColor Gray
Write-Host '     }' -ForegroundColor Gray
Write-Host '   }' -ForegroundColor Gray
Write-Host ""
Write-Host "2️⃣  Ou utiliser une variable d'environnement (recommandé):"
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Write-Host "   `$env:IDENTITYSERVER_SIGNING_PASSWORD=`"$Password`"" -ForegroundColor Gray
} else {
    Write-Host "   export IDENTITYSERVER_SIGNING_PASSWORD=`"$Password`"" -ForegroundColor Gray
}
Write-Host ""
Write-Host "3️⃣  Vérifier que le certificat n'est PAS committé:"
Write-Host "   git status | grep signing-key.pfx" -ForegroundColor Gray
Write-Host "   (Doit être dans .gitignore)" -ForegroundColor Gray
Write-Host ""
Write-Host "4️⃣  Tester l'application:"
Write-Host "   dotnet run --project src/Johodp.Api --launch-profile https" -ForegroundColor Gray
Write-Host ""
Write-Host "📚 Pour la rotation du certificat, voir:" -ForegroundColor White
Write-Host "   CERTIFICATE_ROTATION.md"
Write-Host ""
