namespace Johodp.Domain.CustomConfigurations.Events;

using Johodp.Domain.Common;
using Johodp.Messaging.Events;
using Johodp.Domain.CustomConfigurations.ValueObjects;

/// <summary>
/// Event raised when a CustomConfiguration is created
/// </summary>
public class CustomConfigurationCreatedEvent : DomainEvent
{
    public CustomConfigurationId CustomConfigurationId { get; }
    public string Name { get; }

    public CustomConfigurationCreatedEvent(CustomConfigurationId customConfigurationId, string name)
    {
        CustomConfigurationId = customConfigurationId;
        Name = name;
    }
}
