using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DictionaryFieldServices(
    Func<string, ValueKind, Task<string?>>? BrowsePath = null,
    Func<string, bool, Task<string?>>? ShowIconTokenPicker = null,
    Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? ShowThemeTokenPicker = null,
    Func<string, Control>? CreateIconPreview = null,
    Func<string, string?>? ResolveImagePath = null,
    Func<string, string>? GetFieldValue = null,
    Func<IReadOnlyList<FieldOption>>? GetPaletteColorOptions = null,
    Func<string, bool, IReadOnlyList<FieldOption>>? GetRecordReferenceOptions = null,
    Func<string, IReadOnlyList<FieldOption>>? GetComponentVariantOptions = null,
    Func<string, IReadOnlyList<ComponentInputBindingDefinition>>? GetComponentVariantRuntimeInputs = null,
    Func<string, JsonObject>? GetComponentVariantRuntimeValues = null,
    Func<string, IReadOnlyList<RuntimeInputCollectionDefinition>>? GetComponentVariantRuntimeCollections = null,
    Func<string, Task>? OpenComponentVariantReference = null,
    Func<string, Task>? OpenEmbeddedComponent = null,
    Func<string, Task>? RestoreEmbeddedComponentOverrides = null,
    Func<FieldDefinition, ComponentInputBindingDefinition, Task>? OpenComponentInputBinding = null,
    Func<FieldDefinition, string, int>? ResolveBehaviorTimingFrames = null,
    Func<string, Task<bool>>? ConfirmStopRuntimeInputForwarding = null,
    Func<string, JsonObject, Func<JsonObject, Task>, Task>? OpenRuntimeComponentOverrides = null,
    Func<FieldDefinition, string, Task>?
        OpenRecordReferenceOverrides = null,
    Func<FieldDefinition, string, Task>?
        RestoreRecordReferenceOverrides = null,
    Func<string, Task<bool>>? ConfirmStructuredCollectionItemDelete = null,
    Func<string, IReadOnlyList<string>, Task<bool>>? ConfirmDiscardForwardedRuntimeInputs = null,
    Action<string, string>? SetRuntimeTestValue = null,
    Func<ComponentInputDefinition, string, DictionaryFieldControl, Control>? DecorateStructuredCollectionField = null,
    Func<StructuredCollectionAddress, string, IReadOnlyDictionary<string, JsonNode?>, Task>?
        UpdateStructuredCollectionValues = null,
    Func<StructuredCollectionMutation, Task<StructuredCollectionMutationResult>>?
        MutateStructuredCollection = null,
    EditorSessionUiState? StructuredCollectionUiState = null,
    bool AllowIncompleteDraft = false);

internal static class DictionaryRecordReferenceOptions
{
    public static IReadOnlyList<FieldOption> Resolve(
        DictionaryFieldServices services,
        string tableId,
        bool allowEmpty,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(tableId))
        {
            throw new InvalidOperationException(
                $"{owner} is missing its record-reference table id.");
        }

        return services.GetRecordReferenceOptions?.Invoke(
                tableId,
                allowEmpty)
            ?? throw new InvalidOperationException(
                $"{owner} has no record-reference options provider.");
    }
}
