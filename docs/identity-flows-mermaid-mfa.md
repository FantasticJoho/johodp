# 🛠️ Identity Flows - Mermaid Diagrams (MFA)

Ce fichier illustre les mêmes use cases que précédemment, mais avec l'ajout d'un second facteur (MFA, ex : Microsoft Authenticator).

## 1. Onboarding (création de compte avec MFA)
```mermaid
sequenceDiagram
    participant User
    participant App as Application
    participant IdP as Identity Provider
    participant Email

    User->>App: Demande de création de compte
    App->>IdP: POST /api/users/register (email, tenant machin.com)
    IdP->>Email: Envoie email d'activation
    Email->>User: Lien d'activation
    User->>IdP: POST /api/auth/activate (token, mot de passe)
    IdP->>User: Compte activé
    IdP->>User: Invite à configurer MFA
    User->>IdP: Enrôle Microsoft Authenticator
    IdP->>User: MFA configuré
```

## 2. Onboarding alors que le compte existe déjà (MFA)
```mermaid
sequenceDiagram
    participant User
    participant App as Application
    participant IdP as Identity Provider
    participant Email

    User->>App: Demande de création de compte
    App->>IdP: POST /api/users/register (email, tenant machin.com)
    IdP->>Email: Envoie email "Compte existe déjà"
    Email->>User: Notification compte existant
```

## 3. Connexion sur un tenant avec MFA
```mermaid
sequenceDiagram
    participant User
    participant App as Application
    participant IdP as Identity Provider

    User->>App: Accès à l'application (tenant machin.com)
    App->>IdP: Redirect (acr_values=https://machin.com)
    IdP->>App: Auth form
    App->>IdP: Credentials
    IdP->>User: Demande second facteur (MFA)
    User->>IdP: Code Microsoft Authenticator
    IdP->>App: JWT (tenant_id: "machin", claims, mfa: true)
    App->>App: Session créée
```

## 4. Connexion ensuite sur un autre tenant avec MFA
```mermaid
sequenceDiagram
    participant User
    participant App as Application
    participant IdP as Identity Provider

    User->>App: Accès à l'application (tenant truc.com)
    App->>IdP: Redirect (acr_values=https://truc.com)
    IdP->>User: Déconnexion forcée
    User->>IdP: Reconnexion avec tenant truc.com
    IdP->>App: Auth form
    App->>IdP: Credentials
    IdP->>User: Demande second facteur (MFA)
    User->>IdP: Code Microsoft Authenticator
    IdP->>App: JWT (tenant_id: "truc", claims, mfa: true)
    App->>App: Session mise à jour
```

## 5. Déconnexion
```mermaid
sequenceDiagram
    participant User
    participant App as Application
    participant IdP as Identity Provider

    User->>App: Clique sur "Déconnexion"
    App->>IdP: /connect/logout
    IdP->>App: Session terminée
    App->>User: Redirection page d'accueil
```

## 6. Mot de passe oublié avec MFA
```mermaid
sequenceDiagram
    participant User
    participant App as Application
    participant IdP as Identity Provider
    participant Email

    User->>App: Clique sur "Mot de passe oublié"
    App->>IdP: POST /api/auth/forgot-password (email, tenant machin.com)
    IdP->>Email: Envoie email de réinitialisation
    Email->>User: Lien de réinitialisation
    User->>IdP: POST /api/auth/reset-password (token, nouveau mot de passe)
    IdP->>User: Demande second facteur (MFA)
    User->>IdP: Code Microsoft Authenticator
    IdP->>User: Mot de passe réinitialisé
```

> Note : Tous les flux incluent une étape MFA (Microsoft Authenticator) lors de l'authentification ou de la réinitialisation du mot de passe.