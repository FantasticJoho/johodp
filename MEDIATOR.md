# 🎯 Mini Médiator Maison - Documentation

## Vue d'ensemble

Ce projet utilise un **mini médiator maison** au lieu de MediatR pour implémenter le pattern CQRS (Command Query Responsibility Segregation). Cette implémentation légère (~100 lignes de code) fournit les fonctionnalités essentielles sans la complexité d'une bibliothèque externe complète.

**Localisation** : `src/Johodp.Application/Common/Mediator/`

## 📦 Architecture

### Composants Principaux

```
Mediator/
├── IRequest.cs              # Marker interface pour les requêtes
├── IRequestHandler.cs       # Interface pour les handlers
├── ISender.cs               # Interface pour dispatcher les requêtes
├── Sender.cs                # Implémentation du dispatcher
├── MediatorExtensions.cs    # Enregistrement automatique DI
└── Unit.cs                  # Type "void" pour commandes sans retour
```

## 🔧 Interfaces

### `IRequest<TResponse>`

Marker interface (vide) qui identifie une classe comme étant une requête retournant `TResponse`.

```csharp
namespace Johodp.Application.Common.Mediator;

public interface IRequest<out TResponse>
{
}
```

**Utilisation** :
```csharp
// Commande qui retourne un TenantDto
public class CreateTenantCommand : IRequest<TenantDto>
{
    public CreateTenantDto Data { get; set; }
}

// Query qui retourne une liste
public class GetAllTenantsQuery : IRequest<IEnumerable<TenantDto>>
{
}

// Commande sans retour
public class DeleteTenantCommand : IRequest<Unit>
{
    public Guid TenantId { get; set; }
}
```

---

### `IRequestHandler<TRequest, TResponse>`

Interface pour les handlers qui traitent une requête spécifique.

```csharp
namespace Johodp.Application.Common.Mediator;

public interface IRequestHandler<in TRequest, TResponse> 
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}
```

**Utilisation** :
```csharp
public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantDto> Handle(
        CreateTenantCommand request, 
        CancellationToken cancellationToken = default)
    {
        // 1. Validation
        // 2. Logique métier
        // 3. Persistence
        // 4. Retour du résultat
        
        var tenant = Tenant.Create(/* ... */);
        await _tenantRepository.AddAsync(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return MapToDto(tenant);
    }
}
```

---

### `ISender`

Interface pour dispatcher les requêtes vers leurs handlers.

```csharp
namespace Johodp.Application.Common.Mediator;

public interface ISender
{
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
```

**Utilisation dans les Controllers** :
```csharp
[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly ISender _sender;

    public TenantController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantDto dto)
    {
        var command = new CreateTenantCommand { Data = dto };
        var tenant = await _sender.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetAll()
    {
        var tenants = await _sender.Send(new GetAllTenantsQuery());
        return Ok(tenants);
    }
}
```

---

## ⚙️ Implémentation : `Sender`

Le `Sender` est le cœur du médiator. Il utilise la **réflexion** et l'**injection de dépendances** pour router dynamiquement les requêtes.

### Fonctionnement en 4 Étapes

```csharp
public class Sender : ISender
{
    private readonly IServiceProvider _serviceProvider;

    public Sender(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        // 1️⃣ DÉCOUVRIR les types
        var requestType = request.GetType();           // Ex: CreateTenantCommand
        var responseType = typeof(TResponse);          // Ex: TenantDto
        
        // 2️⃣ CONSTRUIRE le type du handler générique
        var handlerType = typeof(IRequestHandler<,>)
            .MakeGenericType(requestType, responseType);
        // Résultat: IRequestHandler<CreateTenantCommand, TenantDto>
        
        // 3️⃣ RÉSOUDRE le handler depuis le conteneur DI
        var handler = _serviceProvider.GetRequiredService(handlerType);
        // Trouve: CreateTenantCommandHandler (enregistré dans DI)
        
        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type {requestType.Name}");
        }

        // 4️⃣ INVOQUER la méthode Handle par réflexion
        var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle));
        
        if (handleMethod == null)
        {
            throw new InvalidOperationException(
                $"Handle method not found on handler for {requestType.Name}");
        }

        var task = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, cancellationToken })!;
        return await task;
    }
}
```

### Exemple de Flux d'Exécution

```
┌──────────────┐
│  Controller  │
└──────┬───────┘
       │ await _sender.Send(new CreateTenantCommand { ... })
       ↓
┌──────────────┐
│   Sender     │
└──────┬───────┘
       │ 1. Découvre: CreateTenantCommand → TenantDto
       │ 2. Construit: IRequestHandler<CreateTenantCommand, TenantDto>
       │ 3. Résout: CreateTenantCommandHandler (depuis DI)
       │ 4. Invoque: handler.Handle(command, cancellationToken)
       ↓
┌─────────────────────────────┐
│ CreateTenantCommandHandler  │
└──────┬──────────────────────┘
       │ Exécute la logique métier
       │ Retourne: TenantDto
       ↓
┌──────────────┐
│  Controller  │ ← Reçoit TenantDto
└──────────────┘
```

---

## 🔌 Enregistrement Automatique : `MediatorExtensions`

### Configuration dans `Program.cs`

```csharp
// Dans Program.cs ou ConfigureServices
builder.Services.AddMediator(typeof(CreateTenantCommandHandler).Assembly);
```

### Fonctionnement de `AddMediator`

```csharp
public static IServiceCollection AddMediator(
    this IServiceCollection services,
    params Assembly[] assemblies)
{
    // 1️⃣ Enregistrer ISender
    services.AddScoped<ISender, Sender>();

    // 2️⃣ Si aucun assembly spécifié, utiliser l'assembly appelant
    assemblies = assemblies.Any() 
        ? assemblies 
        : new[] { Assembly.GetCallingAssembly() };

    // 3️⃣ Enregistrer tous les handlers automatiquement
    RegisterHandlers(services, assemblies);

    return services;
}

private static void RegisterHandlers(IServiceCollection services, Assembly[] assemblies)
{
    // Scanner tous les types dans les assemblies
    var handlerTypes = assemblies
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type =>
            type.IsClass &&                          // Classe concrète
            !type.IsAbstract &&                      // Non abstraite
            type.GetInterfaces().Any(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))  // Implémente IRequestHandler
        .Select(type => new
        {
            Implementation = type,
            Interface = type.GetInterfaces()
                .First(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
        });

    // Enregistrer chaque handler dans DI
    foreach (var handler in handlerTypes)
    {
        services.AddScoped(handler.Interface, handler.Implementation);
        // Ex: services.AddScoped<IRequestHandler<CreateTenantCommand, TenantDto>, CreateTenantCommandHandler>();
    }
}
```

**Résultat** :
- ✅ Tous les handlers sont automatiquement découverts et enregistrés
- ✅ Pas besoin d'enregistrement manuel pour chaque handler
- ✅ Lifetime: **Scoped** (créé par requête HTTP)

---

## 📘 Type `Unit` (Commandes sans Retour)

Pour les commandes qui ne retournent aucune valeur (équivalent de `void`).

```csharp
namespace Johodp.Application.Common.Mediator;

/// <summary>
/// Represents a void type for requests that don't return a value
/// </summary>
public struct Unit
{
    public static readonly Unit Value = new();
}
```

### Utilisation

```csharp
// Commande sans retour
public class DeleteTenantCommand : IRequest<Unit>
{
    public Guid TenantId { get; set; }
}

// Handler
public class DeleteTenantCommandHandler : IRequestHandler<DeleteTenantCommand, Unit>
{
    private readonly ITenantRepository _repository;

    public async Task<Unit> Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(TenantId.From(request.TenantId));
        return Unit.Value;  // Équivalent de "return void"
    }
}

// Controller
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id)
{
    await _sender.Send(new DeleteTenantCommand { TenantId = id });
    return NoContent();  // 204
}
```

---

## 🎨 Patterns d'Utilisation

### 1. Commands (Modification de Données)

```csharp
// Command
public class UpdateTenantCommand : IRequest<TenantDto>
{
    public Guid TenantId { get; set; }
    public UpdateTenantDto Data { get; set; }
}

// Handler
public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        // 1. Charger l'agrégat
        var tenant = await _repository.GetByIdAsync(TenantId.From(request.TenantId));
        
        if (tenant == null)
            throw new NotFoundException($"Tenant {request.TenantId} not found");

        // 2. Appliquer les changements (Domain logic)
        tenant.UpdateDisplayName(request.Data.DisplayName);
        tenant.UpdateLocalization(/* ... */);

        // 3. Persister
        await _repository.UpdateAsync(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Retourner DTO
        return MapToDto(tenant);
    }
}
```

### 2. Queries (Lecture de Données)

```csharp
// Query
public class GetTenantByIdQuery : IRequest<TenantDto?>
{
    public Guid TenantId { get; set; }
}

// Handler
public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto?>
{
    private readonly ITenantRepository _repository;

    public async Task<TenantDto?> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(TenantId.From(request.TenantId));
        return tenant != null ? MapToDto(tenant) : null;
    }
}
```

### 3. Commandes avec Validation

```csharp
public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, ClientDto>
{
    private readonly IClientRepository _clientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateClientCommandHandler> _logger;

    public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        // Validation métier
        var existingClient = await _clientRepository.GetByNameAsync(request.Data.ClientName);
        if (existingClient != null)
        {
            throw new InvalidOperationException(
                $"Client with name '{request.Data.ClientName}' already exists");
        }

        // Création de l'agrégat
        var client = Client.Create(
            ClientName.From(request.Data.ClientName),
            request.Data.AllowedScopes);

        // Persistence
        await _clientRepository.AddAsync(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Client {ClientId} created successfully", client.Id.Value);

        return MapToDto(client);
    }
}
```

---

## ✅ Avantages du Mini Médiator Maison

| Caractéristique | Mini Médiator | MediatR |
|----------------|---------------|---------|
| **Taille du code** | ~100 lignes | ~10 000 lignes |
| **Dépendances** | Aucune | MediatR + extensions |
| **Complexité** | Simple | Pipeline complexe |
| **Auto-registration** | ✅ Oui | ✅ Oui |
| **Type safety** | ✅ Oui | ✅ Oui |
| **CQRS** | ✅ Oui | ✅ Oui |
| **Pipeline behaviors** | ❌ Non | ✅ Oui |
| **Notifications/Events** | ❌ Non | ✅ Oui |
| **Performance** | Réflexion | Source generators |
| **Maintenance** | Facile (contrôle total) | Dépend de la lib |

### Pourquoi utiliser le mini médiator ?

✅ **Léger** : Pas de dépendances externes lourdes  
✅ **Simple** : Code facilement compréhensible et maintenable  
✅ **Suffisant** : Couvre 90% des cas d'usage CQRS  
✅ **Contrôle** : Vous maîtrisez tout le code  
✅ **Performance acceptable** : Réflexion mise en cache par .NET  

### Quand envisager MediatR ?

❌ Besoin de behaviors (validation, logging, transactions)  
❌ Besoin de pub/sub avec notifications  
❌ Pipeline de traitement complexe  
❌ Performance critique (source generators)  

---

## 🚀 Guide de Migration vers MediatR (si nécessaire)

Si vous décidez plus tard de migrer vers MediatR :

### 1. Changer les Interfaces

```csharp
// Mini Mediator
using Johodp.Application.Common.Mediator;

// MediatR
using MediatR;
```

Les interfaces sont **compatibles** ! Aucun changement dans les handlers.

### 2. Mettre à Jour l'Enregistrement

```csharp
// Avant (Mini Mediator)
services.AddMediator(typeof(CreateTenantCommandHandler).Assembly);

// Après (MediatR)
services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(CreateTenantCommandHandler).Assembly));
```

### 3. Remplacer ISender

```csharp
// Les deux sont compatibles
private readonly ISender _sender;
private readonly IMediator _mediator;  // MediatR
```

**Résultat** : Migration en ~5 minutes ! 🎉

---

## 📚 Exemples Complets

### Exemple 1 : CRUD Complet d'un Tenant

```csharp
// ========== COMMANDS ==========

// Create
public class CreateTenantCommand : IRequest<TenantDto>
{
    public CreateTenantDto Data { get; set; }
}

// Update
public class UpdateTenantCommand : IRequest<TenantDto>
{
    public Guid TenantId { get; set; }
    public UpdateTenantDto Data { get; set; }
}

// Delete
public class DeleteTenantCommand : IRequest<Unit>
{
    public Guid TenantId { get; set; }
}

// ========== QUERIES ==========

// Get by ID
public class GetTenantByIdQuery : IRequest<TenantDto?>
{
    public Guid TenantId { get; set; }
}

// Get all
public class GetAllTenantsQuery : IRequest<IEnumerable<TenantDto>>
{
}

// Get by name
public class GetTenantByNameQuery : IRequest<TenantDto?>
{
    public string Name { get; set; }
}

// ========== CONTROLLER ==========

[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly ISender _sender;

    public TenantController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create([FromBody] CreateTenantDto dto)
    {
        var command = new CreateTenantCommand { Data = dto };
        var tenant = await _sender.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TenantDto>> Update(Guid id, [FromBody] UpdateTenantDto dto)
    {
        var command = new UpdateTenantCommand { TenantId = id, Data = dto };
        var tenant = await _sender.Send(command);
        return Ok(tenant);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteTenantCommand { TenantId = id });
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TenantDto>> GetById(Guid id)
    {
        var tenant = await _sender.Send(new GetTenantByIdQuery { TenantId = id });
        return tenant != null ? Ok(tenant) : NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetAll()
    {
        var tenants = await _sender.Send(new GetAllTenantsQuery());
        return Ok(tenants);
    }

    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<TenantDto>> GetByName(string name)
    {
        var tenant = await _sender.Send(new GetTenantByNameQuery { Name = name });
        return tenant != null ? Ok(tenant) : NotFound();
    }
}
```

---

## 🐛 Dépannage

### Erreur : "No handler registered for request type XXX"

**Cause** : Handler non trouvé dans le conteneur DI

**Solutions** :
1. Vérifier que `AddMediator()` est appelé dans `Program.cs`
2. Vérifier que l'assembly contenant le handler est passé à `AddMediator()`
3. Vérifier que le handler implémente bien `IRequestHandler<TRequest, TResponse>`

```csharp
// ❌ Mauvais
builder.Services.AddMediator();  // N'enregistre que l'assembly appelant

// ✅ Bon
builder.Services.AddMediator(typeof(CreateTenantCommandHandler).Assembly);
```

### Erreur : "Handle method not found on handler"

**Cause** : Méthode `Handle` mal nommée ou signature incorrecte

**Solution** : Vérifier la signature exacte
```csharp
// ✅ Correct
public async Task<TenantDto> Handle(
    CreateTenantCommand request, 
    CancellationToken cancellationToken = default)

// ❌ Incorrect
public async Task<TenantDto> ProcessAsync(CreateTenantCommand request)
```

### Performance : Réflexion Trop Lente ?

**Optimisation possible** : Mise en cache des MethodInfo

```csharp
private static readonly ConcurrentDictionary<Type, MethodInfo> _handleMethodCache = new();

var handleMethod = _handleMethodCache.GetOrAdd(handlerType, type =>
    type.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle))!);
```

---

## 📖 Références

- **Pattern CQRS** : [Martin Fowler - CQRS](https://martinfowler.com/bliki/CQRS.html)
- **Mediator Pattern** : [Refactoring Guru](https://refactoring.guru/design-patterns/mediator)
- **MediatR** : [GitHub](https://github.com/jbogard/MediatR) (pour comparaison)

---

## 🎓 Bonnes Pratiques

### 1. Nommage

```csharp
// Commands : verbe d'action
CreateTenantCommand
UpdateTenantCommand
DeleteTenantCommand
ActivateUserCommand

// Queries : Get/Find + nom
GetTenantByIdQuery
GetAllTenantsQuery
FindActiveClientsQuery
```

### 2. Un Handler = Une Responsabilité

```csharp
// ✅ Bon : Handler focalisé
public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>

// ❌ Mauvais : Handler qui fait trop de choses
public class TenantHandler : IRequestHandler<CreateTenantCommand>, IRequestHandler<UpdateTenantCommand>
```

### 3. Validation dans le Handler

```csharp
public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
{
    // 1. Validation métier
    if (string.IsNullOrWhiteSpace(request.Data.Name))
        throw new ValidationException("Tenant name is required");

    var existing = await _repository.GetByNameAsync(request.Data.Name);
    if (existing != null)
        throw new InvalidOperationException($"Tenant '{request.Data.Name}' already exists");

    // 2. Logique métier
    // ...
}
```

### 4. Logging

```csharp
public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
{
    _logger.LogInformation("Creating tenant: {TenantName}", request.Data.Name);
    
    var tenant = Tenant.Create(/* ... */);
    await _repository.AddAsync(tenant);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    
    _logger.LogInformation("Successfully created tenant {TenantId}", tenant.Id.Value);
    
    return MapToDto(tenant);
}
```

---

## 🔄 Versions

| Version | Date | Changements |
|---------|------|-------------|
| 1.0 | Dec 2025 | Version initiale |

---

**Maintenu par** : L'équipe Johodp  
**Dernière mise à jour** : Décembre 2025
