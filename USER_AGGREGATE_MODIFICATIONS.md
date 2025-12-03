# 📝 Guide de Modification de l'Agrégat User

## Vue d'Ensemble

Ce document explique **comment ajouter ou supprimer une propriété** à l'agrégat `User` et propager les changements dans toute la couche de persistance (Entity Framework Core).

---

## 🗂️ Architecture des Fichiers

L'agrégat `User` est défini dans plusieurs fichiers répartis dans les couches Domain et Infrastructure :

| Fichier | Localisation | Responsabilité |
|---------|--------------|----------------|
| **User.cs** | `src/Johodp.Domain/Users/Aggregates/User.cs` | ✅ **Agrégat DDD** - Logique métier, propriétés, méthodes, invariants |
| **UserConfiguration.cs** | `src/Johodp.Infrastructure/Persistence/Configurations/UserConfiguration.cs` | ✅ **Configuration EF Core** - Mapping objet → table, colonnes, index, FK |
| **JohodpDbContext.cs** | `src/Johodp.Infrastructure/Persistence/DbContext/JohodpDbContext.cs` | ✅ **Contexte de persistance** - DbSet, application des configurations |
| **Migrations/** | `src/Johodp.Infrastructure/Migrations/` | ✅ **Migrations EF Core** - Scripts de modification de schéma SQL |

---

## ➕ Ajouter une Propriété à l'Agrégat User

### Exemple : Ajouter `PhoneNumber` (string, optionnel, max 20 caractères)

### Étape 1 : Modifier la Classe Domain (`User.cs`)

**Fichier:** `src/Johodp.Domain/Users/Aggregates/User.cs`

**Ajouter la propriété dans la classe `User` :**

```csharp
public class User : AggregateRoot
{
    public UserId Id { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public bool EmailConfirmed { get; private set; }
    public bool IsActive => Status == UserStatus.Active;
    public bool MFAEnabled { get; private set; }
    public UserStatus Status { get; private set; } = UserStatus.PendingActivation;
    public DateTime? ActivatedAt { get; private set; }
    public string? PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    
    // ✅ NOUVELLE PROPRIÉTÉ
    public string? PhoneNumber { get; private set; }

    // Tenant, role, scope...
    public TenantId TenantId { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public string Scope { get; private set; } = null!;

    private User() { }
    
    // ...
}
```

**Ajouter validation et setter si nécessaire :**

```csharp
public void SetPhoneNumber(string? phoneNumber)
{
    if (!string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.Length > 20)
        throw new ArgumentException("Phone number cannot exceed 20 characters", nameof(phoneNumber));
    
    PhoneNumber = phoneNumber?.Trim();
    UpdatedAt = DateTime.UtcNow;
}
```

---

### Étape 2 : Modifier la Configuration EF Core (`UserConfiguration.cs`)

**Fichier:** `src/Johodp.Infrastructure/Persistence/Configurations/UserConfiguration.cs`

**Ajouter le mapping de la propriété :**

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        // ... autres propriétés existantes ...

        builder.Property(x => x.LastName)
            .HasMaxLength(50)
            .IsRequired();

        // ✅ NOUVELLE PROPRIÉTÉ
        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired(false);  // Nullable

        builder.Property(x => x.EmailConfirmed)
            .HasDefaultValue(false);

        // ... reste de la configuration ...
    }
}
```

**Options de configuration courantes :**

```csharp
// String nullable avec longueur max
builder.Property(x => x.PhoneNumber)
    .HasMaxLength(20)
    .IsRequired(false);

// String obligatoire avec longueur max
builder.Property(x => x.Department)
    .HasMaxLength(100)
    .IsRequired();

// Entier nullable
builder.Property(x => x.Age)
    .IsRequired(false);

// DateTime nullable avec type PostgreSQL
builder.Property(x => x.BirthDate)
    .HasColumnType("timestamp with time zone")
    .IsRequired(false);

// Booléen avec valeur par défaut
builder.Property(x => x.IsVerified)
    .HasDefaultValue(false);

// Decimal avec précision (pour montants monétaires)
builder.Property(x => x.Balance)
    .HasColumnType("decimal(18,2)")
    .IsRequired();

// Enum stocké comme int
builder.Property(x => x.AccountType)
    .HasConversion<int>()
    .IsRequired();
```

---

### Étape 3 : Créer la Migration EF Core

**Commande à exécuter depuis la racine du projet :**

```bash
dotnet ef migrations add AddPhoneNumberToUser `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

**Ce que fait cette commande :**
1. ✅ Analyse les différences entre le modèle actuel (`User.cs` + `UserConfiguration.cs`) et le dernier snapshot EF Core
2. ✅ Génère un fichier de migration dans `src/Johodp.Infrastructure/Migrations/`
3. ✅ Nom du fichier : `YYYYMMDDHHMMSS_AddPhoneNumberToUser.cs`
4. ✅ Contient les méthodes `Up()` (ajout colonne) et `Down()` (rollback)

**Fichier généré (exemple) :**

```csharp
// src/Johodp.Infrastructure/Migrations/20241203100000_AddPhoneNumberToUser.cs

public partial class AddPhoneNumberToUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PhoneNumber",
            schema: "dbo",
            table: "users",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PhoneNumber",
            schema: "dbo",
            table: "users");
    }
}
```

---

### Étape 4 : Appliquer la Migration à la Base de Données

**Option 1 : Mise à jour automatique (Développement)**

```bash
dotnet ef database update `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

**Option 2 : Générer un script SQL (Production recommandée)**

```bash
dotnet ef migrations script `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext `
  --idempotent `
  --output migration-add-phone.sql
```

**Ensuite appliquer manuellement :**

```bash
psql -U johodp_user -d johodp -f migration-add-phone.sql
```

---

### Étape 5 : Vérifier la Migration

**Inspecter la table `users` dans PostgreSQL :**

```sql
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_schema = 'dbo'
  AND table_name = 'users'
ORDER BY ordinal_position;
```

**Résultat attendu (nouvelles lignes) :**

```
column_name  | data_type        | character_maximum_length | is_nullable
-------------+------------------+--------------------------+-------------
PhoneNumber  | character varying| 20                       | YES
```

---

## ➖ Supprimer une Propriété de l'Agrégat User

### Exemple : Supprimer `PhoneNumber`

### Étape 1 : Retirer la Propriété de `User.cs`

**Fichier:** `src/Johodp.Domain/Users/Aggregates/User.cs`

```csharp
public class User : AggregateRoot
{
    public UserId Id { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    
    // ❌ SUPPRIMER cette ligne
    // public string? PhoneNumber { get; private set; }
    
    // ... reste des propriétés
}
```

**Supprimer également les méthodes associées (ex: `SetPhoneNumber()`).**

---

### Étape 2 : Retirer le Mapping de `UserConfiguration.cs`

**Fichier:** `src/Johodp.Infrastructure/Persistence/Configurations/UserConfiguration.cs`

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // ... autres configurations ...

        // ❌ SUPPRIMER ce mapping
        // builder.Property(x => x.PhoneNumber)
        //     .HasMaxLength(20)
        //     .IsRequired(false);

        // ... reste de la configuration ...
    }
}
```

---

### Étape 3 : Créer la Migration de Suppression

```bash
dotnet ef migrations add RemovePhoneNumberFromUser `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

**Fichier généré :**

```csharp
public partial class RemovePhoneNumberFromUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PhoneNumber",
            schema: "dbo",
            table: "users");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PhoneNumber",
            schema: "dbo",
            table: "users",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);
    }
}
```

---

### Étape 4 : Appliquer la Migration

**Développement :**

```bash
dotnet ef database update `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

**Production (script SQL) :**

```bash
dotnet ef migrations script `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext `
  --idempotent `
  --output migration-remove-phone.sql
```

---

## 🛠️ Commandes EF Core Essentielles

### Créer une Migration

```bash
dotnet ef migrations add <NomMigration> `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

**Exemples de noms :**
- `AddPhoneNumberToUser`
- `RemovePhoneNumberFromUser`
- `UpdateUserEmailMaxLength`
- `AddUserAvatarUrlColumn`

---

### Appliquer une Migration (Dev)

```bash
dotnet ef database update `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

---

### Générer Script SQL (Production)

```bash
# Script idempotent (peut être rejoué sans erreur)
dotnet ef migrations script `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext `
  --idempotent `
  --output migration-johodp.sql
```

---

### Annuler la Dernière Migration

**Si la migration n'a PAS encore été appliquée à la DB :**

```bash
dotnet ef migrations remove `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

**Si la migration a DÉJÀ été appliquée :**

```bash
# 1. Revenir à la migration précédente
dotnet ef database update <NomMigrationPrécédente> `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext

# 2. Supprimer la migration
dotnet ef migrations remove `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

---

### Lister les Migrations

```bash
dotnet ef migrations list `
  --project src/Johodp.Infrastructure `
  --startup-project src/Johodp.Api `
  --context JohodpDbContext
```

**Exemple de sortie :**

```
20251201153543_InitialCreate (Applied)
20251203021924_RenameIdentityServerTablesToSnakeCase (Applied)
20241203100000_AddPhoneNumberToUser (Pending)
```

---

## 📋 Checklist Complète (Ajout de Propriété)

### Phase 1 : Modifications Code

- [ ] **1.1** Ajouter la propriété dans `User.cs` (Domain)
  - Définir le type (string, int, DateTime, bool, etc.)
  - Décider si nullable (`?`) ou obligatoire
  - Ajouter `{ get; private set; }` pour encapsulation
  
- [ ] **1.2** Ajouter validation dans méthode setter si nécessaire
  - Longueur max pour strings
  - Plage de valeurs pour nombres
  - Format pour dates
  
- [ ] **1.3** Mettre à jour constructeur/factory si propriété obligatoire
  - Ajouter paramètre dans `Create()`
  - Initialiser dans le constructeur privé
  
- [ ] **1.4** Ajouter mapping dans `UserConfiguration.cs` (Infrastructure)
  - `HasMaxLength()` pour strings
  - `IsRequired()` ou `IsRequired(false)` pour nullabilité
  - `HasColumnType()` pour types spécifiques (PostgreSQL)
  - `HasDefaultValue()` si valeur par défaut nécessaire

### Phase 2 : Migration Base de Données

- [ ] **2.1** Créer migration EF Core
  ```bash
  dotnet ef migrations add <NomDescriptif> -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
  ```

- [ ] **2.2** Vérifier le fichier de migration généré
  - Inspecter `Up()` : ajout de colonne correct ?
  - Inspecter `Down()` : rollback correct ?
  - Vérifier type SQL généré (PostgreSQL)
  
- [ ] **2.3** Appliquer migration (DEV)
  ```bash
  dotnet ef database update -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
  ```

- [ ] **2.4** Générer script SQL (PROD)
  ```bash
  dotnet ef migrations script --idempotent -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext -o migration.sql
  ```

### Phase 3 : Tests & Validation

- [ ] **3.1** Vérifier schéma PostgreSQL
  ```sql
  \d+ dbo.users
  ```

- [ ] **3.2** Tester insertion avec nouvelle propriété
  ```csharp
  var user = User.Create("john@acme.com", "John", "Doe", tenantId);
  user.SetPhoneNumber("+33612345678");
  await _repository.AddAsync(user);
  ```

- [ ] **3.3** Vérifier que les tests d'intégration passent
  ```bash
  dotnet test tests/Johodp.Tests/Johodp.IntegrationTests.csproj
  ```

- [ ] **3.4** Mettre à jour les DTOs/Contracts si nécessaire
  - `RegisterUserRequest.cs`
  - `UserResponse.cs`
  - Mapper dans Application layer

### Phase 4 : Documentation

- [ ] **4.1** Mettre à jour `DOMAIN_MODEL.md` si propriété métier importante

- [ ] **4.2** Documenter contraintes dans XML comments de `User.cs`

- [ ] **4.3** Ajouter exemples d'utilisation dans README si pertinent

---

## 📋 Checklist Complète (Suppression de Propriété)

### Phase 1 : Modifications Code

- [ ] **1.1** Supprimer la propriété de `User.cs`
  - Retirer `public Type PropertyName { get; private set; }`
  
- [ ] **1.2** Supprimer méthodes setter/getter associées
  - Ex: `SetPhoneNumber()`, `UpdatePhoneNumber()`
  
- [ ] **1.3** Supprimer du constructeur/factory si propriété y était initialisée

- [ ] **1.4** Supprimer mapping de `UserConfiguration.cs`
  - Retirer `builder.Property(x => x.PropertyName)...`

- [ ] **1.5** Rechercher utilisations dans le code
  ```bash
  grep -r "PhoneNumber" src/
  ```

### Phase 2 : Migration Base de Données

- [ ] **2.1** Créer migration EF Core
  ```bash
  dotnet ef migrations add Remove<PropertyName>FromUser -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
  ```

- [ ] **2.2** Vérifier migration générée
  - `Up()` : `DropColumn()` correct ?
  - `Down()` : `AddColumn()` restaure bien l'ancienne colonne ?

- [ ] **2.3** **ATTENTION DATA LOSS** : Sauvegarder données si nécessaire
  ```sql
  -- Backup avant suppression
  CREATE TABLE users_backup AS SELECT * FROM dbo.users;
  ```

- [ ] **2.4** Appliquer migration (DEV)
  ```bash
  dotnet ef database update -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
  ```

- [ ] **2.5** Générer script SQL (PROD)
  ```bash
  dotnet ef migrations script --idempotent -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext -o migration.sql
  ```

### Phase 3 : Tests & Validation

- [ ] **3.1** Vérifier schéma PostgreSQL (colonne supprimée)
  ```sql
  \d+ dbo.users
  ```

- [ ] **3.2** Tester que l'app fonctionne sans la propriété

- [ ] **3.3** Vérifier tests d'intégration
  ```bash
  dotnet test tests/Johodp.Tests/Johodp.IntegrationTests.csproj
  ```

- [ ] **3.4** Supprimer des DTOs/Contracts
  - Retirer de `UserResponse.cs`
  - Supprimer du mapper Application layer

### Phase 4 : Documentation

- [ ] **4.1** Mettre à jour `DOMAIN_MODEL.md`

- [ ] **4.2** Documenter raison de la suppression (changelog)

---

## 🔍 Cas Particuliers

### Ajouter une Propriété avec Value Object

**Exemple : Ajouter `Address` (Value Object)**

**1. Créer le Value Object :**

```csharp
// src/Johodp.Domain/Users/ValueObjects/Address.cs
public class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string postalCode, string country)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }

    public static Address Create(string street, string city, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street is required", nameof(street));
        
        return new Address(street, city, postalCode, country);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
    }
}
```

**2. Ajouter dans `User.cs` :**

```csharp
public class User : AggregateRoot
{
    // ... autres propriétés ...
    public Address? Address { get; private set; }

    public void SetAddress(Address address)
    {
        Address = address;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**3. Configuration EF Core (Owned Entity) :**

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // ... autres configurations ...

        // Address stockée comme colonnes dans table users
        builder.OwnsOne(x => x.Address, addressBuilder =>
        {
            addressBuilder.Property(a => a.Street)
                .HasColumnName("address_street")
                .HasMaxLength(200)
                .IsRequired(false);

            addressBuilder.Property(a => a.City)
                .HasColumnName("address_city")
                .HasMaxLength(100)
                .IsRequired(false);

            addressBuilder.Property(a => a.PostalCode)
                .HasColumnName("address_postal_code")
                .HasMaxLength(20)
                .IsRequired(false);

            addressBuilder.Property(a => a.Country)
                .HasColumnName("address_country")
                .HasMaxLength(100)
                .IsRequired(false);
        });
    }
}
```

**4. Créer migration :**

```bash
dotnet ef migrations add AddAddressToUser -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
```

**Résultat SQL :**

```sql
ALTER TABLE dbo.users ADD COLUMN address_street varchar(200) NULL;
ALTER TABLE dbo.users ADD COLUMN address_city varchar(100) NULL;
ALTER TABLE dbo.users ADD COLUMN address_postal_code varchar(20) NULL;
ALTER TABLE dbo.users ADD COLUMN address_country varchar(100) NULL;
```

---

### Renommer une Propriété

**Exemple : Renommer `FirstName` → `GivenName`**

**⚠️ Attention : Renommer = Supprimer + Ajouter (perte de données sans migration custom)**

**Option 1 : Migration Custom (Recommandé - Conserve les données)**

```csharp
// Migration générée automatiquement (ne pas utiliser directement)
// dotnet ef migrations add RenameFirstNameToGivenName ...

public partial class RenameFirstNameToGivenName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ✅ Renommer colonne (conserve données)
        migrationBuilder.RenameColumn(
            name: "FirstName",
            schema: "dbo",
            table: "users",
            newName: "GivenName");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "GivenName",
            schema: "dbo",
            table: "users",
            newName: "FirstName");
    }
}
```

**Steps :**

1. Renommer dans `User.cs` : `FirstName` → `GivenName`
2. Renommer dans `UserConfiguration.cs` : `x => x.FirstName` → `x => x.GivenName`
3. Créer migration : `dotnet ef migrations add RenameFirstNameToGivenName ...`
4. **Modifier manuellement le fichier de migration** pour utiliser `RenameColumn()` au lieu de `DropColumn()` + `AddColumn()`

---

### Changer le Type d'une Propriété

**Exemple : `Age` de `int` → `int?` (nullable)**

**1. Modifier `User.cs` :**

```csharp
// Avant
public int Age { get; private set; }

// Après
public int? Age { get; private set; }
```

**2. Modifier `UserConfiguration.cs` :**

```csharp
// Avant
builder.Property(x => x.Age).IsRequired();

// Après
builder.Property(x => x.Age).IsRequired(false);
```

**3. Créer migration :**

```bash
dotnet ef migrations add MakeAgeNullable -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
```

**4. Migration générée :**

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AlterColumn<int>(
        name: "Age",
        schema: "dbo",
        table: "users",
        type: "integer",
        nullable: true,
        oldClrType: typeof(int),
        oldType: "integer");
}
```

---

## 🚨 Pièges Courants

### ❌ Oublier de Configurer dans `UserConfiguration.cs`

**Symptôme :** Migration crée une colonne avec mauvais type ou conventions par défaut.

**Solution :** Toujours ajouter mapping explicite dans `UserConfiguration.cs`.

---

### ❌ Migration Appliquée mais Code Non Modifié

**Symptôme :** Colonne existe dans DB mais propriété absente dans `User.cs`.

**Solution :** Synchroniser code + migration. Si migration déjà appliquée, créer nouvelle migration pour supprimer colonne.

---

### ❌ Supprimer Propriété sans Migration

**Symptôme :** Propriété supprimée dans code mais colonne existe toujours dans DB.

**Solution :** Toujours créer migration de suppression (`DropColumn`).

---

### ❌ Renommer sans `RenameColumn()` (Perte de Données)

**Symptôme :** EF Core génère `DropColumn()` + `AddColumn()` → perte de données.

**Solution :** Modifier manuellement migration pour utiliser `RenameColumn()`.

---

### ❌ Confondre les 2 DbContext (JohodpDbContext vs PersistedGrantDbContext)

**Symptôme :** Migration créée dans mauvais contexte.

**Vérifier :** Toujours spécifier `--context JohodpDbContext` pour l'agrégat User.

---

## 📚 Références

### Documentation
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [EF Core Fluent API](https://learn.microsoft.com/en-us/ef/core/modeling/)
- [PostgreSQL Data Types](https://www.postgresql.org/docs/current/datatype.html)

### Fichiers Clés du Projet
- `src/Johodp.Domain/Users/Aggregates/User.cs` - Agrégat User (DDD)
- `src/Johodp.Infrastructure/Persistence/Configurations/UserConfiguration.cs` - Mapping EF Core
- `src/Johodp.Infrastructure/Persistence/DbContext/JohodpDbContext.cs` - Contexte de persistance
- `src/Johodp.Infrastructure/Migrations/` - Migrations EF Core
- `MIGRATIONS_STRATEGY.md` - Stratégie de migration du projet
- `DOMAIN_MODEL.md` - Modèle de domaine détaillé

---

## 🎯 Résumé (TL;DR)

### Ajouter une Propriété

```bash
# 1. Ajouter dans User.cs (Domain)
public string? PhoneNumber { get; private set; }

# 2. Mapper dans UserConfiguration.cs (Infrastructure)
builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired(false);

# 3. Créer migration
dotnet ef migrations add AddPhoneNumberToUser -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext

# 4. Appliquer
dotnet ef database update -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
```

### Supprimer une Propriété

```bash
# 1. Retirer de User.cs (Domain)
# public string? PhoneNumber { get; private set; } ❌ DELETE

# 2. Retirer de UserConfiguration.cs (Infrastructure)
# builder.Property(x => x.PhoneNumber)... ❌ DELETE

# 3. Créer migration
dotnet ef migrations add RemovePhoneNumberFromUser -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext

# 4. Appliquer
dotnet ef database update -p src/Johodp.Infrastructure -s src/Johodp.Api -c JohodpDbContext
```

**Règle d'or :** Toujours synchroniser Code (Domain + Infrastructure) ↔ Migrations ↔ Base de Données.

---

**Dernière mise à jour :** 2024-12-03  
**Contexte :** Johodp - OAuth2/OIDC Multi-Tenant Platform  
**Stack :** .NET 9 + EF Core 9 + PostgreSQL
