using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System.Reflection;
using System.IO;
using System.Text.Json.Nodes;

var tests = new (string Name, Action Run)[]
{
    ("v2 document rejects malformed roots", RejectsMalformedDocuments),
    ("track activation creates frame-zero state", TrackActivationCreatesInitialKeyframe),
    ("track targets persist and round-trip", TrackTargetsRoundTrip),
    ("keyframe upsert updates and orders", KeyframeUpsertUpdatesAndOrders),
    ("keyframes and tracks can be removed", KeyframesAndTracksCanBeRemoved),
    ("Screen-owned fields start at Screen zero", ScreenFieldsStartAtZero),
    ("target-owned fields use target-relative origins", TargetFieldsUseRelativeOrigins),
    ("target-owned origins include their own delay", TargetOriginsMoveWithOwnDelay),
    ("animated text replaces base write-on duration", AnimatedTextReplacesWriteOnDuration),
    ("later targets move after prior animated extent", LaterTargetsFollowAnimatedExtent),
    ("later targets move after prior finite media", LaterTargetsFollowFiniteMedia),
    ("duration uses half-open keyframe endpoints", DurationUsesHalfOpenEndpoints),
    ("duration combines declared sequence and animation", DurationCombinesSequenceAndAnimation),
    ("animated media actions are finite", AnimatedMediaActionsAreFinite),
    ("field completion dependencies reject cycles", FieldCompletionDependenciesRejectCycles),
    ("target and Screen retime preserve authored keyframes", RetimePreservesAuthoredKeyframes),
    ("non-extending fields overlap later collection items", NonExtendingFieldsOverlapLaterItems),
    ("strict validation rejects duplicate targets", StrictValidationRejectsDuplicateTargets),
    ("strict validation rejects duplicate and negative frames", StrictValidationRejectsInvalidFrames),
    ("strict validation rejects invalid target durations", StrictValidationRejectsInvalidTargetDurations),
    ("normalization creates missing initial keyframes", NormalizationCreatesInitialKeyframe),
    ("legacy events require explicit migration", LegacyEventsAreRejected),
    ("initial animatable field vocabulary is constrained", AnimatableFieldVocabularyIsConstrained),
    ("playback state publishes play, busy and frame changes", PlaybackStatePublishesChanges),
    ("collection item reorder persists stable ids", CollectionItemReorderPersistsStableIds),
    ("collection item presentation summarizes configured fields", CollectionItemPresentationSummarizesConfiguredFields),
    ("Screen tree nodes keep actions in their editor", ScreenTreeNodesKeepActionsInEditor),
    ("natural behavior timing uses graphemes and Theme pace", NaturalBehaviorTimingUsesGraphemesAndThemePace),
    ("timeline reference bands use contract-owned durations", TimelineReferenceBandsUseContractDurations),
};

var failures = new List<string>();
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add(name);
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

Console.WriteLine($"Animation desktop tests: {tests.Length - failures.Count}/{tests.Length} passed.");
if (failures.Count > 0) Environment.Exit(1);

static void RejectsMalformedDocuments()
{
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("[]"));
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("{}"));
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("{\"schemaVersion\":1,\"tracks\":[]}"));
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("{\"schemaVersion\":2}"));
}

static void TrackActivationCreatesInitialKeyframe()
{
    var document = EmptyDocument();
    document.AddTrack("subtitle", "", JsonValue.Create("online")!, "hold");
    var track = Required(document.Track("subtitle", ""));
    Equal(1, track.Keyframes.Count);
    Equal(0, track.Keyframes[0].Frame);
    Equal("online", track.Keyframes[0].Value!.GetValue<string>());
    Equal("hold", track.Keyframes[0].Interpolation);
    True(track.Keyframes[0].Enabled);
    document.AddTrack("subtitle", "", JsonValue.Create("duplicate")!, "linear");
    Equal(1, document.Tracks.Count);
}

static void TrackTargetsRoundTrip()
{
    var document = EmptyDocument();
    document.AddTrack("text", "message-1", JsonValue.Create("hello")!, "writeOn");
    document.AddTrack("subtitle", "", JsonValue.Create("online")!, "hold");
    var json = JsonNode.Parse(document.ToJson())!.AsObject();
    var tracks = json["tracks"]!.AsArray().OfType<JsonObject>().ToList();
    Equal("message-1", tracks.Single(track => track["fieldId"]!.GetValue<string>() == "text")["targetId"]!.GetValue<string>());
    True(tracks.Single(track => track["fieldId"]!.GetValue<string>() == "subtitle")["targetId"] is null);
    var reloaded = new ModuleInstanceAnimationDocument(document.ToJson());
    Equal("message-1", Required(reloaded.Track("text", "message-1")).TargetId);
}

static void KeyframeUpsertUpdatesAndOrders()
{
    var document = EmptyDocument();
    document.AddTrack("value", "", JsonValue.Create(0)!, "hold");
    document.UpsertKeyframe("value", "", 10, JsonValue.Create(10)!, "linear");
    document.UpsertKeyframe("value", "", 4, JsonValue.Create(4)!, "easeInOut");
    document.UpsertKeyframe("value", "", 4, JsonValue.Create(5)!, "linear");
    var frames = Required(document.Track("value", "")).Keyframes;
    SequenceEqual(new[] { 0, 4, 10 }, frames.Select(keyframe => keyframe.Frame));
    Equal(5, frames.Single(keyframe => keyframe.Frame == 4).Value!.GetValue<int>());
    Equal("linear", frames.Single(keyframe => keyframe.Frame == 4).Interpolation);
}

static void KeyframesAndTracksCanBeRemoved()
{
    var document = EmptyDocument();
    document.AddTrack("value", "target", JsonValue.Create(0)!, "hold");
    document.UpsertKeyframe("value", "target", 5, JsonValue.Create(5)!, "linear");
    document.RemoveKeyframe("value", "target", 5);
    Equal(1, Required(document.Track("value", "target")).Keyframes.Count);
    document.RemoveKeyframe("value", "target", 0);
    Equal(1, Required(document.Track("value", "target")).Keyframes.Count);
    document.RemoveTrack("value", "target");
    True(document.Track("value", "target") is null);
}

static void ScreenFieldsStartAtZero()
{
    Equal(0, RuntimeAnimationFrameOrigin.ScreenFrame(new JsonObject(), new JsonObject(), "subtitle", ""));
}

static void TargetFieldsUseRelativeOrigins()
{
    var contract = Object("""
        {
          "collections": [{
            "jsonKey": "messages",
            "animationTimeline": {
              "sequence": "serial",
              "preDurationFieldIds": ["delay"],
              "postDurationFieldIds": ["hold"]
            },
            "fields": [
              {"id":"text","jsonKey":"text","animationTimeline":{"origin":{"kind":"ownerStart"},"completion":{"baseDurationFieldId":"write","minimumEnabledKeyframes":2}}},
              {"id":"delay","jsonKey":"delay"},
              {"id":"write","jsonKey":"write"},
              {"id":"hold","jsonKey":"hold"}
            ]
          }]
        }
        """);
    var runtime = Object("""
        {"messages":[
          {"id":"m1","delay":2,"write":3,"hold":1},
          {"id":"m2","delay":4,"write":2,"hold":1}
        ]}
        """);
    Equal(2, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, "text", "m1"));
    Equal(10, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, "text", "m2"));
}

static void TargetOriginsMoveWithOwnDelay()
{
    var contract = SequenceContract();
    var before = Object("""{"messages":[{"id":"m1","delay":2,"write":3,"hold":1}]}""");
    var after = Object("""{"messages":[{"id":"m1","delay":7,"write":3,"hold":1}]}""");
    var animation = Object("""
        {"schemaVersion":2,"tracks":[{"id":"t","fieldId":"text","targetId":"m1","keyframes":[
          {"id":"k0","frame":0,"value":"start"},
          {"id":"k4","frame":4,"value":"later"}
        ]}]}
        """);

    Equal(2, RuntimeAnimationFrameOrigin.ScreenFrame(contract, before, animation, "text", "m1"));
    Equal(7, RuntimeAnimationFrameOrigin.ScreenFrame(contract, after, animation, "text", "m1"));
    SequenceEqual(
        new[] { 0, 4 },
        animation["tracks"]![0]!["keyframes"]!.AsArray().Select((keyframe) => keyframe!["frame"]!.GetValue<int>()));
}

static void AnimatedTextReplacesWriteOnDuration()
{
    var contract = SequenceContract();
    var runtime = Object("""{"messages":[{"id":"m1","delay":0,"write":10,"hold":0},{"id":"m2","delay":0,"write":1,"hold":0}]}""");
    var textAnimation = Object("""
        {"schemaVersion":2,"tracks":[{"id":"text","fieldId":"text","targetId":"m1","keyframes":[
          {"id":"k0","frame":0,"value":"start"},
          {"id":"k2","frame":2,"value":"finish"}
        ]}]}
        """);
    var statusAnimation = Object("""
        {"schemaVersion":2,"tracks":[{"id":"status","fieldId":"status","targetId":"m1","keyframes":[
          {"id":"k0","frame":0,"value":"sent"}
        ]}]}
        """);

    Equal(3, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, textAnimation, "text", "m2"));
    Equal(10, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, statusAnimation, "text", "m2"));
}

static void LaterTargetsFollowAnimatedExtent()
{
    var contract = SequenceContract();
    var runtime = Object("""{"messages":[{"id":"m1","delay":0,"write":2,"hold":1},{"id":"m2","delay":3,"write":1,"hold":0}]}""");
    var animation = Object("""
        {"schemaVersion":2,"tracks":[{"id":"t","fieldId":"text","targetId":"m1","keyframes":[
          {"id":"k0","frame":0,"value":"start"},
          {"id":"k5","frame":5,"value":"late"}
        ]}]}
        """);
    // m1 occupies max(write 2, keyframe end 6) + hold 1; m2 then adds delay 3.
    Equal(10, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, animation, "text", "m2"));
}

static void LaterTargetsFollowFiniteMedia()
{
    var contract = SequenceContract(withMediaAction: true);
    var runtime = Object("""{"messages":[{"id":"m1","delay":0,"write":2,"hold":1,"playDuration":5},{"id":"m2","delay":3,"write":1,"hold":0,"playDuration":1}]}""");
    var animation = Object("""
        {"schemaVersion":2,"tracks":[{"id":"p","fieldId":"isPlaying","targetId":"m1","keyframes":[
          {"id":"k0","frame":0,"value":false},
          {"id":"k1","frame":1,"value":true}
        ]}]}
        """);
    // Playback starts one frame after text completion: 2 + [1, 6), then hold 1 and delay 3.
    Equal(12, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, animation, "text", "m2"));
}

static void DurationUsesHalfOpenEndpoints()
{
    var animation = """
        {"schemaVersion":2,"tracks":[{"id":"t","fieldId":"subtitle","keyframes":[
          {"id":"k0","frame":0,"value":"a"},
          {"id":"k9","frame":9,"value":"b"},
          {"id":"disabled","frame":99,"value":"x","enabled":false}
        ]}]}
        """;
    Equal(10, RuntimeTimeline.DurationFrames(
        "{\"inputs\":[{\"id\":\"subtitle\",\"jsonKey\":\"subtitle\",\"animationTimeline\":{\"origin\":{\"kind\":\"ownerStart\"}}}]}",
        "{}",
        animation,
        1));
}

static void DurationCombinesSequenceAndAnimation()
{
    var contract = """
        {
          "collections":[{
            "jsonKey":"messages",
            "animationTimeline":{"sequence":"serial","preDurationFieldIds":["delay"],"postDurationFieldIds":[]},
            "fields":[
              {"id":"text","jsonKey":"text","animationTimeline":{"origin":{"kind":"ownerStart"},"completion":{"baseDurationFieldId":"write","minimumEnabledKeyframes":2}}},
              {"id":"delay","jsonKey":"delay"},
              {"id":"write","jsonKey":"write"}
            ]
          }]
        }
        """;
    var runtime = """{"messages":[{"id":"m1","delay":2,"write":3},{"id":"m2","delay":4,"write":2}]}""";
    var animation = """
        {"schemaVersion":2,"tracks":[{"id":"t","fieldId":"text","targetId":"m2","keyframes":[
          {"id":"k0","frame":0,"value":"start"},
          {"id":"k5","frame":5,"value":"late"}
        ]}]}
        """;
    // m2 begins at 5 + 4 = 9; its local frame 5 occupies the half-open end at 15.
    Equal(15, RuntimeTimeline.DurationFrames(contract, runtime, animation, 1));
}

static void AnimatedMediaActionsAreFinite()
{
    var contract = """
        {"collections":[{
          "jsonKey":"messages",
          "animationTimeline":{"sequence":"serial","preDurationFieldIds":[],"postDurationFieldIds":[]},
          "fields":[{"id":"isPlaying","jsonKey":"isPlaying","animationTimeline":{"origin":{"kind":"ownerStart"}}}],
          "itemActions":[{"extendsModuleDuration":true,"playInputId":"isPlaying","durationInputId":"playDurationFrames"}]
        }]}
        """;
    var runtime = """{"messages":[{"id":"m1","playDurationFrames":3}]}""";
    var animation = """
        {"schemaVersion":2,"tracks":[{"id":"play","fieldId":"isPlaying","targetId":"m1","keyframes":[
          {"id":"p0","frame":0,"value":false},
          {"id":"p1","frame":1,"value":true}
        ]}]}
        """;
    Equal(4, RuntimeTimeline.DurationFrames(contract, runtime, animation, 1));
}

static void FieldCompletionDependenciesRejectCycles()
{
    var contract = Object("""
        {"inputs":[
          {"id":"a","jsonKey":"a","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"b"}}},
          {"id":"b","jsonKey":"b","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"a"}}}
        ]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.ScreenFrame(
        contract,
        new JsonObject(),
        new JsonObject { ["schemaVersion"] = 2, ["tracks"] = new JsonArray() },
        "a",
        ""));
}

static void RetimePreservesAuthoredKeyframes()
{
    var contract = SequenceContract();
    var runtime = Object("""{"messages":[{"id":"m1","delay":2,"write":10,"hold":0}]}""");
    var animation = Object("""
        {"schemaVersion":2,"retime":{"targetDurationFrames":20,"targets":{"m1":{"targetDurationFrames":6}}},"tracks":[
          {"id":"text","fieldId":"text","targetId":"m1","keyframes":[
            {"id":"k0","frame":0,"value":"start"},
            {"id":"k2","frame":2,"value":"finish"}
          ]}
        ]}
        """);
    Equal(20, RuntimeAnimationFrameOrigin.DurationFrames(contract, runtime, animation, 1));
    Equal(20, RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(contract, runtime, animation, "m1", 3));
    SequenceEqual(
        new[] { 0, 2 },
        animation["tracks"]![0]!["keyframes"]!.AsArray().Select((keyframe) => keyframe!["frame"]!.GetValue<int>()));
}

static void NonExtendingFieldsOverlapLaterItems()
{
    var contract = Object("""
        {"collections":[{
          "jsonKey":"messages",
          "animationTimeline":{"sequence":"serial","preDurationFieldIds":["delay"],"postDurationFieldIds":[]},
          "fields":[
            {"id":"text","jsonKey":"text","animationTimeline":{"origin":{"kind":"ownerStart"},"completion":{"baseDurationFieldId":"write","minimumEnabledKeyframes":2}}},
            {"id":"delay","jsonKey":"delay"},
            {"id":"write","jsonKey":"write"},
            {"id":"status","jsonKey":"status","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"text"},"extendsOwnerDuration":false}}
          ]
        }]}
        """);
    var runtime = Object("""{"messages":[{"id":"m1","write":2},{"id":"m2","delay":3,"write":1}]}""");
    var animation = Object("""
        {"schemaVersion":2,"tracks":[{"id":"status","fieldId":"status","targetId":"m1","keyframes":[
          {"id":"k0","frame":0,"value":"sent"},{"id":"k30","frame":30,"value":"read"}
        ]}]}
        """);
    Equal(5, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, animation, "text", "m2"));
    Equal(32, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, animation, "status", "m1", 30));
    Equal(33, RuntimeAnimationFrameOrigin.DurationFrames(contract, runtime, animation, 1));
}

static void StrictValidationRejectsDuplicateTargets()
{
    var animation = Object("""
        {"schemaVersion":2,"tracks":[
          {"id":"a","fieldId":"text","targetId":"m1","keyframes":[]},
          {"id":"b","fieldId":"text","targetId":"m1","keyframes":[]}
        ]}
        """);
    Throws<InvalidOperationException>(() => InvokeDatabaseStatic("ValidateAnimationJson", animation, "instance"));
}

static void StrictValidationRejectsInvalidFrames()
{
    var duplicate = Object("""
        {"schemaVersion":2,"tracks":[{"id":"a","fieldId":"text","keyframes":[
          {"id":"k0","frame":0,"value":"a"},{"id":"k1","frame":0,"value":"b"}
        ]}]}
        """);
    var negative = Object("""
        {"schemaVersion":2,"tracks":[{"id":"a","fieldId":"text","keyframes":[
          {"id":"k0","frame":-1,"value":"a"}
        ]}]}
        """);
    Throws<InvalidOperationException>(() => InvokeDatabaseStatic("ValidateAnimationJson", duplicate, "instance"));
    Throws<InvalidOperationException>(() => InvokeDatabaseStatic("ValidateAnimationJson", negative, "instance"));
}

static void StrictValidationRejectsInvalidTargetDurations()
{
    var animation = Object("""
        {"schemaVersion":2,"retime":{"targetDurationFrames":0},"tracks":[]}
        """);
    Throws<InvalidOperationException>(() => InvokeDatabaseStatic("ValidateAnimationJson", animation, "instance"));
}

static void NormalizationCreatesInitialKeyframe()
{
    var animation = Object("""{"schemaVersion":2,"tracks":[{"id":"track","fieldId":"text","targetId":"m1","keyframes":[]}]}""");
    var runtime = Object("""{"messages":[{"id":"m1","text":"hello"}]}""");
    var contract = Object("""
        {"collections":[{"jsonKey":"messages","fields":[{
          "id":"text","jsonKey":"text","kind":"text","animatable":true,"animationInterpolations":["writeOn","hold"]
        }]}]}
        """);
    var changed = (bool)RequiredObject(InvokeDatabaseStatic("EnsureInitialAnimationKeyframes", animation, runtime, contract));
    True(changed);
    var keyframe = animation["tracks"]![0]!["keyframes"]![0]!.AsObject();
    Equal(0, keyframe["frame"]!.GetValue<int>());
    Equal("hello", keyframe["value"]!.GetValue<string>());
    Equal("writeOn", keyframe["interpolation"]!.GetValue<string>());
}

static void LegacyEventsAreRejected()
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    using (var command = connection.CreateCommand())
    {
        command.CommandText = """
            CREATE TABLE modules (id TEXT PRIMARY KEY, design_preview_json TEXT NOT NULL);
            CREATE TABLE module_instances (id TEXT PRIMARY KEY, module_id TEXT NOT NULL, animation_json TEXT NOT NULL, content_json TEXT NOT NULL);
            INSERT INTO modules VALUES ('module', '{}');
            INSERT INTO module_instances VALUES (
              'instance', 'module',
              '{"schemaVersion":1,"tracks":[{"parameterId":"text","events":[{"frame":0}]}]}',
              '{}');
            """;
        command.ExecuteNonQuery();
    }
    Throws<InvalidOperationException>(() => InvokeDatabaseStatic("NormalizeAnimationJson", connection));
}

static void AnimatableFieldVocabularyIsConstrained()
{
    var preview = (JsonObject)RequiredObject(InvokeDatabaseStatic("DefaultConversationDesignPreviewJson"));
    var screenFields = preview["inputs"]!.AsArray().OfType<JsonObject>().ToList();
    var messageFields = preview["collections"]!.AsArray().OfType<JsonObject>()
        .Single(collection => collection["id"]!.GetValue<string>() == "messages")["fields"]!
        .AsArray().OfType<JsonObject>().ToList();
    var screenAnimated = screenFields
        .Where(field => field["animatable"]?.GetValue<bool>() == true)
        .Select(field => field["id"]!.GetValue<string>());
    var messageAnimated = messageFields
        .Where(field => field["animatable"]?.GetValue<bool>() == true)
        .Select(field => field["id"]!.GetValue<string>());
    SequenceEqual(new[] { "headerSubtitle" }, screenAnimated);
    SequenceEqual(new[] { "text", "statusVisible", "status", "statusText", "isPlaying", "fullScreen" }, messageAnimated);
    Equal(
        "ownerStart",
        messageFields.Single(field => field["id"]!.GetValue<string>() == "text")["animationTimeline"]!["origin"]!["kind"]!.GetValue<string>());
    foreach (var fieldId in new[] { "statusVisible", "status", "statusText", "isPlaying", "fullScreen" })
    {
        var origin = messageFields.Single(field => field["id"]!.GetValue<string>() == fieldId)["animationTimeline"]!["origin"]!.AsObject();
        Equal("fieldCompletion", origin["kind"]!.GetValue<string>());
        Equal("text", origin["fieldId"]!.GetValue<string>());
    }
    foreach (var forbidden in new[] { "actor", "direction", "delay", "writeOn", "postWriteOnHold", "mediaSource" })
        True(messageFields.Single(field => field["id"]!.GetValue<string>() == forbidden)["animatable"] is null);
}

static void PlaybackStatePublishesChanges()
{
    var state = new PreviewPlaybackState();
    var changes = 0;
    state.Changed += () => changes++;
    state.SetPlaying(true);
    state.SetBusy(true);
    state.NotifyFrameChanged();
    state.SetPlaying(true);
    state.SetBusy(true);
    Equal(3, changes);
    True(state.IsPlaying);
    True(state.IsBusy);
    state.SetPlaying(false);
    Equal(4, changes);
}

static void CollectionItemReorderPersistsStableIds()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-animation-reorder-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var before = CollectionOrder(temporary);
        True(before.ItemIds.Count >= 2);
        var database = new SpikeDatabase(temporary);
        database.MoveModuleInstanceRuntimeCollectionItem(before.InstanceId, "messages", before.ItemIds[0], 1);
        var moved = CollectionOrder(temporary, before.InstanceId);
        Equal(before.ItemIds[0], moved.ItemIds[1]);
        Equal(before.ItemIds[1], moved.ItemIds[0]);
        database.MoveModuleInstanceRuntimeCollectionItem(before.InstanceId, "messages", before.ItemIds[0], -1);
        SequenceEqual(before.ItemIds, CollectionOrder(temporary, before.InstanceId).ItemIds);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void CollectionItemPresentationSummarizesConfiguredFields()
{
    var preview = Object("""
        {"collections":[{"id":"messages","label":"Messages","jsonKey":"messages","itemLabel":"Message","fields":[
          {"id":"direction","label":"Direction","jsonKey":"direction","kind":"option","defaultValue":"incoming","options":[{"value":"incoming","label":"Incoming"}]},
          {"id":"text","label":"Text","jsonKey":"text","kind":"text","defaultValue":""},
          {"id":"mediaType","label":"Media","jsonKey":"mediaType","kind":"option","defaultValue":"none"}
        ],"itemPresentation":{"subtitleFieldIds":["direction","text"],"subtitleMaxCharacters":24,"iconFieldId":"mediaType","fallbackIcon":"message","iconValueMap":{"image":"image"}}}]}
        """);
    var collection = ComponentPreviewInputSession.ReadRuntimeCollections(preview, new JsonObject()).Single();
    var presentation = RuntimeCollectionItemPresentation.Resolve(
        collection,
        Object("""{"direction":"incoming","text":"A message with enough words to be abbreviated","mediaType":"image"}"""),
        "Payload item 1",
        EditorIcons.Component);

    Equal("Incoming · A message wi…", presentation.Subtitle);
    Equal(EditorIcons.Image, presentation.Icon);
}

static void ScreenTreeNodesKeepActionsInEditor()
{
    var screen = new ProjectTreeNode(ProjectTreeNodeKind.ModuleInstance, "screen", "Screen", "", "module_instance");
    True(!screen.CanDuplicate);
    True(!screen.CanDelete);
}

static void NaturalBehaviorTimingUsesGraphemesAndThemePace()
{
    var contract = Object("""
        {"collections":[{"jsonKey":"messages","animationTimeline":{"sequence":"serial","preDurationFieldIds":[],"postDurationFieldIds":[]},"fields":[
          {"id":"text","jsonKey":"text","animationTimeline":{"origin":{"kind":"ownerStart"},"completion":{"baseDurationFieldId":"writeOn","minimumEnabledKeyframes":2}}},
          {"id":"writeOn","jsonKey":"writeOnTiming","valueKind":"BehaviorTiming","naturalTiming":{"sourceFieldId":"text","unit":"grapheme","baseFramesPerUnit":7}}
        ]}]}
        """);
    var runtime = Object("""
        {"messages":[{"id":"m1","text":"12345678901234567890123456789012345678901234567890","writeOnTiming":{"mode":"natural","fixedFrames":12,"paceToken":"theme.motion.naturalPace.slow"}}]}
        """);
    var theme = Object("""{"motion":{"naturalPace":{"slow":1.5}}}""");
    Equal(525, RuntimeAnimationFrameOrigin.DurationFrames(
        contract,
        runtime,
        Object("""{"schemaVersion":2,"tracks":[]}"""),
        1,
        theme));
}

static void TimelineReferenceBandsUseContractDurations()
{
    var contract = Object("""
        {"collections":[{"jsonKey":"messages","animationTimeline":{"sequence":"serial","preDurationFieldIds":[],"postDurationFieldIds":[]},"fields":[
          {"id":"text","jsonKey":"text","animationTimeline":{"origin":{"kind":"ownerStart"},"completion":{"baseDurationFieldId":"writeOn","minimumEnabledKeyframes":2}}},
          {"id":"writeOn","jsonKey":"writeOnTiming","valueKind":"BehaviorTiming","naturalTiming":{"sourceFieldId":"text","unit":"grapheme","baseFramesPerUnit":7}},
          {"id":"playing","jsonKey":"isPlaying","animationTimeline":{"origin":{"kind":"ownerStart"}}},
          {"id":"playDuration","jsonKey":"playDurationFrames"}
        ],"itemActions":[{"playInputId":"playing","durationInputId":"playDuration"}]}]}
        """);
    var runtime = Object("""
        {"messages":[{"id":"m1","text":"1234567890","writeOnTiming":{"mode":"natural","fixedFrames":12,"paceToken":"theme.motion.naturalPace.slow"},"isPlaying":false,"playDurationFrames":80}]}
        """);
    var animation = Object("""
        {"schemaVersion":2,"tracks":[{"id":"text-track","fieldId":"text","targetId":"m1","keyframes":[
          {"id":"text-0","frame":0,"value":"","interpolation":"hold","enabled":true},
          {"id":"text-37","frame":37,"value":"1234567890","interpolation":"writeOn","enabled":true}
        ]}]}
        """);
    var theme = Object("""{"motion":{"naturalPace":{"slow":1.5}}}""");

    Equal(105, RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(contract, runtime, animation, "text", "m1", theme));
    Equal(80, RuntimeAnimationFrameOrigin.FieldReferenceDurationFrames(contract, runtime, animation, "playing", "m1", theme));
}

static (string InstanceId, List<string> ItemIds) CollectionOrder(string path, string? instanceId = null)
{
    using var connection = new SqliteConnection($"Data Source={path}");
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = instanceId is null
        ? "SELECT id, content_json FROM module_instances WHERE json_array_length(json_extract(content_json, '$.messages')) >= 2 LIMIT 1"
        : "SELECT id, content_json FROM module_instances WHERE id = $id";
    if (instanceId is not null) command.Parameters.AddWithValue("$id", instanceId);
    using var reader = command.ExecuteReader();
    True(reader.Read());
    var content = Object(reader.GetString(1));
    return (
        reader.GetString(0),
        content["messages"]!.AsArray().OfType<JsonObject>()
            .Select((item) => item["id"]!.GetValue<string>())
            .ToList());
}

static ModuleInstanceAnimationDocument EmptyDocument() =>
    new("{\"schemaVersion\":2,\"tracks\":[]}");

static JsonObject SequenceContract(bool withMediaAction = false) => Object($$$$"""
    {
      "collections": [{
        "jsonKey": "messages",
        "animationTimeline": {
          "sequence": "serial",
          "preDurationFieldIds": ["delay"],
          "postDurationFieldIds": ["hold"]
        },
        "fields": [
          {"id":"text","jsonKey":"text","animationTimeline":{"origin":{"kind":"ownerStart"},"completion":{"baseDurationFieldId":"write","minimumEnabledKeyframes":2}}},
          {"id":"delay","jsonKey":"delay"},
          {"id":"write","jsonKey":"write"},
          {"id":"hold","jsonKey":"hold"},
          {"id":"isPlaying","jsonKey":"isPlaying","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"text"}}}
        ]{{{{(withMediaAction ? ",\n        \"itemActions\": [{\"extendsModuleDuration\":true,\"playInputId\":\"isPlaying\",\"durationInputId\":\"playDuration\"}]" : "")}}}}
      }]
    }
    """);

static JsonObject Object(string json) => JsonNode.Parse(json)!.AsObject();

static object? InvokeDatabaseStatic(string name, params object?[] arguments)
{
    var method = typeof(SpikeDatabase).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing SpikeDatabase.{name}.");
    try
    {
        return method.Invoke(null, arguments);
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        throw exception.InnerException;
    }
}

static T Required<T>(T? value) where T : class => value ?? throw new Exception("Expected a value.");
static object RequiredObject(object? value) => value ?? throw new Exception("Expected a value.");
static void True(bool condition)
{
    if (!condition) throw new Exception("Expected true.");
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected '{expected}', received '{actual}'.");
}
static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
{
    if (!expected.SequenceEqual(actual))
        throw new Exception($"Expected [{string.Join(", ", expected)}], received [{string.Join(", ", actual)}].");
}
static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new Exception($"Expected {typeof(TException).Name}.");
}
