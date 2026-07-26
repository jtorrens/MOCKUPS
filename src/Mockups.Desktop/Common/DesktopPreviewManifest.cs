using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.Common;

internal enum DesktopPreviewComponentCategory
{
    Component,
    Atom,
    System,
}

internal sealed record DesktopPreviewComponentManifestEntry(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("contract")] string Contract,
    [property: JsonPropertyName("resolver")] string Resolver,
    [property: JsonPropertyName("renderable")] string Renderable,
    [property: JsonPropertyName("embeds")] IReadOnlyList<string> Embeds);

internal sealed record DesktopPreviewModuleManifestEntry(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("resolver")] string Resolver,
    [property: JsonPropertyName("renderable")] string Renderable,
    [property: JsonPropertyName("embeds")] IReadOnlyList<string> Embeds);

internal sealed record DesktopPreviewManifestDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("components")] IReadOnlyDictionary<string, DesktopPreviewComponentManifestEntry> Components,
    [property: JsonPropertyName("modules")] IReadOnlyDictionary<string, DesktopPreviewModuleManifestEntry> Modules);

internal static class DesktopPreviewManifest
{
    private const string ResourceName = "Mockups.DesktopPreviewManifest.json";
    private static readonly Lazy<DesktopPreviewManifestDocument> Current = new(Load);

    public static IReadOnlyDictionary<string, DesktopPreviewComponentManifestEntry> Components => Current.Value.Components;

    public static IReadOnlyDictionary<string, DesktopPreviewModuleManifestEntry> Modules => Current.Value.Modules;

    public static DesktopPreviewComponentCategory ComponentCategory(string componentType)
    {
        if (!Components.TryGetValue(componentType, out var entry))
        {
            throw new InvalidOperationException($"Component type '{componentType}' is not declared in the desktop preview manifest.");
        }

        return entry.Category switch
        {
            "component" => DesktopPreviewComponentCategory.Component,
            "atom" => DesktopPreviewComponentCategory.Atom,
            "system" => DesktopPreviewComponentCategory.System,
            _ => throw new InvalidOperationException(
                $"Component type '{componentType}' has unsupported manifest category '{entry.Category}'."),
        };
    }

    private static DesktopPreviewManifestDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded desktop preview manifest '{ResourceName}' is missing.");
        var manifest = JsonSerializer.Deserialize<DesktopPreviewManifestDocument>(stream)
            ?? throw new InvalidDataException("Desktop preview manifest is empty.");
        if (manifest.SchemaVersion != 2)
        {
            throw new InvalidDataException($"Unsupported desktop preview manifest schema {manifest.SchemaVersion}.");
        }
        if (manifest.Components.Count == 0 || manifest.Modules.Count == 0)
        {
            throw new InvalidDataException("Desktop preview manifest must declare components and modules.");
        }
        return manifest;
    }
}
