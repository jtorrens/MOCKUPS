using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ComponentPreviewActions
{
    public static void ValidateContract(JsonObject preview, string owner)
    {
        ValidateContractNode(preview, owner, "$");
    }

    public static IReadOnlyList<ComponentPreviewActionDefinition> ReadWithEmbedded(
        JsonObject preview,
        Func<string, JsonObject> componentVariantRuntimeContract)
    {
        var definitions = Read(preview).ToList();
        if (!preview.TryGetPropertyValue("collections", out var collectionsNode))
        {
            return ValidateDurationInputReferences(preview, definitions);
        }
        var collections = collectionsNode as JsonArray
            ?? throw new InvalidOperationException(
                "Design Preview embedded action collections must be an array when present.");
        for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
        {
            var collection = RequiredObject(
                collections[collectionIndex],
                $"Design Preview collection at index {collectionIndex}");
            var collectionJsonKey = JsonString(collection, "jsonKey");
            var itemRuntimeContractJsonKey = JsonString(collection, "itemRuntimeContractJsonKey");
            if (!string.IsNullOrWhiteSpace(collectionJsonKey)
                && !string.IsNullOrWhiteSpace(itemRuntimeContractJsonKey)
                && preview.TryGetPropertyValue(collectionJsonKey, out var runtimeItemsNode))
            {
                var runtimeItems = runtimeItemsNode as JsonArray
                    ?? throw new InvalidOperationException(
                        $"Design Preview collection '{collectionJsonKey}' must be an array when present.");
                RuntimeCollectionDocumentContract.Validate(
                    runtimeItems,
                    $"Design Preview collection '{collectionJsonKey}'");
                for (var itemIndex = 0; itemIndex < runtimeItems.Count; itemIndex++)
                {
                    var item = RequiredObject(
                        runtimeItems[itemIndex],
                        $"Design Preview collection '{collectionJsonKey}' item at index {itemIndex}");
                    var itemId = JsonPath.RequiredString(
                        item,
                        "id",
                        $"Design Preview collection '{collectionJsonKey}' item at index {itemIndex}");
                    var itemContract = JsonPath.RequiredObject(
                        item,
                        itemRuntimeContractJsonKey,
                        $"Design Preview collection '{collectionJsonKey}' item '{itemId}'");
                    definitions.AddRange(Read(itemContract)
                        .Where((action) => !action.IsCollectionItemAction)
                        .Select((action) => action with
                        {
                            Id = $"embedded:{collectionJsonKey}:{itemId}:{itemRuntimeContractJsonKey}:{action.Id}",
                            CollectionJsonKey = collectionJsonKey,
                            CollectionItemId = itemId,
                            TargetJsonPath = itemRuntimeContractJsonKey,
                        }));
                }
            }
            var componentItems = RuntimeComponentCollectionItemDocumentContract.ReadDefinition(
                collection,
                $"Design Preview collection '{collectionJsonKey}'");
            if (string.IsNullOrWhiteSpace(collectionJsonKey) || componentItems is null) continue;
            var inputsJsonKey = componentItems.InputsJsonKey;
            if (!preview.TryGetPropertyValue(collectionJsonKey, out var itemsNode)) continue;
            var items = itemsNode as JsonArray
                ?? throw new InvalidOperationException(
                    $"Design Preview collection '{collectionJsonKey}' must be an array when present.");
            RuntimeCollectionDocumentContract.Validate(
                items,
                $"Design Preview collection '{collectionJsonKey}'");
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var item = RequiredObject(
                    items[itemIndex],
                    $"Design Preview collection '{collectionJsonKey}' item at index {itemIndex}");
                var itemId = JsonString(item, "id");
                RuntimeComponentCollectionItemDocumentContract.ValidateItem(
                    item,
                    componentItems,
                    $"Design Preview collection '{collectionJsonKey}' item '{itemId}'");
                var variantReference = RuntimeComponentCollectionItemDocumentContract.RequireVariantReference(
                    item,
                    componentItems,
                    $"Design Preview collection '{collectionJsonKey}' item '{itemId}'");
                if (variantReference.Length == 0) continue;
                var childContract = componentVariantRuntimeContract(variantReference);
                definitions.AddRange(Read(childContract)
                    .Where((action) => !action.IsCollectionItemAction)
                    .Select((action) => action with
                    {
                        Id = $"embedded:{collectionJsonKey}:{itemId}:{inputsJsonKey}:{action.Id}",
                        CollectionJsonKey = collectionJsonKey,
                        CollectionItemId = itemId,
                        TargetJsonPath = inputsJsonKey,
                    }));
            }
        }
        return ValidateDurationInputReferences(preview, definitions);
    }

    public static IReadOnlyList<ComponentPreviewActionDefinition> Read(JsonObject preview)
    {
        ValidateContract(preview, "Design Preview action contract");
        var definitions = new List<ComponentPreviewActionDefinition>();
        if (preview["actions"] is JsonArray actions)
        {
            definitions.AddRange(actions
                .Select((action, index) => RequiredObject(
                    action,
                    $"Design Preview action contract $.actions[{index}]"))
                .Select((action) => ParseAction(action)));
        }

        if (!preview.TryGetPropertyValue("collections", out var collectionsNode))
        {
            return ValidateDurationInputReferences(preview, definitions);
        }
        var collections = collectionsNode as JsonArray
            ?? throw new InvalidOperationException(
                "Design Preview action contract collections must be an array when present.");

        for (var collectionIndex = 0; collectionIndex < collections.Count; collectionIndex++)
        {
            var collection = RequiredObject(
                collections[collectionIndex],
                $"Design Preview action contract $.collections[{collectionIndex}]");
            var collectionJsonKey = JsonString(collection, "jsonKey");
            if (string.IsNullOrWhiteSpace(collectionJsonKey)
                || !collection.ContainsKey("itemActions"))
            {
                continue;
            }
            var itemActions = collection["itemActions"] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Design Preview collection '{collectionJsonKey}' itemActions must be an array when present.");

            var items = DesignPreviewTestValues.CollectionItems(
                preview,
                new RuntimeInputCollectionDefinition(
                    JsonString(collection, "id"),
                    JsonString(collection, "label"),
                    collectionJsonKey,
                    string.IsNullOrWhiteSpace(JsonString(collection, "itemLabel"))
                        ? "Item"
                        : JsonString(collection, "itemLabel"),
                    [],
                    JsonString(collection, "sourceCollectionJsonKey")));
            foreach (var item in items)
            {
                var itemId = JsonString(item, "id");
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                for (var actionIndex = 0; actionIndex < itemActions.Count; actionIndex++)
                {
                    var itemAction = RequiredObject(
                        itemActions[actionIndex],
                        $"Design Preview collection '{collectionJsonKey}' itemActions[{actionIndex}]");
                    var action = itemAction.DeepClone().AsObject();
                    var baseId = RequiredString(action, "id", "Design Preview item action");
                    action["id"] = $"{baseId}:{itemId}";
                    definitions.Add(ParseAction(action, collectionJsonKey, itemId));
                }
            }
        }

        return ValidateDurationInputReferences(preview, definitions);
    }

    public static JsonNode? Value(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key)
    {
        return Target(preview, action)?[key];
    }

    public static JsonObject RequiredOwner(
        JsonObject preview,
        ComponentPreviewActionDefinition action) =>
        Target(preview, action)
        ?? throw new InvalidOperationException(
            $"Design Preview action '{action.Id}' has no current runtime owner.");

    public static string DurationJsonKey(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.DurationInputId))
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' has no durationInputId.");
        }
        if (!string.IsNullOrWhiteSpace(action.DurationJsonKey))
        {
            return action.DurationJsonKey;
        }

        var definitions = DurationInputDefinitions(preview, action);
        var definition = definitions
            .Select((node, index) => RequiredObject(
                node,
                $"Design Preview action '{action.Id}' duration definition at index {index}"))
            .SingleOrDefault((candidate) =>
                JsonString(candidate, "id").Equals(action.DurationInputId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' references missing duration field id '{action.DurationInputId}'.");
        return RequiredString(
            definition,
            "jsonKey",
            $"Design Preview action '{action.Id}' duration field '{action.DurationInputId}'");
    }

    public static void SetValue(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key,
        object value)
    {
        var target = Target(preview, action);
        if (target is not null)
        {
            target[key] = JsonValue.Create(value);
        }
    }

    public static void SetStoredValue(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key,
        string value)
    {
        var target = Target(preview, action);
        if (target is null) return;
        target[key] = target[key] switch
        {
            JsonValue existing when existing.TryGetValue<bool>(out _) =>
                JsonValue.Create(BooleanText.ParseRequired(value, $"Preview action value '{key}'")),
            JsonValue existing when existing.TryGetValue<int>(out _) =>
                JsonValue.Create(ParseStoredInteger(value, key)),
            JsonValue existing when existing.TryGetValue<double>(out _) =>
                JsonValue.Create(ParseStoredNumber(value, key)),
            _ => JsonValue.Create(value),
        };
    }

    public static void RemoveValue(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string key)
    {
        Target(preview, action)?.Remove(key);
    }

    public static bool AppliesToItem(ComponentPreviewActionDefinition action, JsonObject item)
    {
        if (string.IsNullOrWhiteSpace(action.VisibleWhenItemJsonKey)
            || action.VisibleWhenItemValues is not { Count: > 0 })
        {
            return true;
        }

        var current = JsonString(item, action.VisibleWhenItemJsonKey);
        return action.VisibleWhenItemValues.Contains(current, StringComparer.Ordinal);
    }

    public static bool IsApplicable(JsonObject preview, ComponentPreviewActionDefinition action)
    {
        if (!action.IsCollectionItemAction) return true;
        var item = Target(preview, action);
        return item is not null && AppliesToItem(action, item);
    }

    public static IReadOnlyList<ComponentPreviewActionDefinition> ReadApplicable(JsonObject preview)
    {
        return Read(preview).Where((action) => IsApplicable(preview, action)).ToList();
    }

    private static JsonObject? Target(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        if (!action.IsCollectionItemAction)
        {
            return preview;
        }

        JsonObject? item = null;
        if (preview[action.CollectionJsonKey] is JsonArray items)
        {
            RuntimeCollectionDocumentContract.Validate(
                items,
                $"Design Preview action collection '{action.CollectionJsonKey}'");
            item = items
                .Select((candidate, index) => RequiredObject(
                    candidate,
                    $"Design Preview action collection '{action.CollectionJsonKey}' item at index {index}"))
                .FirstOrDefault((candidate) =>
                    candidate["id"] is JsonValue value
                    && value.TryGetValue<string>(out var id)
                    && id == action.CollectionItemId);
        }
        if (item is null || string.IsNullOrWhiteSpace(action.TargetJsonPath)) return item;
        JsonNode? target = item;
        foreach (var segment in action.TargetJsonPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            target = target is JsonObject owner ? owner[segment] : null;
        }
        return target as JsonObject;
    }

    private static JsonArray DurationInputDefinitions(
        JsonObject preview,
        ComponentPreviewActionDefinition action)
    {
        if (!string.IsNullOrWhiteSpace(action.TargetJsonPath))
        {
            var owner = RequiredOwner(preview, action);
            return owner["inputs"] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Design Preview action '{action.Id}' embedded owner requires an inputs array.");
        }

        if (!action.IsCollectionItemAction)
        {
            return preview["inputs"] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Design Preview action '{action.Id}' owner requires an inputs array.");
        }

        var collections = preview["collections"] as JsonArray
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' owner requires a collections array.");
        var collection = collections
            .Select((node, index) => RequiredObject(
                node,
                $"Design Preview action '{action.Id}' collection at index {index}"))
            .SingleOrDefault((candidate) =>
                JsonString(candidate, "jsonKey").Equals(action.CollectionJsonKey, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' collection '{action.CollectionJsonKey}' is missing.");
        return collection["fields"] as JsonArray
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' collection '{action.CollectionJsonKey}' requires a fields array.");
    }

    private static IReadOnlyList<ComponentPreviewActionDefinition> ValidateDurationInputReferences(
        JsonObject preview,
        IReadOnlyList<ComponentPreviewActionDefinition> definitions)
    {
        return definitions.Select((action) =>
            string.IsNullOrWhiteSpace(action.DurationInputId)
                ? action
                : action with { DurationJsonKey = DurationJsonKey(preview, action) })
            .ToList();
    }

    private static ComponentPreviewActionDefinition ParseAction(
        JsonObject action,
        string collectionJsonKey = "",
        string collectionItemId = "")
    {
        ValidateActionDefinition(action, "Design Preview action");
        var playInputId = JsonString(action, "playInputId");
        var durationInputId = JsonString(action, "durationInputId");
        var durationBehaviorTimingInputId = JsonString(action, "durationBehaviorTimingInputId");
        var durationCollectionJsonKey = JsonString(action, "durationCollectionJsonKey");
        var durationThemeToken = JsonString(action, "durationThemeToken");
        var durationStateCollectionJsonKey = JsonString(action, "durationStateCollectionJsonKey");
        var durationOwnerTimeline = JsonBoolean(action, "durationOwnerTimeline", false);
        var durationSeconds = JsonNumber(action, "durationSeconds", 0);
        var timeJsonKey = JsonString(action, "timeJsonKey");
        var id = RequiredString(action, "id", "Design Preview action");
        var label = RequiredString(action, "label", $"Design Preview action '{id}'");

        return new ComponentPreviewActionDefinition(
            id,
            label,
            playInputId,
            durationInputId,
            "",
            durationBehaviorTimingInputId,
            durationSeconds,
            durationCollectionJsonKey,
            durationThemeToken,
            durationStateCollectionJsonKey,
            JsonString(action, "durationStateIdJsonKey"),
            JsonString(action, "durationEnterMotionJsonKey"),
            JsonString(action, "durationExitMotionJsonKey"),
            JsonStringArray(action, "durationAdditionalThemeTokens"),
            JsonStringArray(action, "durationItemNumberKeys"),
            JsonStringArray(action, "durationCollectionMultiplierNumberKeys"),
            JsonNumber(action, "durationBaseFrames", 0),
            durationOwnerTimeline,
            timeJsonKey,
            ParseTimeUnit(JsonString(action, "timeUnit")),
            ParseCompletionBehavior(JsonString(action, "completionBehavior")),
            JsonBoolean(action, "prewarmFrames", true),
            JsonString(action, "prewarmWhenJsonKey"),
            JsonString(action, "prewarmWhenConfigPath"),
            JsonString(action, "prewarmWhenValue"),
            JsonStringArray(action, "activateInputIds"),
            JsonStringArray(action, "deactivateInputIds"),
            JsonString(action, "targetInputId"),
            ParseTargetMode(JsonString(action, "targetMode")),
            JsonString(action, "targetFromJsonKey"),
            ParseTargetOptions(action),
            collectionJsonKey,
            collectionItemId,
            "",
            JsonString(action, "visibleWhenItemJsonKey"),
            JsonStringArray(action, "visibleWhenItemValues"));
    }

    public static double MotionStateTransitionDurationMilliseconds(
        JsonObject preview,
        ComponentPreviewActionDefinition action,
        string themeTokensJson)
    {
        if (string.IsNullOrWhiteSpace(action.DurationStateCollectionJsonKey)) return 0;
        var target = Target(preview, action)
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' has no target owner.");
        var states = target[action.DurationStateCollectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' requires state collection '{action.DurationStateCollectionJsonKey}'.");
        RuntimeCollectionDocumentContract.Validate(
            states,
            $"Design Preview action state collection '{action.DurationStateCollectionJsonKey}'");
        var theme = JsonPath.ParseRequiredObject(themeTokensJson, "Theme tokens");
        var stateIdKey = action.DurationStateIdJsonKey;
        var targetId = JsonString(target, action.TargetInputId);
        var fromId = JsonString(target, action.TargetFromJsonKey);
        var stateObjects = states
            .Select((state, index) => RequiredObject(
                state,
                $"Design Preview action state at index {index}"))
            .ToList();
        var duration = Math.Max(
            StateMotionDurationMilliseconds(
                theme,
                stateObjects,
                stateIdKey,
                targetId,
                action.DurationEnterMotionJsonKey,
                action.Id,
                "target"),
            StateMotionDurationMilliseconds(
                theme,
                stateObjects,
                stateIdKey,
                fromId,
                action.DurationExitMotionJsonKey,
                action.Id,
                "source"));
        foreach (var token in action.DurationAdditionalThemeTokens)
        {
            duration = Math.Max(
                duration,
                ThemeNumericTokenValue.RequirePositive(
                    theme,
                    token,
                    $"Design Preview action '{action.Id}' duration"));
        }
        return duration;
    }

    private static double StateMotionDurationMilliseconds(
        JsonObject theme,
        IReadOnlyList<JsonObject> states,
        string stateIdKey,
        string stateId,
        string motionKey,
        string actionId,
        string role)
    {
        // State selections are session values and do not exist before an action has a
        // concrete transition. Once present, they must resolve to an exact authored State.
        if (string.IsNullOrWhiteSpace(stateId)) return 0;
        var state = states.SingleOrDefault((candidate) => JsonString(candidate, stateIdKey) == stateId)
            ?? throw new InvalidOperationException(
                $"Design Preview action '{actionId}' {role} State '{stateId}' does not exist.");
        return MotionDurationMilliseconds(
            theme,
            JsonPath.RequiredObject(
                state,
                motionKey,
                $"Design Preview action '{actionId}' {role} State '{stateId}'"));
    }

    private static double MotionDurationMilliseconds(JsonObject theme, JsonObject motion)
    {
        return MotionTimingDuration.ResolveMilliseconds(
            theme,
            motion,
            "Design Preview State Motion");
    }

    private static ComponentPreviewActionTargetMode ParseTargetMode(string value)
    {
        return value switch
        {
            "" => ComponentPreviewActionTargetMode.None,
            "toggle" => ComponentPreviewActionTargetMode.Toggle,
            "option" => ComponentPreviewActionTargetMode.Option,
            "value" => ComponentPreviewActionTargetMode.Value,
            _ => throw new InvalidOperationException($"Unknown component preview action targetMode '{value}'."),
        };
    }

    private static IReadOnlyList<FieldOption> ParseTargetOptions(JsonObject action)
    {
        if (!action.ContainsKey("targetOptions"))
        {
            return [];
        }
        var options = action["targetOptions"] as JsonArray
            ?? throw new InvalidOperationException("Design Preview action targetOptions must be an array when present.");
        return options
            .Select((option, index) => RequiredObject(
                option,
                $"Design Preview action targetOptions[{index}]"))
            .Select((option, index) => new FieldOption(
                RequiredString(option, "value", $"Design Preview action targetOptions[{index}]"),
                RequiredString(option, "label", $"Design Preview action targetOptions[{index}]")))
            .ToList();
    }

    private static ComponentPreviewActionTimeUnit ParseTimeUnit(string value)
    {
        return value switch
        {
            "milliseconds" => ComponentPreviewActionTimeUnit.Milliseconds,
            "frames" => ComponentPreviewActionTimeUnit.Frames,
            "seconds" => ComponentPreviewActionTimeUnit.Seconds,
            _ => throw new InvalidOperationException(
                $"Missing or unknown component preview action timeUnit '{value}'."),
        };
    }

    private static ComponentPreviewActionCompletionBehavior ParseCompletionBehavior(string value)
    {
        if (value.Equals("reset", StringComparison.Ordinal))
        {
            return ComponentPreviewActionCompletionBehavior.Reset;
        }
        if (value.Equals("holdFinal", StringComparison.Ordinal))
        {
            return ComponentPreviewActionCompletionBehavior.HoldFinal;
        }
        throw new InvalidOperationException($"Missing or unknown component preview action completionBehavior '{value}'.");
    }

    private static IReadOnlyList<string> JsonStringArray(JsonObject owner, string key)
    {
        if (!owner.ContainsKey(key))
        {
            return [];
        }
        var values = owner[key] as JsonArray
            ?? throw new InvalidOperationException(
                $"Design Preview action {key} must be an array when present.");

        return values.Select((value, index) => value is JsonValue item
                && item.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text)
                    ? text
                    : throw new InvalidOperationException(
                        $"Design Preview action {key}[{index}] must be a non-empty string."))
            .ToList();
    }

    private static string JsonString(JsonObject owner, string key)
    {
        if (!owner.ContainsKey(key))
        {
            return "";
        }
        return owner[key] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : throw new InvalidOperationException(
                $"Design Preview action {key} must be a string when present.");
    }

    private static double JsonNumber(JsonObject owner, string key, double fallback)
    {
        if (!owner.ContainsKey(key))
        {
            return fallback;
        }
        if (owner[key] is not JsonValue value)
        {
            throw new InvalidOperationException(
                $"Design Preview action {key} must be a finite number when present.");
        }

        if (TryJsonNumber(value, out var number))
        {
            return number;
        }

        throw new InvalidOperationException(
            $"Design Preview action {key} must be a finite JSON number when present.");
    }

    private static bool JsonBoolean(JsonObject owner, string key, bool fallback)
    {
        if (!owner.ContainsKey(key))
        {
            return fallback;
        }
        if (owner[key] is not JsonValue value)
        {
            throw new InvalidOperationException(
                $"Design Preview action {key} must be a boolean when present.");
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        throw new InvalidOperationException(
            $"Design Preview action {key} must be a JSON boolean when present.");
    }

    private static bool TryJsonNumber(JsonValue value, out double number)
    {
        if (value.TryGetValue<double>(out number) && double.IsFinite(number))
        {
            return true;
        }
        if (value.TryGetValue<decimal>(out var decimalValue))
        {
            number = (double)decimalValue;
            return double.IsFinite(number);
        }
        if (value.TryGetValue<long>(out var longValue))
        {
            number = longValue;
            return true;
        }
        if (value.TryGetValue<int>(out var integerValue))
        {
            number = integerValue;
            return true;
        }
        number = 0;
        return false;
    }

    private static void ValidateContractNode(JsonNode? node, string owner, string path)
    {
        if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                ValidateContractNode(array[index], owner, $"{path}[{index}]");
            }
            return;
        }
        if (node is not JsonObject obj)
        {
            return;
        }

        ValidateActionArray(obj, "actions", owner, path);
        ValidateActionArray(obj, "itemActions", owner, path);
        foreach (var (key, child) in obj)
        {
            ValidateContractNode(child, owner, $"{path}.{key}");
        }
    }

    private static void ValidateActionArray(
        JsonObject ownerDocument,
        string key,
        string owner,
        string path)
    {
        if (!ownerDocument.ContainsKey(key))
        {
            return;
        }
        var actions = ownerDocument[key] as JsonArray
            ?? throw new InvalidOperationException(
                $"{owner} {path}.{key} must be an array when present.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < actions.Count; index++)
        {
            var actionOwner = $"{owner} {path}.{key}[{index}]";
            var action = RequiredObject(actions[index], actionOwner);
            ValidateActionDefinition(action, actionOwner);
            var id = RequiredString(action, "id", actionOwner);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException(
                    $"{owner} {path}.{key} has duplicate action id '{id}'.");
            }
        }
    }

    private static void ValidateActionDefinition(JsonObject action, string owner)
    {
        _ = RequiredString(action, "id", owner);
        _ = RequiredString(action, "label", owner);
        _ = RequiredString(action, "playInputId", owner);
        _ = RequiredString(action, "timeJsonKey", owner);
        _ = ParseTimeUnit(RequiredString(action, "timeUnit", owner));
        _ = ParseCompletionBehavior(RequiredString(action, "completionBehavior", owner));

        var durationInputId = JsonString(action, "durationInputId");
        var durationBehaviorTimingInputId = JsonString(action, "durationBehaviorTimingInputId");
        var durationCollectionJsonKey = JsonString(action, "durationCollectionJsonKey");
        var durationThemeToken = JsonString(action, "durationThemeToken");
        var durationStateCollectionJsonKey = JsonString(action, "durationStateCollectionJsonKey");
        var durationMotionConfigPath = JsonString(action, "durationMotionConfigPath");
        var targetInputId = JsonString(action, "targetInputId");
        _ = JsonString(action, "playFieldId");
        _ = JsonString(action, "durationEnabledInputId");
        _ = JsonString(action, "prewarmWhenJsonKey");
        _ = JsonString(action, "prewarmWhenConfigPath");
        _ = JsonString(action, "prewarmWhenValue");
        var durationOwnerTimeline = JsonBoolean(action, "durationOwnerTimeline", false);
        var durationSeconds = JsonNumber(action, "durationSeconds", 0);
        var durationBaseFrames = JsonNumber(action, "durationBaseFrames", 0);
        if (string.IsNullOrWhiteSpace(durationInputId)
            && string.IsNullOrWhiteSpace(durationBehaviorTimingInputId)
            && string.IsNullOrWhiteSpace(durationCollectionJsonKey)
            && string.IsNullOrWhiteSpace(durationThemeToken)
            && string.IsNullOrWhiteSpace(durationStateCollectionJsonKey)
            && string.IsNullOrWhiteSpace(durationMotionConfigPath)
            && !durationOwnerTimeline
            && durationSeconds <= 0
            && durationBaseFrames <= 0)
        {
            throw new InvalidOperationException(
                $"{owner} requires one explicit finite duration source.");
        }
        if (action.ContainsKey("durationSeconds") && durationSeconds <= 0)
        {
            throw new InvalidOperationException($"{owner} durationSeconds must be positive when present.");
        }
        if (action.ContainsKey("durationBaseFrames") && durationBaseFrames < 0)
        {
            throw new InvalidOperationException($"{owner} durationBaseFrames must not be negative.");
        }

        _ = JsonBoolean(action, "prewarmFrames", true);
        _ = JsonBoolean(action, "definesModuleDuration", false);
        _ = JsonBoolean(action, "extendsModuleDuration", false);
        if (!string.IsNullOrWhiteSpace(durationThemeToken)
            && !ThemeNumericTokenCatalog.TryGet(durationThemeToken, out _))
        {
            throw new InvalidOperationException(
                $"{owner} durationThemeToken '{durationThemeToken}' is not declared.");
        }
        var durationAdditionalThemeTokens = JsonStringArray(action, "durationAdditionalThemeTokens");
        foreach (var token in durationAdditionalThemeTokens)
        {
            if (!ThemeNumericTokenCatalog.TryGet(token, out _))
            {
                throw new InvalidOperationException(
                    $"{owner} durationAdditionalThemeToken '{token}' is not declared.");
            }
        }
        var hasStateDuration = !string.IsNullOrWhiteSpace(durationStateCollectionJsonKey);
        foreach (var (key, value) in new[]
                 {
                     ("durationStateIdJsonKey", JsonString(action, "durationStateIdJsonKey")),
                     ("durationEnterMotionJsonKey", JsonString(action, "durationEnterMotionJsonKey")),
                     ("durationExitMotionJsonKey", JsonString(action, "durationExitMotionJsonKey")),
                     ("targetFromJsonKey", JsonString(action, "targetFromJsonKey")),
                 })
        {
            if (hasStateDuration && string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{owner} {key} is required by durationStateCollectionJsonKey.");
            }
        }
        if (hasStateDuration && string.IsNullOrWhiteSpace(targetInputId))
        {
            throw new InvalidOperationException(
                $"{owner} state duration requires targetInputId.");
        }
        _ = JsonStringArray(action, "durationItemNumberKeys");
        _ = JsonStringArray(action, "durationCollectionMultiplierNumberKeys");
        _ = JsonStringArray(action, "activateInputIds");
        _ = JsonStringArray(action, "deactivateInputIds");
        _ = ParseTargetOptions(action);

        var targetMode = ParseTargetMode(JsonString(action, "targetMode"));
        if ((targetMode == ComponentPreviewActionTargetMode.None) != string.IsNullOrWhiteSpace(targetInputId))
        {
            throw new InvalidOperationException(
                $"{owner} targetInputId and targetMode must be declared together.");
        }
        var visibleKey = JsonString(action, "visibleWhenItemJsonKey");
        var visibleValues = JsonStringArray(action, "visibleWhenItemValues");
        if (string.IsNullOrWhiteSpace(visibleKey) != (visibleValues.Count == 0))
        {
            throw new InvalidOperationException(
                $"{owner} visibleWhenItemJsonKey and visibleWhenItemValues must be declared together.");
        }
    }

    private static JsonObject RequiredObject(JsonNode? node, string owner)
    {
        return node as JsonObject
            ?? throw new InvalidOperationException($"{owner} must be an object.");
    }

    private static string RequiredString(JsonObject ownerDocument, string key, string owner)
    {
        var value = JsonString(ownerDocument, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{owner} requires non-empty string '{key}'.");
        }
        return value;
    }

    private static int ParseStoredInteger(string value, string key)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Preview action value '{key}' must be an integer.");
    }

    private static double ParseStoredNumber(string value, string key)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed)
                ? parsed
                : throw new InvalidOperationException(
                    $"Preview action value '{key}' must be a finite number.");
    }
}

internal sealed record ComponentPreviewActionDefinition(
    string Id,
    string Label,
    string PlayInputId,
    string DurationInputId,
    string DurationJsonKey,
    string DurationBehaviorTimingInputId,
    double DurationSeconds,
    string DurationCollectionJsonKey,
    string DurationThemeToken,
    string DurationStateCollectionJsonKey,
    string DurationStateIdJsonKey,
    string DurationEnterMotionJsonKey,
    string DurationExitMotionJsonKey,
    IReadOnlyList<string> DurationAdditionalThemeTokens,
    IReadOnlyList<string> DurationItemNumberKeys,
    IReadOnlyList<string> DurationCollectionMultiplierNumberKeys,
    double DurationBaseFrames,
    bool DurationOwnerTimeline,
    string TimeJsonKey,
    ComponentPreviewActionTimeUnit TimeUnit,
    ComponentPreviewActionCompletionBehavior CompletionBehavior,
    bool PrewarmFrames,
    string PrewarmWhenJsonKey,
    string PrewarmWhenConfigPath,
    string PrewarmWhenValue,
    IReadOnlyList<string> ActivateInputIds,
    IReadOnlyList<string> DeactivateInputIds,
    string TargetInputId,
    ComponentPreviewActionTargetMode TargetMode,
    string TargetFromJsonKey,
    IReadOnlyList<FieldOption> TargetOptions,
    string CollectionJsonKey = "",
    string CollectionItemId = "",
    string TargetJsonPath = "",
    string VisibleWhenItemJsonKey = "",
    IReadOnlyList<string>? VisibleWhenItemValues = null)
{
    public bool IsCollectionItemAction => !string.IsNullOrWhiteSpace(CollectionJsonKey)
        && !string.IsNullOrWhiteSpace(CollectionItemId);
}

internal enum ComponentPreviewActionTargetMode
{
    None,
    Toggle,
    Option,
    Value,
}

internal enum ComponentPreviewActionTimeUnit
{
    Seconds,
    Frames,
    Milliseconds,
}

internal enum ComponentPreviewActionCompletionBehavior
{
    Reset,
    HoldFinal,
}
