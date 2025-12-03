# 📚 Index Complet - Johodp Identity Provider

> **Référence principale** - Document consolidé et à jour (3 décembre 2025)

---

## 🎯 Documents Essentiels

### Démarrage et Installation
- **[QUICKSTART.md](QUICKSTART.md)** - Démarrage rapide (Docker + PostgreSQL) - 5 min
- **[INSTALL.md](INSTALL.md)** - Installation détaillée - 15 min
- **[README.md](README.md)** - Vue d'ensemble du projet - 10 min

### Architecture et Modèle
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Clean Architecture, DDD, multi-tenant, webhooks OAuth2
- **[DOMAIN_MODEL.md](DOMAIN_MODEL.md)** - Modèle de domaine (User, Client, Tenant, CustomConfiguration)
- **[PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)** - Structure des dossiers et fichiers

---

## 📦 Fonctionnalités par Lot

### ✅ LOT 1 - Core (PRODUCTION - 97% complet)
- **[LOT_PLANNING.md](LOT_PLANNING.md)** - Stratégie de livraison
- **[USER_STORIES.md](USER_STORIES.md)** - 48 user stories, 10 épics
- **[USE_CASES.md](USE_CASES.md)** - Cas d'usage (UC-00 à UC-12)
- **[ENDPOINTS_MATRIX.md](ENDPOINTS_MATRIX.md)** - Matrice implémentation vs documentation

**Status:** 34/35 endpoints (97%) | 7/35 tests (20%)

### 🔄 LOT 2 - MFA/TOTP (EN COURS - 60% complet)
- **[MFA_TOTP.md](MFA_TOTP.md)** - Guide technique TOTP/Google Authenticator
- **[MFA_CLIENT.md](MFA_CLIENT.md)** - Configuration client
- **[USER_STORIES.md](USER_STORIES.md)** - Epic 6: MFA (lignes 1018-1200)

**Status:** 3/5 endpoints (60%) | 0/5 tests (0%)

**Endpoints implémentés:**
- `POST /api/auth/mfa/enroll`
- `POST /api/auth/mfa/verify-enrollment`
- `POST /api/auth/login-with-totp`

### 📋 LOT 3 - Avancé (PLANIFIÉ)
Administration, monitoring, tests E2E (~50 SP, 2-3 sprints)

---

## 🔐 Sécurité

### OAuth2/OIDC et Authentification
- **[ACCOUNT_FLOWS.md](ACCOUNT_FLOWS.md)** - Flux complets (inscription, activation, login, reset password, onboarding)

### Certificats et Rotation
- **[IDENTITY_SERVER_KEYS.md](IDENTITY_SERVER_KEYS.md)** - Clés de signature
- **[CERTIFICATE_ROTATION.md](CERTIFICATE_ROTATION.md)** - Rotation X.509

### Configuration CORS
- **[CORS_SECURITY.md](CORS_SECURITY.md)** - Defense-in-depth (7 couches)

---

## 🗄️ Base de Données et Infrastructure

### PostgreSQL
- **[MIGRATIONS_STRATEGY.md](MIGRATIONS_STRATEGY.md)** - Stratégie EF Core
- **[MIGRATIONS_API.md](MIGRATIONS_API.md)** - API de gestion
- **[TABLE_NAMING.md](TABLE_NAMING.md)** - Standardisation snake_case
- **[SCHEMA_DBO_MIGRATION.md](SCHEMA_DBO_MIGRATION.md)** - Migration vers schéma dbo
- **[init-db.sh](init-db.sh)** / **[init-db.ps1](init-db.ps1)** - Scripts d'initialisation

### MongoDB
- **[MONGODB_CREDENTIAL_ROTATION.md](MONGODB_CREDENTIAL_ROTATION.md)** - Rotation credentials (sidecar + Vault + reloadOnChange)

### Cache et Monitoring
- **[CACHE.md](CACHE.md)** - Cache distribuée
- **[HEALTH_CHECKS.md](HEALTH_CHECKS.md)** - Health checks
- **[MONITORING.md](MONITORING.md)** - Observabilité (métriques, logs, traces)
- **[JOURNALISATION.md](JOURNALISATION.md)** - Stratégie de logging
- **[LOGGING_ENRICHERS.md](LOGGING_ENRICHERS.md)** - Enrichment automatique (tenant_id, client_id)

---

## 🔍 API et Endpoints

- **[API_ENDPOINTS.md](API_ENDPOINTS.md)** - Liste complète REST avec exemples
- **[ACCOUNT_FLOWS.md](ACCOUNT_FLOWS.md)** - Flux d'authentification et gestion de compte
- **[MULTI_TENANT_USER_API.md](MULTI_TENANT_USER_API.md)** - Gestion utilisateurs multi-tenant

---

## 🧪 Tests et Qualité

- **[tests/Johodp.Tests/](tests/Johodp.Tests/)** - Tests d'intégration (SQLite in-memory)
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Dépannage

**Status:** 6/6 tests actifs passent | **Gap:** 28/40 endpoints sans tests

---

## 🎨 Configuration et Customisation

- **[CUSTOM_CONFIGURATION.md](CUSTOM_CONFIGURATION.md)** - CustomConfiguration (branding, langues partagées)
- **[TENANT_MANAGEMENT.md](TENANT_MANAGEMENT.md)** - Gestion des tenants
- **[TENANT_URL_FORMAT.md](TENANT_URL_FORMAT.md)** - Format des URLs multi-tenant
- **[CONTRACTS_PROJECT.md](CONTRACTS_PROJECT.md)** - Projet Johodp.Contracts (DTOs partagés)

---

## 🔧 Patterns et Techniques

- **[MEDIATOR.md](MEDIATOR.md)** - Pattern Mediator (MediatR)
- **[RESULT_SPECIFICATION_PATTERNS.md](RESULT_SPECIFICATION_PATTERNS.md)** - Result pattern et Specification pattern
- **[USER_AGGREGATE_MODIFICATIONS.md](USER_AGGREGATE_MODIFICATIONS.md)** - Modifications agrégat User

---

## 📝 Métadonnées et Récapitulatifs

- **[SESSION_RECAP.md](SESSION_RECAP.md)** - Récap session 2024-12-03
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Résumé implémentation
- **[COMPLETION_SUMMARY.md](COMPLETION_SUMMARY.md)** - Résumé complétion

---

## 🎯 Navigation Rapide

| Besoin | Document |
|--------|----------|
| Démarrer | [QUICKSTART.md](QUICKSTART.md) |
| Architecture | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Endpoints | [ENDPOINTS_MATRIX.md](ENDPOINTS_MATRIX.md) |
| MFA | [MFA_TOTP.md](MFA_TOTP.md) |
| Lots | [LOT_PLANNING.md](LOT_PLANNING.md) |
| Dépannage | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| MongoDB | [MONGODB_CREDENTIAL_ROTATION.md](MONGODB_CREDENTIAL_ROTATION.md) |
| Multi-tenant | [TENANT_MANAGEMENT.md](TENANT_MANAGEMENT.md) |
| CustomConfig | [CUSTOM_CONFIGURATION.md](CUSTOM_CONFIGURATION.md) |

---

**Version:** 2.1  
**Dernière mise à jour:** 3 décembre 2025  
**Status:** ✅ Production-ready (LOT 1) | 🔄 LOT 2 en cours
