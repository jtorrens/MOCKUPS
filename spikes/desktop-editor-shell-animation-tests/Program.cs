using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System.Reflection;
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
    ("duration uses half-open keyframe endpoints", DurationUsesHalfOpenEndpoints),
    ("duration combines declared sequence and animation", DurationCombinesSequenceAndAnimation),
    ("animated media actions are finite", AnimatedMediaActionsAreFinite),
    ("strict validation rejects duplicate targets", StrictValidationRejectsDuplicateTargets),
    ("strict validation rejects duplicate and negative frames", StrictValidationRejectsInvalidFrames),
    ("normalization creates missing initial keyframes", NormalizationCreatesInitialKeyframe),
    ("legacy events require explicit migration", LegacyEventsAreRejected),
    ("initial animatable field vocabulary is constrained", AnimatableFieldVocabularyIsConstrained),
    ("playback state publishes play, busy and frame changes", PlaybackStatePublishesChanges),
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
            "animationTargetSequenceNumberKeys": ["delay", "write", "hold"],
            "animationTargetStartNumberKeys": ["delay"],
            "fields": [{"id":"text","animationFrameOrigin":"targetStart"}]
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
    Equal(0, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, "missing", "m2"));
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
    Equal(10, RuntimeTimeline.DurationFrames("{}", "{}", animation, 1));
}

static void DurationCombinesSequenceAndAnimation()
{
    var contract = """
        {
          "actions":[{"definesModuleDuration":true,"durationBaseFrames":1,"durationCollectionJsonKey":"messages","durationItemNumberKeys":["delay","write"]}],
          "collections":[{
            "jsonKey":"messages",
            "animationTargetSequenceNumberKeys":["delay","write"],
            "animationTargetStartNumberKeys":["delay"],
            "fields":[{"id":"text","animationFrameOrigin":"targetStart"}]
          }]
        }
        """;
    var runtime = """{"messages":[{"id":"m1","delay":2,"write":3},{"id":"m2","delay":4,"write":2}]}""";
    var animation = """
        {"schemaVersion":2,"tracks":[{"id":"t","fieldId":"text","targetId":"m2","keyframes":[
          {"id":"k","frame":5,"value":"late"}
        ]}]}
        """;
    // m2 begins at 5 + 4 = 9; its local frame 5 occupies Screen frame 14, so end is 15.
    Equal(15, RuntimeTimeline.DurationFrames(contract, runtime, animation, 1));
}

static void AnimatedMediaActionsAreFinite()
{
    var contract = """
        {"collections":[{
          "jsonKey":"messages",
          "animationTargetSequenceNumberKeys":[],
          "animationTargetStartNumberKeys":[],
          "fields":[{"id":"isPlaying","animationFrameOrigin":"targetStart"}],
          "itemActions":[{"extendsModuleDuration":true,"playInputId":"isPlaying","durationInputId":"playDurationFrames"}]
        }]}
        """;
    var runtime = """{"messages":[{"id":"m1","playDurationFrames":3}]}""";
    var animation = """
        {"schemaVersion":2,"tracks":[{"id":"play","fieldId":"isPlaying","targetId":"m1","keyframes":[
          {"id":"p","frame":1,"value":true}
        ]}]}
        """;
    Equal(4, RuntimeTimeline.DurationFrames(contract, runtime, animation, 1));
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
    foreach (var field in messageFields.Where(field => field["animatable"]?.GetValue<bool>() == true))
        Equal("targetStart", field["animationFrameOrigin"]!.GetValue<string>());
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

static ModuleInstanceAnimationDocument EmptyDocument() =>
    new("{\"schemaVersion\":2,\"tracks\":[]}");

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
