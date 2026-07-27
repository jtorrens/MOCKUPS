using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorDictionaryRecordOptionsKey(
    string TableId,
    bool IncludeNone);

internal sealed record EditorDictionaryContextSnapshot(
    string ProjectId,
    string IconThemeId,
    string ThemeTokensJson,
    IReadOnlyDictionary<string, string?> IconAssetPaths,
    IReadOnlyList<FieldOption> PaletteColorOptions,
    IReadOnlyDictionary<
        EditorDictionaryRecordOptionsKey,
        IReadOnlyList<FieldOption>> RecordReferenceOptions,
    IReadOnlyDictionary<string, IReadOnlyList<FieldOption>>
        ComponentVariantOptions,
    IReadOnlyDictionary<
        string,
        IReadOnlyList<ComponentInputBindingDefinition>>
        ComponentVariantRuntimeInputs,
    IReadOnlyDictionary<string, string>
        ComponentVariantRuntimeValuesJson,
    IReadOnlyDictionary<
        string,
        IReadOnlyList<RuntimeInputCollectionDefinition>>
        ComponentVariantRuntimeCollections,
    IReadOnlyDictionary<
        string,
        DictionaryComponentVariantSelectionSource>
        ComponentVariantSelections)
{
    public JsonObject ThemeTokens() =>
        DesignPreviewTestValues.Parse(ThemeTokensJson);

    public string? IconAssetPath(string token) =>
        IconAssetPaths.TryGetValue(token, out var path)
            ? path
            : null;

    public IReadOnlyList<FieldOption> RecordOptions(
        string tableId,
        bool includeNone)
    {
        var key = new EditorDictionaryRecordOptionsKey(
            tableId,
            includeNone);
        return RecordReferenceOptions.TryGetValue(key, out var options)
            ? options
            : throw new InvalidOperationException(
                $"Record-reference options '{tableId}' ({includeNone}) were not included in the prepared editor context.");
    }

    public IReadOnlyList<FieldOption> VariantOptions(
        string componentType) =>
        ComponentVariantOptions.TryGetValue(
            componentType,
            out var options)
            ? options
            : throw new InvalidOperationException(
                $"Component options '{componentType}' were not included in the prepared editor context.");

    public IReadOnlyList<ComponentInputBindingDefinition> RuntimeInputs(
        string variantReference) =>
        ComponentVariantRuntimeInputs.TryGetValue(
            variantReference,
            out var inputs)
            ? inputs
            : throw MissingVariantContext(
                variantReference,
                "Runtime inputs");

    public JsonObject RuntimeValues(string variantReference) =>
        ComponentVariantRuntimeValuesJson.TryGetValue(
            variantReference,
            out var json)
            ? DesignPreviewTestValues.Parse(json)
            : throw MissingVariantContext(
                variantReference,
                "Runtime values");

    public IReadOnlyList<RuntimeInputCollectionDefinition>
        RuntimeCollections(string variantReference) =>
        ComponentVariantRuntimeCollections.TryGetValue(
            variantReference,
            out var collections)
            ? collections
            : throw MissingVariantContext(
                variantReference,
                "Runtime collections");

    public bool TryVariantSelection(
        string variantReference,
        out DictionaryComponentVariantSelectionSource selection) =>
        ComponentVariantSelections.TryGetValue(
            variantReference,
            out selection!);

    private static InvalidOperationException MissingVariantContext(
        string variantReference,
        string capability) => new(
        $"{capability} for Component Variant '{variantReference}' were not included in the prepared editor context.");
}
