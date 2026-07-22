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
    Func<string, IReadOnlyList<FieldOption>>? GetComponentVariantOptions = null,
    Func<string, IReadOnlyList<ComponentInputBindingDefinition>>? GetComponentVariantRuntimeInputs = null,
    Func<string, JsonObject>? GetComponentVariantRuntimeValues = null,
    Func<string, IReadOnlyList<RuntimeInputCollectionDefinition>>? GetComponentVariantRuntimeCollections = null,
    Func<string, Task>? OpenComponentVariantReference = null,
    Func<string, Task>? OpenEmbeddedComponent = null,
    Func<FieldDefinition, ComponentInputBindingDefinition, Task>? OpenComponentInputBinding = null,
    Func<FieldDefinition, string, int?>? ResolveBehaviorTimingFrames = null,
    Func<string, Task<bool>>? ConfirmStopRuntimeInputForwarding = null,
    Func<string, JsonObject, Action<JsonObject>, Task>? OpenRuntimeComponentOverrides = null,
    Func<string, Task<bool>>? ConfirmStructuredCollectionItemDelete = null,
    Func<string, IReadOnlyList<string>, Task<bool>>? ConfirmDiscardForwardedRuntimeInputs = null,
    Action<string, string>? SetRuntimeTestValue = null,
    Func<ComponentInputDefinition, string, DictionaryFieldControl, Control>? DecorateStructuredCollectionField = null,
    Action<IReadOnlyList<string>>? RemoveStructuredCollectionAnimationTargets = null,
    Action<IReadOnlyDictionary<string, string>>? DuplicateStructuredCollectionAnimationTargets = null,
    EditorSessionUiState? StructuredCollectionUiState = null);
