# HTTP Proxy - Inspector de trafic HTTP

Application console pour intercepter et afficher le trafic HTTP avec une interface colorée.

## Utilisation

```bash
# Par défaut : écoute sur 8888 et forward vers localhost:5000
dotnet run

# Port personnalisé
dotnet run 9000

# Port et URL cible personnalisés
dotnet run 9000 http://localhost:7000

# Build et exécution
dotnet build
dotnet run --project HttpProxy.csproj
```

## Exemples

### Proxy pour Johodp API

```bash
# Terminal 1 : Démarrer l'API sur le port 5000
cd ../../src/Johodp.Api
dotnet run

# Terminal 2 : Démarrer le proxy
cd tools/HttpProxy
dotnet run 8888 http://localhost:5000
```

Ensuite, dans vos fichiers `.http`, changez :
```http
@baseUrl = http://localhost:8888
```

### Proxy pour application externe

```bash
dotnet run 8080 https://api.example.com
```

## Fonctionnalités

- ✅ Affichage en temps réel des requêtes/réponses
- ✅ Headers complets (request + response)
- ✅ Body (JSON, texte) avec troncature intelligente
- ✅ Codes couleur par méthode HTTP (GET=vert, POST=bleu, etc.)
- ✅ Codes couleur par status (2xx=vert, 4xx=orange, 5xx=rouge)
- ✅ Durée de chaque requête
- ✅ Numérotation des requêtes pour suivi
- ✅ Gestion d'erreurs avec affichage clair

## Interface

```
  ╭──────────────────────────────────╮
  │ #1 REQUEST                       │
  ├──────────────────────────────────┤
  │ POST /api/clients                │
  │ From: 127.0.0.1:52341           │
  │ Headers: 5                       │
  ╰──────────────────────────────────╯

  ┌─────────────────┬──────────────────┐
  │ Header          │ Value            │
  ├─────────────────┼──────────────────┤
  │ Content-Type    │ application/json │
  │ Content-Length  │ 145              │
  └─────────────────┴──────────────────┘

  ╭──────────────────────────────────╮
  │ #1 RESPONSE                      │
  ├──────────────────────────────────┤
  │ 201 Created                      │
  │ Duration: 45ms                   │
  │ Content-Type: application/json   │
  │ Content-Length: 234 bytes        │
  ╰──────────────────────────────────╯
```

## Dépendances

- **Spectre.Console** : Affichage console avec couleurs et formatage

## Notes

- Le proxy fonctionne uniquement en HTTP (pas HTTPS direct)
- Pour HTTPS, utilisez un tunnel ou Fiddler
- Les headers `Host` et `Connection` sont automatiquement ajustés
- Body tronqué à 500/1000 chars pour lisibilité (full body forwardé)
