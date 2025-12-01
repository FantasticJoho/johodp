namespace Johodp.Api.Extensions;

using System.Text.Json;
using System.Text.Json.Serialization;
using Johodp.Domain.Tenants.ValueObjects;
using Johodp.Domain.Clients.ValueObjects;
using Johodp.Domain.CustomConfigurations.ValueObjects;

/// <summary>
/// JSON converter for TenantId ValueObject
/// </summary>
public class TenantIdJsonConverter : JsonConverter<TenantId>
{
    public override TenantId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var guidString = reader.GetString();
            if (string.IsNullOrEmpty(guidString))
            {
                return null;
            }

            if (Guid.TryParse(guidString, out var guid))
            {
                return TenantId.From(guid);
            }

            throw new JsonException($"Unable to convert '{guidString}' to TenantId.");
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value.ToString());
    }
}

/// <summary>
/// JSON converter for ClientId ValueObject
/// </summary>
public class ClientIdJsonConverter : JsonConverter<ClientId>
{
    public override ClientId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var guidString = reader.GetString();
            if (string.IsNullOrEmpty(guidString))
            {
                return null;
            }

            if (Guid.TryParse(guidString, out var guid))
            {
                return ClientId.From(guid);
            }

            throw new JsonException($"Unable to convert '{guidString}' to ClientId.");
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, ClientId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value.ToString());
    }
}

/// <summary>
/// JSON converter for CustomConfigurationId ValueObject
/// </summary>
public class CustomConfigurationIdJsonConverter : JsonConverter<CustomConfigurationId>
{
    public override CustomConfigurationId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var guidString = reader.GetString();
            if (string.IsNullOrEmpty(guidString))
            {
                return null;
            }

            if (Guid.TryParse(guidString, out var guid))
            {
                return CustomConfigurationId.From(guid);
            }

            throw new JsonException($"Unable to convert '{guidString}' to CustomConfigurationId.");
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, CustomConfigurationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value.ToString());
    }
}
