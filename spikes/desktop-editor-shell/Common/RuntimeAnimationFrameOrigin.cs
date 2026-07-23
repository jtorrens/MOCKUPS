using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class RuntimeAnimationFrameOrigin
{
    public static int ScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        string fieldId,
        string targetId) => ScreenFrame(contract, runtime, new JsonObject(), fieldId, targetId);

    public static int ScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).ScreenFrame(fieldId, targetId, 0);

    public static int ScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        int localFrame,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).ScreenFrame(fieldId, targetId, localFrame);

    public static double LocalFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        int screenFrame,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).LocalFrame(fieldId, targetId, screenFrame);

    public static int DurationFrames(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        int storedFallback,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, storedFallback, themeTokens).DurationFrames;

    public static int OwnerNaturalDuration(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        JsonObject? themeTokens = null) =>
        Math.Max(1, Round(Model(contract, runtime, animation, themeTokens: themeTokens).OwnerNaturalDuration(targetId)));

    public static double OwnerLocalFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        int screenFrame,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).OwnerLocalFrame(targetId, screenFrame);

    public static int ScreenFrameForOwnerFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        double ownerFrame,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).ScreenFrameForOwnerFrame(targetId, ownerFrame);

    public static double FieldOwnerFrameOrigin(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).FieldOwnerFrameOrigin(fieldId, targetId);

    public static int FieldReferenceDurationFrames(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).FieldReferenceDurationFrames(fieldId, targetId);

    public static int CollectionDurationFrames(
        JsonObject collection,
        JsonArray items,
        JsonObject animation)
    {
        var contract = new JsonObject { ["collections"] = new JsonArray(collection.DeepClone()) };
        var key = CollectionKey(collection);
        var runtime = new JsonObject { [key] = items.DeepClone() };
        return Model(contract, runtime, animation).DurationFrames;
    }

    private static TimelineModel Model(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        int storedFallback = 0,
        JsonObject? themeTokens = null) => new(contract, runtime, animation, storedFallback, themeTokens ?? new JsonObject());

    private sealed class TimelineModel
    {
        private readonly JsonObject _contract;
        private readonly JsonObject _runtime;
        private readonly JsonObject _animation;
        private readonly JsonObject _themeTokens;
        private readonly Dictionary<string, ItemTiming> _items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FieldTiming> _topFields = new(StringComparer.Ordinal);
        private readonly double _naturalDuration;
        private readonly double _effectiveDuration;

        public TimelineModel(JsonObject contract, JsonObject runtime, JsonObject animation, int storedFallback, JsonObject themeTokens)
        {
            ValidateAnimationEnvelope(animation);
            _contract = contract;
            _runtime = runtime;
            _animation = animation;
            _themeTokens = themeTokens;
            var naturalEnd = (double)Math.Max(1, DeclaredBaseDuration(contract));
            foreach (var collection in Collections(contract))
            {
                var key = CollectionKey(collection);
                if (!runtime.TryGetPropertyValue(key, out var collectionNode)) continue;
                if (collectionNode is not JsonArray values)
                {
                    throw new InvalidOperationException(
                        $"Runtime owner collection '{key}' must be a JSON array when present.");
                }
                var sequenceItems = Timeline(collection)["sequenceItems"]?.GetValue<bool>() != false;
                var cursor = 0d;
                foreach (var item in JsonPath.ObjectItems(values, $"Runtime owner collection '{key}'"))
                {
                    var targetId = JsonPath.RequiredString(
                        item,
                        "id",
                        $"Runtime owner collection '{key}' item");
                    var fields = Fields(collection, item);
                    var pre = StringArray(collection, "preDurationFieldIds")
                        .Sum((fieldId) => FieldValue(item, fields, fieldId));
                    var start = (sequenceItems ? cursor : ItemOwnerOrigin(collection, item)) + pre;
                    var durations = CalculateItemDurations(collection, item, targetId);
                    var effectiveSpan = TargetDuration(targetId, durations.Span);
                    var effectiveSequence = Scale(durations.Sequence, durations.Span, effectiveSpan);
                    _items[targetId] = new ItemTiming(
                        collection,
                        item,
                        start,
                        durations.Span,
                        effectiveSpan,
                        durations.Sequence,
                        effectiveSequence);
                    if (sequenceItems) cursor = start + effectiveSequence;
                    naturalEnd = Math.Max(naturalEnd, start + effectiveSpan);
                }
                if (sequenceItems) naturalEnd = Math.Max(naturalEnd, cursor);
            }

            foreach (var definition in Inputs(contract))
            {
                var fieldId = Text(definition["id"]);
                if (string.IsNullOrWhiteSpace(fieldId)) continue;
                var timing = ResolveFieldTiming(definition, runtime, "", Inputs(contract), new HashSet<string>(StringComparer.Ordinal));
                _topFields[fieldId] = timing;
                naturalEnd = Math.Max(naturalEnd, timing.EndExclusive);
            }
            if (naturalEnd <= 1 && storedFallback > 0) naturalEnd = storedFallback;
            _naturalDuration = Math.Max(1, naturalEnd);
            _effectiveDuration = RootTargetDuration(_naturalDuration);
        }

        public int DurationFrames => Math.Max(1, Round(_effectiveDuration));

        public double OwnerNaturalDuration(string targetId) =>
            string.IsNullOrWhiteSpace(targetId)
                ? _naturalDuration
                : _items.TryGetValue(targetId, out var item) ? item.NaturalSpan : 1;

        public double OwnerLocalFrame(string targetId, int screenFrame)
        {
            var rootNatural = Unscale(Math.Max(0, screenFrame), _naturalDuration, _effectiveDuration);
            if (string.IsNullOrWhiteSpace(targetId)) return rootNatural;
            if (!_items.TryGetValue(targetId, out var item)) return 0;
            return Unscale(rootNatural - item.RootStart, item.NaturalSpan, item.EffectiveSpan);
        }

        public int ScreenFrameForOwnerFrame(string targetId, double ownerFrame)
        {
            var rootNatural = ownerFrame;
            if (!string.IsNullOrWhiteSpace(targetId) && _items.TryGetValue(targetId, out var item))
            {
                rootNatural = item.RootStart + Scale(ownerFrame, item.NaturalSpan, item.EffectiveSpan);
            }
            return Round(Scale(rootNatural, _naturalDuration, _effectiveDuration));
        }

        public double FieldOwnerFrameOrigin(string fieldId, string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return TopField(fieldId).Origin;
            return _items.TryGetValue(targetId, out var item) ? ItemField(item, fieldId).Origin : 0;
        }

        public int FieldReferenceDurationFrames(string fieldId, string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                var fields = Inputs(_contract);
                var definition = fields.FirstOrDefault((field) => Text(field["id"]) == fieldId);
                return definition is null ? 0 : ReferenceDuration(definition, _runtime, fields, Actions(_contract));
            }
            if (!_items.TryGetValue(targetId, out var item)) return 0;
            var itemFields = Fields(item.Collection, item.Item);
            var itemDefinition = itemFields.FirstOrDefault((field) => Text(field["id"]) == fieldId);
            return itemDefinition is null
                ? 0
                : ReferenceDuration(itemDefinition, item.Item, itemFields, ItemActions(item.Collection, item.Item));
        }

        public int ScreenFrame(string fieldId, string targetId, int localFrame)
        {
            var rootNaturalFrame = RootNaturalFrame(fieldId, targetId, Math.Max(0, localFrame));
            return Round(Scale(rootNaturalFrame, _naturalDuration, _effectiveDuration));
        }

        public double LocalFrame(string fieldId, string targetId, int screenFrame)
        {
            var rootNaturalFrame = Unscale(Math.Max(0, screenFrame), _naturalDuration, _effectiveDuration);
            if (string.IsNullOrWhiteSpace(targetId))
            {
                var origin = TopField(fieldId).Origin;
                return rootNaturalFrame - origin;
            }
            if (!_items.TryGetValue(targetId, out var item)) return 0;
            var ownerEffectiveFrame = rootNaturalFrame - item.RootStart;
            var ownerNaturalFrame = Unscale(ownerEffectiveFrame, item.NaturalSpan, item.EffectiveSpan);
            var field = ItemField(item, fieldId);
            return ownerNaturalFrame - field.Origin;
        }

        private double RootNaturalFrame(string fieldId, string targetId, int localFrame)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return TopField(fieldId).Origin + localFrame;
            if (!_items.TryGetValue(targetId, out var item)) return localFrame;
            var field = ItemField(item, fieldId);
            var ownerNaturalFrame = field.Origin + localFrame;
            return item.RootStart + Scale(ownerNaturalFrame, item.NaturalSpan, item.EffectiveSpan);
        }

        private FieldTiming TopField(string fieldId)
        {
            if (_topFields.TryGetValue(fieldId, out var timing)) return timing;
            var definition = Inputs(_contract).FirstOrDefault((field) => Text(field["id"]) == fieldId);
            if (definition is null) return new FieldTiming(0, 0, 0);
            timing = ResolveFieldTiming(definition, _runtime, "", Inputs(_contract), new HashSet<string>(StringComparer.Ordinal));
            _topFields[fieldId] = timing;
            return timing;
        }

        private FieldTiming ItemField(ItemTiming item, string fieldId)
        {
            if (item.Fields.TryGetValue(fieldId, out var timing)) return timing;
            var fields = Fields(item.Collection, item.Item);
            var definition = fields.FirstOrDefault((field) => Text(field["id"]) == fieldId);
            if (definition is null) return new FieldTiming(0, 0, 0);
            timing = ResolveFieldTiming(definition, item.Item, Text(item.Item["id"]), fields, new HashSet<string>(StringComparer.Ordinal));
            item.Fields[fieldId] = timing;
            return timing;
        }

        private ItemDurations CalculateItemDurations(JsonObject collection, JsonObject item, string targetId)
        {
            var fields = Fields(collection, item);
            var sequenceBodyEnd = 0d;
            var spanEnd = 0d;
            foreach (var definition in fields)
            {
                var end = ResolveFieldTiming(
                    definition,
                    item,
                    targetId,
                    fields,
                    new HashSet<string>(StringComparer.Ordinal)).EndExclusive;
                spanEnd = Math.Max(spanEnd, end);
                if (Timeline(definition)["extendsOwnerDuration"]?.GetValue<bool>() != false)
                    sequenceBodyEnd = Math.Max(sequenceBodyEnd, end);
            }
            var actionEnd = LastFiniteActionEnd(collection, item, targetId, fields);
            sequenceBodyEnd = Math.Max(sequenceBodyEnd, actionEnd);
            spanEnd = Math.Max(spanEnd, actionEnd);
            var post = StringArray(collection, "postDurationFieldIds")
                .Sum((fieldId) => FieldValue(item, fields, fieldId));
            var sequence = Math.Max(1, sequenceBodyEnd + post);
            return new ItemDurations(sequence, Math.Max(sequence, spanEnd));
        }

        private FieldTiming ResolveFieldTiming(
            JsonObject definition,
            JsonObject owner,
            string targetId,
            IReadOnlyList<JsonObject> ownerFields,
            HashSet<string> resolving)
        {
            var fieldId = Text(definition["id"]);
            if (!resolving.Add(fieldId))
                throw new InvalidOperationException($"Animation timeline dependency cycle at field '{fieldId}'.");
            var fieldTimeline = Timeline(definition);
            var originDefinition = fieldTimeline["origin"] as JsonObject;
            var origin = 0d;
            if (Text(originDefinition?["kind"]) == "fieldCompletion")
            {
                var sourceId = Text(originDefinition?["fieldId"]);
                var source = ownerFields.FirstOrDefault((field) => Text(field["id"]) == sourceId)
                    ?? throw new InvalidOperationException($"Animation field '{fieldId}' references missing field '{sourceId}'.");
                origin = ResolveFieldTiming(source, owner, targetId, ownerFields, resolving).Completion
                    + Number(originDefinition?["offsetFrames"]);
            }
            resolving.Remove(fieldId);

            var enabledKeyframes = EnabledKeyframes(Track(fieldId, targetId));
            var completionDefinition = fieldTimeline["completion"] as JsonObject;
            var baseDurationFieldId = Text(completionDefinition?["baseDurationFieldId"]);
            var minimumOverrideKeyframes = Math.Max(2, (int)Number(completionDefinition?["minimumEnabledKeyframes"], 2));
            if (!string.IsNullOrWhiteSpace(baseDurationFieldId)
                && enabledKeyframes.Count < minimumOverrideKeyframes)
            {
                var baseDurationDefinition = ownerFields.FirstOrDefault((field) => Text(field["id"]) == baseDurationFieldId);
                var baseDurationJsonKey = Text(baseDurationDefinition?["jsonKey"]);
                var completion = origin + (Text(baseDurationDefinition?["valueKind"]) == "BehaviorTiming"
                    ? BehaviorTimingResolver.ResolveFrames(owner, baseDurationDefinition!, ownerFields, _themeTokens)
                    : Number(owner[baseDurationJsonKey]));
                var end = Math.Max(completion, enabledKeyframes.Count > 0 ? origin + 1 : 0);
                return new FieldTiming(origin, completion, end);
            }
            if (enabledKeyframes.Count == 0) return new FieldTiming(origin, origin, 0);
            var last = Number(enabledKeyframes[^1]["frame"]);
            return new FieldTiming(origin, origin + last, origin + last + 1);
        }

        private int ReferenceDuration(
            JsonObject definition,
            JsonObject owner,
            IReadOnlyList<JsonObject> ownerFields,
            IReadOnlyList<JsonObject> actions)
        {
            var baseDurationFieldId = Text((Timeline(definition)["completion"] as JsonObject)?["baseDurationFieldId"]);
            if (!string.IsNullOrWhiteSpace(baseDurationFieldId))
            {
                var baseDefinition = ownerFields.FirstOrDefault((field) => Text(field["id"]) == baseDurationFieldId);
                if (baseDefinition is null) return 0;
                return Text(baseDefinition["valueKind"]) == "BehaviorTiming"
                    ? BehaviorTimingResolver.ResolveFrames(owner, baseDefinition, ownerFields, _themeTokens)
                    : Math.Max(0, Round(Number(owner[Text(baseDefinition["jsonKey"])])));
            }

            var fieldId = Text(definition["id"]);
            return actions
                .Where((action) => (Text(action["playFieldId"]) is { Length: > 0 } playFieldId
                    ? playFieldId
                    : Text(action["playInputId"])) == fieldId)
                .Select((action) =>
                {
                    var durationFieldId = Text(action["durationInputId"]);
                    var durationDefinition = ownerFields.FirstOrDefault((field) => Text(field["id"]) == durationFieldId);
                    var durationJsonKey = Text(durationDefinition?["jsonKey"]);
                    return Math.Max(0, Round(Number(owner[durationJsonKey])));
                })
                .DefaultIfEmpty(0)
                .Max();
        }

        private double LastFiniteActionEnd(
            JsonObject collection,
            JsonObject item,
            string targetId,
            IReadOnlyList<JsonObject> fields)
        {
            var lastEnd = 0d;
            foreach (var action in ItemActions(collection, item)
                .Where((candidate) => candidate["extendsModuleDuration"]?.GetValue<bool>() == true))
            {
                var playFieldId = Text(action["playInputId"]);
                var definition = fields.FirstOrDefault((field) => Text(field["id"]) == playFieldId);
                if (definition is null) continue;
                var fieldOrigin = ResolveFieldTiming(
                    definition,
                    item,
                    targetId,
                    fields,
                    new HashSet<string>(StringComparer.Ordinal)).Origin;
                var baseEnabled = item[Text(action["durationEnabledInputId"])] is JsonValue enabled
                    && enabled.TryGetValue<bool>(out var enabledValue)
                    && enabledValue;
                var keyframes = EnabledKeyframes(Track(playFieldId, targetId));
                var hasActiveKeyframe = keyframes.Any((keyframe) =>
                    keyframe["value"] is JsonValue value
                    && value.TryGetValue<bool>(out var playing)
                    && playing);
                if (!baseEnabled && !hasActiveKeyframe)
                {
                    continue;
                }

                var durationInputId = Text(action["durationInputId"]);
                if (string.IsNullOrWhiteSpace(durationInputId))
                {
                    throw new InvalidOperationException(
                        "A finite runtime action that extends Module duration requires durationInputId.");
                }
                var duration = JsonPath.RequiredPositiveNumber(
                    item[durationInputId],
                    $"Runtime action duration input '{durationInputId}'");
                if (baseEnabled)
                {
                    lastEnd = Math.Max(lastEnd, fieldOrigin + duration);
                }
                for (var index = 0; index < keyframes.Count; index++)
                {
                    if (keyframes[index]["value"] is not JsonValue value
                        || !value.TryGetValue<bool>(out var playing)
                        || !playing) continue;
                    var start = fieldOrigin + Number(keyframes[index]["frame"]);
                    var replacement = index + 1 < keyframes.Count
                        ? fieldOrigin + Number(keyframes[index + 1]["frame"])
                        : double.MaxValue;
                    lastEnd = Math.Max(lastEnd, Math.Min(start + duration, replacement));
                }
            }
            return lastEnd;
        }

        private JsonObject? Track(string fieldId, string targetId) =>
            JsonPath.OptionalObjectArray(_animation, "tracks", "Runtime owner animation").FirstOrDefault((track) =>
                Text(track["fieldId"]) == fieldId
                && Text(track["targetId"]) == targetId);

        private double ItemOwnerOrigin(JsonObject collection, JsonObject item)
        {
            var origin = Timeline(collection)["ownerOrigin"] as JsonObject;
            if (Text(origin?["kind"]) != "firstMatchingValue") return 0;

            var sourceCollectionKey = Text(origin?["sourceCollectionJsonKey"]);
            var sourceTargetIdJsonKey = Text(origin?["sourceTargetIdJsonKey"]);
            var sourceFieldId = Text(origin?["sourceFieldId"]);
            var sourceValueJsonKey = Text(origin?["sourceValueJsonKey"]);
            var matchValueJsonKey = Text(origin?["matchValueJsonKey"]);
            var sourceTargetId = Text(item[sourceTargetIdJsonKey]);
            var matchValue = Text(item[matchValueJsonKey]);
            if (string.IsNullOrWhiteSpace(sourceCollectionKey)
                || string.IsNullOrWhiteSpace(sourceTargetId)
                || string.IsNullOrWhiteSpace(sourceFieldId)
                || string.IsNullOrWhiteSpace(sourceValueJsonKey)
                || string.IsNullOrWhiteSpace(matchValue))
            {
                throw new InvalidOperationException("Incomplete firstMatchingValue owner-origin contract.");
            }

            var sourceItems = _runtime[sourceCollectionKey] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Owner-origin source collection '{sourceCollectionKey}' must be a JSON array.");
            var sourceItem = JsonPath.ObjectItems(
                    sourceItems,
                    $"Owner-origin source collection '{sourceCollectionKey}'")
                .FirstOrDefault((candidate) => Text(candidate["id"]) == sourceTargetId)
                ?? throw new InvalidOperationException(
                    $"Owner-origin source item '{sourceTargetId}' is missing from '{sourceCollectionKey}'.");
            if (Text(sourceItem[sourceValueJsonKey]) == matchValue) return 0;

            var firstMatch = EnabledKeyframes(Track(sourceFieldId, sourceTargetId))
                .Where((keyframe) => keyframe["enabled"]?.GetValue<bool>() != false)
                .Where((keyframe) => Text(keyframe["value"]) == matchValue)
                .Select((keyframe) => Number(keyframe["frame"]))
                .DefaultIfEmpty(0)
                .Min();
            return firstMatch;
        }

        private static IReadOnlyList<JsonObject> EnabledKeyframes(JsonObject? track)
        {
            if (track is null) return [];
            return JsonPath.OptionalObjectArray(track, "keyframes", "Runtime animation track")
                .Where((keyframe) => keyframe["enabled"]?.GetValue<bool>() != false)
                .OrderBy((keyframe) => Number(keyframe["frame"]))
                .ToList();
        }

        private double TargetDuration(string targetId, double natural) =>
            PositiveDuration((((_animation["retime"] as JsonObject)?["targets"] as JsonObject)?[targetId] as JsonObject)?["targetDurationFrames"])
            ?? natural;

        private double RootTargetDuration(double natural) =>
            PositiveDuration((_animation["retime"] as JsonObject)?["targetDurationFrames"])
            ?? natural;

        private static double? PositiveDuration(JsonNode? node)
        {
            var value = Number(node);
            return value > 0 ? value : null;
        }

        private sealed record FieldTiming(double Origin, double Completion, double EndExclusive);

        private sealed record ItemDurations(double Sequence, double Span);

        private sealed record ItemTiming(
            JsonObject Collection,
            JsonObject Item,
            double RootStart,
            double NaturalSpan,
            double EffectiveSpan,
            double NaturalSequence,
            double EffectiveSequence)
        {
            public Dictionary<string, FieldTiming> Fields { get; } = new(StringComparer.Ordinal);
        }
    }

    private static void ValidateAnimationEnvelope(JsonObject animation)
    {
        var tracks = JsonPath.OptionalObjectArray(animation, "tracks", "Runtime owner animation");
        foreach (var track in tracks)
        {
            _ = JsonPath.RequiredString(track, "fieldId", "Runtime animation track");
            if (track.TryGetPropertyValue("targetId", out _))
            {
                _ = JsonPath.RequiredString(track, "targetId", "Runtime animation track");
            }
            foreach (var keyframe in JsonPath.OptionalObjectArray(
                track,
                "keyframes",
                "Runtime animation track"))
            {
                var frame = JsonPath.RequiredInteger(keyframe, "frame", "Runtime animation keyframe");
                if (frame < 0)
                {
                    throw new InvalidOperationException("Runtime animation keyframe frame must not be negative.");
                }
                if (keyframe.TryGetPropertyValue("enabled", out _))
                {
                    _ = JsonPath.RequiredBoolean(keyframe, "enabled", "Runtime animation keyframe");
                }
            }
        }

        var retime = JsonPath.OptionalObject(animation, "retime", "Runtime owner animation");
        if (retime is null) return;
        ValidateOptionalPositiveFrameCount(retime, "targetDurationFrames", "Runtime animation retime");
        var targets = JsonPath.OptionalObject(retime, "targets", "Runtime animation retime");
        if (targets is null) return;
        foreach (var (targetId, targetNode) in targets)
        {
            if (string.IsNullOrWhiteSpace(targetId) || targetNode is not JsonObject target)
            {
                throw new InvalidOperationException("Runtime animation retime target must be a named JSON object.");
            }
            ValidateOptionalPositiveFrameCount(
                target,
                "targetDurationFrames",
                $"Runtime animation retime target '{targetId}'");
        }
    }

    private static void ValidateOptionalPositiveFrameCount(JsonObject owner, string key, string context)
    {
        if (!owner.TryGetPropertyValue(key, out _)) return;
        var value = JsonPath.RequiredInteger(owner, key, context);
        if (value <= 0)
        {
            throw new InvalidOperationException($"{context} '{key}' must be positive.");
        }
    }

    private static IReadOnlyList<JsonObject> Collections(JsonObject contract) =>
        JsonPath.OptionalObjectArray(contract, "collections", "Runtime owner contract");

    private static IReadOnlyList<JsonObject> Inputs(JsonObject contract) =>
        JsonPath.OptionalObjectArray(contract, "inputs", "Runtime owner contract");

    private static IReadOnlyList<JsonObject> Actions(JsonObject contract) =>
        JsonPath.OptionalObjectArray(contract, "actions", "Runtime owner contract");

    private static IReadOnlyList<JsonObject> Fields(JsonObject collection, JsonObject item)
    {
        var fields = JsonPath.OptionalObjectArray(collection, "fields", "Runtime owner collection").ToList();
        var runtimeContractKey = Text(collection["itemRuntimeContractJsonKey"]);
        if (runtimeContractKey.Length > 0)
        {
            var targetId = JsonPath.RequiredString(item, "id", "Projected Runtime collection item");
            var runtimeContract = JsonPath.RequiredObject(
                item,
                runtimeContractKey,
                $"Projected Runtime collection item '{targetId}'");
            fields.AddRange(JsonPath.OptionalObjectArray(
                runtimeContract,
                "inputs",
                $"Projected Runtime contract '{targetId}'"));
        }
        return fields;
    }

    private static IReadOnlyList<JsonObject> ItemActions(JsonObject collection, JsonObject item)
    {
        var actions = JsonPath.OptionalObjectArray(
            collection,
            "itemActions",
            "Runtime owner collection").ToList();
        var runtimeContractKey = Text(collection["itemRuntimeContractJsonKey"]);
        if (runtimeContractKey.Length > 0)
        {
            var targetId = JsonPath.RequiredString(item, "id", "Projected Runtime collection item");
            var runtimeContract = JsonPath.RequiredObject(
                item,
                runtimeContractKey,
                $"Projected Runtime collection item '{targetId}'");
            actions.AddRange(JsonPath.OptionalObjectArray(
                runtimeContract,
                "actions",
                $"Projected Runtime contract '{targetId}'"));
        }
        return actions;
    }

    private static JsonObject Timeline(JsonObject owner) =>
        JsonPath.OptionalObject(owner, "animationTimeline", "Runtime animation owner") ?? new JsonObject();

    private static string CollectionKey(JsonObject collection) =>
        Text(collection["storageCollectionJsonKey"]) is { Length: > 0 } storage
            ? storage
            : Text(collection["sourceCollectionJsonKey"]) is { Length: > 0 } source
                ? source
                : Text(collection["jsonKey"]);

    private static IReadOnlyList<string> StringArray(JsonObject collection, string key) =>
        JsonPath.OptionalStringArray(Timeline(collection), key, "Runtime collection animation timeline");

    private static int DeclaredBaseDuration(JsonObject contract) =>
        Actions(contract)
            .Where((action) => action["definesModuleDuration"]?.GetValue<bool>() == true)
            .Select((action) => (int)JsonPath.RequiredNonNegativeNumber(
                action["durationBaseFrames"],
                $"Runtime action '{Text(action["id"])}' durationBaseFrames"))
            .DefaultIfEmpty(0)
            .Max();

    private static double Scale(double value, double natural, double effective) =>
        natural <= 0 ? value : value * effective / natural;

    private static double Unscale(double value, double natural, double effective) =>
        effective <= 0 ? value : value * natural / effective;

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static double Number(JsonNode? node, double fallback = 0)
    {
        if (node is JsonValue integer && integer.TryGetValue<int>(out var intValue)) return Math.Max(0, intValue);
        if (node is JsonValue number && number.TryGetValue<double>(out var doubleValue)) return Math.Max(0, doubleValue);
        if (node is JsonValue decimalNode && decimalNode.TryGetValue<decimal>(out var decimalValue)) return Math.Max(0, (double)decimalValue);
        return fallback;
    }

    private static double FieldValue(JsonObject owner, IReadOnlyList<JsonObject> fields, string fieldId)
    {
        var definition = fields.FirstOrDefault((field) => Text(field["id"]) == fieldId);
        return Number(owner[Text(definition?["jsonKey"])]);
    }
}
