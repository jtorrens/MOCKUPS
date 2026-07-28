using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDictionaryContextPreparer
{
    private readonly DictionaryFieldContextDataSource _contextData;
    private readonly RuntimeInputOptionsDataSource _runtimeInputOptions;

    public EditorDictionaryContextPreparer(
        DictionaryFieldContextDataSource contextData,
        RuntimeInputOptionsDataSource runtimeInputOptions)
    {
        _contextData = contextData;
        _runtimeInputOptions = runtimeInputOptions;
    }

    public EditorDictionaryContextSnapshot Prepare(
        ProjectTreeNode node,
        string? selectedThemeId,
        IReadOnlyDictionary<string, FieldValue> fields,
        CancellationToken cancellationToken)
        => Prepare(
            node,
            selectedThemeId,
            [fields],
            cancellationToken);

    public EditorDictionaryContextSnapshot Prepare(
        ProjectTreeNode node,
        string? selectedThemeId,
        IEnumerable<IReadOnlyDictionary<string, FieldValue>> fieldSets,
        CancellationToken cancellationToken)
    {
        var requirements = new DictionaryContextRequirements();
        foreach (var fields in fieldSets)
        {
            foreach (var field in fields.Values)
            {
                requirements.Add(
                    field.Definition,
                    field.Value,
                    fields);
            }
        }
        return Prepare(
            node,
            selectedThemeId,
            requirements,
            cancellationToken);
    }

    public EditorDictionaryContextSnapshot PrepareRuntimeContext(
        ProjectTreeNode node,
        string? selectedThemeId,
        RuntimeInputSurface surface,
        CancellationToken cancellationToken)
    {
        var requirements = new DictionaryContextRequirements();
        foreach (var input in surface.Inputs)
        {
            requirements.Add(input);
        }
        foreach (var collection in surface.Collections)
        {
            requirements.Add(collection);
        }
        requirements.AddVariantReferences(surface.Preview);
        requirements.AddEmbeddedRuntimeContracts(
            surface.Preview);
        return Prepare(
            node,
            selectedThemeId,
            requirements,
            cancellationToken);
    }

    private EditorDictionaryContextSnapshot Prepare(
        ProjectTreeNode node,
        string? selectedThemeId,
        DictionaryContextRequirements requirements,
        CancellationToken cancellationToken)
    {
        var projectId = ProjectAncestor(node).Id;
        var componentOptions =
            new Dictionary<string, IReadOnlyList<FieldOption>>(
                StringComparer.Ordinal);
        var runtimeInputs =
            new Dictionary<
                string,
                IReadOnlyList<ComponentInputBindingDefinition>>(
                StringComparer.Ordinal);
        var runtimeValues =
            new Dictionary<string, string>(
                StringComparer.Ordinal);
        var runtimeCollections =
            new Dictionary<
                string,
                IReadOnlyList<RuntimeInputCollectionDefinition>>(
                StringComparer.Ordinal);
        var selections =
            new Dictionary<
                string,
                DictionaryComponentVariantSelectionSource>(
                StringComparer.Ordinal);
        var expandedFullRuntimeTypes =
            new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var componentType = requirements.ComponentTypes
                .Where((candidate) =>
                    !componentOptions.ContainsKey(candidate))
                .OrderBy(
                    (candidate) => candidate,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (componentType is not null)
            {
                var options = _contextData.ComponentVariantOptions(
                        projectId,
                        componentType)
                    .ToList()
                    .AsReadOnly();
                componentOptions[componentType] = options;
                if (requirements.FullRuntimeComponentTypes.Contains(
                        componentType))
                {
                    foreach (var option in options)
                    {
                        requirements.AddVariantReference(
                            option.Value);
                    }
                }
                continue;
            }

            var fullRuntimeType = requirements
                .FullRuntimeComponentTypes
                .Where((candidate) =>
                    componentOptions.ContainsKey(candidate)
                    && !expandedFullRuntimeTypes.Contains(
                        candidate))
                .OrderBy(
                    (candidate) => candidate,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (fullRuntimeType is not null)
            {
                expandedFullRuntimeTypes.Add(fullRuntimeType);
                foreach (var option in
                         componentOptions[fullRuntimeType])
                {
                    requirements.AddVariantReference(
                        option.Value);
                }
                continue;
            }

            var variantReference = requirements.VariantReferences
                .Where((candidate) =>
                    !runtimeInputs.ContainsKey(candidate))
                .OrderBy(
                    (candidate) => candidate,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (variantReference is null)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var bindings = _contextData
                .ComponentVariantRuntimeInputBindings(
                    variantReference)
                .ToList()
                .AsReadOnly();
            var values = _contextData
                .ComponentVariantRuntimeValues(
                    variantReference);
            var collections = _contextData
                .ComponentVariantRuntimeCollections(
                    variantReference)
                .ToList()
                .AsReadOnly();
            runtimeInputs[variantReference] = bindings;
            runtimeValues[variantReference] =
                values.ToJsonString();
            runtimeCollections[variantReference] = collections;
            selections[variantReference] =
                _contextData.ComponentVariantSelection(
                    variantReference);
            foreach (var binding in bindings)
            {
                requirements.Add(binding);
            }
            foreach (var collection in collections)
            {
                requirements.Add(collection);
            }
        }

        var recordOptions =
            new Dictionary<
                EditorDictionaryRecordOptionsKey,
                IReadOnlyList<FieldOption>>();
        foreach (var key in requirements.RecordOptions
                     .OrderBy(
                         (candidate) => candidate.TableId,
                         StringComparer.Ordinal)
                     .ThenBy((candidate) => candidate.IncludeNone))
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordOptions[key] =
                _runtimeInputOptions.RecordReferenceOptions(
                    projectId,
                    key.TableId,
                    key.IncludeNone)
                .ToList()
                .AsReadOnly();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var iconThemeId = requirements.NeedsIconAssets
            ? _contextData.IconThemeId(
                node,
                selectedThemeId)
            : "";
        cancellationToken.ThrowIfCancellationRequested();
        var themeTokens = requirements.NeedsThemeTokens
            ? _contextData.ThemeTokens(
                node,
                selectedThemeId)
            : new JsonObject();
        var variantNames =
            new Dictionary<string, string>(
                StringComparer.Ordinal);
        foreach (var reference in requirements
            .VariantReferences
            .Where((reference) =>
                VariantReferenceId.TryParse(
                    reference,
                    out _,
                    out _))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                (reference) => reference,
                StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            variantNames[reference] =
                _contextData.RuntimeComponentVariantName(
                    reference);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new EditorDictionaryContextSnapshot(
            projectId,
            iconThemeId,
            themeTokens.ToJsonString(),
            requirements.NeedsIconAssets
                ? _contextData.IconTokenAssetPaths(iconThemeId)
                    .ToFrozenDictionary(StringComparer.Ordinal)
                : FrozenDictionary<string, string?>.Empty,
            requirements.NeedsPaletteOptions
                ? _contextData.PaletteColorOptions(
                    projectId).ToList().AsReadOnly()
                : [],
            recordOptions.ToFrozenDictionary(),
            componentOptions.ToFrozenDictionary(
                StringComparer.Ordinal),
            runtimeInputs.ToFrozenDictionary(
                StringComparer.Ordinal),
            runtimeValues.ToFrozenDictionary(
                StringComparer.Ordinal),
            runtimeCollections.ToFrozenDictionary(
                StringComparer.Ordinal),
            selections.ToFrozenDictionary(
                StringComparer.Ordinal),
            variantNames.ToFrozenDictionary(
                StringComparer.Ordinal));
    }

    private static ProjectTreeNode ProjectAncestor(
        ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent
                ?? throw new InvalidOperationException(
                    $"{node.Kind} has no project ancestor.");
        }
        return current;
    }

    private sealed class DictionaryContextRequirements
    {
        public HashSet<string> ComponentTypes { get; } =
            new(StringComparer.Ordinal);
        public HashSet<string> FullRuntimeComponentTypes { get; } =
            new(StringComparer.Ordinal);
        public HashSet<string> VariantReferences { get; } =
            new(StringComparer.Ordinal);
        public HashSet<EditorDictionaryRecordOptionsKey>
            RecordOptions { get; } = [];
        public bool NeedsIconAssets { get; private set; }
        public bool NeedsThemeTokens { get; private set; }
        public bool NeedsPaletteOptions { get; private set; }

        public void Add(
            FieldDefinition definition,
            string value,
            IReadOnlyDictionary<string, FieldValue> fields)
        {
            Add(
                definition.ValueKind,
                definition.BehaviorTiming);
            if (definition.ValueKind == ValueKind.IconSlots)
            {
                ComponentTypes.Add("button");
                AddVariantReferences(value);
            }
            foreach (var input in
                     definition.ComponentInputBindings ?? [])
            {
                Add(input);
            }
            if (definition.StructuredCollection is { } collection)
            {
                Add(collection);
            }
            AddReferencedField(
                definition.RuntimeInputComponentVariantFieldId,
                fields);
            AddReferencedField(
                definition.RuntimeCollectionComponentVariantFieldId,
                fields);
            if (definition.StructuredCollection is not null
                || !string.IsNullOrWhiteSpace(
                    definition
                        .RuntimeCollectionComponentVariantFieldId))
            {
                AddVariantReferences(value);
            }
        }

        public void Add(
            ComponentInputBindingDefinition input)
        {
            Add(input.ValueKind, input.BehaviorTiming);
            AddComponentType(
                input.ValueKind,
                input.ComponentType,
                fullRuntime: false);
            if (input.ValueKind == ValueKind.IconSlots)
            {
                ComponentTypes.Add("button");
            }
            if (input.ValueKind == ValueKind.RecordReference
                && !string.IsNullOrWhiteSpace(input.TableId))
            {
                RecordOptions.Add(new(
                    input.TableId,
                    false));
            }
        }

        public void Add(
            ComponentInputDefinition input)
        {
            Add(input.ValueKind, input.BehaviorTiming);
            AddComponentType(
                input.ValueKind,
                input.ComponentType,
                fullRuntime: false);
            AddRecordOptions(input);
            if (input.StructuredCollection is { } collection)
            {
                Add(collection);
            }
        }

        public void Add(
            RuntimeInputCollectionDefinition collection)
        {
            foreach (var input in collection.Fields)
            {
                Add(input.ValueKind, input.BehaviorTiming);
                AddComponentType(
                    input.ValueKind,
                    input.ComponentType,
                    fullRuntime:
                        collection.ComponentItems is not null);
                AddRecordOptions(input);
                if (input.StructuredCollection is { } nested)
                {
                    Add(nested);
                }
            }
        }

        public void AddVariantReference(string value)
        {
            if (VariantReferenceId.TryParse(
                    value,
                    out _,
                    out _))
            {
                VariantReferences.Add(value);
            }
        }

        public void AddVariantReferences(JsonNode? node)
        {
            if (node is JsonValue value
                && value.TryGetValue<string>(out var text))
            {
                AddVariantReference(text);
                return;
            }
            if (node is JsonObject valueObject)
            {
                foreach (var child in valueObject)
                {
                    AddVariantReferences(child.Value);
                }
                return;
            }
            if (node is JsonArray valueArray)
            {
                foreach (var child in valueArray)
                {
                    AddVariantReferences(child);
                }
            }
        }

        public void AddEmbeddedRuntimeContracts(
            JsonNode? node)
        {
            if (node is JsonArray array)
            {
                foreach (var child in array)
                {
                    AddEmbeddedRuntimeContracts(child);
                }
                return;
            }
            if (node is not JsonObject valueObject)
            {
                return;
            }

            if (valueObject["inputs"] is JsonArray)
            {
                foreach (var input in
                         RuntimeInputDefinitionReader.ReadInputs(
                             valueObject,
                             new JsonObject()))
                {
                    Add(input);
                }
            }
            if (valueObject["collections"] is JsonArray)
            {
                foreach (var collection in
                         RuntimeInputDefinitionReader
                             .ReadCollections(
                                 valueObject,
                                 new JsonObject(),
                                 includeHidden: true))
                {
                    Add(collection);
                }
            }
            foreach (var child in valueObject)
            {
                AddEmbeddedRuntimeContracts(child.Value);
            }
        }

        private void AddVariantReferences(string value)
        {
            try
            {
                AddVariantReferences(JsonNode.Parse(value));
            }
            catch
            {
                // Scalar dictionary values are not JSON documents.
            }
        }

        private void AddReferencedField(
            string fieldId,
            IReadOnlyDictionary<string, FieldValue> fields)
        {
            if (string.IsNullOrWhiteSpace(fieldId)
                || !fields.TryGetValue(
                    fieldId,
                    out var field))
            {
                return;
            }
            AddVariantReference(field.Value);
            AddVariantReferences(field.Value);
        }

        private void AddRecordOptions(
            ComponentInputDefinition input)
        {
            if (input.ValueKind != ValueKind.RecordReference
                || string.IsNullOrWhiteSpace(input.TableId))
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(
                    input.AllowEmptyWhenItemJsonKey)
                && input.AllowEmptyWhenItemValues
                    is { Count: > 0 })
            {
                RecordOptions.Add(new(
                    input.TableId,
                    false));
                RecordOptions.Add(new(
                    input.TableId,
                    true));
                return;
            }
            RecordOptions.Add(new(
                input.TableId,
                input.AllowEmpty));
        }

        private void Add(
            ValueKind valueKind,
            BehaviorTimingDefinition? behaviorTiming)
        {
            NeedsIconAssets |= valueKind is
                ValueKind.IconToken
                or ValueKind.IconTokenList
                or ValueKind.IconSlots;
            NeedsPaletteOptions |= valueKind is
                ValueKind.PaletteColorToken
                or ValueKind.PaletteColorPair
                or ValueKind.PaletteColorAlphaPair;
            NeedsThemeTokens |= behaviorTiming is not null;
        }

        private void AddComponentType(
            ValueKind valueKind,
            string componentType,
            bool fullRuntime)
        {
            if (valueKind is not (
                    ValueKind.ComponentVariant
                    or ValueKind.ComponentVariantSlot)
                || string.IsNullOrWhiteSpace(componentType))
            {
                return;
            }
            ComponentTypes.Add(componentType);
            if (fullRuntime)
            {
                FullRuntimeComponentTypes.Add(componentType);
            }
        }
    }
}
