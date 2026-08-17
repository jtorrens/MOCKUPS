using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ScaffoldDictionaryFieldContract
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ComponentClassFieldDescriptor Component(string json)
    {
        var descriptor = JsonSerializer.Deserialize<ComponentClassFieldDescriptor>(
            json,
            SerializerOptions)
            ?? throw new InvalidOperationException(
                "Generated Component dictionary field must be an object.");
        if (string.IsNullOrWhiteSpace(descriptor.Id)
            || string.IsNullOrWhiteSpace(descriptor.Label)
            || descriptor.JsonPath.Length == 0)
        {
            throw new InvalidOperationException(
                "Generated Component dictionary field requires id, label and jsonPath.");
        }
        if (descriptor.Options is { Count: > 0 })
        {
            _ = FieldOptionContract.RequireOptions(
                descriptor.Options,
                $"Generated Component field '{descriptor.Id}'");
        }
        return descriptor with
        {
            Options = descriptor.Options is { Count: > 0 }
                ? descriptor.Options
                : null,
            ComponentInputBindings = descriptor.ComponentInputBindings is { Count: > 0 }
                ? descriptor.ComponentInputBindings
                : null,
        };
    }

    public static RecordClassFieldDescriptor Module(string json)
    {
        var descriptor = JsonSerializer.Deserialize<RecordClassFieldDescriptor>(
            json,
            SerializerOptions)
            ?? throw new InvalidOperationException(
                "Generated Module dictionary field must be an object.");
        if (string.IsNullOrWhiteSpace(descriptor.Id)
            || string.IsNullOrWhiteSpace(descriptor.Label)
            || descriptor.ConfigJsonPath is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "Generated Module dictionary field requires id, label and configJsonPath.");
        }
        if (descriptor.Options is { Count: > 0 })
        {
            _ = FieldOptionContract.RequireOptions(
                descriptor.Options,
                $"Generated Module field '{descriptor.Id}'");
        }
        return descriptor with
        {
            Options = descriptor.Options is { Count: > 0 }
                ? descriptor.Options
                : null,
            ComponentInputBindings = descriptor.ComponentInputBindings is { Count: > 0 }
                ? descriptor.ComponentInputBindings
                : null,
        };
    }
}
