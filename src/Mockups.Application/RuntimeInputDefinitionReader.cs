using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class RuntimeInputDefinitionReader
{
    public static IReadOnlyList<ComponentInputDefinition> ReadInputs(
        JsonObject preview,
        JsonObject config)
    {
        preview = RuntimeInputForwardingContract.EffectivePreview(preview, config);
        if (preview["inputs"] is null)
        {
            return [];
        }
        var inputs = preview["inputs"] as JsonArray
            ?? throw new InvalidOperationException(
                "Runtime Input definitions must be an array when present.");
        var inputDefinitions = inputs.Select((node, index) => node as JsonObject
                ?? throw new InvalidOperationException(
                    $"Runtime Input definition at index {index} must be an object."))
            .ToList();
        RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
            inputDefinitions,
            "Runtime Input definitions");

        var definitions = new List<ComponentInputDefinition>();
        for (var index = 0; index < inputDefinitions.Count; index++)
        {
            var item = inputDefinitions[index];
            var owner = $"Runtime Input definition at index {index}";
            var id = JsonPath.RequiredString(item, "id", owner);
            var label = JsonPath.RequiredString(item, "label", owner);
            var jsonKey = JsonPath.RequiredString(item, "jsonKey", owner);
            var kind = JsonPath.RequiredString(item, "kind", owner);
            _ = RuntimeInputValueKindContract.CreateDefaultValue(
                item,
                $"Runtime Input '{id}'");
            var source = ParseInputSource(JsonString(item, "source"));
            var definition = CreateInputDefinition(
                id,
                label,
                jsonKey,
                kind,
                JsonString(item, "valueKind"),
                JsonString(item, "defaultValue"),
                ReadOptions(item),
                JsonDecimal(item, "minimum", 0),
                JsonDecimal(item, "maximum", 9999),
                JsonDecimal(item, "increment", 1),
                JsonString(item, "tableId"),
                JsonString(item, "resolvedJsonKey"),
                JsonString(item, "componentType"),
                source,
                ReadPairLabels(item),
                ParseInputUiOrigin(JsonString(item, "uiOrigin")),
                JsonString(item, "uiGroupId"),
                JsonString(item, "uiGroupLabel"),
                JsonString(item, "uiParentGroupId"),
                JsonString(item, "unit")) with
            {
                UiOrder = (int)JsonDecimal(item, "uiOrder", 0),
                UiSectionLabel = JsonString(item, "uiSectionLabel"),
                EnabledWhenPath = JsonString(item, "enabledWhenPath"),
                EnabledWhenValue = JsonString(item, "enabledWhenValue"),
                RefreshOnCommit = item["refreshOnCommit"]?.GetValue<bool>() == true,
                ActionOnly = item["actionOnly"]?.GetValue<bool>() == true,
                AllowEmpty = item["allowEmpty"]?.GetValue<bool>() == true,
                AllowEmptyWhenItemJsonKey = JsonString(item, "allowEmptyWhenItemJsonKey"),
                AllowEmptyWhenItemValues = JsonStringArray(item, "allowEmptyWhenItemValues"),
                OptionsSourceCollectionJsonKey = JsonString(item, "optionsSourceCollectionJsonKey"),
                OptionsSourceValueJsonKey = JsonString(item, "optionsSourceValueJsonKey", "id"),
                OptionsSourceLabelJsonKey = JsonString(item, "optionsSourceLabelJsonKey"),
                OptionsSourceFirstItemBadge = JsonString(item, "optionsSourceFirstItemBadge"),
                Animation = ReadAnimationDefinition(item),
                BehaviorTiming = ReadBehaviorTimingDefinition(item),
                Transition = ReadInputTransitionDefinition(item),
                HelpText = JsonString(item, "helpText"),
                ValuePattern = JsonString(item, "valuePattern"),
                ValuePatternMessage = JsonString(item, "valuePatternMessage"),
            };
            if (source == ComponentInputSource.Runtime && InputIsVisible(item, config))
            {
                definitions.Add(definition);
            }
        }
        return definitions;
    }

    public static IReadOnlyList<RuntimeInputCollectionDefinition> ReadCollections(
        JsonObject preview,
        JsonObject config,
        bool includeHidden = false)
    {
        if (preview["collections"] is null)
        {
            return [];
        }
        var collections = preview["collections"] as JsonArray
            ?? throw new InvalidOperationException(
                "Runtime Input collections must be an array when present.");

        var definitions = new List<RuntimeInputCollectionDefinition>();
        for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
        {
            var collection = collections[collectionIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Runtime Input collection at index {collectionIndex} must be an object.");
            var owner = $"Runtime Input collection at index {collectionIndex}";
            var id = JsonPath.RequiredString(collection, "id", owner);
            var label = JsonPath.RequiredString(collection, "label", owner);
            var jsonKey = JsonPath.RequiredString(collection, "jsonKey", owner);
            var itemLabel = JsonPath.RequiredString(collection, "itemLabel", owner);
            var fields = collection["fields"] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Runtime Input collection '{id}' fields must be an array.");
            var fieldDefinitions = fields.Select((node, index) => node as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Runtime Input collection '{id}' field at index {index} must be an object."))
                .ToList();
            RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
                fieldDefinitions,
                $"Runtime Input collection '{id}' fields");
            var isVisible = InputIsVisible(collection, config);

            var itemFields = new List<ComponentInputDefinition>();
            for (var fieldIndex = 0; fieldIndex < fieldDefinitions.Count; fieldIndex++)
            {
                var field = fieldDefinitions[fieldIndex];
                var fieldOwner = $"Runtime Input collection '{id}' field at index {fieldIndex}";
                var fieldId = JsonPath.RequiredString(field, "id", fieldOwner);
                var fieldLabel = JsonPath.RequiredString(field, "label", fieldOwner);
                var fieldJsonKey = JsonPath.RequiredString(field, "jsonKey", fieldOwner);
                var kind = JsonPath.RequiredString(field, "kind", fieldOwner);
                _ = RuntimeInputValueKindContract.CreateDefaultValue(
                    field,
                    $"Runtime Input collection '{id}' field '{fieldId}'");

                var definition = CreateInputDefinition(
                    fieldId,
                    fieldLabel,
                    fieldJsonKey,
                    kind,
                    JsonString(field, "valueKind"),
                    JsonString(field, "defaultValue"),
                    ReadOptions(field),
                    JsonDecimal(field, "minimum", 0),
                    JsonDecimal(field, "maximum", 9999),
                    JsonDecimal(field, "increment", 1),
                    JsonString(field, "tableId"),
                    JsonString(field, "resolvedJsonKey"),
                    JsonString(field, "componentType"),
                    ComponentInputSource.Runtime,
                    ReadPairLabels(field),
                    string.IsNullOrWhiteSpace(JsonString(field, "uiGroupId"))
                        ? ComponentInputUiOrigin.Self
                        : ComponentInputUiOrigin.Embedded,
                    JsonString(field, "uiGroupId"),
                    JsonString(field, "uiGroupLabel"),
                    JsonString(field, "uiParentGroupId"),
                    JsonString(field, "unit")) with
                {
                    EnabledWhenItemJsonKey = JsonString(field, "enabledWhenItemJsonKey"),
                    EnabledWhenItemValues = JsonStringArray(field, "enabledWhenItemValues"),
                    MinimumItemIndex = (int)JsonDecimal(field, "minimumItemIndex", 0),
                    UiOrder = (int)JsonDecimal(field, "uiOrder", 0),
                    UiSectionLabel = JsonString(field, "uiSectionLabel"),
                    Animation = ReadAnimationDefinition(field),
                    BehaviorTiming = ReadBehaviorTimingDefinition(field),
                    StructuredCollection = ReadNestedCollection(OptionalObject(
                        field,
                        "structuredCollection",
                        $"Runtime Input collection '{id}' field '{fieldId}'")),
                    AllowEmpty = field["allowEmpty"]?.GetValue<bool>() == true,
                    AllowEmptyWhenItemJsonKey = JsonString(field, "allowEmptyWhenItemJsonKey"),
                    AllowEmptyWhenItemValues = JsonStringArray(field, "allowEmptyWhenItemValues"),
                    ActionOnly = field["actionOnly"]?.GetValue<bool>() == true,
                    Transition = ReadInputTransitionDefinition(field),
                    OptionsSourceCollectionJsonKey = JsonString(field, "optionsSourceCollectionJsonKey"),
                    OptionsSourceValueJsonKey = JsonString(field, "optionsSourceValueJsonKey", "id"),
                    OptionsSourceLabelJsonKey = JsonString(field, "optionsSourceLabelJsonKey"),
                    OptionsSourceFirstItemBadge = JsonString(field, "optionsSourceFirstItemBadge"),
                    HelpText = JsonString(field, "helpText"),
                    ValuePattern = JsonString(field, "valuePattern"),
                    ValuePatternMessage = JsonString(field, "valuePatternMessage"),
                };
                itemFields.Add(definition);
            }

            var itemPresentation = ReadItemPresentation(collection);
            var componentItems = ReadComponentItems(collection);
            if (componentItems is not null)
            {
                var matchingFields = itemFields.Where((field) =>
                        field.JsonKey.Equals(
                            componentItems.VariantReferenceJsonKey,
                            StringComparison.Ordinal))
                    .ToList();
                if (matchingFields.Count != 1
                    || matchingFields[0].ValueKind != ValueKind.ComponentVariant)
                {
                    throw new InvalidOperationException(
                        $"Runtime Input collection '{id}' componentItems must reference one ComponentVariant field.");
                }
                if (itemFields.Any((field) =>
                        field.JsonKey.Equals(componentItems.OverridesJsonKey, StringComparison.Ordinal)
                        || field.JsonKey.Equals(componentItems.InputsJsonKey, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Runtime Input collection '{id}' componentItems object keys must not overlap field keys.");
                }
            }
            if (!isVisible && !includeHidden) continue;
            var uiPresentation = JsonString(collection, "uiPresentation", "collection");
            if (uiPresentation is not "collection" and not "itemSections")
            {
                throw new InvalidOperationException(
                    $"Runtime Input collection '{id}' has unsupported uiPresentation '{uiPresentation}'.");
            }
            var itemRuntimePresentation = JsonString(collection, "itemRuntimePresentation", "card");
            if (itemRuntimePresentation is not "card" and not "sections")
            {
                throw new InvalidOperationException(
                    $"Runtime Input collection '{id}' has unsupported itemRuntimePresentation '{itemRuntimePresentation}'.");
            }
            definitions.Add(new RuntimeInputCollectionDefinition(
                id,
                label,
                jsonKey,
                itemLabel,
                itemFields,
                JsonString(collection, "sourceCollectionJsonKey"),
                itemPresentation,
                componentItems,
                JsonString(collection, "storageCollectionJsonKey"),
                JsonString(collection, "itemRuntimeContractJsonKey"),
                JsonString(collection, "uiParentCollectionJsonKey"),
                JsonString(collection, "uiParentItemIdJsonKey"),
                JsonString(collection, "animationPresentation", "item"),
                collection["canEditStructure"]?.GetValue<bool>() ?? true,
                FixedCollectionItemCount(collection, config),
                uiPresentation,
                itemRuntimePresentation,
                JsonStringArray(collection, "itemRuntimeHiddenInputIds"),
                JsonString(collection, "itemRuntimeVariantReferencePath"),
                JsonString(collection, "itemRuntimeOwnerVariantReferencePath")));
        }
        return definitions;
    }

    private static ComponentInputDefinition CreateInputDefinition(
        string id,
        string label,
        string jsonKey,
        string kind,
        string valueKind,
        string defaultValue,
        IReadOnlyList<FieldOption> options,
        decimal minimum,
        decimal maximum,
        decimal increment,
        string tableId,
        string resolvedJsonKey,
        string componentType,
        ComponentInputSource source,
        PairFieldLabels? pairLabels,
        ComponentInputUiOrigin uiOrigin,
        string uiGroupId,
        string uiGroupLabel,
        string uiParentGroupId,
        string unit)
    {
        var normalizedValueKind = RuntimeInputValueKindContract.RequireCompatible(
            kind,
            valueKind,
            $"Runtime Input '{id}'");
        return new ComponentInputDefinition(
            id,
            label,
            jsonKey,
            ParseKind(kind),
            normalizedValueKind,
            defaultValue,
            options,
            minimum,
            maximum,
            increment,
            tableId,
            resolvedJsonKey,
            componentType,
            source,
            PairFieldLabelsContract.ForField(
                normalizedValueKind,
                pairLabels,
                $"Runtime Input '{id}'"),
            uiOrigin,
            uiGroupId,
            uiGroupLabel,
            uiParentGroupId,
            Unit: unit);
    }

    private static RuntimeInputCollectionDefinition? ReadNestedCollection(JsonObject? collection)
    {
        if (collection is null) return null;
        var wrapper = new JsonObject { ["collections"] = new JsonArray(collection.DeepClone()) };
        return ReadCollections(wrapper, new JsonObject()).SingleOrDefault();
    }

    private static int FixedCollectionItemCount(JsonObject collection, JsonObject config)
    {
        var path = JsonString(collection, "fixedCountPath");
        if (string.IsNullOrWhiteSpace(path)) return 0;
        var node = JsonPath.Get(config, path.Split('.', StringSplitOptions.RemoveEmptyEntries));
        if (node is null && config.Count == 0) return 0;
        if (node is not JsonValue value
            || !value.TryGetValue<double>(out var number)
            || number < 1
            || number != Math.Truncate(number))
        {
            throw new InvalidOperationException(
                $"Runtime Input collection '{JsonString(collection, "id")}' fixedCountPath '{path}' must resolve to a positive integer.");
        }
        return checked((int)number);
    }

    private static RuntimeInputCollectionItemPresentation? ReadItemPresentation(JsonObject collection)
    {
        var presentation = OptionalObject(
            collection,
            "itemPresentation",
            $"Runtime Input collection '{JsonString(collection, "id")}'");
        if (presentation is null) return null;
        var iconValueMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (presentation["iconValueMap"] is not null)
        {
            var mapping = presentation["iconValueMap"] as JsonObject
                ?? throw new InvalidOperationException(
                    "Runtime Input itemPresentation iconValueMap must be an object when present.");
            foreach (var (key, node) in mapping)
            {
                if (string.IsNullOrWhiteSpace(key)
                    || node is not JsonValue value
                    || !value.TryGetValue<string>(out var icon)
                    || string.IsNullOrWhiteSpace(icon))
                {
                    throw new InvalidOperationException(
                        "Runtime Input itemPresentation iconValueMap requires non-empty string keys and values.");
                }
                iconValueMap.Add(key, icon);
            }
        }
        return new RuntimeInputCollectionItemPresentation(
            JsonString(presentation, "titleFieldId"),
            JsonString(presentation, "firstItemBadge"),
            JsonStringArray(presentation, "subtitleFieldIds"),
            Math.Max(16, (int)JsonDecimal(presentation, "subtitleMaxCharacters", 72)),
            JsonString(presentation, "iconFieldId"),
            JsonString(presentation, "fallbackIcon", "component"),
            iconValueMap);
    }

    private static RuntimeComponentCollectionItemDefinition? ReadComponentItems(JsonObject collection)
    {
        var keys = RuntimeComponentCollectionItemDocumentContract.ReadDefinition(
            collection,
            $"Runtime Input collection '{JsonString(collection, "id")}'");
        return keys is null
            ? null
            : new RuntimeComponentCollectionItemDefinition(
                keys.VariantReferenceJsonKey,
                keys.OverridesJsonKey,
                keys.InputsJsonKey);
    }

    private static BehaviorTimingDefinition? ReadBehaviorTimingDefinition(JsonObject field) =>
        RuntimeInputValueKindContract.ReadBehaviorTimingDefinition(
            field,
            ownerDefinitions: null,
            "Runtime Input");

    private static bool InputIsVisible(JsonObject input, JsonObject config)
    {
        var path = JsonString(input, "visibleWhenPath");
        var expected = JsonString(input, "visibleWhenValue");
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(expected)) return true;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expected))
        {
            throw new InvalidOperationException(
                "Runtime Input visibility requires visibleWhenPath and visibleWhenValue together.");
        }
        var current = JsonPath.Get(config, path.Split('.', StringSplitOptions.RemoveEmptyEntries));
        return current is JsonValue value
            && value.TryGetValue<string>(out var text)
            && text.Equals(expected, StringComparison.Ordinal);
    }

    private static AnimationFieldDefinition? ReadAnimationDefinition(JsonObject input)
    {
        if (input["animatable"] is null) return null;
        if (input["animatable"] is not JsonValue enabled
            || !enabled.TryGetValue<bool>(out var isEnabled))
        {
            throw new InvalidOperationException(
                "Runtime Input animatable must be a JSON boolean when present.");
        }
        if (!isEnabled) return null;
        var interpolations = JsonStringArray(input, "animationInterpolations");
        var animationTimeline = OptionalObject(input, "animationTimeline", "Runtime Input animation");
        var extendsOwnerDuration = animationTimeline?["extendsOwnerDuration"] is null
            || JsonPath.RequiredBoolean(
                animationTimeline,
                "extendsOwnerDuration",
                "Runtime Input animationTimeline");
        var completion = animationTimeline is null
            ? null
            : OptionalObject(animationTimeline, "completion", "Runtime Input animationTimeline");
        var baseDurationFieldId = completion?["baseDurationFieldId"] is null
            ? ""
            : JsonPath.RequiredString(
                completion,
                "baseDurationFieldId",
                "Runtime Input animationTimeline completion");
        var minimumEnabledKeyframes = completion?["minimumEnabledKeyframes"] is null
            ? 2
            : JsonPath.RequiredInteger(
                completion,
                "minimumEnabledKeyframes",
                "Runtime Input animationTimeline completion");
        return interpolations.Count > 0
            ? new AnimationFieldDefinition(
                interpolations,
                extendsOwnerDuration,
                baseDurationFieldId,
                minimumEnabledKeyframes)
            : new AnimationFieldDefinition(
                ["hold"],
                extendsOwnerDuration,
                baseDurationFieldId,
                minimumEnabledKeyframes);
    }

    private static ComponentInputTransitionDefinition? ReadInputTransitionDefinition(JsonObject input)
    {
        var transition = OptionalObject(input, "transition", "Runtime Input");
        if (transition is null) return null;
        var targetInputId = JsonString(transition, "targetInputId");
        var replacementValue = JsonString(transition, "replacementValue");
        var triggerValues = JsonStringArray(transition, "triggerValues");
        if (string.IsNullOrWhiteSpace(targetInputId) || triggerValues.Count == 0)
        {
            throw new InvalidOperationException(
                "Component input transitions require targetInputId and triggerValues.");
        }
        return new ComponentInputTransitionDefinition(
            targetInputId,
            triggerValues,
            replacementValue,
            JsonString(transition, "targetValuePattern"),
            transition["forwardedTargetOnly"]?.GetValue<bool>() == true);
    }

    private static IReadOnlyList<FieldOption> ReadOptions(JsonObject input)
    {
        if (input["options"] is null) return [];
        var options = input["options"] as JsonArray
            ?? throw new InvalidOperationException(
                "Runtime Input options must be an array when present.");
        var values = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<FieldOption>(options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Runtime Input option at index {index} must be an object.");
            var value = JsonPath.RequiredString(
                option,
                "value",
                $"Runtime Input option at index {index}");
            var label = JsonPath.RequiredString(
                option,
                "label",
                $"Runtime Input option at index {index}");
            if (!values.Add(value))
            {
                throw new InvalidOperationException(
                    $"Runtime Input options contain duplicate value '{value}'.");
            }
            result.Add(new FieldOption(value, label));
        }
        return result;
    }

    private static IReadOnlyList<string> JsonStringArray(JsonObject input, string key)
    {
        if (input[key] is null) return [];
        var values = input[key] as JsonArray
            ?? throw new InvalidOperationException(
                $"Runtime Input {key} must be an array when present.");
        return values.Select((node, index) => node is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text)
                    ? text
                    : throw new InvalidOperationException(
                        $"Runtime Input {key}[{index}] must be a non-empty string."))
            .ToList();
    }

    private static ComponentInputKind ParseKind(string kind) =>
        kind.Trim().ToLowerInvariant() switch
        {
            "text" => ComponentInputKind.Text,
            "number" => ComponentInputKind.Number,
            "integerpair" => ComponentInputKind.IntegerPair,
            "boolean" => ComponentInputKind.Boolean,
            "option" => ComponentInputKind.Option,
            "recordreference" => ComponentInputKind.RecordReference,
            "componentvariant" => ComponentInputKind.ComponentVariant,
            "componentvariantslot" => ComponentInputKind.ComponentVariantSlot,
            "themetoken" => ComponentInputKind.ThemeToken,
            "icon" => ComponentInputKind.Icon,
            "iconlist" => ComponentInputKind.IconList,
            "multilinetext" => ComponentInputKind.MultilineText,
            "mediafilepath" or "behaviortiming" or "collection" => ComponentInputKind.Text,
            _ => throw new InvalidOperationException(
                $"Unsupported runtime input kind '{kind}'."),
        };

    private static ComponentInputSource ParseInputSource(string source) =>
        source switch
        {
            "" or "runtime" => ComponentInputSource.Runtime,
            "variant" => ComponentInputSource.Variant,
            "calculated" => ComponentInputSource.Calculated,
            _ => throw new InvalidOperationException(
                $"Unknown Runtime Input source '{source}'."),
        };

    private static ComponentInputUiOrigin ParseInputUiOrigin(string origin) =>
        origin switch
        {
            "" or "self" => ComponentInputUiOrigin.Self,
            "embedded" => ComponentInputUiOrigin.Embedded,
            _ => throw new InvalidOperationException(
                $"Unknown Runtime Input uiOrigin '{origin}'."),
        };

    private static PairFieldLabels? ReadPairLabels(JsonObject owner)
    {
        var first = JsonString(owner, "pairFirstLabel");
        var second = JsonString(owner, "pairSecondLabel");
        return string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second)
            ? null
            : new PairFieldLabels(first, second);
    }

    private static string JsonString(JsonObject owner, string key, string fallback = "")
    {
        if (owner[key] is null) return fallback;
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : throw new InvalidOperationException(
                $"Runtime Input {key} must be a string when present.");
    }

    private static decimal JsonDecimal(JsonObject owner, string key, decimal fallback)
    {
        if (owner[key] is null) return fallback;
        try
        {
            return (decimal)JsonPath.RequiredNumber(owner[key], $"Runtime Input {key}");
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                $"Runtime Input {key} must fit a decimal value.",
                exception);
        }
    }

    private static JsonObject? OptionalObject(JsonObject owner, string key, string context)
    {
        if (owner[key] is null) return null;
        return owner[key] as JsonObject
            ?? throw new InvalidOperationException(
                $"{context} {key} must be an object when present.");
    }
}
