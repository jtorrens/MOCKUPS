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
    ("nested collection duplication and deletion preserve animation targets", NestedCollectionTargetsFollowIdentity),
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
    ("new collection items become the only expanded item", NewCollectionItemBecomesOnlyExpanded),
    ("active component variants expose parent class actions", ActiveVariantExposesParentClassActions),
    ("module parents open Default then remember the session Variant", ModuleParentsFollowComponentVariantSelection),
    ("only Default system bar variants are protected", OnlyDefaultSystemBarVariantsAreProtected),
    ("collection item presentation summarizes configured fields", CollectionItemPresentationSummarizesConfiguredFields),
    ("Screen tree nodes keep actions in their editor", ScreenTreeNodesKeepActionsInEditor),
    ("natural behavior timing uses graphemes and Theme pace", NaturalBehaviorTimingUsesGraphemesAndThemePace),
    ("timeline reference bands use contract-owned durations", TimelineReferenceBandsUseContractDurations),
    ("Component Stack opens from Atoms and renders its empty seed", ComponentStackSeedOpensAndRenders),
    ("Collection Stack exposes one runtime-owned Default Variant", CollectionStackSeedOpensAndRenders),
    ("Notifications composes Notification items through Collection Stack", NotificationsSeedOpensAndRenders),
    ("Keypad exposes Variant keys and renders from System", KeypadSeedOpensAndRenders),
    ("Simplified editor captures Keypad defaults without live inheritance", SimplifiedEditorCapturesKeypadDefaults),
    ("dictionary fields contract labels before stacking compound actions", DictionaryFieldsRespondToCompactWidths),
    ("Label subtext placement migrates to explicit relative alignment", LabelSubtextPlacementMigrates),
    ("Password composes stateful atoms and BehaviorTiming", PasswordSeedOpensAndRenders),
    ("Lock Screen composes its runtime Stack and optional system bars", LockScreenComposesRuntimeStack),
    ("forwarded child inputs become effective parent runtime inputs", ForwardedChildInputsBecomeParentRuntimeInputs),
    ("forwarded runtime collections expose slot state actions", ForwardedRuntimeCollectionsExposeSlotStateActions),
    ("module variants are explicit and selected by Screen instances", ModuleVariantsAreExplicit),
};

static void ModuleVariantsAreExplicit()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-variants-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var roots = database.LoadProjectTree();
        var module = Descendants(roots).First((node) => node.Kind == ProjectTreeNodeKind.Module
            && node.RecordClassId == "module.core.lockScreen");
        var defaultVariant = module.Children.Single((node) => node.Id.EndsWith("::variant::default", StringComparison.Ordinal));
        True(defaultVariant.IsProtected);

        var android = database.SaveModuleVariant(defaultVariant, "Android");
        database.UpdateModuleVariantField(android, "module.appearanceMode", "dark");
        Equal("dark", JsonNode.Parse(database.GetModuleVariantSettings(android).ConfigJson)?["appearanceMode"]?.GetValue<string>());
        Equal("inherit", JsonNode.Parse(database.GetModuleVariantSettings(defaultVariant).ConfigJson)?["appearanceMode"]?.GetValue<string>());

        var shot = Descendants(database.LoadProjectTree()).First((node) => node.Kind == ProjectTreeNodeKind.Shot);
        var appId = module.Parent?.Id ?? throw new InvalidOperationException("Lock Screen module has no App.");
        var screen = database.AddModuleInstance(shot, new SpikeDatabase.ShotModuleChoice(
            module.Id, module.Name, module.Parent!.Name, appId, module.RecordClassId));
        database.UpdateModuleInstanceRuntimeValue(screen.Id, "orphan", JsonValue.Create("remove me"));
        database.UpdateModuleInstanceAnimationJson(screen.Id,
            "{\"schemaVersion\":2,\"tracks\":[{\"id\":\"orphan-track\",\"fieldId\":\"orphan\",\"targetId\":\"\",\"keyframes\":[{\"id\":\"orphan-kf\",\"frame\":0,\"value\":true,\"interpolation\":\"hold\",\"enabled\":true}]}]}");
        database.UpdateModuleInstanceVariant(screen.Id, android.Id);
        Equal(android.Id, database.GetModuleInstanceVariantReference(screen.Id));
        Equal("dark", JsonNode.Parse(database.GetModuleInstanceVariantSettings(screen.Id).ConfigJson)?["appearanceMode"]?.GetValue<string>());
        True(JsonNode.Parse(database.GetModuleInstanceSettings(screen.Id).ContentJson)?["orphan"] is null);
        Equal(0, JsonNode.Parse(database.GetModuleInstanceSettings(screen.Id).AnimationJson)?["tracks"]?.AsArray().Count);
        Throws<InvalidOperationException>(() => database.DeleteModuleVariant(android));

        database.UpdateModuleInstanceVariant(screen.Id, defaultVariant.Id);
        database.DeleteModuleVariant(android);
        True(!database.GetModuleVariantOptions(module.Id).Any((option) => option.Value == android.Id));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static IEnumerable<ProjectTreeNode> Descendants(IEnumerable<ProjectTreeNode> nodes)
{
    foreach (var node in nodes)
    {
        yield return node;
        foreach (var child in Descendants(node.Children)) yield return child;
    }
}

static void LabelSubtextPlacementMigrates()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-label-layout-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT config_json, metadata_json FROM component_classes WHERE component_type = 'label' LIMIT 1";
            using var reader = select.ExecuteReader();
            True(reader.Read());
            var config = JsonNode.Parse(reader.GetString(0))?.AsObject()
                ?? throw new InvalidOperationException("Missing Label config.");
            var metadata = JsonNode.Parse(reader.GetString(1))?.AsObject()
                ?? throw new InvalidOperationException("Missing Label metadata.");
            static void RestoreLegacyPlacement(JsonObject owner)
            {
                var label = owner["label"]?.AsObject()
                    ?? throw new InvalidOperationException("Missing Label values.");
                label.Remove("subtextVerticalPosition");
                label.Remove("subtextHorizontalAlign");
                label["subtextPlacement"] = JsonNode.Parse(
                    """{"mode":"outsideEdge","alignX":0.9,"alignY":0.25,"offsetX":19,"offsetY":-7}""");
            }
            RestoreLegacyPlacement(config);
            foreach (var preset in metadata["presets"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                if (preset["config"] is JsonObject presetConfig) RestoreLegacyPlacement(presetConfig);
            }
            reader.Close();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE component_classes SET config_json = $config, metadata_json = $metadata WHERE component_type = 'label'";
            update.Parameters.AddWithValue("$config", config.ToJsonString());
            update.Parameters.AddWithValue("$metadata", metadata.ToJsonString());
            update.ExecuteNonQuery();
        }

        var database = new SpikeDatabase(temporary);
        var settings = database.GetComponentClassSettings("component_project_foqn_s2_label");
        var migrated = JsonNode.Parse(settings.ConfigJson)?["label"]?.AsObject()
            ?? throw new InvalidOperationException("Missing migrated Label values.");
        True(migrated["subtextPlacement"] is null);
        Equal("top", migrated["subtextVerticalPosition"]?.GetValue<string>() ?? "");
        Equal("center", migrated["subtextHorizontalAlign"]?.GetValue<string>() ?? "");
        var subtextFields = database.LoadEditorLayout("component.label").Cards
            .SelectMany((card) => card.VisibleGroups)
            .Single((group) => group.Id == "labelSubtext")
            .VisibleFields.OrderBy((field) => field.Order).Select((field) => field.Id).ToList();
        SequenceEqual(
            ["component.label.textGapToken", "component.label.reserveSubtextSpace", "component.label.subtextVerticalPosition", "component.label.subtextHorizontalAlign", "component.label.subtextColorToken", "component.label.subtextTypography"],
            subtextFields);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void DictionaryFieldsRespondToCompactWidths()
{
    Equal(180d, DictionaryFieldLayoutRules.ResponsiveLabelWidth(1000, compact: false));
    Equal(136d, DictionaryFieldLayoutRules.ResponsiveLabelWidth(400, compact: false));
    Equal(72d, DictionaryFieldLayoutRules.ResponsiveLabelWidth(120, compact: true));
    True(DictionaryFieldLayoutRules.UsesStackedActions(
        availableWidth: 250,
        contentMinimumWidth: 106,
        actionsMinimumWidth: 154,
        columnGapCount: 2,
        columnSpacing: 8));
    True(!DictionaryFieldLayoutRules.UsesStackedActions(
        availableWidth: 300,
        contentMinimumWidth: 106,
        actionsMinimumWidth: 154,
        columnGapCount: 2,
        columnSpacing: 8));
}

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

static void SimplifiedEditorCapturesKeypadDefaults()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-simplified-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var keypadLayout = database.LoadEditorLayout("component.keypad");
        True(keypadLayout.Simplified is not null);
        var enabledChildCount = keypadLayout.Simplified!.Entries.Count((entry) => entry.Enabled);
        True(enabledChildCount >= 5);
        True(keypadLayout.Simplified.Entries.Any((entry) =>
            entry.FieldId == "component.keypad.sizingMode" && entry.Enabled));
        True(keypadLayout.Simplified.Entries.Any((entry) =>
            entry.CollectionFieldId == "component.keypad.keys"
            && entry.ItemId == "key_star"
            && entry.ItemFieldId == "kind"
            && entry.Enabled));

        var passwordLayout = database.LoadEditorLayout("component.password");
        var passwordProjection = new EditorSimplifiedProjectionState(
            database,
            "component.password",
            passwordLayout);
        True(passwordProjection.IsAvailable);
        var captured = passwordProjection.Layout!.Entries
            .Where((entry) => entry.Captured
                && entry.SlotFieldIds.FirstOrDefault() == "component.password.keypad.editor")
            .ToList();
        Equal(enabledChildCount, captured.Count);

        keypadLayout.Simplified.Groups[0].Entries.Add(new EditorSimplifiedEntry
        {
            Id = "late-child-field",
            Kind = "field",
            FieldId = "component.keypad.columns",
            Order = 999,
            Enabled = true,
        });
        database.SaveEditorLayout("component.keypad", keypadLayout);

        var reopenedPassword = database.LoadEditorLayout("component.password");
        var reopenedProjection = new EditorSimplifiedProjectionState(
            database,
            "component.password",
            reopenedPassword);
        var recaptured = reopenedProjection.Layout!.Entries
            .Where((entry) => entry.Captured
                && entry.SlotFieldIds.FirstOrDefault() == "component.password.keypad.editor")
            .ToList();
        Equal(enabledChildCount, recaptured.Count);
        True(recaptured.All((entry) => !entry.Id.EndsWith(":late-child-field", StringComparison.Ordinal)));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ForwardedChildInputsBecomeParentRuntimeInputs()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-forwarding-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var settings = database.GetComponentClassSettings("component_project_foqn_s2_textInputBar");
        var config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        var preview = DesignPreviewTestValues.Parse(settings.DesignPreviewJson);
        var effective = RuntimeInputForwardingContract.EffectivePreview(preview, config);
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(effective, config);
        var forwarded = inputs.Single((input) =>
            input.Id == "forwarded.component.textInput.textBox.inputs.sampleText");
        Equal("Text", forwarded.Label);
        Equal("Message", forwarded.DefaultValue);
        Equal("Message", effective[forwarded.JsonKey]?.GetValue<string>());
        True(config["textInput"]?["textBoxInputs"]?[RuntimeInputForwardingContract.StorageKey]?["sampleText"] is JsonObject);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ForwardedRuntimeCollectionsExposeSlotStateActions()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-forwarded-slots-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var moduleVariant = database.LoadProjectTree()
            .SelectMany(DescendantsAndSelf)
            .Single((node) => node.Kind == ProjectTreeNodeKind.ModuleVariant
                && node.Parent?.RecordClassId == "module.core.lockScreen"
                && node.Name == "Default");
        var settings = database.GetModuleVariantSettings(moduleVariant);
        var config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        var authoredItems = config["lockScreen"]?["stackInputs"]?["items"] as JsonArray
            ?? throw new InvalidOperationException("Missing Lock Screen Stack items.");
        var authoredStates = authoredItems[0]?["alternatives"] as JsonArray
            ?? throw new InvalidOperationException("Missing Lock Screen Stack states.");
        if (authoredStates.Count < 2)
        {
            var added = authoredStates[0]?.DeepClone() as JsonObject
                ?? throw new InvalidOperationException("Missing Lock Screen default State.");
            added["id"] = $"{authoredItems[0]?["id"]?.GetValue<string>()}_test_state";
            added["active"] = false;
            added["behavior"] = "replace";
            authoredStates.Add(added);
            database.UpdateModuleVariantField(moduleVariant, "module.lockScreen.stackItems", authoredItems.ToJsonString());
            settings = database.GetModuleVariantSettings(moduleVariant);
            config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        }
        var effective = RuntimeInputForwardingContract.EffectivePreview(
            DesignPreviewTestValues.Parse(settings.DesignPreviewJson),
            config);
        var collections = ComponentPreviewInputSession.ReadRuntimeCollections(effective, config);
        var slots = collections.Single((collection) => collection.Id == "stackStates");
        var items = DesignPreviewTestValues.CollectionItems(effective, slots);
        Equal(2, (items[0]?["alternatives"] as JsonArray)?.Count ?? 0);
        var actions = ComponentPreviewActions.Read(effective);
        var stateAction = actions.Single((action) => action.CollectionJsonKey == slots.JsonKey
            && action.TargetInputId == "runtimeStateId"
            && action.CollectionItemId == items[0]?["id"]?.GetValue<string>());

        var theme = database.LoadProjectTree().SelectMany(DescendantsAndSelf)
            .First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var payload = Required(DesignPreviewPayloadFactory.Create(database, moduleVariant, theme.Id));
        var session = new ComponentPreviewInputSession(database, () => { });
        session.UpdateForPayload(payload, settings.ProjectId);
        var deletedStateId = items[0]?["alternatives"]?[1]?["id"]?.GetValue<string>() ?? "";
        True(session.TriggerAction(stateAction.Id, deletedStateId));
        var selected = session.ApplyInputs(payload, "light", settings.ProjectId);
        var selectedPreview = DesignPreviewTestValues.Parse(selected.DesignPreviewJson);
        Equal(deletedStateId, selectedPreview[slots.JsonKey]?[0]?["runtimeStateId"]?.GetValue<string>() ?? "");

        var stackItems = config["lockScreen"]?["stackInputs"]?["items"]?.DeepClone() as JsonArray
            ?? throw new InvalidOperationException("Missing Lock Screen Stack items.");
        (stackItems[0]?["alternatives"] as JsonArray)?.RemoveAt(1);
        database.UpdateModuleVariantField(moduleVariant, "module.lockScreen.stackItems", stackItems.ToJsonString());
        var updatedPayload = Required(DesignPreviewPayloadFactory.Create(database, moduleVariant, theme.Id));
        session.UpdateForPayload(updatedPayload, settings.ProjectId);
        var normalized = session.ApplyInputs(updatedPayload, "light", settings.ProjectId);
        var normalizedPreview = DesignPreviewTestValues.Parse(normalized.DesignPreviewJson);
        var firstRemainingStateId = normalizedPreview[slots.JsonKey]?[0]?["alternatives"]?[0]?["id"]?.GetValue<string>() ?? "";
        Equal(firstRemainingStateId, normalizedPreview[slots.JsonKey]?[0]?["runtimeStateId"]?.GetValue<string>() ?? "");
        True(firstRemainingStateId != deletedStateId);
    }
    finally
    {
        File.Delete(temporary);
    }
}

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

static void NestedCollectionTargetsFollowIdentity()
{
    var document = EmptyDocument();
    document.AddTrack("active", "state-1", JsonValue.Create(false)!, "hold");
    document.UpsertKeyframe("active", "state-1", 8, JsonValue.Create(true)!, "hold");
    document.DuplicateTargets(new Dictionary<string, string> { ["state-1"] = "state-2" });
    var duplicate = Required(document.Track("active", "state-2"));
    SequenceEqual([0, 8], duplicate.Keyframes.Select((keyframe) => keyframe.Frame));
    document.RemoveTarget("state-1");
    True(document.Track("active", "state-1") is null);
    True(document.Track("active", "state-2") is not null);
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
            CREATE TABLE modules (id TEXT PRIMARY KEY, design_preview_json TEXT NOT NULL, metadata_json TEXT NOT NULL);
            CREATE TABLE module_instances (id TEXT PRIMARY KEY, module_id TEXT NOT NULL, animation_json TEXT NOT NULL, content_json TEXT NOT NULL, metadata_json TEXT NOT NULL);
            INSERT INTO modules VALUES ('module', '{}', '{"variants":[{"id":"default","name":"Default","protected":true,"locked":false,"config":{}}]}');
            INSERT INTO module_instances VALUES (
              'instance', 'module',
              '{"schemaVersion":1,"tracks":[{"parameterId":"text","events":[{"frame":0}]}]}',
              '{}', '{"moduleVariantReference":"module::variant::default"}');
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

static void NewCollectionItemBecomesOnlyExpanded()
{
    var state = new EditorSessionUiState();
    state.SetExpanded("first", true);
    state.SetOnlyExpanded(["first", "second"], "second");
    state.RequestReveal("second");
    True(!state.IsExpanded("first"));
    True(state.IsExpanded("second"));
    True(state.ConsumeReveal("second"));
    True(!state.ConsumeReveal("second"));
}

static void ActiveVariantExposesParentClassActions()
{
    var componentClass = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentClass, "component", "Component", "", "component.audio");
    var variant = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentPreset, "variant", "Default", "", "component.audio", componentClass);
    var otherComponentClass = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentClass, "other", "Other", "", "component.avatar");
    True(EditorNavigationRenderer.ShowsActions(componentClass, variant));
    True(EditorNavigationRenderer.ShowsActions(variant, variant));
    True(!EditorNavigationRenderer.ShowsActions(otherComponentClass, variant));
    True(!EditorNavigationRenderer.ShowsActions(componentClass, null));
}

static void ModuleParentsFollowComponentVariantSelection()
{
    var app = new ProjectTreeNode(ProjectTreeNodeKind.App, "app", "System", "", "app.system");
    var module = new ProjectTreeNode(
        ProjectTreeNodeKind.Module, "module", "Lock Screen", "", "module.core.lockScreen", app);
    app.AddChild(module);
    var defaultVariant = new ProjectTreeNode(
        ProjectTreeNodeKind.ModuleVariant, "module::variant::default", "Default", "", "module.variant", module,
        isProtected: true);
    var androidVariant = new ProjectTreeNode(
        ProjectTreeNodeKind.ModuleVariant, "module::variant::android", "Android", "", "module.variant", module);
    module.AddChild(defaultVariant);
    module.AddChild(androidVariant);

    var selection = new EditorNodeSelectionState();
    Equal(defaultVariant.Id, selection.ResolveSelectionNode(module).Id);
    selection.RememberComponentPresetSelection(androidVariant);
    Equal(androidVariant.Id, selection.ResolveSelectionNode(module).Id);
    True(module.CanRenameDirectly);
    True(EditorNavigationRenderer.ShowsActions(module, androidVariant));
}

static void OnlyDefaultSystemBarVariantsAreProtected()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-system-variants-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        foreach (var componentType in new[] { "status_bar", "navigation_bar" })
        {
            var componentClass = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
                && database.GetComponentClassSettings(node.Id).ComponentType == componentType);
            var variants = componentClass.Children
                .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset)
                .ToList();
            var defaultVariant = variants.Single((node) => node.Name == "Default");
            True(defaultVariant.IsProtected);
            True(variants.Where((node) => node != defaultVariant).All((node) => !node.IsProtected));
        }
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ComponentStackSeedOpensAndRenders()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-component-stack-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var stack = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "componentStack");
        Equal("Atoms", stack.Parent?.Name ?? "");
        var defaultVariant = stack.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset && node.IsLocked);
        var settings = database.GetComponentClassSettings(stack.Id);
        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject ?? throw new InvalidOperationException("Missing Component Stack config.");
        var stackConfig = config["componentStack"] as JsonObject ?? throw new InvalidOperationException("Missing Component Stack contract.");
        True(!stackConfig.ContainsKey("order"));
        True(!stackConfig.ContainsKey("slots"));
        Equal(0, stackConfig.Count);
        var designPreview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject ?? throw new InvalidOperationException("Missing Component Stack Runtime Inputs.");
        True(designPreview["items"] is JsonArray);
        var runtimeInputs = ComponentPreviewInputSession.ReadRuntimeInputs(designPreview, config);
        SequenceEqual(["sizingMode", "startGapToken", "endGapToken"], runtimeInputs.Select((input) => input.Id).ToList());
        Equal("fill", runtimeInputs[0].DefaultValue);
        Equal("theme.spacing.none", runtimeInputs[1].DefaultValue);
        Equal("theme.spacing.none", runtimeInputs[2].DefaultValue);
        var collections = designPreview["collections"] as JsonArray ?? throw new InvalidOperationException("Missing Component Stack collection contract.");
        Equal("items", collections.OfType<JsonObject>().Single()["jsonKey"]?.GetValue<string>() ?? "");
        var runtimeCollection = ComponentPreviewInputSession.ReadRuntimeCollections(designPreview, config).Single();
        var alternatives = runtimeCollection.Fields.Single((field) => field.Id == "alternatives").StructuredCollection
            ?? throw new InvalidOperationException("Missing Component Stack state collection contract.");
        True(runtimeCollection.Fields.All((field) => field.Id != "alignment"));
        var placementField = alternatives.Fields.Single((field) => field.Id == "placement");
        Equal(ValueKind.AlignmentPlacement, placementField.ValueKind);
        True(DesignPreviewTestValues.ValueNode(placementField, placementField.DefaultValue) is JsonObject);
        var defaultStates = JsonNode.Parse(runtimeCollection.Fields.Single((field) => field.Id == "alternatives").DefaultValue) as JsonArray;
        Equal(1, defaultStates?.Count ?? -1);
        var fixedGapField = runtimeCollection.Fields.Single((field) => field.Id == "gapBeforeToken");
        var reflowWeightField = runtimeCollection.Fields.Single((field) => field.Id == "gapBeforeWeight");
        var fixedGapItem = new JsonObject { ["gapBeforeMode"] = "fixed" };
        True(!CollectionFieldAvailability.IsEnabled(fixedGapItem, fixedGapField, 0));
        True(CollectionFieldAvailability.IsEnabled(fixedGapItem, fixedGapField, 1));
        True(!CollectionFieldAvailability.IsEnabled(fixedGapItem, reflowWeightField, 1));
        var reflowGapItem = new JsonObject { ["gapBeforeMode"] = "reflow" };
        True(!CollectionFieldAvailability.IsEnabled(reflowGapItem, fixedGapField, 1));
        True(CollectionFieldAvailability.IsEnabled(reflowGapItem, reflowWeightField, 1));
        var componentOptions = database.GetComponentPresetReferenceOptions(settings.ProjectId, "*,-componentStack");
        True(componentOptions.All((option) => !option.Value.StartsWith(stack.Id + "::preset::", StringComparison.Ordinal)));
        True(componentOptions.All((option) => !string.IsNullOrWhiteSpace(option.GroupValue)));
        True(componentOptions.GroupBy((option) => option.GroupValue)
            .All((group) => group.Any((option) => option.Value == $"{group.Key}::preset::default")));
        _ = database.GetReferenceUsageDetails(stack);
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(DesignPreviewPayloadFactory.Create(database, defaultVariant, theme.Id));
        var refreshCount = 0;
        var inputSession = new ComponentPreviewInputSession(database, () => refreshCount++);
        inputSession.UpdateForPayload(payload, settings.ProjectId);
        var resolvedPayload = inputSession.ApplyInputs(payload, "light", settings.ProjectId);
        var resolvedPreview = DesignPreviewTestValues.Parse(resolvedPayload.DesignPreviewJson);
        True(resolvedPreview["items"]?[1]?["alternatives"]?[0]?["inputs"]?["showBadge"]?.GetValue<bool>() == true);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            resolvedPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(html));
        True(!html.Contains("preview-error", StringComparison.Ordinal));

        var childVariant = database.GetComponentPresetReferenceOptionsByType(settings.ProjectId, "audio").First().Value;
        var audioInputs = database.GetComponentPresetRuntimeInputs(childVariant);
        True(audioInputs["showBadge"] is JsonValue);
        Equal("icon", audioInputs["badgeContentMode"]?.GetValue<string>() ?? "");
        True(RuntimeInputFieldDefinitionFactory.Create(
            database,
            defaultVariant,
            alternatives.Fields.Single((field) => field.Id == "presetId")).SelectComponentClass);
        var runtimeItem = new JsonObject
        {
            ["id"] = "test_button",
            ["alternatives"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "test_button_default",
                    ["presetId"] = childVariant,
                    ["overrides"] = new JsonObject(),
                    ["inputs"] = audioInputs,
                    ["active"] = false,
                    ["behavior"] = "replace",
                    ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":0.5,"alignY":0.5,"offsetX":0,"offsetY":0}"""),
                    ["enterMotion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString()),
                    ["exitMotion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString()),
                },
            },
            ["gapBeforeMode"] = "fixed",
            ["gapBeforeToken"] = "theme.spacing.m",
            ["gapBeforeWeight"] = 1,
        };
        inputSession.SetExternalCollectionItems("items", [runtimeItem]);
        Equal(1, refreshCount);
        var childVariantNode = nodes.Single((node) => node.Id == childVariant);
        True(!database.CreateComponentPresetFieldValue(
            childVariantNode,
            "component.audio.surface.editor").Definition.SelectComponentClass);
        var otherPayload = Required(DesignPreviewPayloadFactory.Create(database, childVariantNode, theme.Id));
        inputSession.UpdateForPayload(otherPayload, settings.ProjectId);
        var revisitedPreview = inputSession.ApplyTransientTestValues(designPreview, payload);
        Equal(1, (revisitedPreview["items"] as JsonArray)?.Count ?? -1);
        var transientPayload = inputSession.ApplyInputs(payload, "light", settings.ProjectId);
        var transientPreview = JsonNode.Parse(transientPayload.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing transient Component Stack preview.");
        Equal(1, (transientPreview["items"] as JsonArray)?.Count ?? -1);
        var transientHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            transientPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(transientHtml));
        True(!transientHtml.Contains("preview-error", StringComparison.Ordinal));

        var selectedComponent = database.GetComponentPresetSelectionSettings(childVariant);
        var overrides = new JsonObject();
        var runtimeOverrideChanges = 0;
        var runtimeContext = new EditorEmbeddedContext(
            defaultVariant,
            [],
            new RuntimeComponentOverrideSource(
                selectedComponent.ProjectId,
                childVariant,
                selectedComponent.ComponentType,
                selectedComponent.RecordClassId,
                selectedComponent.ConfigJson,
                overrides,
                (_) => runtimeOverrideChanges++));
        Equal(selectedComponent.RecordClassId, runtimeContext.RecordClassId);
        Equal(selectedComponent.ComponentType, runtimeContext.ComponentType);
        True(runtimeContext.CreateFieldValue(database, "component.audio.padding").IsInherited);
        runtimeContext.CommitFieldValue(database, "component.audio.padding", "theme.spacing.xl|theme.spacing.l");
        Equal(1, runtimeOverrideChanges);
        True(!runtimeContext.CreateFieldValue(database, "component.audio.padding").IsInherited);
        runtimeContext.CommitFieldValue(database, "component.audio.padding", "inherited");
        Equal(2, runtimeOverrideChanges);
        var surfaceSlot = EmbeddedComponentSlotCatalog.Get("component.audio.surface.editor");
        var badgeSlot = EmbeddedComponentSlotCatalog.Get("component.audio.badge.editor");
        Equal("badge", badgeSlot.EmbeddedComponentType);
        Equal("component.badge", badgeSlot.RecordClassId);
        var nestedRuntimeContext = runtimeContext.Nested(surfaceSlot);
        Equal(surfaceSlot.RecordClassId, nestedRuntimeContext.RecordClassId);
        Equal(surfaceSlot.EmbeddedComponentType, nestedRuntimeContext.ComponentType);
        var nestedFieldId = database.LoadEditorLayout(surfaceSlot.RecordClassId).Cards
            .Where((card) => card.Visible)
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .Select((field) => field.Id)
            .First(ComponentClassFieldCatalog.IsRuntimeOverrideField);
        _ = nestedRuntimeContext.CreateFieldValue(database, nestedFieldId);
        var avatarVariant = database.GetComponentPresetReferenceOptionsByType(settings.ProjectId, "avatar").First().Value;
        var avatarSelection = database.GetComponentPresetSelectionSettings(avatarVariant);
        var avatarContext = new EditorEmbeddedContext(
            defaultVariant,
            [],
            new RuntimeComponentOverrideSource(
                avatarSelection.ProjectId,
                avatarVariant,
                avatarSelection.ComponentType,
                avatarSelection.RecordClassId,
                avatarSelection.ConfigJson,
                new JsonObject(),
                (_) => { }));
        foreach (var avatarFieldId in database.LoadEditorLayout(avatarSelection.RecordClassId).Cards
                     .Where((card) => card.Visible)
                     .SelectMany((card) => card.VisibleGroups)
                     .SelectMany((group) => group.VisibleFields)
                     .Select((field) => field.Id)
                     .Where(ComponentClassFieldCatalog.IsRuntimeOverrideField)
                     .Distinct(StringComparer.Ordinal))
        {
            _ = avatarContext.CreateFieldValue(database, avatarFieldId);
        }
        var selectedLayout = database.LoadEditorLayout(selectedComponent.RecordClassId);
        foreach (var fieldId in selectedLayout.Cards
                     .Where((card) => card.Visible)
                     .OrderBy((card) => card.Order)
                     .SelectMany((card) => card.VisibleGroups)
                     .SelectMany((group) => group.VisibleFields)
                     .Select((field) => field.Id)
                     .Where(ComponentClassFieldCatalog.IsRuntimeOverrideField)
                     .Distinct(StringComparer.Ordinal))
        {
            _ = database.CreateRuntimeComponentOverrideFieldValue(
                selectedComponent.ProjectId,
                selectedComponent.ConfigJson,
                overrides,
                fieldId);
        }
        True(!ComponentClassFieldCatalog.IsRuntimeOverrideField("core.name"));
        True(!ComponentClassFieldCatalog.IsRuntimeOverrideField("core.notes"));
        True(ComponentClassFieldCatalog.IsRuntimeOverrideField("component.audio.padding"));
        var inheritedPadding = database.CreateRuntimeComponentOverrideFieldValue(
            selectedComponent.ProjectId,
            selectedComponent.ConfigJson,
            overrides,
            "component.audio.padding");
        True(inheritedPadding.IsInherited);
        database.UpdateRuntimeComponentOverride(overrides, "component.audio.padding", "theme.spacing.xl|theme.spacing.l");
        True(!database.CreateRuntimeComponentOverrideFieldValue(
            selectedComponent.ProjectId,
            selectedComponent.ConfigJson,
            overrides,
            "component.audio.padding").IsInherited);
        database.UpdateRuntimeComponentOverride(overrides, "component.audio.padding", "inherited");
        True(database.CreateRuntimeComponentOverrideFieldValue(
            selectedComponent.ProjectId,
            selectedComponent.ConfigJson,
            overrides,
            "component.audio.padding").IsInherited);

        designPreview["items"] = new JsonArray(runtimeItem.DeepClone());
        database.UpdateComponentClassDesignPreviewJson(stack.Id, designPreview.ToJsonString());
        var populatedPayloadSource = Required(DesignPreviewPayloadFactory.Create(database, defaultVariant, theme.Id));
        var populatedInputSession = new ComponentPreviewInputSession(database, () => { });
        populatedInputSession.UpdateForPayload(populatedPayloadSource, settings.ProjectId);
        var populatedPayload = populatedInputSession.ApplyInputs(populatedPayloadSource, "light", settings.ProjectId);
        var populatedPreview = DesignPreviewTestValues.Parse(populatedPayload.DesignPreviewJson);
        True(populatedPreview["items"]?[0]?["alternatives"]?[0]?["inputs"]?["showBadge"] is JsonValue);
        var populatedHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            populatedPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(populatedHtml));
        True(!populatedHtml.Contains("preview-error", StringComparison.Ordinal));
        var reopened = new SpikeDatabase(temporary);
        var reopenedPreview = JsonNode.Parse(reopened.GetComponentClassSettings(stack.Id).DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing reopened Component Stack Runtime Inputs.");
        Equal(1, (reopenedPreview["items"] as JsonArray)?.Count ?? -1);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void CollectionStackSeedOpensAndRenders()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-collection-stack-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var stack = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "collectionStack");
        Equal("Atoms", stack.Parent?.Name ?? "");
        var variants = stack.Children.Where((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset).ToList();
        Equal(1, variants.Count);
        Equal("Default", variants[0].Name);
        True(variants[0].IsLocked);

        var settings = database.GetComponentClassSettings(stack.Id);
        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Collection Stack config.");
        Equal(0, (config["collectionStack"] as JsonObject)?.Count ?? -1);
        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Collection Stack preview.");
        var runtimeInputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);
        SequenceEqual(
            ["distributionMode", "sizingMode", "startGapToken", "endGapToken", "stackDirection", "stackOffsetToken", "itemSizingMode", "scaleRatio", "opacityRatio"],
            runtimeInputs.Select((input) => input.Id).ToList());
        True(runtimeInputs.Single((input) => input.Id == "distributionMode").RefreshOnCommit);
        Equal("distributionMode", runtimeInputs.Single((input) => input.Id == "sizingMode").EnabledWhenPath);
        Equal("flow", runtimeInputs.Single((input) => input.Id == "sizingMode").EnabledWhenValue);
        Equal("stacked", runtimeInputs.Single((input) => input.Id == "scaleRatio").EnabledWhenValue);
        Equal("stacked", runtimeInputs.Single((input) => input.Id == "opacityRatio").EnabledWhenValue);
        Equal("stacked", preview["distributionMode"]?.GetValue<string>() ?? "");
        Equal("content", preview["sizingMode"]?.GetValue<string>() ?? "");
        var collection = ComponentPreviewInputSession.ReadRuntimeCollections(preview, config).Single();
        Equal("items", collection.JsonKey);
        Equal("*,-collectionStack", collection.Fields.Single((field) => field.Id == "presetId").ComponentType);

        var componentOptions = database.GetComponentPresetReferenceOptions(settings.ProjectId, "*,-collectionStack");
        True(componentOptions.All((option) => !option.Value.StartsWith(stack.Id + "::preset::", StringComparison.Ordinal)));
        True(componentOptions.Any((option) => option.GroupValue.EndsWith("componentStack", StringComparison.Ordinal)));

        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(DesignPreviewPayloadFactory.Create(database, variants[0], theme.Id));
        var inputSession = new ComponentPreviewInputSession(database, () => { });
        inputSession.UpdateForPayload(payload, settings.ProjectId);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            inputSession.ApplyInputs(payload, "light", settings.ProjectId)).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(html));
        True(!html.Contains("preview-error", StringComparison.Ordinal));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void NotificationsSeedOpensAndRenders()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-notifications-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var notification = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "notification");
        var notifications = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "notifications");
        var badge = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "badge");
        Equal("Components", notification.Parent?.Name ?? "");
        Equal("Components", notifications.Parent?.Name ?? "");
        Equal("Atoms", badge.Parent?.Name ?? "");
        Equal("component.surface", Required(EmbeddedComponentSlotCatalog.Get("component.notification.surface.editor")).RecordClassId);
        Equal("component.label", Required(EmbeddedComponentSlotCatalog.Get("component.notification.summaryLabel.editor")).RecordClassId);
        Equal("component.label", Required(EmbeddedComponentSlotCatalog.Get("component.notification.detailLabel.editor")).RecordClassId);
        Equal("component.badge", Required(EmbeddedComponentSlotCatalog.Get("component.notifications.badge.editor")).RecordClassId);
        var avatarLayout = database.LoadEditorLayout("component.avatar");
        SequenceEqual(
            ["component.avatar.badge.editor", "component.avatar.badge.placement"],
            avatarLayout.Cards.Single((card) => card.Id == "avatar").VisibleGroups
                .Single((group) => group.Id == "avatarBadge").VisibleFields
                .OrderBy((field) => field.Order)
                .Select((field) => field.Id)
                .ToList());
        var notificationVariant = notification.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset);
        var notificationsVariant = notifications.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset);
        var notificationLayout = database.LoadEditorLayout("component.notification");
        Equal("component.notification", EditorContentController.OwnerLayoutRecordClassId(notificationVariant));
        SequenceEqual(["general", "layout", "avatar", "summaryLabel", "detailLabel"],
            notificationLayout.Cards.OrderBy((card) => card.Order).Select((card) => card.Id).ToList());
        SequenceEqual(
            ["component.notification.dimensionMode", "component.notification.size", "component.notification.padding", "component.notification.gapToken", "component.notification.surface.editor"],
            notificationLayout.Cards.Single((card) => card.Id == "layout").VisibleGroups
                .SelectMany((group) => group.VisibleFields)
                .OrderBy((field) => field.Order)
                .Select((field) => field.Id)
                .ToList());
        SequenceEqual(
            ["component.notification.avatar.editor", "component.notification.avatarPlacement", "component.notification.avatar.inputs"],
            notificationLayout.Cards.Single((card) => card.Id == "avatar").VisibleGroups
                .SelectMany((group) => group.VisibleFields)
                .OrderBy((field) => field.Order)
                .Select((field) => field.Id)
                .ToList());
        SequenceEqual(
            ["component.notification.summaryLabel.editor", "component.notification.labelPlacement"],
            notificationLayout.Cards.Single((card) => card.Id == "summaryLabel").VisibleGroups
                .SelectMany((group) => group.VisibleFields)
                .OrderBy((field) => field.Order)
                .Select((field) => field.Id)
                .ToList());
        var notificationConfig = JsonNode.Parse(database.GetComponentClassSettings(notification.Id).ConfigJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing Notification config.");
        True(notificationConfig["notification"]?["surfaceSlot"] is JsonObject);
        True(notificationConfig["notification"]?["avatarPosition"] is null);
        True(notificationConfig["notification"]?["avatarInputs"]?["showBadge"] is JsonValue);
        True(notificationConfig["notification"]?["summaryLabelSlot"] is JsonObject);
        True(notificationConfig["notification"]?["detailLabelSlot"] is JsonObject);
        True(notificationConfig["notification"]?["labelSlot"] is null);
        Equal("icon", notificationConfig["notification"]?["avatarInputs"]?["badgeContentMode"]?.GetValue<string>() ?? "");
        Equal(20, notificationConfig["notification"]?["avatarInputs"]?["badgeSize"]?.GetValue<int>() ?? 0);
        var notificationsConfig = JsonNode.Parse(database.GetComponentClassSettings(notifications.Id).ConfigJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing Notifications config.");
        True(notificationsConfig["notifications"]?["badgeSlot"] is JsonObject);
        True(notificationsConfig["notifications"]?["notificationSlot"] is JsonObject);
        True(notificationsConfig["notifications"]?["notificationInputs"] is JsonObject);
        Equal("center", notificationsConfig["notifications"]?["itemAlignment"]?.GetValue<string>() ?? "");
        Equal("fixed", notificationsConfig["notifications"]?["itemGapBeforeMode"]?.GetValue<string>() ?? "");
        True(notificationsConfig["notifications"]?["itemPresenceMotion"] is JsonObject);
        Equal(3, notificationsConfig["notifications"]?["closedItemLimit"]?.GetValue<int>() ?? 0);
        var notificationsPreview = JsonNode.Parse(database.GetComponentClassSettings(notifications.Id).DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing Notifications design preview.");
        var notificationsCollectionFields = notificationsPreview["collections"]?[0]?["fields"]?.AsArray()
            .OfType<JsonObject>()
            .Select((field) => field["id"]?.GetValue<string>() ?? "")
            .ToHashSet(StringComparer.Ordinal)
            ?? throw new InvalidOperationException("Missing Notifications collection fields.");
        True(notificationsCollectionFields.Contains("present"));
        True(!notificationsCollectionFields.Overlaps(["presetId", "presenceMotion", "alignment", "gapBeforeMode", "gapBeforeToken", "gapBeforeWeight"]));
        var notificationsLayout = database.LoadEditorLayout("component.notifications");
        SequenceEqual(["general", "layout"], notificationsLayout.Cards.OrderBy((card) => card.Order).Select((card) => card.Id).ToList());
        SequenceEqual(
            ["stack", "notification", "badge", "motion"],
            notificationsLayout.Cards.Single((card) => card.Id == "layout").VisibleGroups.OrderBy((group) => group.Order).Select((group) => group.Id).ToList());

        foreach (var variant in new[] { notificationVariant, notificationsVariant })
        {
            var payload = Required(DesignPreviewPayloadFactory.Create(database, variant, theme.Id));
            var inputSession = new ComponentPreviewInputSession(database, () => { });
            inputSession.UpdateForPayload(payload, database.GetComponentClassSettings(variant.Parent!.Id).ProjectId);
            var html = WebDesignPreviewRenderer.RenderBodyAsync(
                database.GetDevicePreviewMetrics(device.Id), "light", false,
                inputSession.ApplyInputs(payload, "light", database.GetComponentClassSettings(variant.Parent.Id).ProjectId)).GetAwaiter().GetResult();
            True(!html.Contains("preview-error", StringComparison.Ordinal));
        }

        var transitionPayload = Required(DesignPreviewPayloadFactory.Create(database, notificationVariant, theme.Id));
        var transitionPreview = JsonNode.Parse(transitionPayload.DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing Notification transition preview.");
        var transitionAction = ComponentPreviewActions.ReadWithEmbedded(database, transitionPreview)
            .Single((action) => action.Id == "changeDisplayMode");
        var transitionSession = new ComponentPreviewInputSession(database, () => { });
        var transitionBusy = false;
        transitionSession.PlaybackBusyChanged += (value) => transitionBusy = value;
        transitionSession.UpdateForPayload(transitionPayload, database.GetComponentClassSettings(notification.Id).ProjectId);
        var durationMethod = typeof(ComponentPreviewInputSession).GetMethod(
            "DurationFrames",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing shared preview action duration resolver.");
        var transitionFrames = (int)(durationMethod.Invoke(transitionSession, [transitionAction]) ?? -1);
        var reflowDurationMs = JsonNode.Parse(transitionPayload.ThemeTokensJson)?["motion"]?["reflowDurationMs"]?.GetValue<double>()
            ?? throw new InvalidOperationException("Missing Theme reflow duration.");
        Equal(
            Math.Max(1, (int)Math.Ceiling(reflowDurationMs / 1000 * transitionSession.PlaybackFrameRate)),
            transitionFrames);
        transitionSession.PresentEveryPlaybackFrame = true;
        True(transitionSession.TriggerAction(transitionAction.Id, "detail"));
        True(transitionBusy);
        transitionSession.NotifyPlaybackFramePresented();
        var advanceMethod = typeof(ComponentPreviewInputSession).GetMethod(
            "AdvancePlaybackFrame",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing shared preview frame advance.");
        for (var frame = 1; frame <= transitionFrames; frame++)
        {
            advanceMethod.Invoke(transitionSession, null);
            transitionSession.NotifyPlaybackFramePresented();
        }
        True(!transitionSession.IsPlaybackActive);
        True(!transitionBusy);

        var wrappingSettings = database.GetComponentClassSettings(notification.Id);
        var wrappingPreview = JsonNode.Parse(wrappingSettings.DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing Notification wrapping preview.");
        wrappingPreview["maxWidth"] = 45;
        wrappingPreview["summaryText"] = "A deliberately long notification title that must wrap";
        database.UpdateComponentClassDesignPreviewJson(notification.Id, wrappingPreview.ToJsonString());
        var wrappingPayload = Required(DesignPreviewPayloadFactory.Create(database, notificationVariant, theme.Id));
        var wrappingSession = new ComponentPreviewInputSession(database, () => { });
        wrappingSession.UpdateForPayload(wrappingPayload, wrappingSettings.ProjectId);
        var wrappingHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id), "light", false,
            wrappingSession.ApplyInputs(wrappingPayload, "light", wrappingSettings.ProjectId)).GetAwaiter().GetResult();
        True(wrappingHtml.Contains("component.notification.label.text.1", StringComparison.Ordinal));
        database.UpdateComponentClassDesignPreviewJson(notification.Id, wrappingSettings.DesignPreviewJson);

        var settings = database.GetComponentClassSettings(notifications.Id);
        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Notifications preview.");
        var reference = notificationVariant.Id;
        var notificationInputs = database.GetComponentPresetRuntimeInputs(reference);
        Equal(90, notificationInputs["maxWidth"]?.GetValue<int>() ?? 0);
        True(notificationInputs["availableWidth"] is null);
        True(notificationInputs["displayMode"] is JsonValue);
        preview["items"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "notification_1",
                ["actorId"] = notificationInputs["actorId"]?.DeepClone(),
                ["displayMode"] = notificationInputs["displayMode"]?.DeepClone(),
                ["summaryText"] = notificationInputs["summaryText"]?.DeepClone(),
                ["summarySubtext"] = notificationInputs["summarySubtext"]?.DeepClone(),
                ["detailText"] = notificationInputs["detailText"]?.DeepClone(),
                ["detailSubtext"] = notificationInputs["detailSubtext"]?.DeepClone(),
                ["present"] = true,
            },
            new JsonObject
            {
                ["id"] = "notification_2",
                ["actorId"] = notificationInputs["actorId"]?.DeepClone(),
                ["displayMode"] = notificationInputs["displayMode"]?.DeepClone(),
                ["summaryText"] = notificationInputs["summaryText"]?.DeepClone(),
                ["summarySubtext"] = notificationInputs["summarySubtext"]?.DeepClone(),
                ["detailText"] = notificationInputs["detailText"]?.DeepClone(),
                ["detailSubtext"] = notificationInputs["detailSubtext"]?.DeepClone(),
                ["present"] = true,
            },
        };
        preview["distributionMode"] = "stacked";
        database.UpdateComponentClassDesignPreviewJson(notifications.Id, preview.ToJsonString());
        var populated = Required(DesignPreviewPayloadFactory.Create(database, notificationsVariant, theme.Id));
        var populatedSession = new ComponentPreviewInputSession(database, () => { });
        populatedSession.UpdateForPayload(populated, settings.ProjectId);
        var populatedContract = JsonNode.Parse(populated.DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing populated Notifications contract.");
        var embeddedActions = ComponentPreviewActions.ReadWithEmbedded(database, populatedContract)
            .Where((action) => action.TargetInputId == "displayMode")
            .ToList();
        Equal(2, embeddedActions.Count);
        True(embeddedActions.All((action) => string.IsNullOrWhiteSpace(action.TargetJsonPath)));
        var firstDisplayAction = embeddedActions.Single((action) => action.CollectionItemId == "notification_1");
        True(populatedSession.TriggerAction(firstDisplayAction.Id, "detail"));
        var targetedPayload = populatedSession.ApplyInputs(populated, "light", settings.ProjectId);
        var targetedItems = JsonNode.Parse(targetedPayload.DesignPreviewJson)?["items"]?.AsArray()
            ?? throw new InvalidOperationException("Missing targeted Notification items.");
        Equal("detail", targetedItems[0]?["displayMode"]?.GetValue<string>() ?? "");
        Equal("summary", targetedItems[1]?["displayMode"]?.GetValue<string>() ?? "");
        True(populatedSession.RestoreAction(firstDisplayAction.Id));
        var populatedHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id), "light", false,
            populatedSession.ApplyInputs(populated, "light", settings.ProjectId)).GetAwaiter().GetResult();
        if (populatedHtml.Contains("preview-error", StringComparison.Ordinal))
            throw new InvalidOperationException("Stacked Notifications preview failed.");
        if (!populatedHtml.Contains("component.notifications.badge", StringComparison.Ordinal))
            throw new InvalidOperationException("Stacked Notifications preview omitted its Badge.");
        populatedSession.SetExternalInputValue("distributionMode", "flow");
        var flowHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id), "light", false,
            populatedSession.ApplyInputs(populated, "light", settings.ProjectId)).GetAwaiter().GetResult();
        if (flowHtml.Contains("preview-error", StringComparison.Ordinal))
            throw new InvalidOperationException("Flow Notifications preview failed.");
        if (flowHtml.Contains("component.notifications.badge", StringComparison.Ordinal))
            throw new InvalidOperationException("Flow Notifications preview retained its Badge.");
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void KeypadSeedOpensAndRenders()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-keypad-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var keypad = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "keypad");
        Equal("System", keypad.Parent?.Name ?? "");
        var defaultVariant = keypad.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset && node.IsProtected);
        var settings = database.GetComponentClassSettings(keypad.Id);
        var layout = database.LoadEditorLayout("component.keypad");
        SequenceEqual(["general", "layout", "keys", "states"],
            layout.Cards.OrderBy((card) => card.Order).Select((card) => card.Id).ToList());
        Equal("stacked", layout.Cards.Single((card) => card.Id == "layout").GroupLayout);
        var statesCard = layout.Cards.Single((card) => card.Id == "states");
        Equal("verticalCards", statesCard.GroupLayout);
        SequenceEqual(["normalState", "activeState", "pushedState", "disabledState"],
            statesCard.VisibleGroups.Select((group) => group.Id).ToList());
        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Keypad config.");
        var keypadConfig = config["keypad"] as JsonObject
            ?? throw new InvalidOperationException("Missing Keypad contract.");
        Equal(3, keypadConfig["columns"]?.GetValue<int>() ?? -1);
        Equal(12, (keypadConfig["keys"] as JsonArray)?.Count ?? -1);
        var keysField = database.CreateComponentPresetFieldValue(defaultVariant, "component.keypad.keys");
        True(keysField.Definition.StructuredCollection is not null);
        Equal(6, keysField.Definition.StructuredCollection?.Fields.Count ?? -1);
        var iconField = keysField.Definition.StructuredCollection!.Fields.Single((field) => field.Id == "iconToken");
        Equal(ValueKind.IconToken, iconField.ValueKind);
        True(CollectionFieldAvailability.IsEnabled(
            new JsonObject { ["kind"] = "icon" }, iconField, 0));
        True(!CollectionFieldAvailability.IsEnabled(
            new JsonObject { ["kind"] = "text" }, iconField, 0));
        Equal("text", keypadConfig["keys"]?[0]?["kind"]?.GetValue<string>() ?? "");
        True(keypadConfig["labelSlot"] is JsonObject);
        Equal("theme.keyboard.keyBackground", keypadConfig["states"]?["normal"]?["backgroundColorToken"]?.GetValue<string>() ?? "");
        Equal("theme.colors.accent", keypadConfig["states"]?["pushed"]?["textColorToken"]?.GetValue<string>() ?? "");
        Equal(1d, keypadConfig["states"]?["disabled"]?["borderAlpha"]?.GetValue<double>() ?? -1);
        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Keypad preview.");
        SequenceEqual(
            ["availableWidth", "activeKey", "pushedKey", "enabled"],
            ComponentPreviewInputSession.ReadRuntimeInputs(preview, config).Select((input) => input.Id).ToList());
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(DesignPreviewPayloadFactory.Create(database, defaultVariant, theme.Id));
        var inputSession = new ComponentPreviewInputSession(database, () => { });
        inputSession.UpdateForPayload(payload, settings.ProjectId);
        inputSession.SetExternalInputValue("activeKey", "5");
        inputSession.SetExternalInputValue("pushedKey", "5");
        var resolvedPayload = inputSession.ApplyInputs(payload, "light", settings.ProjectId);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            resolvedPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(html));
        True(!html.Contains("preview-error", StringComparison.Ordinal));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void PasswordSeedOpensAndRenders()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-password-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var indicator = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "codeIndicator");
        Equal("Atoms", indicator.Parent?.Name ?? "");
        var password = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "password");
        Equal("System", password.Parent?.Name ?? "");
        var defaultVariant = password.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset && node.IsProtected);
        var settings = database.GetComponentClassSettings(password.Id);
        var layout = database.LoadEditorLayout("component.password");
        SequenceEqual(["general", "layout", "labels", "indicator", "modes", "iconBar"],
            layout.Cards.OrderBy((card) => card.Order).Select((card) => card.Id).ToList());
        Equal("verticalCards", layout.Cards.Single((card) => card.Id == "labels").GroupLayout);
        Equal("verticalCards", layout.Cards.Single((card) => card.Id == "modes").GroupLayout);

        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Password config.");
        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Password preview.");
        var runtimeInputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);
        SequenceEqual(
            ["expectedPassword", "attemptPassword", "enabled", "entryTiming"],
            runtimeInputs.Select((input) => input.Id).ToList());
        var timing = runtimeInputs.Single((input) => input.Id == "entryTiming");
        Equal(ValueKind.BehaviorTiming, timing.ValueKind);
        Equal(4d, preview["inputs"]?.AsArray()
            .OfType<JsonObject>()
            .Single((input) => input["id"]?.GetValue<string>() == "entryTiming")
            ["naturalTiming"]?["baseFramesPerUnit"]?.GetValue<double>() ?? -1);
        var action = ComponentPreviewActions.Read(preview).Single();
        Equal("entryTiming", action.DurationBehaviorTimingInputId);
        Equal(ComponentPreviewActionTimeUnit.Frames, action.TimeUnit);
        Equal(ComponentPreviewActionCompletionBehavior.HoldFinal, action.CompletionBehavior);
        var passwordConfig = config["password"]?.AsObject()
            ?? throw new InvalidOperationException("Missing Password config block.");
        True(new[] { "container", "input" }.Contains(passwordConfig["upperAnchor"]?.GetValue<string>() ?? ""));
        True(new[] { "container", "input" }.Contains(passwordConfig["lowerAnchor"]?.GetValue<string>() ?? ""));
        True(passwordConfig["labelGapToken"] is null);
        True(passwordConfig["indicatorGapToken"] is null);
        True(passwordConfig["keypadGapToken"] is null);

        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(DesignPreviewPayloadFactory.Create(database, defaultVariant, theme.Id));
        var inputSession = new ComponentPreviewInputSession(database, () => { });
        var playbackBusy = false;
        inputSession.PlaybackBusyChanged += (value) => playbackBusy = value;
        inputSession.UpdateForPayload(payload, settings.ProjectId);
        var durationMethod = typeof(ComponentPreviewInputSession).GetMethod(
            "DurationFrames",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing shared preview action duration resolver.");
        Equal(16, (int)(durationMethod.Invoke(inputSession, [action]) ?? -1));
        var advanceMethod = typeof(ComponentPreviewInputSession).GetMethod(
            "AdvancePlaybackFrame",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing shared preview frame advance.");
        inputSession.PresentEveryPlaybackFrame = true;
        True(inputSession.TriggerAction(action.Id));
        True(playbackBusy);
        inputSession.NotifyPlaybackFramePresented();
        for (var frame = 1; frame <= 16; frame++)
        {
            advanceMethod.Invoke(inputSession, null);
            inputSession.NotifyPlaybackFramePresented();
        }
        True(!inputSession.IsPlaybackActive);
        True(!playbackBusy);
        Equal(16, inputSession.CurrentPreviewFrame);
        var resolvedPayload = inputSession.ApplyInputs(payload, "light", settings.ProjectId);
        var finalPreview = JsonNode.Parse(resolvedPayload.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing resolved Password preview.");
        Equal(true, finalPreview["entryTrigger"]?.GetValue<bool>() ?? false);
        Equal(16, finalPreview["entryFrame"]?.GetValue<int>() ?? -1);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            resolvedPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(html));
        True(!html.Contains("preview-error", StringComparison.Ordinal));
        True(inputSession.CanRestoreAction(action.Id));
        True(inputSession.RestoreAction(action.Id));
        True(!inputSession.CanRestoreAction(action.Id));
        var restoredPayload = inputSession.ApplyInputs(payload, "light", settings.ProjectId);
        var restoredPreview = JsonNode.Parse(restoredPayload.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing restored Password preview.");
        Equal(false, restoredPreview["entryTrigger"]?.GetValue<bool>() ?? true);
        Equal(0, restoredPreview["entryFrame"]?.GetValue<int>() ?? -1);
        True(inputSession.TriggerAction(action.Id));
        Equal(0, inputSession.CurrentPreviewFrame);
        True(inputSession.IsPlaybackActive);
        True(inputSession.ResetCurrentTestValues());

        foreach (var componentType in new[] { "fingerprint", "faceRecognition", "drawPassword" })
        {
            var component = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
                && database.GetComponentClassSettings(node.Id).ComponentType == componentType);
            Equal("System", component.Parent?.Name ?? "");
            var componentVariant = component.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset && node.IsProtected);
            var componentPayload = Required(DesignPreviewPayloadFactory.Create(database, componentVariant, theme.Id));
            var componentHtml = WebDesignPreviewRenderer.RenderBodyAsync(
                database.GetDevicePreviewMetrics(device.Id),
                "light",
                false,
                componentPayload).GetAwaiter().GetResult();
            True(!componentHtml.Contains("preview-error", StringComparison.Ordinal));
        }

        foreach (var mode in new[] { "fingerprint", "faceRecognition", "drawPassword" })
        {
            var variant = password.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset && node.Id.EndsWith($"::preset::{mode}", StringComparison.Ordinal));
            var modePayload = Required(DesignPreviewPayloadFactory.Create(database, variant, theme.Id));
            var modePreview = JsonNode.Parse(modePayload.DesignPreviewJson)?.AsObject()
                ?? throw new InvalidOperationException($"Missing {mode} Password preview.");
            modePreview["entryTrigger"] = true;
            modePreview["entryFrame"] = 16;
            modePayload = modePayload with { DesignPreviewJson = modePreview.ToJsonString() };
            var modeHtml = WebDesignPreviewRenderer.RenderBodyAsync(
                database.GetDevicePreviewMetrics(device.Id),
                "light",
                false,
                modePayload).GetAwaiter().GetResult();
            True(!modeHtml.Contains("preview-error", StringComparison.Ordinal));
        }
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void LockScreenComposesRuntimeStack()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-lock-screen-stack-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var module = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.Module
            && database.GetModuleSettings(node.Id).RecordClassId == "module.core.lockScreen");
        var systemApp = module.Parent ?? throw new InvalidOperationException("Lock Screen has no System app parent.");
        Equal("app.system", systemApp.RecordClassId);
        var systemConfig = JsonNode.Parse(database.GetAppSettings(systemApp.Id).ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing System app config.");
        True(systemConfig["wallpaper"] is null);
        True(database.LoadEditorLayout("app.system").Cards
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .All((field) => !field.Id.StartsWith("app.wallpaper.", StringComparison.Ordinal)));
        Throws<InvalidOperationException>(() => database.UpdateAppField(systemApp.Id, "app.wallpaper.opacity", "1"));
        var settings = database.GetModuleSettings(module.Id);
        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen config.");
        var lockScreen = config["lockScreen"] as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen contract.");
        var stackSlot = lockScreen["stackSlot"] as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen Stack slot.");
        True(lockScreen["statusBarSlot"] is JsonObject);
        True(lockScreen["navigationBarSlot"] is JsonObject);
        True((stackSlot["presetId"]?.GetValue<string>() ?? "").Contains("::preset::default", StringComparison.Ordinal));
        True(stackSlot["overrides"] is JsonObject);
        True(lockScreen["stackVariant"] is null);
        True(lockScreen["statusBarVariant"] is null);
        True(lockScreen["navigationBarVariant"] is null);

        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen Runtime Inputs.");
        var inputs = ComponentPreviewInputSession.ReadRuntimeInputs(preview, config);
        SequenceEqual(
            ["actor", "showStatusBar", "showNavigationBar"],
            inputs.Take(3).Select((input) => input.Id).ToList());
        Equal("true", DesignPreviewTestValues.Value(preview, inputs.Single((input) => input.Id == "showStatusBar")));
        Equal("true", DesignPreviewTestValues.Value(preview, inputs.Single((input) => input.Id == "showNavigationBar")));
        Equal(0, ComponentPreviewInputSession.ReadRuntimeCollections(preview, config).Count);
        var stackInputs = lockScreen["stackInputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen Stack bindings.");
        Equal("fill", stackInputs["sizingMode"]?.GetValue<string>() ?? "");
        True(stackInputs["items"] is JsonArray);
        var lockScreenFields = database.LoadEditorLayout("module.core.lockScreen").Cards
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .Select((field) => field.Id)
            .ToHashSet(StringComparer.Ordinal);
        True(lockScreenFields.Contains("module.lockScreen.stackInputs"));
        True(lockScreenFields.Contains("module.lockScreen.stackItems"));

        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(DesignPreviewPayloadFactory.Create(database, module, theme.Id));
        var session = new ComponentPreviewInputSession(database, () => { });
        session.UpdateForPayload(payload, settings.ProjectId);
        var resolved = session.ApplyInputs(payload, "light", settings.ProjectId);
        var resolvedPreview = JsonNode.Parse(resolved.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing resolved Lock Screen preview.");
        foreach (var forwardedInput in inputs.Skip(3))
        {
            True(resolvedPreview.ContainsKey(forwardedInput.JsonKey));
        }
        Equal(1d, resolvedPreview["actor"]?["wallpaper"]?["opacity"]?.GetValue<double>() ?? -1);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            resolved).GetAwaiter().GetResult();
        True(!html.Contains("preview-error", StringComparison.Ordinal));

        var childVariant = database.GetComponentPresetReferenceOptionsByType(settings.ProjectId, "label").First().Value;
        var childInputs = database.GetComponentPresetRuntimeInputs(childVariant);
        var subtitleBinding = database.GetComponentPresetRuntimeInputBindings(childVariant)
            .Single((input) => input.Id == "sampleSubtext");
        childInputs[RuntimeInputForwardingContract.StorageKey] = new JsonObject
        {
            [subtitleBinding.JsonKey] = RuntimeInputForwardingContract.Definition(
                new FieldDefinition(
                    "module.lockScreen.stackItems.lock_screen_label.inputs",
                    "Component inputs",
                    ValueKind.ComponentInputBindings),
                subtitleBinding,
                "Lock subtitle",
                "Subtitle"),
        };
        database.UpdateModuleField(module.Id, "module.lockScreen.stackItems", new JsonArray
        {
            new JsonObject
            {
                ["id"] = "lock_screen_label",
                ["alternatives"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "lock_screen_label_default",
                        ["presetId"] = childVariant,
                        ["overrides"] = new JsonObject(),
                        ["inputs"] = childInputs,
                        ["active"] = false,
                        ["behavior"] = "replace",
                        ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":0.5,"alignY":0.5,"offsetX":0,"offsetY":0}"""),
                        ["enterMotion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString()),
                        ["exitMotion"] = JsonNode.Parse(MotionVariantValue.Default.ToJsonString()),
                    },
                },
                ["gapBeforeMode"] = "fixed",
                ["gapBeforeToken"] = "theme.spacing.none",
                ["gapBeforeWeight"] = 1,
            },
        }.ToJsonString());
        var populatedPayload = Required(DesignPreviewPayloadFactory.Create(database, module, theme.Id));
        var populatedSession = new ComponentPreviewInputSession(database, () => { });
        populatedSession.UpdateForPayload(populatedPayload, settings.ProjectId);
        var populatedInputs = ComponentPreviewInputSession.ReadRuntimeInputs(
            JsonNode.Parse(populatedPayload.DesignPreviewJson) as JsonObject ?? new JsonObject(),
            JsonNode.Parse(populatedPayload.ConfigJson) as JsonObject ?? new JsonObject());
        var forwardedSubtitle = populatedInputs.Single((input) => input.Label == "Lock subtitle");
        populatedSession.SetExternalInputValue(forwardedSubtitle.JsonKey, "Forwarded subtitle");
        populatedSession.SetExternalInputValue("showStatusBar", "false");
        populatedSession.SetExternalInputValue("showNavigationBar", "false");
        var populated = populatedSession.ApplyInputs(populatedPayload, "light", settings.ProjectId);
        var populatedHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            populated).GetAwaiter().GetResult();
        True(!populatedHtml.Contains("preview-error", StringComparison.Ordinal));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static IEnumerable<ProjectTreeNode> DescendantsAndSelf(ProjectTreeNode node)
{
    yield return node;
    foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
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
