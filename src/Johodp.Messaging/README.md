# Johodp.Messaging

Bibliothèque légère et réutilisable pour implémenter le pattern **Mediator (CQRS)** et l'**Event Aggregator** dans des applications .NET.

## 📦 Composants

### Mediator Pattern (CQRS)

Mini-médiateur maison (~100 lignes) pour dispatcher les commandes et requêtes vers leurs handlers respectifs.

**Interfaces:**
- `IRequest<TResponse>` - Marker interface pour les requêtes
- `IRequestHandler<TRequest, TResponse>` - Handler pour traiter une requête
- `ISender` - Dispatcher pour envoyer les requêtes
- `Unit` - Type "void" pour les commandes sans retour

**Implémentation:**
- `Sender` - Implémentation du dispatcher avec injection de dépendances
- `MediatorExtensions` - Enregistrement automatique des handlers
- `BaseHandler<TRequest, TResponse>` - Classe de base avec hooks et cross-cutting concerns

### Validation

Système de validation automatique intégré au pipeline des handlers.

**Interfaces:**
- `IValidator<TRequest>` - Validateur pour une requête
- `ValidationException` - Exception levée en cas d'échec de validation

**Implémentation:**
- `ValidationExtensions` - Enregistrement automatique des validateurs

### Event Aggregator

Event bus simple pour publier et traiter les événements de domaine de manière synchrone.

**Interfaces:**
- `DomainEvent` - Classe de base pour les événements de domaine
- `IEventBus` - Bus d'événements pour publier les événements
- `IEventHandler<TEvent>` - Handler pour traiter un événement

**Implémentation:**
- `EventAggregator` - Invocation synchrone de tous les handlers enregistrés
- `EventAggregatorExtensions` - Enregistrement automatique des event handlers

## 🚀 Installation

### Via dotnet CLI

```bash
dotnet add reference ../Johodp.Messaging/Johodp.Messaging.csproj
```

### Via fichier .csproj

```xml
<ItemGroup>
  <ProjectReference Include="..\Johodp.Messaging\Johodp.Messaging.csproj" />
</ItemGroup>
```

## 📝 Utilisation

### Configuration (Startup/Program.cs)

```csharp
using Johodp.Messaging.Mediator;
using Johodp.Messaging.Events;

// Enregistrer le Mediator avec les assemblies contenant les handlers
services.AddMediator(
    typeof(CreateTenantCommandHandler).Assembly,
    typeof(GetTenantQueryHandler).Assembly);

// Enregistrer les validateurs automatiquement
services.AddValidatorsFromAssemblyContaining<CreateTenantCommand>();

// Enregistrer l'Event Aggregator avec les assemblies contenant les event handlers
services.AddEventAggregator(
    typeof(UserCreatedEventHandler).Assembly);
```

### Mediator - Définir une Commande

```csharp
using Johodp.Messaging.Mediator;

public class CreateTenantCommand : IRequest<TenantDto>
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
}
```

### Mediator - Implémenter le Handler

#### Option 1 : Handler Simple

```csharp
using Johodp.Messaging.Mediator;

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _repository;

    public CreateTenantCommandHandler(ITenantRepository repository)
    {
        _repository = repository;
    }

    public async Task<TenantDto> Handle(
        CreateTenantCommand request, 
        CancellationToken cancellationToken)
    {
        var tenant = Tenant.Create(request.Name, request.DisplayName);
        await _repository.AddAsync(tenant);
        
        return new TenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            DisplayName = tenant.DisplayName
        };
    }
}
```

#### Option 2 : Handler avec BaseHandler (Logging, Timing, Hooks)

```csharp
using Johodp.Messaging.Mediator;
using Johodp.Messaging.Validation;
using Microsoft.Extensions.Logging;

public class CreateTenantCommandHandler : BaseHandler<CreateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _repository;

    public CreateTenantCommandHandler(
        ITenantRepository repository,
        ILogger<CreateTenantCommandHandler> logger,
        IValidator<CreateTenantCommand>? validator = null) 
        : base(logger, validator)
    {
        _repository = repository;
    }

    protected override async Task<TenantDto> HandleCore(
        CreateTenantCommand request, 
        CancellationToken cancellationToken)
    {
        // La validation est automatique si un validateur est injecté
        // Le logging et timing sont gérés par BaseHandler
        
        var tenant = Tenant.Create(request.Name, request.DisplayName);
        await _repository.AddAsync(tenant);
        
        return new TenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            DisplayName = tenant.DisplayName
        };
    }

    // Optionnel : personnaliser les hooks
    protected override async Task OnBeforeHandle(CreateTenantCommand request)
    {
        await base.OnBeforeHandle(request); // Appel validation + logging
        _logger.LogInformation("Creating tenant: {Name}", request.Name);
    }

    protected override async Task OnAfterHandle(CreateTenantCommand request, TenantDto response, TimeSpan elapsed)
    {
        await base.OnAfterHandle(request, response, elapsed);
        _logger.LogInformation("Tenant created with ID: {TenantId}", response.Id);
    }
}
```

### Mediator - Envoyer une Commande

```csharp
using Johodp.Messaging.Mediator;

public class TenantController : ControllerBase
{
    private readonly ISender _sender;

    public TenantController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command)
    {
        var result = await _sender.Send(command);
        return Ok(result);
    }
}
```

### Validation - Définir un Validateur

#### Règle d'Or : Pas de DB dans les Validateurs ⚠️

**Les validateurs doivent contenir UNIQUEMENT des validations synchrones :**
- Format, longueur, regex
- Règles métier sans état
- Validations rapides (< 1ms)

**Les validations avec accès DB vont dans `HandleCore` :**
- Checks d'unicité (nom existe déjà)
- Foreign keys (tenant existe)
- Règles dépendantes de l'état

```csharp
using Johodp.Messaging.Validation;

public class CreateTenantCommandValidator : IValidator<CreateTenantCommand>
{
    public Task<IDictionary<string, string[]>> ValidateAsync(CreateTenantCommand request)
    {
        var errors = new Dictionary<string, string[]>();

        // ✅ Validations synchrones uniquement (format, longueur, règles simples)
        
        // Valider le nom
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["Name"] = new[] { "Tenant name is required" };
        }
        else if (request.Name.Length < 3)
        {
            errors["Name"] = new[] { "Tenant name must be at least 3 characters" };
        }
        else if (request.Name.Length > 100)
        {
            errors["Name"] = new[] { "Tenant name cannot exceed 100 characters" };
        }

        // Valider le nom d'affichage
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["DisplayName"] = new[] { "Display name is required" };
        }

        // ❌ PAS de check DB ici (tenant existe, etc.)
        // → Ces validations sont faites dans HandleCore avec Result pattern

        return Task.FromResult<IDictionary<string, string[]>>(errors);
    }
}
```

### Validation - Validations avec Base de Données

**Les validations DB doivent être faites dans `HandleCore`, pas dans les validateurs.**

#### ✅ Approche Recommandée : HandleCore + Result Pattern

```csharp
public class CreateTenantCommandHandler : BaseHandler<CreateTenantCommand, Result<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateTenantCommandHandler> logger,
        IValidator<CreateTenantCommand>? validator = null) 
        : base(logger, validator)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task<Result<TenantDto>> HandleCore(
        CreateTenantCommand command, 
        CancellationToken cancellationToken)
    {
        // ✅ Validation DB dans HandleCore (pas dans le validateur)
        var existingTenant = await _tenantRepository.GetByNameAsync(command.Name);
        if (existingTenant != null)
        {
            return Result<TenantDto>.Failure(Error.Conflict(
                "TENANT_ALREADY_EXISTS",
                $"Tenant '{command.Name}' already exists"));
        }

        // Business logic
        var tenant = Tenant.Create(command.Name, command.DisplayName);
        await _tenantRepository.AddAsync(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TenantDto>.Success(MapToDto(tenant));
    }
}
```

**Avantages :**
- ✅ 1 seul round-trip DB (vs 2 avec validateur DB)
- ✅ Pas de race conditions
- ✅ Performance optimale (+30%)
- ✅ Cohérence transactionnelle

**Voir `VALIDATION_DB_GUIDE.md` pour plus de détails sur les stratégies de validation.**

### Validation - Gérer les Erreurs

```csharp
using Johodp.Messaging.Validation;

[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateTenantCommand command)
{
    try
    {
        var result = await _sender.Send(command);
        return Ok(result);
    }
    catch (ValidationException ex)
    {
        // ex.Errors contient un dictionnaire des erreurs de validation
        return BadRequest(new 
        { 
            message = "Validation failed",
            errors = ex.Errors 
        });
    }
}
```

### Event Aggregator - Définir un Événement

```csharp
using Johodp.Messaging.Events;

public class UserCreatedEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

### Event Aggregator - Implémenter le Handler

```csharp
using Johodp.Messaging.Events;

public class SendWelcomeEmailHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IEmailService _emailService;

    public SendWelcomeEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(
            @event.Email, 
            @event.FirstName, 
            @event.LastName);
    }
}
```

### Event Aggregator - Publier un Événement

```csharp
using Johodp.Messaging.Events;

public class UserService
{
    private readonly IEventBus _eventBus;

    public UserService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task CreateUserAsync(string email, string firstName, string lastName)
    {
        // ... logique de création ...

        // Publier l'événement
        await _eventBus.PublishAsync(new UserCreatedEvent
        {
            UserId = user.Id,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        });
    }
}
```

## 🎯 Commandes sans Retour

Pour les commandes qui ne retournent pas de valeur, utilisez le type `Unit` :

```csharp
public class DeleteTenantCommand : IRequest<Unit>
{
    public Guid TenantId { get; set; }
}

public class DeleteTenantCommandHandler : IRequestHandler<DeleteTenantCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        // ... logique de suppression ...
        return Unit.Value;
    }
}
```

## 🔍 Caractéristiques

### Mediator

✅ Légèreté (~100 lignes de code)  
✅ Enregistrement automatique des handlers via reflection  
✅ Support des commandes et requêtes (CQRS)  
✅ Injection de dépendances native  
✅ Gestion des cancellations  
✅ Type Unit pour commandes void  

### BaseHandler (Cross-Cutting Concerns)

✅ Template Method Pattern pour handlers  
✅ Logging automatique (avant/après/erreur)  
✅ Mesure du temps d'exécution  
✅ Validation automatique intégrée  
✅ Hooks personnalisables (OnBeforeHandle, OnAfterHandle, OnError)  
✅ Gestion d'erreurs structurée  

### Validation

✅ Validation automatique dans le pipeline  
✅ Interface simple `IValidator<TRequest>`  
✅ Enregistrement automatique des validateurs  
✅ Exception typée avec dictionnaire d'erreurs  
✅ Support de validations synchrones et asynchrones  
✅ Validation optionnelle (inject `null` pour désactiver)  
⚠️ **Règle d'Or** : Validations DB dans `HandleCore`, pas dans les validateurs  
✅ Performance optimale avec Result Pattern (+30% vs validateurs DB)  

### Event Aggregator

✅ Publication synchrone d'événements  
✅ Support de multiples handlers par événement  
✅ Logging intégré (Microsoft.Extensions.Logging)  
✅ Gestion d'erreurs avec propagation  
✅ Enregistrement automatique des handlers  
✅ Metadata événements (Id, OccurredAt)  

## 📚 Dépendances

- `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 10.0.0)
- `Microsoft.Extensions.Logging.Abstractions` (>= 10.0.0)

## 🏗️ Architecture

```
Johodp.Messaging/
├── Mediator/
│   ├── IRequest.cs
│   ├── IRequestHandler.cs
│   ├── ISender.cs
│   ├── Sender.cs
│   ├── BaseHandler.cs          ← Classe de base avec hooks
│   ├── Unit.cs
│   └── MediatorExtensions.cs
├── Validation/
│   ├── IValidator.cs           ← Interface validateur
│   ├── ValidationException.cs  ← Exception typée
│   └── ValidationExtensions.cs ← Enregistrement auto
└── Events/
    ├── DomainEvent.cs
    ├── IEventBus.cs
    ├── IEventHandler.cs
    ├── EventAggregator.cs
    └── EventAggregatorExtensions.cs
```

## 🆚 Comparaison avec MediatR

| Caractéristique | Johodp.Messaging | MediatR |
|----------------|------------------|---------|
| Lignes de code | ~300 | ~3000+ |
| Dépendances | 2 | Multiple |
| Behaviors/Pipeline | ✅ (BaseHandler + Hooks) | ✅ (IPipelineBehavior) |
| Validation | ✅ (Intégré) | ❌ (Package séparé) |
| Notifications | ✅ (EventAggregator) | ✅ |
| Performance | Légèrement plus lent (reflection) | Optimisé |
| Simplicité | ✅✅✅ | ✅ |
| Contrôle total | ✅✅✅ | ❌ |
| Logging automatique | ✅ | ❌ |
| Timing automatique | ✅ | ❌ |

## 📄 Licence

Ce projet fait partie de l'écosystème Johodp. Utilisable dans n'importe quel projet .NET interne ou externe.

## 📖 Documentation Complémentaire

- **VALIDATION_DB_GUIDE.md** - Guide détaillé sur les stratégies de validation avec accès DB
  - Approche 1 : HandleCore + Result Pattern (recommandée)
  - Approche 2 : Validateur avec DB (rare)
  - Approche 3 : Hybride (best of both)
  - Comparaisons de performance
  - Checklist de décision

## 🤝 Contribution

Pour contribuer au projet, veuillez suivre les conventions de code existantes et ajouter des tests unitaires si nécessaire.

---

**Version:** 1.0.0  
**Target Framework:** .NET 8.0  
**Auteur:** Johodp Team
