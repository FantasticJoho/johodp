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
│   ├── Unit.cs
│   └── MediatorExtensions.cs
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
| Lignes de code | ~200 | ~3000+ |
| Dépendances | 2 | Multiple |
| Behaviors/Pipeline | ❌ | ✅ |
| Notifications | ✅ (EventAggregator) | ✅ |
| Performance | Légèrement plus lent (reflection) | Optimisé |
| Simplicité | ✅✅✅ | ✅ |
| Contrôle total | ✅✅✅ | ❌ |

## 📄 Licence

Ce projet fait partie de l'écosystème Johodp. Utilisable dans n'importe quel projet .NET interne ou externe.

## 🤝 Contribution

Pour contribuer au projet, veuillez suivre les conventions de code existantes et ajouter des tests unitaires si nécessaire.

---

**Version:** 1.0.0  
**Target Framework:** .NET 8.0  
**Auteur:** Johodp Team
