using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Common;

public static class RuntimeAnimationFrameOrigin
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
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate).ScreenFrame(fieldId, targetId, 0);

    public static int ScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        int localFrame,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate).ScreenFrame(fieldId, targetId, localFrame);

    public static double LocalFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        int screenFrame,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate).LocalFrame(fieldId, targetId, screenFrame);

    public static int DurationFrames(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        int storedFallback,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, storedFallback, themeTokens, frameRate).DurationFrames;

    public static int OwnerNaturalDuration(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        JsonObject? themeTokens = null) =>
        Math.Max(1, Round(Model(contract, runtime, animation, themeTokens: themeTokens).OwnerNaturalDuration(targetId)));

    public static int OwnerNaturalSequenceDuration(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        JsonObject? themeTokens = null) =>
        Math.Max(1, Round(Model(contract, runtime, animation, themeTokens: themeTokens).OwnerNaturalSequenceDuration(targetId)));

    public static int OwnerSequenceEndScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate).OwnerSequenceEndScreenFrame(targetId);

    public static int OwnerPresenceEndScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        int automaticEndFrame,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate)
            .OwnerPresenceEndScreenFrame(targetId, automaticEndFrame);

    public static bool OwnerHasExplicitPresenceEnd(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens)
            .OwnerHasExplicitPresenceEnd(targetId);

    public static double OwnerLocalFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        int screenFrame,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate).OwnerLocalFrame(targetId, screenFrame);

    public static int ScreenFrameForOwnerFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        double ownerFrame,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate).ScreenFrameForOwnerFrame(targetId, ownerFrame);

    public static int OwnerAppearanceScreenFrame(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string targetId,
        JsonObject? themeTokens = null) =>
        Model(contract, runtime, animation, themeTokens: themeTokens).OwnerAppearanceScreenFrame(targetId);

    public static double FieldOwnerFrameOrigin(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(contract, runtime, animation, themeTokens: themeTokens, frameRate: frameRate).FieldOwnerFrameOrigin(fieldId, targetId);

    public static int FieldReferenceDurationFrames(
        JsonObject contract,
        JsonObject runtime,
        JsonObject animation,
        string fieldId,
        string targetId,
        JsonObject? themeTokens = null,
        int frameRate = 0) =>
        Model(
            contract,
            runtime,
            animation,
            themeTokens: themeTokens,
            frameRate: frameRate).FieldReferenceDurationFrames(fieldId, targetId);

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
        JsonObject? themeTokens = null,
        int frameRate = 0) => new(contract, runtime, animation, storedFallback, themeTokens ?? new JsonObject(), frameRate);

    private sealed class TimelineModel
    {
        private readonly JsonObject _contract;
        private readonly JsonObject _runtime;
        private readonly JsonObject _animation;
        private readonly JsonObject _themeTokens;
        private readonly int _frameRate;
        private readonly Dictionary<string, ItemTiming> _items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FieldTiming> _topFields = new(StringComparer.Ordinal);
        private readonly double _naturalDuration;
        private readonly double _effectiveDuration;

        public TimelineModel(JsonObject contract, JsonObject runtime, JsonObject animation, int storedFallback, JsonObject themeTokens, int frameRate)
        {
            ValidateAnimationEnvelope(animation);
            _contract = contract;
            _runtime = runtime;
            _animation = animation;
            _themeTokens = themeTokens;
            _frameRate = frameRate;
            ValidateOwnerPhase(Timeline(contract), "Runtime owner animation timeline");
            var naturalEnd = (double)Math.Max(1, DeclaredBaseDuration(contract));
            var collectionKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var collection in Collections(contract))
            {
                var key = CollectionKey(collection);
                if (!collectionKeys.Add(key))
                {
                    throw new InvalidOperationException(
                        $"Runtime owner contract contains duplicate collection key '{key}'.");
                }
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
                    var phase = OwnerPhaseFrames(Timeline(collection), item);
                    var pre = StringArray(collection, "preDurationFieldIds")
                        .Sum((fieldId) => SignedFieldValue(item, fields, fieldId));
                    var appearance = sequenceItems
                        ? cursor
                        : ItemOwnerOrigin(collection, item);
                    var start = appearance + pre;
                    var durations = CalculateItemDurations(collection, item, targetId, phase);
                    var effectiveSpan = TargetDuration(targetId, durations.Span);
                    var effectiveSequence = Scale(durations.Sequence, durations.Span, effectiveSpan);
                    if (!_items.TryAdd(targetId, new ItemTiming(
                        collection,
                        item,
                        appearance,
                        start,
                        durations.Span,
                        effectiveSpan,
                        durations.Sequence,
                        effectiveSequence)))
                    {
                        throw new InvalidOperationException(
                            $"Runtime owner collections contain duplicate target id '{targetId}'.");
                    }
                    if (sequenceItems) cursor = start + effectiveSequence;
                    naturalEnd = Math.Max(naturalEnd, start + effectiveSpan);
                }
                if (sequenceItems) naturalEnd = Math.Max(naturalEnd, cursor);
            }

            var inputs = Inputs(contract);
            var topPhase = OwnerPhaseFrames(Timeline(contract), runtime);
            foreach (var definition in inputs)
            {
                var fieldId = JsonPath.RequiredString(definition, "id", "Runtime owner input");
                var timing = ResolveFieldTiming(definition, runtime, "", inputs, new HashSet<string>(StringComparer.Ordinal), topPhase);
                if (!_topFields.TryAdd(fieldId, timing))
                {
                    throw new InvalidOperationException(
                        $"Runtime owner contract contains duplicate input id '{fieldId}'.");
                }
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

        public double OwnerNaturalSequenceDuration(string targetId) =>
            string.IsNullOrWhiteSpace(targetId)
                ? _naturalDuration
                : _items.TryGetValue(targetId, out var item) ? item.NaturalSequence : 1;

        public int OwnerSequenceEndScreenFrame(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return DurationFrames;
            return _items.TryGetValue(targetId, out var item)
                ? Round(Scale(
                    item.RootStart + item.EffectiveSequence,
                    _naturalDuration,
                    _effectiveDuration))
                : 0;
        }

        public int OwnerPresenceEndScreenFrame(string targetId, int automaticEndFrame)
        {
            if (!_items.TryGetValue(targetId, out var item)) return 0;
            var duration = PresenceDuration(item);
            var start = ScreenFrameForOwnerFrame(targetId, 0);
            if (duration is null) return OwnerSequenceEndScreenFrame(targetId);
            return duration.Value > 0
                ? start + Round(duration.Value)
                : Math.Max(start + 1, automaticEndFrame);
        }

        public bool OwnerHasExplicitPresenceEnd(string targetId) =>
            _items.TryGetValue(targetId, out var item) && PresenceDuration(item) is > 0;

        private static double? PresenceDuration(ItemTiming item) =>
            PresenceDuration(item.Collection, item.Item);

        private static double? PresenceDuration(JsonObject collection, JsonObject item)
        {
            var durationFieldId = Text(Timeline(collection)["presenceDurationFieldId"]);
            if (string.IsNullOrWhiteSpace(durationFieldId)) return null;
            return FieldValue(item, Fields(collection, item), durationFieldId);
        }

        public double OwnerLocalFrame(string targetId, int screenFrame)
        {
            var rootNatural = Unscale(
                string.IsNullOrWhiteSpace(targetId) ? Math.Max(0, screenFrame) : screenFrame,
                _naturalDuration,
                _effectiveDuration);
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

        public int OwnerAppearanceScreenFrame(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return 0;
            return _items.TryGetValue(targetId, out var item)
                ? Round(Scale(item.RootAppearance, _naturalDuration, _effectiveDuration))
                : 0;
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
            var rootNaturalFrame = Unscale(
                string.IsNullOrWhiteSpace(targetId) ? Math.Max(0, screenFrame) : screenFrame,
                _naturalDuration,
                _effectiveDuration);
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
            timing = ResolveFieldTiming(
                definition,
                _runtime,
                "",
                Inputs(_contract),
                new HashSet<string>(StringComparer.Ordinal),
                OwnerPhaseFrames(Timeline(_contract), _runtime));
            _topFields[fieldId] = timing;
            return timing;
        }

        private FieldTiming ItemField(ItemTiming item, string fieldId)
        {
            if (item.Fields.TryGetValue(fieldId, out var timing)) return timing;
            var fields = Fields(item.Collection, item.Item);
            var definition = fields.FirstOrDefault((field) => Text(field["id"]) == fieldId);
            if (definition is null) return new FieldTiming(0, 0, 0);
            timing = ResolveFieldTiming(
                definition,
                item.Item,
                Text(item.Item["id"]),
                fields,
                new HashSet<string>(StringComparer.Ordinal),
                OwnerPhaseFrames(Timeline(item.Collection), item.Item));
            item.Fields[fieldId] = timing;
            return timing;
        }

        private ItemDurations CalculateItemDurations(JsonObject collection, JsonObject item, string targetId, int phase)
        {
            var fields = Fields(collection, item);
            var collectionTimeline = Timeline(collection);
            HashSet<string>? sequenceCompletionFieldIds = null;
            if (collectionTimeline.TryGetPropertyValue("sequenceCompletionFieldIds", out _))
            {
                sequenceCompletionFieldIds = new HashSet<string>(
                    JsonPath.OptionalStringArray(
                        collectionTimeline,
                        "sequenceCompletionFieldIds",
                        "Runtime collection animation timeline"),
                    StringComparer.Ordinal);
            }
            var sequenceBodyEnd = 0d;
            var spanEnd = 0d;
            foreach (var definition in fields)
            {
                var end = ResolveFieldTiming(
                    definition,
                    item,
                    targetId,
                    fields,
                    new HashSet<string>(StringComparer.Ordinal),
                    phase).EndExclusive;
                spanEnd = Math.Max(spanEnd, end);
                var fieldId = JsonPath.RequiredString(
                    definition,
                    "id",
                    "Runtime owner item field");
                if (sequenceCompletionFieldIds is not null
                        ? sequenceCompletionFieldIds.Contains(fieldId)
                        : FieldTimeline(definition)["extendsOwnerDuration"]?.GetValue<bool>() != false)
                    sequenceBodyEnd = Math.Max(sequenceBodyEnd, end);
            }
            var actionEnd = LastFiniteActionEnd(collection, item, targetId, fields, phase);
            if (sequenceCompletionFieldIds is null)
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
            HashSet<string> resolving,
            int phase = 0)
        {
            var fieldId = Text(definition["id"]);
            if (!resolving.Add(fieldId))
                throw new InvalidOperationException($"Animation timeline dependency cycle at field '{fieldId}'.");
            var fieldTimeline = FieldTimeline(definition);
            var originDefinition = JsonPath.OptionalObject(
                fieldTimeline,
                "origin",
                $"Runtime animation field '{fieldId}' timeline");
            var origin = (double)phase;
            if (Text(originDefinition?["kind"]) == "fieldCompletion")
            {
                var sourceId = Text(originDefinition?["fieldId"]);
                var source = ownerFields.FirstOrDefault((field) => Text(field["id"]) == sourceId)
                    ?? throw new InvalidOperationException($"Animation field '{fieldId}' references missing field '{sourceId}'.");
                origin = ResolveFieldTiming(source, owner, targetId, ownerFields, resolving, phase).Completion
                    + JsonPath.RequiredInteger(
                        originDefinition!,
                        "offsetFrames",
                        $"Runtime animation field '{fieldId}' origin");
            }
            resolving.Remove(fieldId);

            var enabledKeyframes = EnabledKeyframes(Track(fieldId, targetId));
            var completionDefinition = JsonPath.OptionalObject(
                fieldTimeline,
                "completion",
                $"Runtime animation field '{fieldId}' timeline");
            var baseDurationFieldId = Text(completionDefinition?["baseDurationFieldId"]);
            var minimumOverrideKeyframes = completionDefinition is not null
                && completionDefinition.TryGetPropertyValue("minimumEnabledKeyframes", out _)
                    ? JsonPath.RequiredInteger(
                        completionDefinition,
                        "minimumEnabledKeyframes",
                        $"Runtime animation field '{fieldId}' completion")
                    : 2;
            if (!string.IsNullOrWhiteSpace(baseDurationFieldId)
                && enabledKeyframes.Count < minimumOverrideKeyframes)
            {
                var baseDurationDefinition = ownerFields.FirstOrDefault((field) => Text(field["id"]) == baseDurationFieldId)
                    ?? throw new InvalidOperationException(
                        $"Animation field '{fieldId}' references missing duration field '{baseDurationFieldId}'.");
                var baseDurationJsonKey = JsonPath.RequiredString(
                    baseDurationDefinition,
                    "jsonKey",
                    $"Runtime duration field '{baseDurationFieldId}'");
                var completion = origin + (Text(baseDurationDefinition["valueKind"]) == "BehaviorTiming"
                    ? BehaviorTimingResolver.ResolveFrames(owner, baseDurationDefinition, ownerFields, _themeTokens)
                    : JsonPath.RequiredNonNegativeNumber(
                        owner[baseDurationJsonKey],
                        $"Runtime duration field '{baseDurationFieldId}' value"));
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
            var completion = JsonPath.OptionalObject(
                FieldTimeline(definition),
                "completion",
                $"Runtime animation field '{Text(definition["id"])}' timeline");
            var baseDurationFieldId = Text(completion?["baseDurationFieldId"]);
            if (!string.IsNullOrWhiteSpace(baseDurationFieldId))
            {
                var baseDefinition = ownerFields.FirstOrDefault((field) => Text(field["id"]) == baseDurationFieldId)
                    ?? throw new InvalidOperationException(
                        $"Animation field '{Text(definition["id"])}' references missing duration field '{baseDurationFieldId}'.");
                return Text(baseDefinition["valueKind"]) == "BehaviorTiming"
                    ? BehaviorTimingResolver.ResolveFrames(owner, baseDefinition, ownerFields, _themeTokens)
                    : Math.Max(0, Round(JsonPath.RequiredNonNegativeNumber(
                        owner[JsonPath.RequiredString(
                            baseDefinition,
                            "jsonKey",
                            $"Runtime duration field '{baseDurationFieldId}'")],
                        $"Runtime duration field '{baseDurationFieldId}' value")));
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
            IReadOnlyList<JsonObject> fields,
            int phase)
        {
            var lastEnd = 0d;
            foreach (var action in ItemActions(collection, item))
            {
                if (!action.ContainsKey("extendsModuleDuration")) continue;
                if (!JsonPath.RequiredBoolean(
                        action,
                        "extendsModuleDuration",
                        "Finite runtime action"))
                {
                    continue;
                }
                var actionId = JsonPath.RequiredString(action, "id", "Finite runtime action");
                var durationInputId = JsonPath.RequiredString(
                    action,
                    "durationInputId",
                    $"Finite runtime action '{actionId}'");
                if (!fields.Any((field) => Text(field["id"]) == durationInputId))
                {
                    throw new InvalidOperationException(
                        $"Finite runtime action '{actionId}' references missing duration field '{durationInputId}'.");
                }
                var playFieldId = action.ContainsKey("playFieldId")
                    ? JsonPath.RequiredString(action, "playFieldId", $"Finite runtime action '{actionId}'")
                    : JsonPath.RequiredString(action, "playInputId", $"Finite runtime action '{actionId}'");
                var definition = fields.FirstOrDefault((field) => Text(field["id"]) == playFieldId);
                if (definition is null)
                {
                    throw new InvalidOperationException(
                        $"Finite runtime action '{actionId}' references missing play field '{playFieldId}'.");
                }
                var fieldOrigin = ResolveFieldTiming(
                    definition,
                    item,
                    targetId,
                    fields,
                    new HashSet<string>(StringComparer.Ordinal),
                    phase).Origin;
                var enabledJsonKey = JsonPath.RequiredString(
                    action,
                    "durationEnabledInputId",
                    $"Finite runtime action '{actionId}'");
                var baseEnabled = JsonPath.RequiredBoolean(
                    item,
                    enabledJsonKey,
                    $"Finite runtime action '{actionId}' owner");
                var keyframes = EnabledKeyframes(Track(playFieldId, targetId));
                var hasActiveKeyframe = keyframes.Any((keyframe) =>
                    JsonPath.RequiredBoolean(
                        keyframe,
                        "value",
                        $"Finite runtime action '{actionId}' play keyframe"));
                if (!baseEnabled && !hasActiveKeyframe)
                {
                    continue;
                }

                var duration = FieldValue(item, fields, durationInputId);
                if (duration <= 0)
                {
                    throw new InvalidOperationException(
                        $"Finite runtime action '{actionId}' duration input '{durationInputId}' must be positive.");
                }
                if (baseEnabled)
                {
                    lastEnd = Math.Max(lastEnd, fieldOrigin + duration);
                }
                for (var index = 0; index < keyframes.Count; index++)
                {
                    if (!JsonPath.RequiredBoolean(
                            keyframes[index],
                            "value",
                            $"Finite runtime action '{actionId}' play keyframe")) continue;
                    var start = fieldOrigin + Number(keyframes[index]["frame"]);
                    var replacement = index + 1 < keyframes.Count
                        ? fieldOrigin + Number(keyframes[index + 1]["frame"])
                        : double.MaxValue;
                    lastEnd = Math.Max(lastEnd, Math.Min(start + duration, replacement));
                }
            }
            return lastEnd;
        }

        private int OwnerPhaseFrames(JsonObject timeline, JsonObject owner)
        {
            if (!timeline.TryGetPropertyValue("ownerPhase", out var phaseNode)) return 0;
            var phase = phaseNode as JsonObject
                ?? throw new InvalidOperationException("Runtime owner phase must be an object.");
            var kind = JsonPath.RequiredString(phase, "kind", "Runtime owner phase");
            JsonObject motionOwner;
            if (kind.Equals("resolvedMotion", StringComparison.Ordinal))
            {
                motionOwner = phase;
            }
            else if (kind.Equals("itemMotion", StringComparison.Ordinal))
            {
                var jsonKey = JsonPath.RequiredString(phase, "jsonKey", "Runtime item owner phase");
                motionOwner = JsonPath.RequiredObject(owner, jsonKey, "Runtime item owner phase");
            }
            else
            {
                throw new InvalidOperationException($"Runtime owner phase has unknown kind '{kind}'.");
            }
            if (_frameRate <= 0)
            {
                throw new InvalidOperationException("Runtime owner phase requires a positive project frame rate.");
            }
            var motion = JsonPath.RequiredObject(motionOwner, "motion", "Runtime owner phase motion");
            return Math.Max(0, (int)Math.Ceiling(
                MotionTimingDuration.ResolveMilliseconds(_themeTokens, motion, "Runtime owner phase motion")
                / 1000.0
                * _frameRate));
        }

        private JsonObject? Track(string fieldId, string targetId) =>
            JsonPath.OptionalObjectArray(_animation, "tracks", "Runtime owner animation").FirstOrDefault((track) =>
                Text(track["fieldId"]) == fieldId
                && Text(track["targetId"]) == targetId);

        private double ItemOwnerOrigin(JsonObject collection, JsonObject item)
        {
            var origin = JsonPath.OptionalObject(
                Timeline(collection),
                "ownerOrigin",
                "Runtime collection animation timeline");
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
            double RootAppearance,
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
        var trackTargets = new HashSet<(string FieldId, string TargetId)>();
        foreach (var track in tracks)
        {
            var fieldId = JsonPath.RequiredString(track, "fieldId", "Runtime animation track");
            var targetId = "";
            if (track.TryGetPropertyValue("targetId", out _))
            {
                targetId = JsonPath.RequiredString(
                    track,
                    "targetId",
                    "Runtime animation track",
                    allowEmpty: true);
                if (targetId.Length > 0 && string.IsNullOrWhiteSpace(targetId))
                {
                    throw new InvalidOperationException(
                        "Runtime animation track targetId must be stable or the empty Screen sentinel.");
                }
            }
            if (!trackTargets.Add((fieldId, targetId)))
            {
                throw new InvalidOperationException(
                    $"Runtime animation contains duplicate track target '{fieldId}'/'{targetId}'.");
            }
            var frames = new HashSet<int>();
            var previousFrame = -1;
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
                if (!frames.Add(frame))
                {
                    throw new InvalidOperationException(
                        $"Runtime animation track '{fieldId}'/'{targetId}' contains duplicate frame {frame}.");
                }
                if (frame < previousFrame)
                {
                    throw new InvalidOperationException(
                        $"Runtime animation track '{fieldId}'/'{targetId}' keyframes must be ordered by frame.");
                }
                previousFrame = frame;
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

    private static IReadOnlyList<JsonObject> Collections(JsonObject contract)
    {
        var collections = JsonPath.OptionalObjectArray(contract, "collections", "Runtime owner contract");
        foreach (var collection in collections)
        {
            var fields = JsonPath.OptionalObjectArray(
                collection,
                "fields",
                "Runtime owner collection");
            ValidateCollectionTimeline(collection, fields);
            foreach (var field in fields)
            {
                ValidateFieldTimeline(field);
            }
            ValidateUniqueFieldIds(fields, "Runtime owner collection fields");
        }
        return collections;
    }

    private static IReadOnlyList<JsonObject> Inputs(JsonObject contract)
    {
        var inputs = JsonPath.OptionalObjectArray(contract, "inputs", "Runtime owner contract");
        foreach (var input in inputs) ValidateFieldTimeline(input);
        ValidateUniqueFieldIds(inputs, "Runtime owner inputs");
        return inputs;
    }

    private static IReadOnlyList<JsonObject> Actions(JsonObject contract) =>
        ValidateTemporalActions(
            JsonPath.OptionalObjectArray(contract, "actions", "Runtime owner contract"),
            "Runtime owner actions");

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
        foreach (var field in fields) ValidateFieldTimeline(field);
        ValidateUniqueFieldIds(fields, "Runtime owner item fields");
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
        return ValidateTemporalActions(actions, "Runtime owner item actions");
    }

    private static IReadOnlyList<JsonObject> ValidateTemporalActions(
        IReadOnlyList<JsonObject> actions,
        string context)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index];
            foreach (var flag in new[] { "definesModuleDuration", "extendsModuleDuration" })
            {
                if (!action.ContainsKey(flag)) continue;
                _ = JsonPath.RequiredBoolean(action, flag, $"{context}[{index}]");
            }
            if (action["definesModuleDuration"]?.GetValue<bool>() == true)
            {
                var actionId = JsonPath.RequiredString(action, "id", $"{context}[{index}]");
                _ = JsonPath.RequiredNonNegativeNumber(
                    action["durationBaseFrames"],
                    $"Runtime action '{actionId}' durationBaseFrames");
            }
            if (action["extendsModuleDuration"]?.GetValue<bool>() != true) continue;
            var finiteActionId = JsonPath.RequiredString(action, "id", $"{context}[{index}]");
            _ = JsonPath.RequiredString(
                action,
                "playInputId",
                $"Finite runtime action '{finiteActionId}'");
            if (action.ContainsKey("playFieldId"))
            {
                _ = JsonPath.RequiredString(
                    action,
                    "playFieldId",
                    $"Finite runtime action '{finiteActionId}'");
            }
            _ = JsonPath.RequiredString(
                action,
                "durationInputId",
                $"Finite runtime action '{finiteActionId}'");
            _ = JsonPath.RequiredString(
                action,
                "durationEnabledInputId",
                $"Finite runtime action '{finiteActionId}'");
        }
        return actions;
    }

    private static JsonObject Timeline(JsonObject owner) =>
        JsonPath.OptionalObject(owner, "animationTimeline", "Runtime animation owner") ?? new JsonObject();

    private static JsonObject FieldTimeline(JsonObject field)
    {
        if (!field.TryGetPropertyValue("animationTimeline", out var node) || node is null)
        {
            return new JsonObject();
        }
        return node as JsonObject
            ?? throw new InvalidOperationException(
                "Runtime animation field animationTimeline must be an object or the explicit null sentinel.");
    }

    private static void ValidateCollectionTimeline(
        JsonObject collection,
        IReadOnlyList<JsonObject> fields)
    {
        var timeline = Timeline(collection);
        if (timeline.TryGetPropertyValue("sequence", out _)
            && !JsonPath.RequiredString(timeline, "sequence", "Runtime collection animation timeline")
                .Equals("serial", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Runtime collection animation timeline has an unknown sequence mode.");
        }
        if (timeline.TryGetPropertyValue("sequenceItems", out _))
        {
            _ = JsonPath.RequiredBoolean(
                timeline,
                "sequenceItems",
                "Runtime collection animation timeline");
        }
        ValidateOwnerPhase(timeline, "Runtime collection animation timeline");
        _ = JsonPath.OptionalStringArray(
            timeline,
            "preDurationFieldIds",
            "Runtime collection animation timeline");
        _ = JsonPath.OptionalStringArray(
            timeline,
            "postDurationFieldIds",
            "Runtime collection animation timeline");
        if (timeline.TryGetPropertyValue("sequenceCompletionFieldIds", out _))
        {
            var sequenceFieldIds = JsonPath.OptionalStringArray(
                timeline,
                "sequenceCompletionFieldIds",
                "Runtime collection animation timeline");
            var declaredFieldIds = fields
                .Select((field) => JsonPath.RequiredString(
                    field,
                    "id",
                    "Runtime owner collection fields"))
                .ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fieldId in sequenceFieldIds)
            {
                if (!seen.Add(fieldId))
                {
                    throw new InvalidOperationException(
                        $"Runtime collection sequenceCompletionFieldIds contains duplicate field '{fieldId}'.");
                }
                if (!declaredFieldIds.Contains(fieldId))
                {
                    throw new InvalidOperationException(
                        $"Runtime collection sequenceCompletionFieldIds references missing field '{fieldId}'.");
                }
            }
        }

        var ownerOrigin = JsonPath.OptionalObject(
            timeline,
            "ownerOrigin",
            "Runtime collection animation timeline");
        if (ownerOrigin is null) return;
        if (!JsonPath.RequiredString(ownerOrigin, "kind", "Runtime collection owner origin")
            .Equals("firstMatchingValue", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Runtime collection owner origin has an unknown kind.");
        }
        foreach (var key in new[]
        {
            "sourceCollectionJsonKey",
            "sourceTargetIdJsonKey",
            "sourceFieldId",
            "sourceValueJsonKey",
            "matchValueJsonKey",
        })
        {
            _ = JsonPath.RequiredString(ownerOrigin, key, "Runtime collection owner origin");
        }
    }

    private static void ValidateOwnerPhase(JsonObject timeline, string owner)
    {
        if (!timeline.TryGetPropertyValue("ownerPhase", out var node)) return;
        var phase = node as JsonObject
            ?? throw new InvalidOperationException($"{owner} ownerPhase must be an object.");
        var kind = JsonPath.RequiredString(phase, "kind", $"{owner} owner phase");
        if (kind.Equals("resolvedMotion", StringComparison.Ordinal))
        {
            _ = JsonPath.RequiredObject(phase, "motion", $"{owner} resolved owner phase");
            return;
        }
        if (kind.Equals("itemMotion", StringComparison.Ordinal))
        {
            _ = JsonPath.RequiredString(phase, "jsonKey", $"{owner} item owner phase");
            return;
        }
        throw new InvalidOperationException($"{owner} ownerPhase has unknown kind '{kind}'.");
    }

    private static void ValidateFieldTimeline(JsonObject field)
    {
        var fieldId = JsonPath.RequiredString(field, "id", "Runtime animation field");
        var timeline = FieldTimeline(field);
        if (timeline.TryGetPropertyValue("extendsOwnerDuration", out _))
        {
            _ = JsonPath.RequiredBoolean(
                timeline,
                "extendsOwnerDuration",
                $"Runtime animation field '{fieldId}' timeline");
        }

        var origin = JsonPath.OptionalObject(
            timeline,
            "origin",
            $"Runtime animation field '{fieldId}' timeline");
        if (origin is not null)
        {
            var kind = JsonPath.RequiredString(
                origin,
                "kind",
                $"Runtime animation field '{fieldId}' origin");
            if (kind.Equals("fieldCompletion", StringComparison.Ordinal))
            {
                _ = JsonPath.RequiredString(
                    origin,
                    "fieldId",
                    $"Runtime animation field '{fieldId}' origin");
                var offset = JsonPath.RequiredInteger(
                    origin,
                    "offsetFrames",
                    $"Runtime animation field '{fieldId}' origin");
                if (offset < 0)
                {
                    throw new InvalidOperationException(
                        $"Runtime animation field '{fieldId}' origin offsetFrames must not be negative.");
                }
            }
            else if (!kind.Equals("ownerStart", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime animation field '{fieldId}' origin has an unknown kind.");
            }
        }

        var completion = JsonPath.OptionalObject(
            timeline,
            "completion",
            $"Runtime animation field '{fieldId}' timeline");
        if (completion is null) return;
        _ = JsonPath.RequiredString(
            completion,
            "baseDurationFieldId",
            $"Runtime animation field '{fieldId}' completion");
        if (completion.TryGetPropertyValue("trackOverride", out _)
            && !JsonPath.RequiredString(
                    completion,
                    "trackOverride",
                    $"Runtime animation field '{fieldId}' completion")
                .Equals("lastEnabledKeyframe", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Runtime animation field '{fieldId}' completion has an unknown trackOverride.");
        }
        if (completion.TryGetPropertyValue("minimumEnabledKeyframes", out _))
        {
            var minimum = JsonPath.RequiredInteger(
                completion,
                "minimumEnabledKeyframes",
                $"Runtime animation field '{fieldId}' completion");
            if (minimum < 2)
            {
                throw new InvalidOperationException(
                    $"Runtime animation field '{fieldId}' completion minimumEnabledKeyframes must be at least 2.");
            }
        }
    }

    private static string CollectionKey(JsonObject collection)
    {
        foreach (var key in new[] { "storageCollectionJsonKey", "sourceCollectionJsonKey", "jsonKey" })
        {
            if (!collection.ContainsKey(key)) continue;
            return JsonPath.RequiredString(collection, key, "Runtime owner collection");
        }
        throw new InvalidOperationException("Runtime owner collection requires an explicit storage key.");
    }

    private static void ValidateUniqueFieldIds(
        IReadOnlyList<JsonObject> fields,
        string context)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var fieldId = JsonPath.RequiredString(field, "id", context);
            if (!ids.Add(fieldId))
            {
                throw new InvalidOperationException($"{context} contain duplicate id '{fieldId}'.");
            }
        }
    }

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
        var definition = fields.FirstOrDefault((field) => Text(field["id"]) == fieldId)
            ?? throw new InvalidOperationException(
                $"Runtime animation duration references missing field '{fieldId}'.");
        var jsonKey = JsonPath.RequiredString(
            definition,
            "jsonKey",
            $"Runtime animation duration field '{fieldId}'");
        return JsonPath.RequiredNonNegativeNumber(
            owner[jsonKey],
            $"Runtime animation duration field '{fieldId}' value");
    }

    private static double SignedFieldValue(
        JsonObject owner,
        IReadOnlyList<JsonObject> fields,
        string fieldId)
    {
        var definition = fields.FirstOrDefault((field) => Text(field["id"]) == fieldId)
            ?? throw new InvalidOperationException(
                $"Runtime animation offset references missing field '{fieldId}'.");
        var jsonKey = JsonPath.RequiredString(
            definition,
            "jsonKey",
            $"Runtime animation offset field '{fieldId}'");
        var node = owner[jsonKey]
            ?? throw new InvalidOperationException(
                $"Runtime animation offset field '{fieldId}' value is required.");
        if (node is JsonValue integer && integer.TryGetValue<int>(out var intValue)) return intValue;
        if (node is JsonValue number && number.TryGetValue<double>(out var doubleValue)) return doubleValue;
        if (node is JsonValue decimalNode && decimalNode.TryGetValue<decimal>(out var decimalValue)) return (double)decimalValue;
        throw new InvalidOperationException(
            $"Runtime animation offset field '{fieldId}' value must be numeric.");
    }
}
