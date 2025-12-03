# 🔄 MongoDB Credential Rotation avec Sidecar Pattern

## Vue d'ensemble

Ce document décrit l'architecture et le fonctionnement du système de rotation automatique des credentials MongoDB dans l'application Johodp. Le système utilise un **sidecar container** pour renouveler les credentials quotidiennement, combiné avec un **MongoDbFactory singleton** qui recrée le client MongoDB de manière transparente lors du changement de credentials.

## Architecture Globale

```mermaid
graph TB
    subgraph "Kubernetes Pod"
        subgraph "App Container (Johodp.Api)"
            API[ASP.NET Core API]
            Factory[MongoDbFactory Singleton]
            Monitor[IOptionsMonitor]
            Client[MongoClient Instance]
            
            API -->|Inject| Factory
            Factory -->|Observe| Monitor
            Monitor -->|Notify change| Factory
            Factory -->|Maintain| Client
        end
        
        subgraph "Sidecar Container"
            Sidecar[Credential Rotator]
            ConfigMap[ConfigMap/Secret Volume]
            
            Sidecar -->|Write new credentials| ConfigMap
        end
        
        ConfigMap -->|Mount as file| Monitor
    end
    
    Vault[HashiCorp Vault / Cloud Secret Manager] -->|Fetch credentials daily| Sidecar
    Client -->|Connect with credentials| MongoDB[(MongoDB Atlas/Cluster)]
    
    style Factory fill:#e1f5ff
    style Sidecar fill:#fff4e6
    style Vault fill:#f3e5f5
```

## Problématique et Solution

### ⚠️ Problème

1. **MongoClient doit être singleton** - Gestion du connection pool, performance
2. **Credentials changent régulièrement** - Rotation quotidienne pour sécurité
3. **Pas de downtime** - Application doit rester disponible pendant la rotation
4. **Thread-safety** - Multiples requêtes simultanées pendant le changement

### ✅ Solution

- **Sidecar Pattern** - Container séparé gérant la rotation des credentials
- **MongoDbFactory Singleton** - Encapsule la logique de recréation du client
- **IOptionsMonitor** - Détection automatique des changements de configuration
- **Lazy reload** - Recréation du client uniquement lors de la prochaine utilisation

## Composants du Système

### 1. Sidecar Container (Credential Rotator)

Le sidecar est un container indépendant qui s'exécute dans le même pod que l'application.

```yaml
# k8s-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: johodp-api
spec:
  template:
    spec:
      containers:
      # Application principale
      - name: johodp-api
        image: johodp/api:latest
        volumeMounts:
        - name: mongodb-config
          mountPath: /app/config
          readOnly: true
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: Production
      
      # Sidecar de rotation des credentials
      - name: credential-rotator
        image: johodp/credential-rotator:latest
        env:
        - name: VAULT_ADDR
          value: https://vault.company.com
        - name: VAULT_ROLE
          value: johodp-mongodb-reader
        - name: ROTATION_INTERVAL_HOURS
          value: "24"
        - name: OUTPUT_PATH
          value: /config/appsettings.MongoDB.json
        volumeMounts:
        - name: mongodb-config
          mountPath: /config
      
      volumes:
      - name: mongodb-config
        emptyDir: {}
```

### 2. Script Sidecar (Bash/Python)

```python
#!/usr/bin/env python3
# credential-rotator.py

import os
import time
import json
import logging
from datetime import datetime, timedelta
import hvac  # HashiCorp Vault client

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class CredentialRotator:
    def __init__(self):
        self.vault_addr = os.getenv('VAULT_ADDR')
        self.vault_role = os.getenv('VAULT_ROLE')
        self.output_path = os.getenv('OUTPUT_PATH', '/config/appsettings.MongoDB.json')
        
        # Authentification Vault via Kubernetes Service Account
        self.vault_client = hvac.Client(url=self.vault_addr)
        with open('/var/run/secrets/kubernetes.io/serviceaccount/token') as f:
            jwt = f.read()
        self.vault_client.auth.kubernetes.login(
            role=self.vault_role,
            jwt=jwt
        )
    
    def fetch_credentials(self):
        """Récupérer les credentials depuis Vault"""
        try:
            secret = self.vault_client.secrets.kv.v2.read_secret_version(
                path='mongodb/johodp-prod'
            )
            
            data = secret['data']['data']
            
            # Format appsettings.json
            config = {
                'MongoDB': {
                    'ConnectionString': data['connection_string'],
                    'Database': data['database'],
                    'Username': data['username'],
                    'Password': data['password']
                }
            }
            
            logger.info(f"✅ Credentials fetched successfully")
            return config
            
        except Exception as e:
            logger.error(f"❌ Failed to fetch credentials: {e}")
            raise
    
    def write_credentials(self, config):
        """Écrire les credentials dans appsettings.MongoDB.json (atomic write)"""
        temp_path = f"{self.output_path}.tmp"
        
        try:
            # Écriture atomique (write + rename)
            with open(temp_path, 'w') as f:
                json.dump(config, f, indent=2)
            
            # Rename atomique (évite lecture partielle)
            os.rename(temp_path, self.output_path)
            
            logger.info(f"✅ Credentials written to {self.output_path}")
            
        except Exception as e:
            logger.error(f"❌ Failed to write credentials: {e}")
            if os.path.exists(temp_path):
                os.remove(temp_path)
            raise
    
    def run(self):
        """Boucle principale de rotation"""
        logger.info("🚀 Credential rotator started (scheduled at 2:00 AM daily)")
        
        # Rotation immédiate au démarrage du pod
        try:
            logger.info("⚡ Initial rotation at pod startup")
            config = self.fetch_credentials()
            self.write_credentials(config)
            logger.info("✅ Initial credentials written successfully")
        except Exception as e:
            logger.error(f"❌ Initial rotation failed: {e}")
            logger.info("⏰ Retrying in 5 minutes")
            time.sleep(300)
        
        while True:
            try:
                # Calculer le temps jusqu'à 2h du matin
                now = datetime.utcnow()
                next_run = now.replace(hour=2, minute=0, second=0, microsecond=0)
                
                # Si on a dépassé 2h aujourd'hui, planifier pour demain
                if now.hour >= 2:
                    next_run += timedelta(days=1)
                
                sleep_seconds = (next_run - now).total_seconds()
                logger.info(f"😴 Next rotation at {next_run.strftime('%Y-%m-%d %H:%M:%S UTC')} (in {sleep_seconds/3600:.1f}h)")
                time.sleep(sleep_seconds)
                
                # Rotation à 2h du matin
                logger.info(f"🔄 Scheduled rotation at {datetime.utcnow()}")
                config = self.fetch_credentials()
                self.write_credentials(config)
                
                # IOptionsMonitor détectera automatiquement le changement (reloadOnChange: true)
                
            except Exception as e:
                logger.error(f"❌ Rotation failed: {e}")
                # Retry après 5 minutes en cas d'erreur
                logger.info("⏰ Retrying in 5 minutes")
                time.sleep(300)

if __name__ == "__main__":
    rotator = CredentialRotator()
    rotator.run()
```

**Alternative Bash :**

```bash
#!/bin/bash
# credential-rotator.sh

set -e

VAULT_ADDR="${VAULT_ADDR}"
VAULT_ROLE="${VAULT_ROLE:-johodp-app}"
OUTPUT_PATH="${OUTPUT_PATH:-/config/appsettings.MongoDB.json}"
VAULT_TOKEN_PATH="/var/run/secrets/kubernetes.io/serviceaccount/token"

log() {
    echo "[$(date -u +'%Y-%m-%d %H:%M:%S UTC')] $1"
}

vault_login() {
    local jwt=$(cat "$VAULT_TOKEN_PATH")
    local response=$(curl -s --request POST \
        --data "{\"role\":\"$VAULT_ROLE\",\"jwt\":\"$jwt\"}" \
        "$VAULT_ADDR/v1/auth/kubernetes/login")
    
    echo "$response" | jq -r '.auth.client_token'
}

fetch_credentials() {
    local token=$1
    curl -s --header "X-Vault-Token: $token" \
        "$VAULT_ADDR/v1/secret/data/mongodb/johodp-prod" | jq -r '.data.data'
}

write_config() {
    local credentials=$1
    local temp_file="${OUTPUT_PATH}.tmp"
    
    # Créer JSON pour appsettings.MongoDB.json
    cat > "$temp_file" <<EOF
{
  "MongoDB": {
    "ConnectionString": $(echo "$credentials" | jq -r '.connection_string'),
    "Database": $(echo "$credentials" | jq -r '.database'),
    "Username": $(echo "$credentials" | jq -r '.username'),
    "Password": $(echo "$credentials" | jq -r '.password')
  }
}
EOF
    
    # Écriture atomique
    mv "$temp_file" "$OUTPUT_PATH"
    log "✅ Credentials written to $OUTPUT_PATH"
}

calculate_next_2am() {
    local now_hour=$(date -u +%H)
    local now_min=$(date -u +%M)
    
    if [ "$now_hour" -lt 2 ]; then
        # Aujourd'hui à 2h00
        local target_time="$(date -u +%Y-%m-%d) 02:00:00"
    else
        # Demain à 2h00
        local target_time="$(date -u -d '+1 day' +%Y-%m-%d) 02:00:00"
    fi
    
    local now_ts=$(date -u +%s)
    local target_ts=$(date -u -d "$target_time" +%s)
    local sleep_seconds=$((target_ts - now_ts))
    
    echo "$sleep_seconds|$target_time"
}

rotate_credentials() {
    log "🔄 Rotating credentials..."
    local token=$(vault_login)
    local credentials=$(fetch_credentials "$token")
    write_config "$credentials"
}

# Main
log "🚀 Credential rotator started (scheduled at 2:00 AM UTC daily)"

# Rotation immédiate au démarrage
log "⚡ Initial rotation at pod startup"
rotate_credentials

# Boucle principale
while true; do
    # Calculer le temps jusqu'à 2h00 du matin
    IFS='|' read -r sleep_seconds target_time <<< "$(calculate_next_2am)"
    local sleep_hours=$(echo "scale=1; $sleep_seconds / 3600" | bc)
    
    log "😴 Next rotation at $target_time (in ${sleep_hours}h)"
    sleep "$sleep_seconds"
    
    # Rotation à 2h00
    rotate_credentials || {
        log "❌ Rotation failed, retrying in 5 minutes"
        sleep 300
    }
done
```

### 3. Configuration ASP.NET Core

Le sidecar écrit les credentials dans un fichier `appsettings.MongoDB.json` monté sur un volume partagé.

```json
// appsettings.json (configuration statique)
{
  "MongoDB": {
    "ConnectionTimeout": 30,
    "MaxConnectionPoolSize": 100,
    "MinConnectionPoolSize": 10
  }
}
```

```json
// appsettings.MongoDB.json (généré par le sidecar, rechargé automatiquement)
{
  "MongoDB": {
    "ConnectionString": "mongodb+srv://user:pass@cluster.mongodb.net/db",
    "Database": "johodp",
    "Username": "johodp-app-user",
    "Password": "rotated-password-abc123"
  }
}
```

```csharp
// Infrastructure/Configuration/MongoDbOptions.cs
public class MongoDbOptions
{
    public const string SectionName = "MongoDB";
    
    public string ConnectionString { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int ConnectionTimeout { get; set; } = 30;
    public int MaxConnectionPoolSize { get; set; } = 100;
    public int MinConnectionPoolSize { get; set; } = 10;
    
    // Pas besoin de LoadFromFile(), IOptionsMonitor gère automatiquement
}
```

### 4. MongoDbFactory Singleton

```csharp
// Infrastructure/MongoDB/MongoDbFactory.cs
public interface IMongoDbFactory
{
    IMongoClient GetClient();
    IMongoDatabase GetDatabase();
}

public class MongoDbFactory : IMongoDbFactory, IDisposable
{
    private readonly IOptionsMonitor<MongoDbOptions> _optionsMonitor;
    private readonly ILogger<MongoDbFactory> _logger;
    private IMongoClient? _client;
    private MongoClientSettings? _currentSettings;
    private readonly object _lock = new();
    private IDisposable? _optionsChangeToken;
    
    public MongoDbFactory(
        IOptionsMonitor<MongoDbOptions> optionsMonitor,
        ILogger<MongoDbFactory> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        
        // S'abonner aux changements de configuration (reloadOnChange: true)
        _optionsChangeToken = _optionsMonitor.OnChange(OnOptionsChanged);
        
        // Initialiser le client au démarrage
        InitializeClient();
    }
    
    private void OnOptionsChanged(MongoDbOptions options)
    {
        _logger.LogInformation(
            "📢 MongoDB configuration changed (new credentials detected), " +
            "will recreate client on next access");
        
        // Marquer le client actuel comme obsolète
        // Le nouveau client sera créé lors du prochain GetClient()
        lock (_lock)
        {
            _currentSettings = null; // Force recreation
        }
    }
    
    private void InitializeClient()
    {
        lock (_lock)
        {
            if (_client != null && _currentSettings != null)
                return; // Client déjà initialisé et à jour
            
            var options = _optionsMonitor.CurrentValue;
            
            if (string.IsNullOrEmpty(options.ConnectionString))
            {
                _logger.LogError("MongoDB connection string is empty");
                throw new InvalidOperationException("MongoDB connection string not configured");
            }
            
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            
            // Configuration du connection pool
            settings.MaxConnectionPoolSize = options.MaxConnectionPoolSize;
            settings.MinConnectionPoolSize = options.MinConnectionPoolSize;
            settings.ConnectTimeout = TimeSpan.FromSeconds(options.ConnectionTimeout);
            
            // Credentials explicites (override de la connection string si besoin)
            if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
            {
                settings.Credential = MongoCredential.CreateCredential(
                    options.Database,
                    options.Username,
                    options.Password
                );
            }
            
            // Logging
            settings.ClusterConfigurator = cb =>
            {
                cb.Subscribe<CommandStartedEvent>(e =>
                {
                    _logger.LogDebug("MongoDB Command: {CommandName}", e.CommandName);
                });
            };
            
            // Créer nouveau client
            var newClient = new MongoClient(settings);
            
            // Tester la connexion
            try
            {
                newClient.GetDatabase(options.Database).RunCommand<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1)
                );
                _logger.LogInformation("✅ MongoDB client created successfully with new credentials");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to connect to MongoDB with new credentials");
                throw;
            }
            
            // Remplacer l'ancien client
            var oldClient = _client;
            _client = newClient;
            _currentSettings = settings;
            
            // Dispose de l'ancien client après un délai (laisser finir les requêtes en cours)
            if (oldClient != null)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    // Note: MongoClient n'implémente pas IDisposable,
                    // le GC s'en chargera
                    _logger.LogInformation("🗑️ Old MongoDB client disposed");
                });
            }
        }
    }
    
    public IMongoClient GetClient()
    {
        // Vérifier si on doit recréer le client
        if (_client == null || _currentSettings == null)
        {
            _logger.LogInformation("MongoDB client not initialized or expired, recreating...");
            InitializeClient();
        }
        
        return _client!;
    }
    
    public IMongoDatabase GetDatabase()
    {
        var options = _optionsMonitor.CurrentValue;
        return GetClient().GetDatabase(options.Database);
    }
    
    public void Dispose()
    {
        _optionsChangeToken?.Dispose();
        // MongoClient ne nécessite pas de dispose explicite
    }
}
```

### 5. Enregistrement dans le DI

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Ajouter appsettings.MongoDB.json avec reloadOnChange: true
builder.Configuration.AddJsonFile(
    path: "/app/config/appsettings.MongoDB.json",
    optional: false,
    reloadOnChange: true  // ⚡ Active le rechargement automatique
);

// Configurer MongoDB
builder.Services.AddMongoDb(builder.Configuration);

var app = builder.Build();
app.Run();
```

```csharp
// Extensions/MongoDbServiceCollectionExtensions.cs
public static class MongoDbServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configurer IOptionsMonitor pour MongoDB
        // IOptionsMonitor détecte automatiquement les changements grâce à reloadOnChange: true
        services.Configure<MongoDbOptions>(
            configuration.GetSection(MongoDbOptions.SectionName)
        );
        
        // 2. Enregistrer MongoDbFactory comme Singleton
        services.AddSingleton<IMongoDbFactory, MongoDbFactory>();
        
        // 3. Enregistrer IMongoClient et IMongoDatabase en Scoped (via factory)
        services.AddScoped(sp => sp.GetRequiredService<IMongoDbFactory>().GetClient());
        services.AddScoped(sp => sp.GetRequiredService<IMongoDbFactory>().GetDatabase());
        
        return services;
    }
}
```

**Points clés :**
- ✅ `reloadOnChange: true` active le mécanisme de rechargement automatique
- ✅ `IOptionsMonitor` détecte les changements du fichier sans `FileSystemWatcher` custom
- ✅ `OnChange` callback déclenché automatiquement par le framework ASP.NET Core
- ✅ Pas besoin de code custom pour surveiller le fichier

### 6. Utilisation dans les Repositories

```csharp
// Infrastructure/MongoDB/Repositories/UserMongoRepository.cs
public class UserMongoRepository
{
    private readonly IMongoDbFactory _mongoDbFactory;
    private readonly ILogger<UserMongoRepository> _logger;
    
    public UserMongoRepository(
        IMongoDbFactory mongoDbFactory,
        ILogger<UserMongoRepository> logger)
    {
        _mongoDbFactory = mongoDbFactory;
        _logger = logger;
    }
    
    public async Task<User?> GetByIdAsync(string userId)
    {
        // Le factory retourne toujours un client à jour
        var database = _mongoDbFactory.GetDatabase();
        var collection = database.GetCollection<User>("users");
        
        return await collection.Find(u => u.Id == userId).FirstOrDefaultAsync();
    }
    
    public async Task InsertAsync(User user)
    {
        var database = _mongoDbFactory.GetDatabase();
        var collection = database.GetCollection<User>("users");
        
        await collection.InsertOneAsync(user);
    }
}
```

## Workflow de Rotation

### Séquence Complète

```mermaid
sequenceDiagram
    participant S as Sidecar
    participant V as Vault
    participant F as File System<br/>(emptyDir volume)
    participant ASP as ASP.NET Core<br/>Configuration
    participant OM as IOptionsMonitor
    participant MF as MongoDbFactory
    participant API as API Request
    participant MC as MongoClient
    participant DB as MongoDB
    
    Note over S: Démarrage ou<br/>Timer 24h
    
    S->>V: 1. Fetch new credentials
    V-->>S: username, password, connection_string
    
    S->>F: 2. Write appsettings.MongoDB.json (atomic)
    Note over F: Rename temp → final<br/>(atomic operation)
    
    F->>ASP: 3. File change detected (reloadOnChange: true)
    ASP->>OM: 4. Configuration reloaded, trigger OnChange()
    OM->>MF: 5. OnOptionsChanged(newOptions)
    
    Note over MF: Mark current client<br/>as obsolete<br/>(_currentSettings = null)
    
    Note over API,DB: Prochaine requête API
    
    API->>MF: 6. GetClient()
    
    alt Client obsolète
        MF->>OM: 7. Get CurrentValue (new credentials)
        MF->>MF: 8. Create new MongoClient
        MF->>DB: 9. Test connection (ping)
        DB-->>MF: Pong ✅
        Note over MF: Replace old client<br/>with new one
    end
    
    MF-->>API: Return MongoClient
    API->>MC: Use client
    MC->>DB: Query with new credentials
    DB-->>MC: Results
    MC-->>API: Results
    
    Note over MF: After 30s delay
    MF->>MF: Dispose old client<br/>(background task)
```

### États du Système

```mermaid
stateDiagram-v2
    [*] --> Initializing: App starts
    Initializing --> Healthy: Client created<br/>Connection OK
    Healthy --> CredentialsRotating: File changed<br/>detected
    CredentialsRotating --> Recreating: Next GetClient()<br/>call
    Recreating --> Healthy: New client OK
    Recreating --> Error: Connection failed
    Error --> Recreating: Retry
    Healthy --> [*]: App stops
    
    note right of Healthy
        Current client valid
        Serving requests
    end note
    
    note right of CredentialsRotating
        Old client still used
        Waiting for next GetClient()
    end note
    
    note right of Recreating
        Creating new client
        Old client kept for 30s
    end note
```

## Gestion des Erreurs

### Scénarios d'Erreur et Récupération

```mermaid
flowchart TD
    Start([Rotation triggered]) --> FetchCreds[Fetch credentials]
    FetchCreds --> FetchOK{Success?}
    
    FetchOK -->|No| LogError1[Log error]
    LogError1 --> Retry1[Wait 5 min]
    Retry1 --> FetchCreds
    
    FetchOK -->|Yes| WriteFile[Write credentials.json]
    WriteFile --> WriteOK{Success?}
    
    WriteOK -->|No| LogError2[Log error]
    LogError2 --> Retry2[Wait 5 min]
    Retry2 --> WriteFile
    
    WriteOK -->|Yes| FileChanged[ASP.NET Core detects change<br/>reloadOnChange: true]
    FileChanged --> OptionsChange[IOptionsMonitor fires OnChange]
    OptionsChange --> MarkObsolete[Mark client as obsolete]
    MarkObsolete --> WaitRequest[Wait for next request]
    
    WaitRequest --> GetClient[GetClient called]
    GetClient --> LoadFile[Load credentials from file]
    LoadFile --> CreateClient[Create new MongoClient]
    CreateClient --> TestConnection[Test connection: ping]
    
    TestConnection --> ConnectOK{Success?}
    ConnectOK -->|No| LogError3[Log connection error]
    LogError3 --> KeepOld[Keep old client]
    KeepOld --> NextRetry[Next GetClient will retry]
    NextRetry --> GetClient
    
    ConnectOK -->|Yes| ReplaceClient[Replace old client]
    ReplaceClient --> DisposeOld[Dispose old after 30s]
    DisposeOld --> Success([✅ Rotation complete])
    
    Note over S,Success: ASP.NET Core gère automatiquement<br/>la détection des changements<br/>(reloadOnChange: true)
    
    style Success fill:#90EE90
    style LogError1 fill:#FFB6C1
    style LogError2 fill:#FFB6C1
    style LogError3 fill:#FFB6C1
```

### Code de Gestion d'Erreurs

```csharp
private void InitializeClient()
{
    const int maxRetries = 3;
    int attempt = 0;
    
    while (attempt < maxRetries)
    {
        try
        {
            lock (_lock)
            {
                var options = _optionsMonitor.CurrentValue;
                options.LoadFromFile();
                
                if (string.IsNullOrEmpty(options.ConnectionString))
                {
                    throw new InvalidOperationException(
                        "MongoDB connection string not configured");
                }
                
                var settings = BuildMongoClientSettings(options);
                var newClient = new MongoClient(settings);
                
                // Test de connexion avec timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                newClient.GetDatabase(options.Database)
                    .RunCommand<BsonDocument>(
                        new BsonDocument("ping", 1),
                        cancellationToken: cts.Token
                    );
                
                _logger.LogInformation(
                    "✅ MongoDB client created successfully (attempt {Attempt})",
                    attempt + 1);
                
                // Remplacer l'ancien client
                var oldClient = _client;
                _client = newClient;
                _currentSettings = settings;
                
                // Cleanup asynchrone de l'ancien client
                if (oldClient != null)
                {
                    ScheduleOldClientDisposal(oldClient);
                }
                
                return; // Success
            }
        }
        catch (Exception ex)
        {
            attempt++;
            _logger.LogError(ex,
                "❌ Failed to create MongoDB client (attempt {Attempt}/{MaxRetries})",
                attempt, maxRetries);
            
            if (attempt >= maxRetries)
            {
                _logger.LogCritical(
                    "🚨 All retries exhausted, keeping old client (if any)");
                throw;
            }
            
            // Exponential backoff
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            _logger.LogInformation("⏰ Retrying in {Delay} seconds", delay.TotalSeconds);
            Thread.Sleep(delay);
        }
    }
}

private void ScheduleOldClientDisposal(IMongoClient oldClient)
{
    Task.Run(async () =>
    {
        try
        {
            // Attendre que les requêtes en cours se terminent
            await Task.Delay(TimeSpan.FromSeconds(30));
            
            _logger.LogInformation("🗑️ Disposing old MongoDB client");
            
            // MongoClient n'a pas de Dispose, mais on peut forcer le GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Error during old client disposal");
        }
    });
}
```

## Monitoring et Observabilité

### Métriques à Surveiller

```csharp
// Infrastructure/MongoDB/MongoDbMetrics.cs
public class MongoDbMetrics
{
    private readonly ILogger<MongoDbMetrics> _logger;
    private readonly Counter<long> _clientRecreations;
    private readonly Counter<long> _connectionErrors;
    private readonly Histogram<double> _credentialRotationDuration;
    
    public MongoDbMetrics(ILogger<MongoDbMetrics> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        var meter = meterFactory.Create("Johodp.MongoDB");
        
        _clientRecreations = meter.CreateCounter<long>(
            "mongodb.client.recreations",
            description: "Number of times MongoClient was recreated"
        );
        
        _connectionErrors = meter.CreateCounter<long>(
            "mongodb.connection.errors",
            description: "Number of MongoDB connection errors"
        );
        
        _credentialRotationDuration = meter.CreateHistogram<double>(
            "mongodb.credential.rotation.duration",
            unit: "ms",
            description: "Duration of credential rotation"
        );
    }
    
    public void RecordClientRecreation()
    {
        _clientRecreations.Add(1);
        _logger.LogInformation("📊 MongoDB client recreation recorded");
    }
    
    public void RecordConnectionError()
    {
        _connectionErrors.Add(1);
        _logger.LogError("📊 MongoDB connection error recorded");
    }
    
    public void RecordRotationDuration(TimeSpan duration)
    {
        _credentialRotationDuration.Record(duration.TotalMilliseconds);
        _logger.LogInformation(
            "📊 Credential rotation took {Duration}ms",
            duration.TotalMilliseconds);
    }
}
```

### Logs Structurés

```csharp
_logger.LogInformation(
    "MongoDB credentials rotated. " +
    "LastRotated: {LastRotated}, " +
    "ClientId: {ClientId}, " +
    "Database: {Database}",
    credentials.LastRotated,
    settings.ClusterConfigurator?.GetHashCode(),
    options.Database
);
```

### Health Check

```csharp
// Infrastructure/MongoDB/MongoDbHealthCheck.cs
public class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoDbFactory _mongoDbFactory;
    private readonly ILogger<MongoDbHealthCheck> _logger;
    
    public MongoDbHealthCheck(
        IMongoDbFactory mongoDbFactory,
        ILogger<MongoDbHealthCheck> logger)
    {
        _mongoDbFactory = mongoDbFactory;
        _logger = logger;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _mongoDbFactory.GetDatabase();
            
            // Ping avec timeout court
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cts.Token
            );
            
            return HealthCheckResult.Healthy("MongoDB is reachable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB health check failed");
            return HealthCheckResult.Unhealthy(
                "MongoDB is unreachable",
                exception: ex
            );
        }
    }
}

// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<MongoDbHealthCheck>("mongodb", tags: new[] { "ready", "database" });
```

## Bonnes Pratiques

### ✅ À Faire

1. **Rotation quotidienne** - Credentials renouvelés toutes les 24h minimum
2. **Écriture atomique** - Utiliser write + rename pour éviter lectures partielles
3. **reloadOnChange: true** - Activer rechargement automatique dans AddJsonFile()
4. **Connection pooling** - MongoClient gère le pool, ne pas créer par requête
5. **Singleton factory** - Une seule instance de MongoDbFactory par application
6. **Thread-safety** - Utiliser lock lors de la recréation du client
7. **Grace period** - Attendre 30s avant de dispose l'ancien client
8. **Retry avec backoff** - Ne pas abandonner immédiatement en cas d'erreur
9. **Monitoring** - Logger toutes les rotations et erreurs
10. **Health checks** - Vérifier la connectivité MongoDB régulièrement
11. **Test de connexion** - Ping MongoDB après création du client
12. **IOptionsMonitor** - Utiliser OnChange pour détecter les changements

### ❌ À Éviter

1. **Créer MongoClient par requête** - Très mauvais pour les performances
2. **Bloquer l'application** - Rotation doit être asynchrone et non bloquante
3. **Ignorer les erreurs** - Logger et monitorer toutes les erreurs
4. **Credentials en clair** - Toujours utiliser Vault ou secret manager
5. **Pas de timeout** - Toujours définir des timeouts pour les opérations réseau
6. **Dispose immédiat** - Laisser le temps aux requêtes en cours de finir
7. **Hard-coded credentials** - Toujours externaliser la configuration
8. **Pas de validation** - Tester la connexion avant de remplacer le client
9. **FileSystemWatcher custom** - Utiliser reloadOnChange du framework
10. **Charger manuellement** - Laisser IOptionsMonitor gérer automatiquement

## Scénarios de Test

### Test 1: Rotation Normale

```bash
# 1. Démarrer l'application
kubectl port-forward deployment/johodp-api 8080:80

# 2. Vérifier connexion initiale
curl http://localhost:8080/health/ready

# 3. Déclencher rotation manuelle (forcer le sidecar)
kubectl exec -it deployment/johodp-api -c credential-rotator -- \
  python /scripts/rotate_now.py

# 4. Attendre 5 secondes (file watcher + options change)
sleep 5

# 5. Faire une requête API (devrait utiliser nouveau client)
curl http://localhost:8080/api/users/me \
  -H "Authorization: Bearer $TOKEN"

# 6. Vérifier les logs
kubectl logs deployment/johodp-api -c johodp-api | grep "MongoDB"
# Expected: "MongoDB client created successfully"
```

### Test 2: Échec de Connexion

```bash
# 1. Fournir des credentials invalides
kubectl exec -it deployment/johodp-api -c credential-rotator -- sh
echo '{"connectionString":"mongodb://invalid:invalid@wrong"}' > /config/credentials.json
exit

# 2. Vérifier que l'ancien client est conservé
curl http://localhost:8080/api/users/me
# Expected: 200 OK (ancien client toujours utilisé)

# 3. Vérifier les logs d'erreur
kubectl logs deployment/johodp-api -c johodp-api | tail -20
# Expected: "Failed to create MongoDB client"
```

### Test 3: Performance sous Charge

```bash
# 1. Lancer un load test
hey -n 10000 -c 100 -m GET \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:8080/api/users/me

# 2. En parallèle, déclencher rotation
kubectl exec deployment/johodp-api -c credential-rotator -- \
  python /scripts/rotate_now.py

# 3. Vérifier que le trafic continue sans erreur
# Expected: Taux d'erreur < 0.1%
```

## Alternatives et Évolutions

### Alternative 1: MongoDB Atlas avec IAM

```yaml
# Pas besoin de sidecar avec MongoDB Atlas + AWS IAM
MongoDB:
  ConnectionString: "mongodb+srv://cluster.mongodb.net"
  AuthMechanism: "MONGODB-AWS"
  AwsRegion: "eu-west-1"
```

### Alternative 2: Kubernetes Operator

Utiliser l'[Atlas Operator](https://www.mongodb.com/docs/atlas/atlas-operator/) pour gérer les credentials automatiquement.

### Évolution: Rotation sans Downtime

```csharp
// Utiliser deux clients en parallèle pendant la transition
public class DualClientMongoDbFactory
{
    private IMongoClient _primaryClient;
    private IMongoClient? _secondaryClient;
    
    public IMongoClient GetClient()
    {
        // Essayer primary, fallback sur secondary
        try
        {
            return _primaryClient;
        }
        catch
        {
            return _secondaryClient ?? throw;
        }
    }
}
```

## Références

- [MongoDB Connection String](https://www.mongodb.com/docs/manual/reference/connection-string/)
- [MongoClient Best Practices](https://www.mongodb.com/docs/drivers/csharp/current/fundamentals/connection/)
- [IOptionsMonitor Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#ioptionsmonitor)
- [Sidecar Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/sidecar)
- [HashiCorp Vault](https://www.vaultproject.io/docs)

---

**Dernière mise à jour:** 3 décembre 2025  
**Équipe:** Johodp Platform Engineering
