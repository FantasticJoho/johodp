# 📖 Index de la Documentation - Johodp Identity Provider

> **Note:** Ce document est obsolète. Utilisez **[INDEX2.md](INDEX2.md)** pour la documentation à jour.

Bienvenue dans la documentation du projet **Johodp** - Identity Provider multi-tenant basé sur Domain-Driven Design et .NET 8.

---

## 🚀 Démarrage Rapide

| Document | Description | Temps |
|----------|-------------|-------|
| **[QUICKSTART.md](QUICKSTART.md)** | Installation et premier lancement | 5 min |
| **[INSTALL.md](INSTALL.md)** | Guide d'installation complet | 15 min |
| **[README.md](README.md)** | Vue d'ensemble du projet | 10 min |

---

## 📐 Architecture et Modèle

| Document | Description |
|----------|-------------|
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Clean Architecture, DDD, multi-tenant, webhooks OAuth2 |
| **[DOMAIN_MODEL.md](DOMAIN_MODEL.md)** | Modèle de domaine (User, Client, Tenant, CustomConfiguration) |
| **[PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)** | Structure des dossiers et fichiers |

---

## 📦 Fonctionnalités et Livraison

| Document | Description | Status |
|----------|-------------|--------|
| **[LOT_PLANNING.md](LOT_PLANNING.md)** | Stratégie de livraison par lots | ⭐ Production |
| **[USER_STORIES.md](USER_STORIES.md)** | User stories complètes (10 épics, 48 US) | ⭐ MàJ |
| **[USE_CASES.md](USE_CASES.md)** | Cas d'usage détaillés (UC-00 à UC-12) | ✅ Complet |
| **[ENDPOINTS_MATRIX.md](ENDPOINTS_MATRIX.md)** | Matrice endpoints implémentés vs documentés | ⭐ Nouveau |

**LOT 1:** 34/35 endpoints (97%) - 7/35 tests (20%)  
**LOT 2 (MFA):** 3/5 endpoints (60%) - 0/5 tests (0%)

---

## 🔐 Sécurité et Authentification

| Document | Description |
|----------|-------------|
| **[MFA_TOTP.md](MFA_TOTP.md)** | Multi-Factor Authentication (TOTP/Google Authenticator) |
| **[MFA_CLIENT.md](MFA_CLIENT.md)** | Configuration client MFA |
| **[CORS_SECURITY.md](CORS_SECURITY.md)** | Configuration CORS multi-tenant (7 couches) |
| **[IDENTITY_SERVER_KEYS.md](IDENTITY_SERVER_KEYS.md)** | Gestion des clés de signature |
| **[CERTIFICATE_ROTATION.md](CERTIFICATE_ROTATION.md)** | Rotation des certificats X.509 |

---

## 🗄️ Base de Données et Infrastructure

| Document | Description |
|----------|-------------|
| **[MIGRATIONS_STRATEGY.md](MIGRATIONS_STRATEGY.md)** | Stratégie de migrations EF Core |
| **[TABLE_NAMING.md](TABLE_NAMING.md)** | Standardisation snake_case |
| **[MONGODB_CREDENTIAL_ROTATION.md](MONGODB_CREDENTIAL_ROTATION.md)** | Rotation credentials MongoDB (sidecar + Vault) |
| **[CACHE.md](CACHE.md)** | Stratégie de cache distribuée |
| **[HEALTH_CHECKS.md](HEALTH_CHECKS.md)** | Health checks et monitoring |

---

## 🔍 API et Endpoints

| Document | Description |
|----------|-------------|
| **[API_ENDPOINTS.md](API_ENDPOINTS.md)** | Liste complète des endpoints REST |
| **[API_LOGIN.md](API_LOGIN.md)** | Flux de connexion OAuth2/OIDC |
| **[ACCOUNT_FLOWS.md](ACCOUNT_FLOWS.md)** | Flux de gestion de compte (inscription, reset password) |
| **[ONBOARDING_FLOW.md](ONBOARDING_FLOW.md)** | Flux d'onboarding avec webhook tierce |

---

## 🧪 Tests et Qualité

| Document | Description |
|----------|-------------|
| **[tests/Johodp.Tests/](tests/Johodp.Tests/)** | Tests d'intégration (SQLite in-memory) |
| **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** | Dépannage et solutions aux problèmes courants |

**Status:** 6/6 tests actifs passent - **Gaps:** 28/40 endpoints sans tests

---

## 📝 Récapitulatifs et Métadonnées

| Document | Description |
|----------|-------------|
| **[SESSION_RECAP.md](SESSION_RECAP.md)** | Récapitulatif session 2024-12-03 |
| **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** | Résumé implémentation |
| **[COMPLETION_SUMMARY.md](COMPLETION_SUMMARY.md)** | Résumé complétion |

---

## 🎯 Navigation par Besoin

| Je veux... | Document |
|------------|----------|
| Démarrer rapidement | [QUICKSTART.md](QUICKSTART.md) |
| Comprendre l'architecture | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Voir les endpoints | [ENDPOINTS_MATRIX.md](ENDPOINTS_MATRIX.md) |
| Implémenter MFA | [MFA_TOTP.md](MFA_TOTP.md) + [USER_STORIES.md](USER_STORIES.md) (Epic 6) |
| Comprendre les lots | [LOT_PLANNING.md](LOT_PLANNING.md) |
| Résoudre un problème | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| Configurer MongoDB | [MONGODB_CREDENTIAL_ROTATION.md](MONGODB_CREDENTIAL_ROTATION.md) |

---

**Version:** 2.0  
**Dernière mise à jour:** 3 décembre 2025  
**Status:** ⭐ INDEX2.md est la référence actuelle
