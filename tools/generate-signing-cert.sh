#!/bin/bash
#
# Génère un certificat X.509 pour signer les tokens IdentityServer
#
# Usage:
#   ./generate-signing-cert.sh [options]
#
# Options:
#   -o, --output PATH    Chemin du fichier PFX (défaut: src/Johodp.Api/keys/signing-key.pfx)
#   -p, --password PWD   Mot de passe du certificat (défaut: génération aléatoire)
#   -d, --days DAYS      Durée de validité en jours (défaut: 365)
#   --openssl            Utiliser OpenSSL au lieu de dotnet dev-certs
#   -h, --help           Afficher cette aide

set -euo pipefail

# Couleurs
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m' # No Color

# Valeurs par défaut
OUTPUT_PATH="src/Johodp.Api/keys/signing-key.pfx"
PASSWORD=""
DAYS=365
USE_OPENSSL=false

# Fonctions d'affichage
step() { echo -e "${CYAN}🔹 $1${NC}"; }
success() { echo -e "${GREEN}✅ $1${NC}"; }
warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
error() { echo -e "${RED}❌ $1${NC}"; exit 1; }

# Aide
show_help() {
    cat << EOF
🔐 Générateur de Certificat de Signature IdentityServer

Usage: $0 [options]

Options:
  -o, --output PATH    Chemin du fichier PFX (défaut: src/Johodp.Api/keys/signing-key.pfx)
  -p, --password PWD   Mot de passe du certificat (défaut: génération aléatoire)
  -d, --days DAYS      Durée de validité en jours (défaut: 365)
  --openssl            Utiliser OpenSSL au lieu de dotnet dev-certs
  -h, --help           Afficher cette aide

Exemples:
  $0
  $0 -p "MonMotDePasse123!" -d 730
  $0 --openssl

EOF
    exit 0
}

# Parser les arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -o|--output)
            OUTPUT_PATH="$2"
            shift 2
            ;;
        -p|--password)
            PASSWORD="$2"
            shift 2
            ;;
        -d|--days)
            DAYS="$2"
            shift 2
            ;;
        --openssl)
            USE_OPENSSL=true
            shift
            ;;
        -h|--help)
            show_help
            ;;
        *)
            error "Option inconnue: $1"
            ;;
    esac
done

echo ""
echo "🔐 Générateur de Certificat de Signature IdentityServer"
echo "========================================================"
echo ""

# Générer un mot de passe aléatoire si non fourni
if [ -z "$PASSWORD" ]; then
    PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
    step "Mot de passe généré automatiquement (32 caractères)"
fi

# Créer le dossier de destination
OUTPUT_DIR=$(dirname "$OUTPUT_PATH")
if [ ! -d "$OUTPUT_DIR" ]; then
    step "Création du dossier: $OUTPUT_DIR"
    mkdir -p "$OUTPUT_DIR"
fi

# Vérifier si le fichier existe déjà
if [ -f "$OUTPUT_PATH" ]; then
    warning "Le fichier $OUTPUT_PATH existe déjà"
    read -p "Voulez-vous l'écraser? (o/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Oo]$ ]]; then
        echo "Opération annulée"
        exit 0
    fi
    rm -f "$OUTPUT_PATH"
fi

# Méthode OpenSSL
if [ "$USE_OPENSSL" = true ]; then
    step "Génération du certificat avec OpenSSL (RSA 4096 bits)..."
    
    # Vérifier qu'OpenSSL est disponible
    if ! command -v openssl &> /dev/null; then
        error "OpenSSL n'est pas installé. Installation: apt install openssl"
    fi
    
    # Fichiers temporaires
    TEMP_KEY=$(mktemp)
    TEMP_CERT=$(mktemp)
    
    # Générer la clé privée et le certificat
    openssl req -x509 -newkey rsa:4096 \
        -keyout "$TEMP_KEY" \
        -out "$TEMP_CERT" \
        -days "$DAYS" \
        -nodes \
        -subj "/CN=Johodp IdentityServer/O=Johodp/C=FR" 2>/dev/null || error "Erreur lors de la génération du certificat"
    
    # Convertir en PFX
    openssl pkcs12 -export \
        -out "$OUTPUT_PATH" \
        -inkey "$TEMP_KEY" \
        -in "$TEMP_CERT" \
        -passout "pass:$PASSWORD" 2>/dev/null || error "Erreur lors de la conversion en PFX"
    
    # Nettoyer les fichiers temporaires
    rm -f "$TEMP_KEY" "$TEMP_CERT"

# Méthode dotnet dev-certs
else
    step "Génération du certificat avec dotnet dev-certs..."
    
    if ! command -v dotnet &> /dev/null; then
        error "dotnet n'est pas installé"
    fi
    
    dotnet dev-certs https -ep "$OUTPUT_PATH" -p "$PASSWORD" 2>/dev/null || error "Erreur lors de la génération du certificat"
fi

# Vérifier la création
if [ ! -f "$OUTPUT_PATH" ]; then
    error "Le certificat n'a pas été créé"
fi

# Configurer les permissions
step "Configuration des permissions..."
chmod 600 "$OUTPUT_PATH"

echo ""
success "Certificat de signature créé avec succès!"
echo ""
echo -e "${NC}📋 Informations:${NC}"
echo "   Fichier       : $OUTPUT_PATH"
echo "   Validité      : $DAYS jours"
echo "   Algorithme    : $([ "$USE_OPENSSL" = true ] && echo "RSA 4096" || echo "RSA 2048")"
echo ""
echo -e "${YELLOW}🔑 Mot de passe:${NC}"
echo "   $PASSWORD"
echo ""
warning "Stockez ce mot de passe de manière sécurisée!"
echo ""
echo -e "${NC}📖 Prochaines étapes:${NC}"
echo ""
echo "1️⃣  Configurer appsettings.Production.json:"
echo '   {'
echo '     "IdentityServer": {'
echo '       "SigningMethod": "Certificate",'
echo '       "SigningKeyPath": "keys/signing-key.pfx",'
echo "       \"SigningKeyPassword\": \"$PASSWORD\""
echo '     }'
echo '   }'
echo ""
echo "2️⃣  Ou utiliser une variable d'environnement (recommandé):"
echo "   export IDENTITYSERVER_SIGNING_PASSWORD=\"$PASSWORD\""
echo ""
echo "3️⃣  Vérifier que le certificat n'est PAS committé:"
echo "   git status | grep signing-key.pfx"
echo "   (Doit être dans .gitignore)"
echo ""
echo "4️⃣  Tester l'application:"
echo "   dotnet run --project src/Johodp.Api --launch-profile https"
echo ""
echo -e "${NC}📚 Pour la rotation du certificat, voir:${NC}"
echo "   CERTIFICATE_ROTATION.md"
echo ""
