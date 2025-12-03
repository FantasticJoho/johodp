# HTTP Proxy Inspector - Desktop UI

Application desktop cross-platform (Windows/Linux/macOS) pour intercepter et inspecter le trafic HTTP avec une interface graphique moderne.

## 🎨 Fonctionnalités

- ✅ **Interface moderne** avec Avalonia UI (Fluent Design)
- ✅ **Liste des requêtes** en temps réel (gauche)
- ✅ **Détails complets** au clic (droite)
- ✅ **Codes couleur** par méthode HTTP et status code
- ✅ **Headers complets** (request + response)
- ✅ **Body JSON/Text** avec scroll
- ✅ **Timing** et taille des réponses
- ✅ **Start/Stop** du proxy
- ✅ **Clear** pour vider la liste
- ✅ **Cross-platform** : Windows, Linux, macOS

## 🚀 Lancement

```bash
cd tools/HttpProxyUI
dotnet run
```

Ou build puis exécuter :
```bash
dotnet build
dotnet run --project HttpProxyUI.csproj
```

## 🌐 Configuration Firefox (Proxy Mode)

1. **Démarrer l'application** avec le port 8888
2. **(Optionnel) Configurer le proxy upstream** :
   - ☑️ **Use System Proxy** : Utilise le proxy configuré dans Windows/Linux (recommandé en entreprise)
   - OU décocher et entrer manuellement : `http://proxy.company.com:8080`
3. **Cliquer Start**
4. **Ouvrir Firefox** → Paramètres → Général → Paramètres réseau
5. **Configuration manuelle du proxy** :
   - HTTP Proxy: `localhost`
   - Port: `8888`
   - ☑️ Utiliser aussi ce proxy pour HTTPS
   - Pas de proxy pour: `(vide ou localhost si besoin)`
6. **Naviguer normalement** sur n'importe quel site
7. **Toutes les requêtes HTTP** apparaissent dans l'application !

### Mode Upstream Proxy (Entreprise)

Si vous êtes derrière un proxy d'entreprise :

**Option 1 - Proxy système (automatique)** :
- ☑️ Cocher "Use System Proxy"
- L'application utilisera le proxy configuré dans les paramètres Windows

**Option 2 - Proxy manuel** :
- ☐ Décocher "Use System Proxy"
- Entrer l'URL du proxy : `http://proxy.company.com:8080`
- Ou avec authentification : `http://username:password@proxy.company.com:8080`

L'application forward alors toutes les requêtes via ce proxy upstream.

### Capture d'écran Firefox Proxy

```
┌─────────────────────────────────────────┐
│ Configuration manuelle du proxy         │
├─────────────────────────────────────────┤
│ Proxy HTTP:      localhost   Port: 8888│
│ ☑ Utiliser aussi ce proxy pour HTTPS   │
│ Proxy SSL:       localhost   Port: 8888│
│ Proxy SOCKS:     (vide)      Port:     │
│                                         │
│ Pas de proxy pour: (vide)              │
└─────────────────────────────────────────┘
```

### Fichier de configuration

La configuration est automatiquement sauvegardée dans :
- **Windows** : `%APPDATA%\HttpProxyUI\config.json`
- **Linux** : `~/.config/HttpProxyUI/config.json`
- **macOS** : `~/Library/Application Support/HttpProxyUI/config.json`

Format du fichier :
```json
{
  "ProxyPort": 8888,
  "TargetUrl": "http://localhost:5000",
  "UpstreamProxy": "http://proxy.company.com:8080",
  "UseSystemProxy": false
}
```

Vous pouvez éditer ce fichier manuellement si besoin.

**Important** : Pour HTTPS, Firefox verra les requêtes mais pas le contenu chiffré (limitations du proxy HTTP simple). Pour inspecter HTTPS, utilisez Fiddler avec certificat root.

## 📊 Interface

### Toolbar (haut)
- **Port** : Port d'écoute du proxy (défaut: 8888)
- **Target** : URL cible pour forwarding (défaut: http://localhost:5000)
- **Start** : Démarrer le proxy
- **Stop** : Arrêter le proxy
- **Clear** : Vider la liste des requêtes
- **Status** : État actuel (Running/Stopped)

### Liste des requêtes (gauche)
Affiche pour chaque requête :
- **#ID** : Numéro séquentiel
- **Méthode** : GET (vert), POST (bleu), PUT (jaune), DELETE (rouge)
- **Path** : Chemin de la requête
- **Heure** : Timestamp (HH:mm:ss.fff)
- **Status** : Code HTTP avec couleur
- **Durée** : Temps de réponse en ms

### Détails (droite)
Sections expandables :
1. **REQUEST INFO** (bleu)
   - Méthode + URL complète
   - Timestamp
   - Remote endpoint

2. **Request Headers**
   - Tous les headers avec clé/valeur

3. **Request Body** (si présent)
   - JSON/Text avec scroll
   - Syntaxe monospace (Consolas)

4. **RESPONSE INFO** (vert)
   - Status code + texte
   - Durée d'exécution
   - Taille de la réponse

5. **Response Headers**
   - Tous les headers retournés

6. **Response Body**
   - JSON/Text avec scroll
   - Max height pour lisibilité

## 🎯 Utilisation avec Johodp API

### Terminal 1 : API
```bash
cd ../../src/Johodp.Api
dotnet run
```

### Terminal 2 : Proxy UI
```bash
cd tools/HttpProxyUI
dotnet run
```

### Configuration dans .http files
```http
# Remplacer
@baseUrl = http://localhost:5000

# Par
@baseUrl = http://localhost:8888
```

Toutes vos requêtes HTTP passeront par le proxy et seront affichées dans l'interface !

## 🎨 Codes couleur

### Méthodes HTTP
- **GET** : Vert (#28a745)
- **POST** : Bleu (#007bff)
- **PUT** : Jaune (#ffc107)
- **DELETE** : Rouge (#dc3545)
- **PATCH** : Cyan (#17a2b8)

### Status Codes
- **2xx** : Vert (#28a745) - Succès
- **3xx** : Jaune (#ffc107) - Redirection
- **4xx** : Orange (#fd7e14) - Erreur client
- **5xx** : Rouge (#dc3545) - Erreur serveur

### Background dans la liste
- **2xx** : Vert pâle (#d4edda)
- **3xx** : Jaune pâle (#fff3cd)
- **4xx** : Rouge pâle (#f8d7da)
- **5xx** : Rouge intense (#f5c6cb)

## 📦 Technologies

- **Avalonia UI 11.3.9** : Framework UI cross-platform
- **CommunityToolkit.Mvvm 8.2.1** : Pattern MVVM avec source generators
- **.NET 8.0** : Runtime moderne
- **HttpListener** : Serveur HTTP natif .NET

## 🐧 Linux

```bash
# Installer les dépendances (Ubuntu/Debian)
sudo apt install libx11-dev libice-dev libsm-dev libfontconfig1-dev

# Lancer
dotnet run
```

## 🍎 macOS

```bash
# Aucune dépendance supplémentaire nécessaire
dotnet run
```

## 📝 Architecture

```
HttpProxyUI/
├── Models/
│   └── ProxyModels.cs       # HttpRequest + ProxyService
├── ViewModels/
│   ├── ViewModelBase.cs
│   └── MainWindowViewModel.cs  # Logique MVVM
├── Views/
│   └── MainWindow.axaml     # Interface XAML
├── Converters/
│   └── ValueConverters.cs   # Convertisseurs couleur/visibilité
└── App.axaml                # Configuration app + ressources
```

## 🔧 Configuration avancée

### Changer le port
```csharp
// Dans l'interface : modifier le champ "Port"
// Ou hardcoder dans MainWindowViewModel.cs
[ObservableProperty] private int _proxyPort = 9000;
```

### Changer l'URL cible
```csharp
// Dans l'interface : modifier le champ "Target"
// Ou hardcoder dans MainWindowViewModel.cs
[ObservableProperty] private string _targetUrl = "https://api.example.com";
```

## 🐛 Troubleshooting

### Port déjà utilisé
- Changez le port dans l'interface
- Ou fermez l'application qui utilise le port 8888

### Permission denied (Linux)
```bash
# Pour écouter sur port < 1024, utiliser sudo
sudo dotnet run
```

### Pas d'affichage des requêtes
- Vérifiez que "Start" est cliqué
- Vérifiez que vos requêtes vont vers `localhost:8888`
- Consultez la status bar en bas (Total Requests)

## 🚧 Limitations

- HTTP uniquement (pas HTTPS direct)
- Pour HTTPS, utilisez Fiddler ou un tunnel SSL
- Mono-thread (pas de parallélisation des requêtes)
- Pas de persistance (les requêtes disparaissent à la fermeture)

## 🎯 Améliorations futures

- [ ] Export des requêtes (JSON/HAR)
- [ ] Filtres (par méthode, status, path)
- [ ] Search dans la liste
- [ ] Pretty print JSON automatique
- [ ] Breakpoints pour modifier requêtes à la volée
- [ ] Support HTTPS avec certificat custom
- [ ] Historique persistant (SQLite)
- [ ] Dark mode
- [ ] Copier request as cURL
