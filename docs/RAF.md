# RAF - Reste À Faire (recommandations)

Ce document liste les tâches recommandées pour compléter l'implémentation de la révocation des refresh tokens et les actions de sécurité associées.

1) Enregistrer le service helper dans DI
- Ajouter l'enregistrement dans `Startup` / `Program` (selon projet) :

```csharp
// Exemple (dans l'extension AddInfrastructure ou dans Program.cs)
services.AddTransient<Johodp.Infrastructure.Identity.IRefreshTokenRevoker, Johodp.Infrastructure.Identity.RefreshTokenRevoker>();
```

2) Appeler le service lors du flow 'connexion sur un autre tenant'
- Emplacement suggéré : `AccountController` (ou service d'auth) au moment où l'on détecte que l'utilisateur se connecte sur un tenant différent (selon `acr_values`).
- Exemple d'appel :

```csharp
await _refreshTokenRevoker.RevokeRefreshTokensAsync(userId, clientId: null);
```

3) Tests d'intégration
- Ajouter un test qui crée des persisted grants (refresh tokens) puis vérifie qu'après l'appel au revoker, les persisted grants ont bien été supprimés. Utiliser le `PersistedGrantDbContext` dans les tests existants.

4) Log & Audit
- Logguer l'action de révocation (subjectId, clientId optionnel, déclencheur, IP/tenant).
- Ajouter entrée d'audit pour permettre enquête en cas de suppression accidentelle.

5) Notifications par e-mail
- Décider si l'envoi d'un e-mail lors de révocation automatique est systématique ou seulement pour cas suspects.
- Si e-mail => standardiser le template (voir `docs/email-templates.md` si créé).

6) Choix produit / UX
- Comportement possible :
  - Révoquer automatiquement tous les refresh tokens à la connexion sur un autre tenant (plus sûr, mais impacte UX multi-app). 
  - Révoquer seulement ceux d'un client donné (moins agressif).
  - Proposer à l'utilisateur une option dans son espace « Se déconnecter de tous les appareils ». 

7) Règles additionnelles
- Rate-limit des actions de révocation.
- Ne pas permettre la révocation par une tierce partie non autorisée.

8) Documentation et formation
- Mettre à jour `docs/identity-flows-mermaid.md` et `docs/identity-flows-mermaid-mfa.md` (déjà mis à jour) et ajouter une note dans la documentation d'exploitation pour expliquer comment enquêter sur persisted grants.

9) Sécurité opérationnelle
- Surveiller les logs pour détecter des patterns de révocation automatique qui pourraient indiquer un bug ou un abus.

---

Si tu veux, j'applique l'enregistrement DI et une modification minimale de `AccountController` pour appeler le revoker au bon endroit (je peux créer une PR contenant :
- l'enregistrement DI
- l'injection et l'appel dans `AccountController`
- un test d'intégration basique
). Dis si tu veux que je fasse cela maintenant, et quel comportement (révoquer tous les refresh tokens vs restreindre par client) tu préfères.
