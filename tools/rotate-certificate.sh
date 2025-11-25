#!/bin/bash
# rotate-certificate.sh
# Script de rotation des certificats IdentityServer sans interruption de service

set -e

KEYS_DIR="/app/keys"
VAULT_PATH="secret/johodp/identityserver"
TOKEN_LIFETIME_HOURS=1  # Durée de vie des access tokens (à adapter selon votre config)
GRACE_PERIOD_HOURS=$((TOKEN_LIFETIME_HOURS + 1))

echo "🔄 IdentityServer Certificate Rotation"
echo "======================================"
echo ""

# 1. Backup du certificat actuel
echo "📦 Step 1: Backing up current certificate..."
CURRENT_CERT_PATH=$(vault kv get -field=cert-path $VAULT_PATH/current)
CURRENT_CERT_PASSWORD=$(vault kv get -field=cert-password $VAULT_PATH/current)

echo "   Current cert: $CURRENT_CERT_PATH"

# 2. Déplacer le certificat actuel vers "previous"
echo "📝 Step 2: Moving current certificate to 'previous' slot..."
vault kv put $VAULT_PATH/previous \
    cert-path="$CURRENT_CERT_PATH" \
    cert-password="$CURRENT_CERT_PASSWORD"

echo "   ✅ Previous certificate saved"

# 3. Générer nouveau certificat
echo "🔑 Step 3: Generating new certificate..."
NEW_CERT_NAME="signing-key-$(date +%Y%m%d-%H%M%S).pfx"
NEW_CERT_PATH="$KEYS_DIR/$NEW_CERT_NAME"
NEW_CERT_PASSWORD=$(openssl rand -base64 32)

# Générer avec OpenSSL (valide 365 jours)
openssl req -x509 -newkey rsa:4096 \
    -keyout /tmp/key.pem \
    -out /tmp/cert.pem \
    -days 365 \
    -nodes \
    -subj "/CN=Johodp IdentityServer/O=Johodp/C=FR"

openssl pkcs12 -export \
    -out "$NEW_CERT_PATH" \
    -inkey /tmp/key.pem \
    -in /tmp/cert.pem \
    -passout pass:"$NEW_CERT_PASSWORD"

# Cleanup temporary files
rm -f /tmp/key.pem /tmp/cert.pem

echo "   ✅ New certificate generated: $NEW_CERT_NAME"

# 4. Stocker le nouveau certificat dans Vault
echo "☁️  Step 4: Uploading new certificate to Vault..."
vault kv put $VAULT_PATH/current \
    cert-path="$NEW_CERT_PATH" \
    cert-password="$NEW_CERT_PASSWORD"

echo "   ✅ New certificate uploaded to Vault"

# 5. Rolling restart des pods (zéro downtime)
echo "🔄 Step 5: Rolling restart of pods..."
if command -v kubectl &> /dev/null; then
    kubectl rollout restart deployment/johodp-api -n production
    kubectl rollout status deployment/johodp-api -n production --timeout=5m
    echo "   ✅ Pods restarted successfully"
else
    echo "   ⚠️  kubectl not found - manual restart required"
fi

# 6. État après rotation
echo ""
echo "📊 Rotation Status:"
echo "   - NEW certificate (current): Signs new tokens"
echo "   - OLD certificate (previous): Validates existing tokens"
echo "   - Grace period: $GRACE_PERIOD_HOURS hours"
echo ""

# 7. Attendre expiration des tokens
echo "⏳ Step 6: Waiting for old tokens to expire..."
echo "   Sleeping for $GRACE_PERIOD_HOURS hours..."
echo "   (Press Ctrl+C to skip and complete manually later)"
sleep $(($GRACE_PERIOD_HOURS * 3600))

# 8. Supprimer l'ancien certificat
echo "🗑️  Step 7: Removing old certificate..."
vault kv delete $VAULT_PATH/previous
rm -f "$CURRENT_CERT_PATH"

echo ""
echo "✅ Certificate rotation completed successfully!"
echo ""
echo "📝 Next rotation due: $(date -d '+90 days' '+%Y-%m-%d')"
