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
        string targetId) =>
        Model(contract, runtime, animation).ScreenFrame(fieldId, targetId, 0);

    public static int ScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        int localFrame) =>
        Model(contract, runtime, animation).ScreenFrame(fieldId, targetId, localFrame);

    public static double LocalFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        int screenFrame) =>
        Model(contract, runtime, animation).LocalFrame(fieldId, targetId, screenFrame);

    public static int DurationFrames(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        int storedFallback) =>
        Model(contract, runtime, animation, storedFallback).DurationFrames;

    public static int OwnerNaturalDuration(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId) =>
        Math.Max(1, Round(Model(contract, runtime, animation).OwnerNaturalDuration(targetId)));

    public static double OwnerLocalFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        int screenFrame) =>
        Model(contract, runtime, animation).OwnerLocalFrame(targetId, screenFrame);

    public static int ScreenFrameForOwnerFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        double ownerFrame) =>
        Model(contract, runtime, animation).ScreenFrameForOwnerFrame(targetId, ownerFrame);

    public static double FieldOwnerFrameOrigin(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId) =>
        Model(contract, runtime, animation).FieldOwnerFrameOrigin(fieldId, targetId);

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
        int storedFallback = 0) => new(contract, runtime, animation, storedFallback);

    private sealed class TimelineModel
    {
        private readonly JsonObject _contract;
        private readonly JsonObject _runtime;
        private readonly JsonObject _animation;
        private readonly Dictionary<string, ItemTiming> _items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FieldTiming> _topFields = new(StringComparer.Ordinal);
        private readonly double _naturalDuration;
        private readonly double _effectiveDuration;

        public TimelineModel(JsonObject contract, JsonObject runtime, JsonObject animation, int storedFallback)
        {
            _contract = contract;
            _runtime = runtime;
            _animation = animation;
            var naturalEnd = (double)Math.Max(1, DeclaredBaseDuration(contract));
            foreach (var collection in Collections(contract))
            {
                var key = CollectionKey(collection);
                if (runtime[key] is not JsonArray values) continue;
                var cursor = 0d;
                foreach (var item in values.OfType<JsonObject>())
                {
                    var targetId = item["id"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrWhiteSpace(targetId)) continue;
                    var fields = Fields(collection);
                    var pre = StringArray(Timeline(collection)["preDurationFieldIds"])
                        .Sum((fieldId) => FieldValue(item, fields, fieldId));
                    var start = cursor + pre;
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
                    cursor = start + effectiveSequence;
                    naturalEnd = Math.Max(naturalEnd, start + effectiveSpan);
                }
                naturalEnd = Math.Max(naturalEnd, cursor);
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
            var fields = Fields(item.Collection);
            var definition = fields.FirstOrDefault((field) => Text(field["id"]) == fieldId);
            if (definition is null) return new FieldTiming(0, 0, 0);
            timing = ResolveFieldTiming(definition, item.Item, Text(item.Item["id"]), fields, new HashSet<string>(StringComparer.Ordinal));
            item.Fields[fieldId] = timing;
            return timing;
        }

        private ItemDurations CalculateItemDurations(JsonObject collection, JsonObject item, string targetId)
        {
            var fields = Fields(collection);
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
            var post = StringArray(Timeline(collection)["postDurationFieldIds"])
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

            var enabledKeyframes = Track(fieldId, targetId)?["keyframes"] is JsonArray keyframes
                ? keyframes.OfType<JsonObject>()
                    .Where((keyframe) => keyframe["enabled"]?.GetValue<bool>() != false)
                    .OrderBy((keyframe) => Number(keyframe["frame"]))
                    .ToList()
                : [];
            var completionDefinition = fieldTimeline["completion"] as JsonObject;
            var baseDurationFieldId = Text(completionDefinition?["baseDurationFieldId"]);
            var minimumOverrideKeyframes = Math.Max(2, (int)Number(completionDefinition?["minimumEnabledKeyframes"], 2));
            if (!string.IsNullOrWhiteSpace(baseDurationFieldId)
                && enabledKeyframes.Count < minimumOverrideKeyframes)
            {
                var baseDurationDefinition = ownerFields.FirstOrDefault((field) => Text(field["id"]) == baseDurationFieldId);
                var baseDurationJsonKey = Text(baseDurationDefinition?["jsonKey"]);
                var completion = origin + Number(owner[baseDurationJsonKey]);
                var end = Math.Max(completion, enabledKeyframes.Count > 0 ? origin + 1 : 0);
                return new FieldTiming(origin, completion, end);
            }
            if (enabledKeyframes.Count == 0) return new FieldTiming(origin, origin, 0);
            var last = Number(enabledKeyframes[^1]["frame"]);
            return new FieldTiming(origin, origin + last, origin + last + 1);
        }

        private double LastFiniteActionEnd(
            JsonObject collection,
            JsonObject item,
            string targetId,
            IReadOnlyList<JsonObject> fields)
        {
            var lastEnd = 0d;
            foreach (var action in (collection["itemActions"] as JsonArray)?.OfType<JsonObject>()
                .Where((candidate) => candidate["extendsModuleDuration"]?.GetValue<bool>() == true) ?? [])
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
                var duration = Math.Max(1, Number(item[Text(action["durationInputId"])]));
                if (item[Text(action["durationEnabledInputId"])] is JsonValue enabled
                    && enabled.TryGetValue<bool>(out var baseEnabled)
                    && baseEnabled)
                {
                    lastEnd = Math.Max(lastEnd, fieldOrigin + duration);
                }
                var keyframes = (Track(playFieldId, targetId)?["keyframes"] as JsonArray)?.OfType<JsonObject>()
                    .Where((keyframe) => keyframe["enabled"]?.GetValue<bool>() != false)
                    .OrderBy((keyframe) => Number(keyframe["frame"]))
                    .ToList() ?? [];
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
            (_animation["tracks"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault((track) =>
                Text(track["fieldId"]) == fieldId
                && Text(track["targetId"]) == targetId);

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

    private static IReadOnlyList<JsonObject> Collections(JsonObject contract) =>
        (contract["collections"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    private static IReadOnlyList<JsonObject> Inputs(JsonObject contract) =>
        (contract["inputs"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    private static IReadOnlyList<JsonObject> Fields(JsonObject collection) =>
        (collection["fields"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    private static JsonObject Timeline(JsonObject owner) => owner["animationTimeline"] as JsonObject ?? new JsonObject();

    private static string CollectionKey(JsonObject collection) =>
        Text(collection["sourceCollectionJsonKey"]) is { Length: > 0 } source ? source : Text(collection["jsonKey"]);

    private static string[] StringArray(JsonNode? value) =>
        (value as JsonArray)?.OfType<JsonValue>().Select((item) => item.GetValue<string>()).ToArray() ?? [];

    private static int DeclaredBaseDuration(JsonObject contract) =>
        (contract["actions"] as JsonArray)?.OfType<JsonObject>()
            .Where((action) => action["definesModuleDuration"]?.GetValue<bool>() == true)
            .Select((action) => (int)Number(action["durationBaseFrames"]))
            .DefaultIfEmpty(0)
            .Max() ?? 0;

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
