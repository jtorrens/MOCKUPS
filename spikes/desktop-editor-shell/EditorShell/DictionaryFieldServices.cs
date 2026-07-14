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
    Func<string, IReadOnlyList<FieldOption>>? GetComponentPresetOptions = null,
    Func<string, IReadOnlyList<ComponentInputBindingDefinition>>? GetComponentPresetRuntimeInputs = null,
    Func<string, JsonObject>? GetComponentPresetRuntimeValues = null,
    Func<string, IReadOnlyList<RuntimeInputCollectionDefinition>>? GetComponentPresetRuntimeCollections = null,
    Func<string, Task>? OpenComponentPresetReference = null,
    Func<string, Task>? OpenEmbeddedComponent = null,
    Func<FieldDefinition, ComponentInputBindingDefinition, Task>? OpenComponentInputBinding = null,
    Func<FieldDefinition, string, int?>? ResolveBehaviorTimingFrames = null,
    Func<string, Task<bool>>? ConfirmStopRuntimeInputForwarding = null,
    Func<string, JsonObject, Action<JsonObject>, Task>? OpenRuntimeComponentOverrides = null,
    Func<string, Task<bool>>? ConfirmStructuredCollectionItemDelete = null,
    Func<string, IReadOnlyList<string>, Task<bool>>? ConfirmDiscardForwardedRuntimeInputs = null,
    EditorSessionUiState? StructuredCollectionUiState = null);
