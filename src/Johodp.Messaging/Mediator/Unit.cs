namespace Johodp.Messaging.Mediator;

/// <summary>
/// Represents a void type for requests that don't return a value
/// </summary>
public struct Unit
{
    public static readonly Unit Value = new();
}
