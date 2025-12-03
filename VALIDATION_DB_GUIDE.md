# Validation avec Accès Base de Données - Guide

## 📋 Règle Générale

**Les validations DB doivent être faites dans `HandleCore`, pas dans les validateurs.**

## 🎯 Les 3 Approches

### ✅ **Approche 1 : Validation dans HandleCore (RECOMMANDÉE)**

**Quand l'utiliser :**
- Checks d'unicité (nom existe déjà)
- Validations de foreign keys (tenant existe)
- Validations dépendantes de l'état DB
- Validations métier complexes

**Avantages :**
- ✅ 1 seul round-trip DB
- ✅ Pas de race conditions
- ✅ Performance optimale
- ✅ Cohérence transactionnelle

**Exemple :**

```csharp
public class CreateClientCommandHandler : BaseHandler<CreateClientCommand, Result<ClientDto>>
{
    private readonly IClientRepository _clientRepository;

    public CreateClientCommandHandler(
        IClientRepository clientRepository,
        ILogger<CreateClientCommandHandler> logger,
        IValidator<CreateClientCommand>? validator = null) 
        : base(logger, validator)
    {
        _clientRepository = clientRepository;
    }

    protected override async Task<Result<ClientDto>> HandleCore(
        CreateClientCommand command, 
        CancellationToken cancellationToken)
    {
        // ✅ Validation DB dans HandleCore
        var existingClient = await _clientRepository.GetByNameAsync(command.Data.ClientName);
        if (existingClient != null)
        {
            return Result<ClientDto>.Failure(Error.Conflict(
                "CLIENT_ALREADY_EXISTS",
                $"Client '{command.Data.ClientName}' already exists"));
        }

        // Business logic
        var client = Client.Create(...);
        await _clientRepository.AddAsync(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ClientDto>.Success(MapToDto(client));
    }
}
```

**Enregistrement (pas de validateur DB) :**

```csharp
// Dans ServiceCollectionExtensions.cs
services.AddScoped<CreateClientCommandHandler>();
services.AddScoped<IValidator<CreateClientCommand>, CreateClientCommandValidator>(); 
// ← Validateur SANS accès DB
```

---

### ⚠️ **Approche 2 : Validateur avec DB (RARE)**

**Quand l'utiliser :**
- Règles métier complexes à réutiliser
- Validations qui ne causent pas de race conditions
- Besoin de séparer validation et business logic

**Inconvénients :**
- ❌ 2 round-trips DB (validation + business logic)
- ❌ Race conditions possibles
- ❌ -30% performance

**Exemple :**

```csharp
public class CreateClientCommandValidatorWithDb : IValidator<CreateClientCommand>
{
    private readonly IClientRepository _clientRepository;

    public CreateClientCommandValidatorWithDb(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<IDictionary<string, string[]>> ValidateAsync(CreateClientCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        // ✅ TOUJOURS faire les validations synchrones FIRST (fail-fast)
        if (string.IsNullOrWhiteSpace(request.Data?.ClientName))
        {
            errors["ClientName"] = new[] { "Required" };
            return errors; // Early return, pas de DB call
        }

        // ⚠️ DB validation (seulement si validations sync passent)
        var exists = await _clientRepository.GetByNameAsync(request.Data.ClientName);
        if (exists != null)
        {
            errors["ClientName"] = new[] { "Client already exists" };
        }

        return errors;
    }
}
```

**Enregistrement :**

```csharp
// Remplacer CreateClientCommandValidator par la version avec DB
services.AddScoped<IValidator<CreateClientCommand>, CreateClientCommandValidatorWithDb>();
```

**Handler (simplifié car validation DB déjà faite) :**

```csharp
protected override async Task<Result<ClientDto>> HandleCore(...)
{
    // ✅ Pas besoin de re-vérifier, validation déjà faite
    var client = Client.Create(...);
    await _clientRepository.AddAsync(client);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    
    return Result<ClientDto>.Success(MapToDto(client));
}
```

---

### 🎯 **Approche 3 : Hybride (BEST OF BOTH)**

**Quand l'utiliser :**
- Validations simples dans le validateur
- Validations DB dans HandleCore
- Meilleur compromis performance/clarté

**Exemple :**

```csharp
// Validateur : UNIQUEMENT validations synchrones
public class CreateClientCommandValidator : IValidator<CreateClientCommand>
{
    public Task<IDictionary<string, string[]>> ValidateAsync(CreateClientCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        // ✅ Validations format/longueur/règles simples
        if (string.IsNullOrWhiteSpace(request.Data?.ClientName))
            errors["ClientName"] = new[] { "Required" };
        else if (request.Data.ClientName.Length < 3)
            errors["ClientName"] = new[] { "Min 3 characters" };
        else if (!IsValidFormat(request.Data.ClientName))
            errors["ClientName"] = new[] { "Invalid format" };

        // ❌ PAS de DB checks ici

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }
}

// Handler : Validations DB + Business logic
protected override async Task<Result<ClientDto>> HandleCore(...)
{
    // ✅ DB validation
    var exists = await _clientRepository.GetByNameAsync(command.Data.ClientName);
    if (exists != null)
        return Result<ClientDto>.Failure(Error.Conflict(...));

    // Business logic
    var client = Client.Create(...);
    await _clientRepository.AddAsync(client);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<ClientDto>.Success(MapToDto(client));
}
```

---

## 📊 Comparaison Performance

| Approche | Round-trips DB | Performance | Race Conditions | Complexité |
|----------|----------------|-------------|-----------------|------------|
| **HandleCore (1)** | 1 | ⚡⚡⚡ Excellent | ✅ Non | ✅ Simple |
| **Validateur DB (2)** | 2 | ⚠️ Moyen (-30%) | ❌ Oui | ⚠️ Moyenne |
| **Hybride (3)** | 1 | ⚡⚡⚡ Excellent | ✅ Non | ✅✅ Optimal |

---

## 🏆 Recommandations Finales

### ✅ À FAIRE

1. **Validations synchrones dans le validateur**
   - Format, longueur, regex, ranges
   - Règles métier sans état
   - Fail-fast (early return)

2. **Validations DB dans HandleCore**
   - Checks d'unicité
   - Foreign keys
   - Règles dépendantes de l'état

3. **Result Pattern pour erreurs métier**
   ```csharp
   return Result<T>.Failure(Error.Conflict("CODE", "Message"));
   ```

### ❌ À ÉVITER

1. **Validations DB dans les validateurs** (sauf cas rare et justifié)
2. **Double validation** (validateur + HandleCore)
3. **Validations asynchrones inutiles**
   ```csharp
   // ❌ Mauvais
   public async Task<IDictionary<...>> ValidateAsync(...)
   {
       await Task.Delay(0); // Inutile!
       return errors;
   }
   
   // ✅ Bon
   public Task<IDictionary<...>> ValidateAsync(...)
   {
       return Task.FromResult(errors); // Synchrone wrappé
   }
   ```

---

## 📝 Checklist de Décision

**Dois-je faire cette validation dans le validateur ou HandleCore ?**

| Question | Oui → Validateur | Non → HandleCore |
|----------|------------------|------------------|
| Validation synchrone ? | ✅ | ❌ |
| Pas de DB/IO ? | ✅ | ❌ |
| Réutilisable ? | ✅ | ⚠️ |
| Check unicité ? | ❌ | ✅ |
| Foreign key ? | ❌ | ✅ |
| Dépend de l'état ? | ❌ | ✅ |
| < 1ms ? | ✅ | ⚠️ |

---

## 🔧 Outils Utiles

### Extension pour Check d'Existence

```csharp
public static class RepositoryExtensions
{
    public static async Task<bool> ExistsAsync<TEntity, TId>(
        this IRepository<TEntity, TId> repository,
        TId id,
        CancellationToken ct = default)
        where TEntity : class
    {
        var entity = await repository.GetByIdAsync(id, ct);
        return entity != null;
    }
}

// Usage dans HandleCore
var tenantExists = await _tenantRepository.ExistsAsync(tenantId);
if (!tenantExists)
    return Result.Failure(Error.NotFound(...));
```

### Helper pour Erreurs Métier

```csharp
public static class Error
{
    public static Error Conflict(string code, string message) 
        => new(code, message, ErrorType.Conflict);
    
    public static Error NotFound(string code, string message)
        => new(code, message, ErrorType.NotFound);
    
    public static Error Validation(string code, string message)
        => new(code, message, ErrorType.Validation);
}
```

---

**Conclusion :** Préférer **Approche 1** (HandleCore) dans 90% des cas. Utiliser **Approche 2** (Validateur DB) uniquement si règle métier complexe et réutilisable.
