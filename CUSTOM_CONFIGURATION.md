# CustomConfiguration - Branding et Langues Partagés

## Vue d'ensemble

`CustomConfiguration` est un agrégat qui appartient à un `Client` et permet de partager une configuration de branding et de langues entre plusieurs tenants. Un Client peut créer plusieurs CustomConfigurations pour différents cas d'usage (production, staging, marques différentes, etc.).

## Architecture

### Relation avec Client et Tenant

```
Client (1) ←── (N) CustomConfiguration ←── (N) Tenant
```

- **Un Client** peut créer **plusieurs CustomConfigurations**
- **Une CustomConfiguration** appartient à **un seul Client** (propriétaire)
- **Une CustomConfiguration** peut être utilisée par **plusieurs Tenants**
- **Un Tenant** peut référencer **zéro ou une CustomConfiguration** (optionnel)

### Cas d'usage

**Exemple 1 : SaaS Multi-Client**
```
CustomConfiguration "Enterprise"
├─ Tenant "acme-corp"
├─ Tenant "globex-inc"
└─ Tenant "initech"
→ Tous partagent le même branding bleu/blanc et supportent FR/EN/ES
```

**Exemple 2 : Environnements**
```
CustomConfiguration "Production"
├─ Tenant "prod-eu"
├─ Tenant "prod-us"
└─ Tenant "prod-asia"
→ Même branding, différentes régions
```

**Exemple 3 : White-Label**
```
CustomConfiguration "Brand-A"
└─ Tenant "brand-a-customer1"

CustomConfiguration "Brand-B"
└─ Tenant "brand-b-customer1"
→ Chaque marque a son propre branding
```

## Structure du Domain

### Agrégat CustomConfiguration

**Fichier :** `src/Johodp.Domain/CustomConfigurations/Aggregates/CustomConfiguration.cs`

**Propriétés :**
```csharp
public class CustomConfiguration : AggregateRoot
{
    public CustomConfigurationId Id { get; private set; }
    public ClientId ClientId { get; private set; } // Owner of this configuration
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Branding
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? BackgroundImageUrl { get; private set; }
    public string? CustomCss { get; private set; }

    // Languages - simple list of BCP47 language codes
    private readonly List<string> _supportedLanguages = new();
    public IReadOnlyList<string> SupportedLanguages => _supportedLanguages.AsReadOnly();
    public string DefaultLanguage { get; private set; } = "fr-FR";
}
```

**Méthodes du domaine :**
- `Create(ClientId clientId, string name, string? description, string? defaultLanguage)` : Création
- `UpdateBranding(...)` : Mise à jour des couleurs, logo, CSS
- `AddSupportedLanguage(string languageCode)` : Ajouter une langue (BCP47)
- `RemoveSupportedLanguage(string languageCode)` : Retirer une langue
- `SetDefaultLanguage(string languageCode)` : Définir la langue par défaut
- `UpdateDescription(string? description)` : Modifier la description
- `Activate()` / `Deactivate()` : Activer/Désactiver

### Value Objects

**CustomConfigurationId**
```csharp
public class CustomConfigurationId : ValueObject
{
    public Guid Value { get; private set; }
    
    public static CustomConfigurationId Create() => new() { Value = Guid.NewGuid() };
    public static CustomConfigurationId From(Guid value) => new() { Value = value };
}
```

## Application Layer

### DTOs

**CustomConfigurationDto.cs**
```csharp
public class CustomConfigurationDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; } // Owner client
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Branding
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }

    // Languages - simple list of BCP47 language codes
    public List<string> SupportedLanguages { get; set; } = new();
    public string DefaultLanguage { get; set; } = "fr-FR";
}

public class CreateCustomConfigurationDto
{
    public Guid ClientId { get; set; } // Required: the client that owns this configuration
    public string Name { get; set; }
    public string? Description { get; set; }
    
    // Optional branding
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    
    // Languages - simple language codes (BCP47 format: fr-FR, en-US, etc.)
    public string? DefaultLanguage { get; set; }
    public List<string>? AdditionalLanguages { get; set; }
}

public class UpdateCustomConfigurationDto
{
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    
    // Branding
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    
    // Languages
    public List<string>? SupportedLanguages { get; set; }
    public string? DefaultLanguage { get; set; }
}
```

### Commands

**CreateCustomConfigurationCommand**
```bash
POST /api/custom-configurations
{
  "clientId": "a1b2c3d4-...",
  "name": "Enterprise",
  "description": "Configuration for enterprise clients",
  "defaultLanguage": "fr-FR",
  "additionalLanguages": ["en-US", "es-ES"],
  "primaryColor": "#003366",
  "secondaryColor": "#FFD700",
  "logoUrl": "https://cdn.example.com/logo.png"
}
```

**UpdateCustomConfigurationCommand**
```bash
PUT /api/custom-configurations/{id}
{
  "description": "Updated description",
  "supportedLanguages": ["fr-FR", "en-US", "de-DE"],
  "defaultLanguage": "en-US"
}
```

### Queries

**GetCustomConfigurationByIdQuery**
```bash
GET /api/custom-configurations/{id}
```

**GetAllCustomConfigurationsQuery**
```bash
GET /api/custom-configurations
```

## Infrastructure Layer

### Repository

**ICustomConfigurationRepository**
```csharp
public interface ICustomConfigurationRepository
{
    Task<CustomConfiguration?> GetByIdAsync(CustomConfigurationId id);
    Task<CustomConfiguration?> GetByNameAsync(string name);
    Task<CustomConfiguration> AddAsync(CustomConfiguration customConfiguration);
    Task<CustomConfiguration> UpdateAsync(CustomConfiguration customConfiguration);
    Task<bool> DeleteAsync(CustomConfigurationId id);
    Task<IEnumerable<CustomConfiguration>> GetAllAsync();
    Task<IEnumerable<CustomConfiguration>> GetActiveAsync();
}
```

### EF Configuration

**CustomConfigurationConfiguration.cs**

```csharp
builder.ToTable("custom_configurations");

// ID conversion
builder.Property(c => c.Id)
    .HasConversion(
        id => id.Value,
        value => CustomConfigurationId.From(value))
    .HasColumnName("id");

// Name with unique index
builder.Property(c => c.Name)
    .IsRequired()
    .HasMaxLength(100)
    .HasColumnName("name");

builder.HasIndex(c => c.Name).IsUnique();

// Supported languages stored as semicolon-separated string
builder.Property<string>("_supportedLanguages")
    .HasColumnName("supported_languages")
    .HasConversion(
        languages => string.Join(";", languages),
        value => string.IsNullOrWhiteSpace(value) 
            ? new List<string>() 
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList());

// Default language
builder.Property(c => c.DefaultLanguage)
    .IsRequired()
    .HasMaxLength(10)
    .HasColumnName("default_language");
```

### Schéma de table

```sql
CREATE TABLE custom_configurations (
    id UUID PRIMARY KEY,
    client_id UUID NOT NULL, -- Owner client (FK to clients.id)
    name VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(500),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP,
    
    -- Branding
    primary_color VARCHAR(50),
    secondary_color VARCHAR(50),
    logo_url VARCHAR(500),
    background_image_url VARCHAR(500),
    custom_css TEXT,
    
    -- Languages
    supported_languages TEXT NOT NULL, -- Semicolon-separated: "fr-FR;en-US;es-ES"
    default_language VARCHAR(10) NOT NULL DEFAULT 'fr-FR'
);
```

## Utilisation avec Tenant

### Associer une CustomConfiguration à un Tenant

```bash
# 1. Créer une CustomConfiguration
POST /api/custom-configurations
{
  "name": "Enterprise",
  "defaultLanguage": "fr-FR",
  "additionalLanguages": ["en-US"],
  "primaryColor": "#003366"
}
→ Returns: { "id": "a1b2c3d4-..." }

# 2. Créer un Tenant avec cette configuration
POST /api/tenant
{
  "name": "acme-corp",
  "displayName": "ACME Corporation",
  "customConfigurationId": "a1b2c3d4-...",
  "allowedReturnUrls": ["https://acme.com/callback"]
}
```

### Modifier l'association

```bash
PUT /api/tenant/{tenantId}
{
  "customConfigurationId": "e5f6g7h8-..."  # Change to another configuration
}

# Ou retirer l'association
PUT /api/tenant/{tenantId}
{
  "customConfigurationId": null
}
```

## Flux de Récupération du Branding

Lorsqu'un utilisateur accède à une page de login :

```
1. Frontend → GET /api/tenant/acme-corp
   ↓
2. IDP récupère Tenant "acme-corp"
   ↓
3. Si Tenant.CustomConfigurationId existe :
   → Récupère CustomConfiguration
   → Retourne branding de CustomConfiguration
   ↓
4. Sinon :
   → Retourne valeurs par défaut ou null
```

## Avantages

✅ **Réutilisabilité** : Une configuration peut être partagée par plusieurs tenants
✅ **Maintenance** : Modifier une CustomConfiguration met à jour tous les tenants associés
✅ **Flexibilité** : Un tenant peut avoir sa propre configuration ou partager une configuration commune
✅ **Séparation** : Le branding est séparé de la logique d'isolation tenant
✅ **Langues BCP47** : Standard international pour les codes de langue
✅ **Simplicité** : Liste simple de codes de langues (pas de complexité inutile)

## Langues BCP47

### Format

Les codes de langues suivent le standard **BCP47** :
- **Langue seule** : `fr`, `en`, `es`, `de`
- **Langue + Région** : `fr-FR`, `en-US`, `en-GB`, `es-ES`, `de-DE`

### Exemples

```json
{
  "supportedLanguages": [
    "fr-FR",  // Français (France)
    "en-US",  // Anglais (États-Unis)
    "en-GB",  // Anglais (Royaume-Uni)
    "es-ES",  // Espagnol (Espagne)
    "de-DE",  // Allemand (Allemagne)
    "it-IT",  // Italien (Italie)
    "pt-BR",  // Portugais (Brésil)
    "zh-CN",  // Chinois (Chine)
    "ja-JP"   // Japonais (Japon)
  ],
  "defaultLanguage": "fr-FR"
}
```

### Recherche de langue

Le système peut rechercher une langue :
- Par code exact : `"fr-FR"`
- Par langue : `"fr"` → trouve `"fr-FR"`, `"fr-BE"`, etc.
- Par défaut : Si langue non trouvée → utilise `DefaultLanguage`

## Migration

Si vous avez des tenants existants avec branding direct :

```sql
-- 1. Créer une CustomConfiguration par défaut
INSERT INTO custom_configurations (id, name, is_active, created_at, default_language, supported_languages)
VALUES (gen_random_uuid(), 'Default', true, NOW(), 'fr-FR', 'fr-FR;en-US');

-- 2. Associer tous les tenants à cette configuration
UPDATE tenants 
SET custom_configuration_id = (SELECT id FROM custom_configurations WHERE name = 'Default')
WHERE custom_configuration_id IS NULL;

-- 3. Supprimer les anciennes colonnes de branding de tenants (après migration complète)
-- ALTER TABLE tenants DROP COLUMN primary_color;
-- ALTER TABLE tenants DROP COLUMN secondary_color;
-- ...
```

## TODO

- [ ] API Controller pour CustomConfiguration (CRUD)
- [ ] Queries pour rechercher les configurations
- [ ] Validation des codes BCP47
- [ ] Cache pour les configurations fréquemment utilisées
- [ ] Interface d'administration pour gérer les configurations
- [ ] Migration automatique des données existantes
