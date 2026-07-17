using Microsoft.Data.Sqlite;
using Avalonia;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var tests = new (string Name, Action Run)[]
{
    ("v2 document rejects malformed roots", RejectsMalformedDocuments),
    ("opening an existing desktop database is byte-for-byte read-only", ExistingDatabaseOpenIsReadOnly),
    ("rejected databases remain byte-for-byte unchanged", RejectedDatabaseOpenIsReadOnly),
    ("persisted JSON roots reject blank malformed and wrong shapes", PersistedJsonRootsAreStrict),
    ("incomplete Component and Module Variants fail read-only", IncompleteVariantsFailReadOnly),
    ("Variant writes never repair missing Variant arrays", VariantWritesDoNotRepairMissingArrays),
    ("editor layout saves omit derived projection properties", EditorLayoutSaveOmitsDerivedProperties),
    ("extracted repositories preserve the SpikeDatabase facade contract", ExtractedRepositoriesPreserveFacadeContract),
    ("resource repositories preserve Palette Device and Actor contracts", ResourceRepositoriesPreserveFacadeContract),
    ("Actor preview data boundary preserves current values read-only", ActorPreviewDataBoundaryPreservesCurrentValues),
    ("Runtime Input option boundary preserves dictionary options read-only", RuntimeInputOptionBoundaryPreservesDictionaryOptions),
    ("dictionary field context boundary preserves current data read-only", DictionaryFieldContextBoundaryPreservesCurrentData),
    ("embedded Component document store preserves Variant and local Override ownership", EmbeddedComponentDocumentStorePreservesOwnership),
    ("editor presentation context boundary preserves current data read-only", EditorPresentationContextBoundaryPreservesCurrentData),
    ("Component Preview input boundary preserves current contracts read-only", ComponentPreviewInputBoundaryPreservesCurrentContracts),
    ("Runtime Input owner store preserves current documents and explicit Preview writes", RuntimeInputOwnerStorePreservesCurrentDocuments),
    ("Runtime Input instance store preserves explicit scalar collection and animation writes", RuntimeInputInstanceStorePreservesExplicitWrites),
    ("Preview visual context boundary preserves options metrics and media root read-only", PreviewVisualContextBoundaryPreservesResolvedResources),
    ("Production Preview session boundary preserves Shot and Screen data read-only", ProductionPreviewSessionBoundaryPreservesCurrentData),
    ("Module Instance animation store preserves current documents and explicit writes", ModuleInstanceAnimationStorePreservesCurrentDocuments),
    ("Theme repository preserves current documents and lifecycle", ThemeRepositoryPreservesFacadeContract),
    ("Production Font repository preserves current rows and lifecycle", ProductionFontRepositoryPreservesFacadeContract),
    ("Icon Theme repository preserves rows and strict token files", IconThemeRepositoryPreservesFacadeContract),
    ("App and Module repository preserves definitions and Rename-only lifecycle", AppModuleRepositoryPreservesFacadeContract),
    ("Component Class repository preserves current definitions and Variants", ComponentClassRepositoryPreservesFacadeContract),
    ("Module Instance repository preserves Screen rows and prepared documents", ModuleInstanceRepositoryPreservesFacadeContract),
    ("Shot repository preserves Production rows and complete duplication", ShotRepositoryPreservesFacadeContract),
    ("Shots require an explicit replaceable owner Actor", ShotActorContextIsExplicit),
    ("Production Shot context boundary preserves explicit inherited context read-only", ProductionShotContextBoundaryPreservesInheritedContext),
    ("explicit Usage references are exact typed and shared", ExplicitReferenceUsageIsExactTypedAndShared),
    ("Usage navigation preserves workspace node and embedded context", UsageNavigationPreservesTypedContext),
    ("Production Data owns actors devices fonts and render presets", ProductionDataOwnsConcreteResources),
    ("editor view state follows the exact record class across records", EditorViewStateFollowsRecordClass),
    ("editor view state round-trips per class and clamps scroll", EditorViewStateRoundTripsPerClass),
    ("track activation creates frame-zero state", TrackActivationCreatesInitialKeyframe),
    ("runtime controls resolve their value at the active owner frame", RuntimeControlsResolveActiveFrameValue),
    ("track targets persist and round-trip", TrackTargetsRoundTrip),
    ("nested collection duplication and deletion preserve animation targets", NestedCollectionTargetsFollowIdentity),
    ("keyframe upsert updates and orders", KeyframeUpsertUpdatesAndOrders),
    ("keyframe moves preserve payload and protect frame zero", KeyframeMovesPreservePayloadAndProtectFrameZero),
    ("keyframe drag snaps to the Screen authoring grid", KeyframeDragSnapsToScreenGrid),
    ("keyframes and tracks can be removed", KeyframesAndTracksCanBeRemoved),
    ("Screen-owned fields start at Screen zero", ScreenFieldsStartAtZero),
    ("Screen duration policy distinguishes calculated and explicit ownership", ScreenDurationPolicyIsContractOwned),
    ("target-owned fields use target-relative origins", TargetFieldsUseRelativeOrigins),
    ("parallel collection targets share the Screen origin", ParallelCollectionTargetsShareScreenOrigin),
    ("entity fields keep their first-appearance origin across re-entry", EntityFieldsKeepFirstAppearanceOrigin),
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
    ("strict validation rejects tracks without an origin keyframe", StrictValidationRejectsMissingOrigin),
    ("legacy animation requires explicit migration", LegacyAnimationRequiresExplicitMigration),
    ("initial animatable field vocabulary is constrained", AnimatableFieldVocabularyIsConstrained),
    ("playback state publishes play, busy and frame changes", PlaybackStatePublishesChanges),
    ("timeline frame updates suppress their own playback feedback", TimelineFrameUpdatesSuppressOwnPlaybackFeedback),
    ("collection item reorder persists stable ids", CollectionItemReorderPersistsStableIds),
    ("new collection items become the only expanded item", NewCollectionItemBecomesOnlyExpanded),
    ("active component variants expose parent class actions", ActiveVariantExposesParentClassActions),
    ("App and Module definitions expose rename-only lifecycle actions", AppAndModuleDefinitionsExposeRenameOnlyLifecycleActions),
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
    ("Label subtext placement uses the current explicit alignment contract", LabelSubtextPlacementUsesCurrentContract),
    ("Password composes stateful atoms and BehaviorTiming", PasswordSeedOpensAndRenders),
    ("Lock Screen composes its runtime Stack and optional system bars", LockScreenComposesRuntimeStack),
    ("forwarded child inputs become effective parent runtime inputs", ForwardedChildInputsBecomeParentRuntimeInputs),
    ("forwarded runtime collections expose slot state actions", ForwardedRuntimeCollectionsExposeSlotStateActions),
    ("module variants are explicit and selected by Screen instances", ModuleVariantsAreExplicit),
};

static void EditorViewStateFollowsRecordClass()
{
    var firstTheme = new ProjectTreeNode(ProjectTreeNodeKind.Theme, "theme-a", "Theme A", "", "theme");
    var secondTheme = new ProjectTreeNode(ProjectTreeNodeKind.Theme, "theme-b", "Theme B", "", "theme");
    var actor = new ProjectTreeNode(ProjectTreeNodeKind.Actor, "actor-a", "Actor A", "", "actor");
    Equal("theme", EditorViewStateController.StateKey(firstTheme));
    Equal(EditorViewStateController.StateKey(firstTheme), EditorViewStateController.StateKey(secondTheme));
    True(EditorViewStateController.StateKey(firstTheme) != EditorViewStateController.StateKey(actor));

    var componentClass = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentClass,
        "component-a",
        "Component A",
        "",
        "component.label");
    var componentVariant = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentPreset,
        "component-a::preset::default",
        "Default",
        "",
        "component.preset",
        componentClass);
    Equal("component.label", EditorViewStateController.StateKey(componentVariant));
}

static void EditorViewStateRoundTripsPerClass()
{
    var store = new EditorSessionViewStateStore();
    True(store.Get("theme") is null);

    var themeState = new EditorViewState(
        ["layout:general"],
        new Vector(12, 240));
    var actorState = new EditorViewState(
        ["layout:wallpaper"],
        new Vector(0, 72));
    store.Set("theme", themeState);
    store.Set("actor", actorState);

    var restoredTheme = Required(store.Get("theme"));
    SequenceEqual(["layout:general"], restoredTheme.ExpandedCardIds);
    Equal(new Vector(12, 240), restoredTheme.ScrollOffset);
    var restoredActor = Required(store.Get("actor"));
    SequenceEqual(["layout:wallpaper"], restoredActor.ExpandedCardIds);
    Equal(new Vector(0, 72), restoredActor.ScrollOffset);

    Equal(
        new Vector(200, 300),
        EditorViewStateController.ClampOffset(
            new Vector(900, 900),
            new Size(500, 700),
            new Size(300, 400)));
    Equal(
        new Vector(0, 0),
        EditorViewStateController.ClampOffset(
            new Vector(-20, -10),
            new Size(100, 100),
            new Size(300, 400)));
}

static void ExistingDatabaseOpenIsReadOnly()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-read-only-open-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        _ = new SpikeDatabase(temporary);
        _ = new SpikeDatabase(temporary);
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RejectedDatabaseOpenIsReadOnly()
{
    AssertRejectedDatabaseIsReadOnly("schema-version", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version = 0";
        command.ExecuteNonQuery();
    });
}

static void PersistedJsonRootsAreStrict()
{
    Equal(0, JsonPath.ParseRequiredObject("{}", "test object").Count);
    Equal(0, JsonPath.ParseRequiredArray("[]", "test array").Count);
    Throws<InvalidOperationException>(() => JsonPath.ParseRequiredObject("", "test object"));
    Throws<InvalidOperationException>(() => JsonPath.ParseRequiredObject("{", "test object"));
    Throws<InvalidOperationException>(() => JsonPath.ParseRequiredObject("[]", "test object"));
    Throws<InvalidOperationException>(() => JsonPath.ParseRequiredObject("null", "test object"));
    Throws<InvalidOperationException>(() => JsonPath.ParseRequiredArray("", "test array"));
    Throws<InvalidOperationException>(() => JsonPath.ParseRequiredArray("{}", "test array"));
    Throws<InvalidOperationException>(() => JsonPath.ParseRequiredArray("null", "test array"));
    AssertRejectedDatabaseIsReadOnly("blank-json-root", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE apps SET config_json = '' WHERE id = 'app_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("malformed-json-root", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE themes SET tokens_json = '{' WHERE id = 'theme_project_foqn_s2_ios_default'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("wrong-json-root", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE production_fonts SET files_json = '{}' WHERE id = (SELECT id FROM production_fonts LIMIT 1)";
        command.ExecuteNonQuery();
    });
}

static void IncompleteVariantsFailReadOnly()
{
    AssertRejectedDatabaseIsReadOnly("component-variant-locked", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET metadata_json = json_remove(metadata_json, '$.presets[0].locked')
            WHERE id = 'component_project_foqn_s2_label'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-config", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET metadata_json = json_remove(metadata_json, '$.presets[0].config')
            WHERE id = 'component_project_foqn_s2_label'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-name", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_remove(metadata_json, '$.presets[0].name') WHERE id = 'component_project_foqn_s2_label'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-protected", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_remove(metadata_json, '$.presets[0].protected') WHERE id = 'component_project_foqn_s2_label'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-duplicate-id", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_insert(metadata_json, '$.presets[#]', json_extract(metadata_json, '$.presets[0]')) WHERE id = 'component_project_foqn_s2_label'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-default-unprotected", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_set(metadata_json, '$.presets[0].protected', json('false')) WHERE id = 'component_project_foqn_s2_label'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("module-variant-locked", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE modules
            SET metadata_json = json_remove(metadata_json, '$.variants[0].locked')
            WHERE id = 'module_core_chat'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("module-variant-entry", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE modules
            SET metadata_json = json_insert(metadata_json, '$.variants[#]', json($entry))
            WHERE id = 'module_core_chat'
            """;
        command.Parameters.AddWithValue("$entry", "\"malformed\"");
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("module-variant-config", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET metadata_json = json_remove(metadata_json, '$.variants[0].config') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("module-variant-name", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET metadata_json = json_remove(metadata_json, '$.variants[0].name') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("module-variant-protected", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET metadata_json = json_remove(metadata_json, '$.variants[0].protected') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
}

static void VariantWritesDoNotRepairMissingArrays()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-variant-write-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var defaultVariant = Descendants(database.LoadProjectTree()).Single((node) =>
            node.Id == "component_project_foqn_s2_label::preset::default");
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE component_classes
                SET metadata_json = json_remove(metadata_json, '$.presets')
                WHERE id = 'component_project_foqn_s2_label'
                """;
            command.ExecuteNonQuery();
        }

        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.SaveComponentPreset(defaultVariant, "Must fail"));
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }

    temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-variant-write-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var defaultVariant = Descendants(database.LoadProjectTree()).Single((node) =>
            node.Id == "module_core_chat::variant::default");
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE modules SET metadata_json = json_remove(metadata_json, '$.variants') WHERE id = 'module_core_chat'";
            command.ExecuteNonQuery();
        }

        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.SaveModuleVariant(defaultVariant, "Must fail"));
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void AssertRejectedDatabaseIsReadOnly(string fixture, Action<SqliteConnection> mutate)
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-rejected-{fixture}-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            mutate(connection);
        }

        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => _ = new SpikeDatabase(temporary));
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void EditorLayoutSaveOmitsDerivedProperties()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-layout-serialization-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var layout = database.LoadEditorLayout("component.keypad");
        database.SaveEditorLayout("component.keypad", layout);
        using var connection = new SqliteConnection($"Data Source={temporary}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.keypad'";
        var json = command.ExecuteScalar() as string ?? throw new InvalidOperationException("Missing Keypad editor layout.");
        True(!json.Contains("\"VisibleGroups\"", StringComparison.Ordinal));
        True(!json.Contains("\"VisibleFields\"", StringComparison.Ordinal));
        True(!json.Contains("\"Entries\"", StringComparison.Ordinal));
        Equal(layout.Cards.Count, JsonNode.Parse(json)?["cards"]?.AsArray().Count ?? -1);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ExtractedRepositoriesPreserveFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-repository-contract-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IEditorLayoutRepository layoutRepository = new EditorLayoutRepository(context);
        IShotRepository shotRepository = new ShotRepository(context);
        IProjectEpisodeRepository projectEpisodeRepository = new ProjectEpisodeRepository(context, shotRepository);
        IRenderPresetRepository renderPresetRepository = new RenderPresetRepository(context);

        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var episode = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.Episode);
        var renderPreset = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.RenderPreset);

        Equal(database.GetProjectSettings(project.Id), projectEpisodeRepository.GetProjectSettings(project.Id));
        Equal(database.GetEpisodeSettings(episode.Id), projectEpisodeRepository.GetEpisodeSettings(episode.Id));
        Equal(database.GetRenderPresetSettings(renderPreset.Id), renderPresetRepository.GetSettings(renderPreset.Id));
        SequenceEqual(
            database.GetRenderPresetOptions(project.Id).Skip(1).Select((option) => option.Value),
            renderPresetRepository.GetOptions(project.Id).Select((option) => option.Value));

        var facadeLayout = database.LoadEditorLayout("component.keypad");
        var repositoryLayout = layoutRepository.Load("component.keypad");
        Equal(facadeLayout.Cards.Count, repositoryLayout.Cards.Count);
        layoutRepository.Save("component.keypad", repositoryLayout);
        Equal(repositoryLayout.Cards.Count, database.LoadEditorLayout("component.keypad").Cards.Count);

        using (var connection = context.OpenConnection())
        {
            True(projectEpisodeRepository.QueryProjects(connection).Any((row) => row.Id == project.Id));
            True(projectEpisodeRepository.QueryEpisodes(connection).Any((row) => row.Id == episode.Id));
            True(renderPresetRepository.QueryAll(connection).Any((row) => row.Id == renderPreset.Id));
        }

        var originalProject = database.GetProjectSettings(project.Id);
        projectEpisodeRepository.UpdateProjectField(project.Id, "project.slug", $"{originalProject.Slug}-repository");
        Equal($"{originalProject.Slug}-repository", database.GetProjectSettings(project.Id).Slug);
        database.UpdateProjectField(project.Id, "project.slug", originalProject.Slug);
        Equal(originalProject, projectEpisodeRepository.GetProjectSettings(project.Id));

        var originalEpisode = database.GetEpisodeSettings(episode.Id);
        projectEpisodeRepository.UpdateEpisodeField(episode.Id, "episode.slug", $"{originalEpisode.Slug}-repository");
        Equal($"{originalEpisode.Slug}-repository", database.GetEpisodeSettings(episode.Id).Slug);
        database.UpdateEpisodeField(episode.Id, "episode.slug", originalEpisode.Slug);
        Equal(originalEpisode, projectEpisodeRepository.GetEpisodeSettings(episode.Id));

        var originalPreset = database.GetRenderPresetSettings(renderPreset.Id);
        renderPresetRepository.UpdateField(renderPreset.Id, "renderPreset.width", "1234");
        Equal(1234, database.GetRenderPresetSettings(renderPreset.Id).Width);
        database.UpdateRenderPresetField(renderPreset.Id, "renderPreset.width", originalPreset.Width.ToString());
        Equal(originalPreset, renderPresetRepository.GetSettings(renderPreset.Id));

        var originalProjectName = project.Name;
        project.Name = $"{project.Name} repository";
        database.UpdateNode(project);
        using (var connection = context.OpenConnection())
        {
            Equal(project.Name, projectEpisodeRepository.QueryProjects(connection).Single((row) => row.Id == project.Id).Name);
        }
        project.Name = originalProjectName;
        database.UpdateNode(project);

        var originalEpisodeName = episode.Name;
        episode.Name = $"{episode.Name} repository";
        database.UpdateNode(episode);
        using (var connection = context.OpenConnection())
        {
            Equal(episode.Name, projectEpisodeRepository.QueryEpisodes(connection).Single((row) => row.Id == episode.Id).Name);
        }
        episode.Name = originalEpisodeName;
        database.UpdateNode(episode);

        var originalPresetName = renderPreset.Name;
        renderPreset.Name = $"{renderPreset.Name} repository";
        database.UpdateNode(renderPreset);
        using (var connection = context.OpenConnection())
        {
            Equal(renderPreset.Name, renderPresetRepository.QueryAll(connection).Single((row) => row.Id == renderPreset.Id).Name);
        }
        renderPreset.Name = originalPresetName;
        database.UpdateNode(renderPreset);

        var episodesRoot = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.EpisodesRoot);
        var createdEpisode = database.AddChild(episodesRoot);
        using (var connection = context.OpenConnection())
        {
            True(projectEpisodeRepository.QueryEpisodes(connection).Any((row) => row.Id == createdEpisode.Id));
        }
        var duplicatedEpisode = database.Duplicate(createdEpisode);
        database.Delete(duplicatedEpisode);
        database.Delete(createdEpisode);

        var renderPresetsRoot = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.RenderPresetsRoot);
        var createdPreset = database.AddChild(renderPresetsRoot);
        using (var connection = context.OpenConnection())
        {
            True(renderPresetRepository.QueryAll(connection).Any((row) => row.Id == createdPreset.Id));
        }
        var duplicatedPreset = database.Duplicate(createdPreset);
        database.Delete(duplicatedPreset);
        database.Delete(createdPreset);

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() =>
            renderPresetRepository.UpdateField(renderPreset.Id, "renderPreset.codec", "[]"));
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ResourceRepositoriesPreserveFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-resource-repositories-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IPaletteRepository paletteRepository = new PaletteRepository(context);
        IDeviceRepository deviceRepository = new DeviceRepository(context);
        IActorRepository actorRepository = new ActorRepository(context);

        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var color = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.PaletteColor);
        var device = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var actor = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.Actor);

        Equal(database.GetPaletteColorSettings(color.Id), paletteRepository.GetSettings(color.Id));
        Equal(database.GetDeviceSettings(device.Id), deviceRepository.GetSettings(device.Id));
        Equal(database.GetActorSettings(actor.Id), actorRepository.GetSettings(actor.Id));
        SequenceEqual(
            database.GetPaletteColorOptions(project.Id).Select((option) => option.Value),
            paletteRepository.GetOptions(project.Id).Select((option) => option.Token));
        SequenceEqual(
            database.GetDeviceOptions(project.Id).Select((option) => option.Value),
            deviceRepository.GetOptions(project.Id).Select((option) => option.Value));
        SequenceEqual(
            database.GetActorOptions(project.Id).Skip(1).Select((option) => option.Value),
            actorRepository.GetOptions(project.Id).Select((option) => option.Value));
        SequenceEqual(database.GetPaletteColorMap(project.Id), paletteRepository.GetColorMap(project.Id));
        SequenceEqual(database.GetPaletteNeutralMap(project.Id), paletteRepository.GetNeutralMap(project.Id));

        using (var connection = context.OpenConnection())
        {
            True(paletteRepository.QueryAll(connection).Any((row) => row.Id == color.Id));
            True(deviceRepository.QueryAll(connection).Any((row) => row.Id == device.Id));
            True(actorRepository.QueryAll(connection).Any((row) => row.Id == actor.Id));
        }

        var originalColor = database.GetPaletteColorSettings(color.Id);
        paletteRepository.UpdateField(color.Id, "palette.valueHex", "#123456");
        Equal("#123456", database.GetPaletteColorSettings(color.Id).ValueHex);
        database.UpdatePaletteColorField(color.Id, "palette.valueHex", originalColor.ValueHex);
        Equal(originalColor, paletteRepository.GetSettings(color.Id));

        var originalDevice = database.GetDeviceSettings(device.Id);
        deviceRepository.UpdateField(device.Id, "device.manufacturer", "Repository Manufacturer");
        Equal("Repository Manufacturer", database.GetDeviceSettings(device.Id).Manufacturer);
        database.UpdateDeviceField(device.Id, "device.manufacturer", originalDevice.Manufacturer);
        var originalScreenSize = database.GetDeviceMetricFieldValue(device.Id, "device.metrics.screen.size");
        deviceRepository.UpdateField(device.Id, "device.metrics.screen.size", "100|200");
        Equal("100|200", database.GetDeviceMetricFieldValue(device.Id, "device.metrics.screen.size"));
        database.UpdateDeviceField(device.Id, "device.metrics.screen.size", originalScreenSize);
        Equal(originalDevice, deviceRepository.GetSettings(device.Id));

        var originalActor = database.GetActorSettings(actor.Id);
        actorRepository.UpdateField(actor.Id, "actor.shortName", "Repository Actor");
        Equal("Repository Actor", database.GetActorSettings(actor.Id).ShortName);
        database.UpdateActorField(actor.Id, "actor.shortName", originalActor.ShortName);
        var originalWallpaperOpacity = database.GetActorFieldValue(actor.Id, "actor.wallpaper.opacity");
        actorRepository.UpdateField(actor.Id, "actor.wallpaper.opacity", "0.35");
        Equal("0.35", database.GetActorFieldValue(actor.Id, "actor.wallpaper.opacity"));
        database.UpdateActorField(actor.Id, "actor.wallpaper.opacity", originalWallpaperOpacity);
        Equal(originalActor, actorRepository.GetSettings(actor.Id));

        var paletteRoot = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.PaletteRoot);
        var createdColor = database.AddChild(paletteRoot);
        var duplicatedColor = database.Duplicate(createdColor);
        duplicatedColor.Name = "resource_test_token";
        duplicatedColor.Notes = "Repository lifecycle note";
        database.UpdateNode(duplicatedColor);
        using (var connection = context.OpenConnection())
        {
            var persisted = paletteRepository.QueryAll(connection).Single((row) => row.Id == duplicatedColor.Id);
            Equal(duplicatedColor.Name, persisted.Token);
            Equal(duplicatedColor.Notes, persisted.Note);
        }
        database.Delete(duplicatedColor);
        database.Delete(createdColor);

        var devicesRoot = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.DevicesRoot);
        var createdDevice = database.AddChild(devicesRoot);
        var duplicatedDevice = database.Duplicate(createdDevice);
        duplicatedDevice.Name = "Repository Device";
        database.UpdateNode(duplicatedDevice);
        Equal(duplicatedDevice.Name, deviceRepository.GetSettings(duplicatedDevice.Id).Name);
        var importedDevice = database.AddImportedDevice(
            devicesRoot,
            new DeviceImportDraft("Imported Repository Device", "Mockups", "Test", "ios", database.GetDeviceSettings(createdDevice.Id).MetricsJson));
        Equal("Imported Repository Device", deviceRepository.GetSettings(importedDevice.Id).Name);
        database.Delete(importedDevice);
        database.Delete(duplicatedDevice);
        database.Delete(createdDevice);

        var actorsRoot = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.ActorsRoot);
        var createdActor = database.AddChild(actorsRoot);
        var duplicatedActor = database.Duplicate(createdActor);
        duplicatedActor.Name = "Repository Actor";
        database.UpdateNode(duplicatedActor);
        Equal(duplicatedActor.Name, actorRepository.GetSettings(duplicatedActor.Id).DisplayName);
        database.Delete(duplicatedActor);
        database.Delete(createdActor);

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.AddImportedDevice(
            devicesRoot,
            new DeviceImportDraft("Invalid Device", "Mockups", "Invalid", "ios", "[]")));
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ActorPreviewDataBoundaryPreservesCurrentValues()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-actor-preview-data-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new ActorPreviewDataSource(database);
        var actor = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.Actor);
        var settings = database.GetActorSettings(actor.Id);
        var context = dataSource.LoadContext(actor.Id);
        var previewSource = dataSource.LoadPreview(actor.Id);

        Equal(settings.ProjectId, context.ProjectId);
        Equal(settings.DisplayName, context.DisplayName);
        Equal(settings.DefaultDeviceId, context.DefaultDeviceId);
        Equal(settings.DefaultThemeId, context.DefaultThemeId);
        Equal(settings.ProjectId, previewSource.ProjectId);
        Equal(settings.DisplayName, previewSource.DisplayName);
        Equal(settings.ShortName, previewSource.ShortName);
        Equal(settings.MetadataJson, previewSource.MetadataJson);
        Equal(database.GetProjectSettings(settings.ProjectId).MediaRoot, previewSource.ProjectMediaRoot);
        Equal(database.GetActorFieldValue(actor.Id, "actor.color.modes"), previewSource.ColorModes);
        Equal(database.GetActorFieldValue(actor.Id, "actor.avatarTextColor.modes"), previewSource.AvatarTextColorModes);
        Equal(database.GetActorFieldValue(actor.Id, "actor.avatar.filePath"), previewSource.AvatarFilePath);
        Equal(database.GetActorFieldValue(actor.Id, "actor.avatar.scale"), previewSource.AvatarScale);
        Equal(database.GetActorFieldValue(actor.Id, "actor.avatar.offset"), previewSource.AvatarOffset);
        Equal(database.GetActorFieldValue(actor.Id, "actor.avatar.useInitials"), previewSource.AvatarUseInitials);
        Equal(database.GetActorFieldValue(actor.Id, "actor.avatar.initialsPadding"), previewSource.AvatarInitialsPadding);
        SequenceEqual(
            database.GetActorOptions(settings.ProjectId).Select((option) => option.Value),
            dataSource.Options(settings.ProjectId).Select((option) => option.Value));
        SequenceEqual(
            database.GetPaletteColorOptions(settings.ProjectId).Select((option) => option.Value),
            dataSource.PaletteColorOptions(settings.ProjectId).Select((option) => option.Value));

        var paletteColors = database.GetPaletteColorMap(settings.ProjectId);
        var lightPayload = ActorPreviewInputFactory.Create(dataSource, actor.Id, "light", paletteColors);
        var darkPayload = ActorPreviewInputFactory.Create(dataSource, actor.Id, "dark", paletteColors);
        Equal(actor.Id, lightPayload["id"]!.GetValue<string>());
        Equal(settings.DisplayName, lightPayload["displayName"]!.GetValue<string>());
        Equal(
            paletteColors[previewSource.ColorModes.Split('|', 2)[0]],
            lightPayload["avatar"]!["backgroundColor"]!.GetValue<string>());
        Equal(
            paletteColors[previewSource.ColorModes.Split('|', 2)[1]],
            darkPayload["avatar"]!["backgroundColor"]!.GetValue<string>());
        True(JsonNode.DeepEquals(
            JsonPath.ParseRequiredObject(settings.MetadataJson, $"Actor '{actor.Id}' metadata_json")["wallpaper"],
            lightPayload["wallpaper"]));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RuntimeInputOptionBoundaryPreservesDictionaryOptions()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-runtime-input-options-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new RuntimeInputOptionsDataSource(database);
        var project = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.Project);

        var actorInput = new ComponentInputDefinition(
            "actor", "Actor", "actorId", ComponentInputKind.RecordReference,
            ValueKind.RecordReference, "", TableId: "actors");
        var actorDefinition = RuntimeInputFieldDefinitionFactory.Create(dataSource, project, actorInput);
        SequenceEqual(
            database.GetActorOptions(project.Id).Select((option) => option.Value),
            actorDefinition.Options!.Select((option) => option.Value));

        var paletteInput = new ComponentInputDefinition(
            "color", "Color", "color", ComponentInputKind.Option,
            ValueKind.PaletteColorToken, "");
        var paletteDefinition = RuntimeInputFieldDefinitionFactory.Create(dataSource, project, paletteInput);
        SequenceEqual(
            database.GetPaletteColorOptions(project.Id).Select((option) => option.Value),
            paletteDefinition.Options!.Select((option) => option.Value));

        var presetInput = new ComponentInputDefinition(
            "audio", "Audio", "presetId", ComponentInputKind.ComponentPreset,
            ValueKind.ComponentPreset, "", ComponentType: "audio");
        var presetDefinition = RuntimeInputFieldDefinitionFactory.Create(dataSource, project, presetInput);
        SequenceEqual(
            database.GetComponentPresetReferenceOptions(project.Id, "audio", false).Select((option) => option.Value),
            presetDefinition.Options!.Select((option) => option.Value));

        var presetReference = presetDefinition.Options!.First().Value;
        var dynamicInput = new ComponentInputDefinition(
            "state", "State", "stateId", ComponentInputKind.Option,
            ValueKind.OptionToken, "",
            OptionsSourceCollectionJsonKey: "states",
            OptionsSourceValueJsonKey: "id",
            OptionsSourceLabelJsonKey: "presetId");
        var values = new JsonObject
        {
            ["states"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "state_default",
                    ["presetId"] = presetReference,
                },
            },
        };
        var dynamicOptions = RuntimeInputDynamicOptions.Resolve(dataSource, dynamicInput, values)!;
        Equal(1, dynamicOptions.Count);
        Equal("state_default", dynamicOptions[0].Value);
        Equal(
            database.GetRuntimeComponentPresetName(presetReference, new JsonObject(), []),
            dynamicOptions[0].Label);

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void DictionaryFieldContextBoundaryPreservesCurrentData()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-dictionary-field-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new DictionaryFieldContextDataSource(database);
        var payloadData = new DesignPreviewPayloadDataSource(database);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var project = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var componentClass = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
        var preset = componentClass.Children.First((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset);
        var componentSettings = database.GetComponentClassSettings(componentClass.Id);
        var screen = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var theme = nodes
            .Where((node) => node.Kind == ProjectTreeNodeKind.Theme)
            .First((node) => !string.IsNullOrWhiteSpace(database.GetThemeSettings(node.Id).IconThemeId));
        var themeSettings = database.GetThemeSettings(theme.Id);

        Equal(themeSettings.IconThemeId, dataSource.IconThemeId(preset, theme.Id));
        True(JsonNode.DeepEquals(
            DesignPreviewTestValues.Parse(themeSettings.TokensJson),
            dataSource.ThemeTokens(preset, theme.Id)));

        var productionThemeId = payloadData.ResolveThemeId(screen, null)
            ?? throw new InvalidOperationException("Production Screen did not resolve its explicit Theme.");
        Equal(
            database.GetThemeSettings(productionThemeId).IconThemeId,
            dataSource.IconThemeId(screen, null));
        True(JsonNode.DeepEquals(
            DesignPreviewTestValues.Parse(database.GetModuleInstanceThemeTokensJson(screen.Id)),
            dataSource.ThemeTokens(screen, null)));

        SequenceEqual(
            database.GetPaletteColorOptions(project.Id).Select((option) => option.Value),
            dataSource.PaletteColorOptions(project.Id).Select((option) => option.Value));
        SequenceEqual(
            database.GetComponentPresetReferenceOptionsByType(
                project.Id,
                componentSettings.ComponentType).Select((option) => option.Value),
            dataSource.ComponentPresetOptions(
                project.Id,
                componentSettings.ComponentType).Select((option) => option.Value));

        SequenceEqual(
            database.GetComponentPresetRuntimeInputBindings(preset.Id)
                .Select((input) => $"{input.Id}\u001f{input.JsonKey}\u001f{input.ValueKind}"),
            dataSource.ComponentPresetRuntimeInputBindings(preset.Id)
                .Select((input) => $"{input.Id}\u001f{input.JsonKey}\u001f{input.ValueKind}"));
        Equal(
            database.GetComponentPresetRuntimeInputs(preset.Id).ToJsonString(),
            dataSource.ComponentPresetRuntimeValues(preset.Id).ToJsonString());
        SequenceEqual(
            database.GetComponentPresetRuntimeCollections(preset.Id)
                .Select((collection) => $"{collection.Id}\u001f{collection.JsonKey}\u001f{collection.Fields.Count}"),
            dataSource.ComponentPresetRuntimeCollections(preset.Id)
                .Select((collection) => $"{collection.Id}\u001f{collection.JsonKey}\u001f{collection.Fields.Count}"));

        var expectedSelection = database.GetComponentPresetSelectionSettings(preset.Id);
        var selection = dataSource.ComponentPresetSelection(preset.Id);
        Equal(expectedSelection.ProjectId, selection.ProjectId);
        Equal(expectedSelection.ComponentType, selection.ComponentType);
        Equal(expectedSelection.RecordClassId, selection.RecordClassId);
        Equal(expectedSelection.ConfigJson, selection.ConfigJson);

        var token = database.GetIconThemeTokens(themeSettings.IconThemeId).First();
        Equal(
            database.ResolveIconThemeAssetPath(themeSettings.IconThemeId, token.File),
            dataSource.IconTokenAssetPath(themeSettings.IconThemeId, token.Token));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void EmbeddedComponentDocumentStorePreservesOwnership()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-embedded-component-documents-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var store = new EmbeddedComponentDocumentStore(database);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var audioClass = nodes
            .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentClass)
            .First((node) => database.GetComponentClassSettings(node.Id).ComponentType == "audio");
        var audioVariant = audioClass.Children
            .First((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset);
        var surfaceSlot = EmbeddedComponentSlotCatalog.Get("component.audio.surface.editor");
        var designContext = new EditorEmbeddedContext(audioVariant, [surfaceSlot]);
        var embeddedFieldId = database.LoadEditorLayout(surfaceSlot.RecordClassId).Cards
            .Where((card) => card.Visible)
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .Select((field) => field.Id)
            .First(ComponentClassFieldCatalog.IsRuntimeOverrideField);

        Equal(
            database.GetEmbeddedComponentPresetName(audioVariant, [surfaceSlot]),
            store.ActivePresetName(designContext));
        var expectedField = database.CreateEmbeddedComponentFieldValue(
            audioVariant,
            [surfaceSlot],
            embeddedFieldId);
        var storedField = store.CreateFieldValue(designContext, embeddedFieldId);
        Equal(expectedField.Value, storedField.Value);
        Equal(expectedField.IsInherited, storedField.IsInherited);

        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        var editableVariant = database.SaveComponentPreset(audioVariant, "Embedded boundary test");
        var editableContext = new EditorEmbeddedContext(editableVariant, [surfaceSlot]);
        var editableField = store.CreateFieldValue(editableContext, embeddedFieldId);
        store.CommitFieldValue(editableContext, embeddedFieldId, editableField.Value);
        Equal(
            editableField.Value,
            database.CreateEmbeddedComponentFieldValue(
                editableVariant,
                [surfaceSlot],
                embeddedFieldId).Value);

        var selection = database.GetComponentPresetSelectionSettings(audioVariant.Id);
        var overrides = new JsonObject();
        var overrideChanges = 0;
        var runtimeContext = new EditorEmbeddedContext(
            audioVariant,
            [],
            new RuntimeComponentOverrideSource(
                selection.ProjectId,
                audioVariant.Id,
                selection.ComponentType,
                selection.RecordClassId,
                selection.ConfigJson,
                overrides,
                (_) => overrideChanges++));
        Equal(
            database.GetRuntimeComponentPresetName(audioVariant.Id, overrides, []),
            store.ActivePresetName(runtimeContext));
        True(store.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);

        var beforeRuntimeOverride = SHA256.HashData(File.ReadAllBytes(temporary));
        store.CommitFieldValue(
            runtimeContext,
            "component.audio.padding",
            "theme.spacing.xl|theme.spacing.l");
        Equal(1, overrideChanges);
        True(!store.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);
        store.CommitFieldValue(runtimeContext, "component.audio.padding", "inherited");
        Equal(2, overrideChanges);
        True(store.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);
        var afterRuntimeOverride = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRuntimeOverride, afterRuntimeOverride);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void EditorPresentationContextBoundaryPreservesCurrentData()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-editor-presentation-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new EditorPresentationContextDataSource(database);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var project = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var productionFont = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ProductionFont);
        var themeSettings = database.GetThemeSettings(theme.Id);

        Equal(database.GetProjectSettings(project.Id).MediaRoot, dataSource.ProjectMediaRoot(project.Id));
        var themeSource = dataSource.ThemeNavigation(theme.Id);
        Equal(themeSettings.Family, themeSource.Family);
        Equal(themeSettings.IconThemeId, themeSource.IconThemeId);
        Equal(themeSettings.StatusBarId, themeSource.StatusBarId);
        Equal(themeSettings.NavigationBarId, themeSource.NavigationBarId);
        Equal(
            database.GetProductionFontFieldValue(productionFont.Id, "font.files"),
            dataSource.ProductionFontFiles(productionFont.Id));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ComponentPreviewInputBoundaryPreservesCurrentContracts()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-component-preview-input-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new ComponentPreviewInputDataSource(database);
        var componentClass = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
        var preset = componentClass.Children
            .First((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset);
        var settings = database.GetComponentClassSettings(componentClass.Id);

        Equal(
            database.GetProjectSettings(settings.ProjectId).DefaultFps,
            dataSource.ProjectDefaultFrameRate(settings.ProjectId));
        Equal(
            database.GetComponentPresetConfig(preset.Id).ToJsonString(),
            dataSource.ComponentPresetConfig(preset.Id).ToJsonString());
        Equal(
            database.GetComponentPresetRuntimeContract(preset.Id).ToJsonString(),
            dataSource.ComponentPresetRuntimeContract(preset.Id).ToJsonString());
        Equal(
            database.ValidateComponentPresetReferenceValue(
                settings.ProjectId,
                settings.ComponentType,
                preset.Id),
            dataSource.ValidateComponentPresetReference(
                settings.ProjectId,
                settings.ComponentType,
                preset.Id));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RuntimeInputOwnerStorePreservesCurrentDocuments()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-runtime-input-owner-store-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var store = new RuntimeInputOwnerDocumentStore(database);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var module = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Module);
        var moduleVariant = module.Children.First((node) => node.Kind == ProjectTreeNodeKind.ModuleVariant);
        var componentClass = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
        var componentPreset = componentClass.Children.First((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset);
        var screen = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);

        var moduleSettings = database.GetModuleSettings(module.Id);
        var moduleSource = store.Load(module);
        Equal(moduleSettings.ConfigJson, moduleSource.ConfigJson);
        Equal(moduleSettings.DesignPreviewJson, moduleSource.RuntimePreviewJson);
        Equal(RuntimeInputDesignPreviewOwnerKind.Module, moduleSource.DesignPreviewOwnerKind);
        Equal(module.Id, moduleSource.DesignPreviewOwnerId);

        var moduleVariantSettings = database.GetModuleVariantSettings(moduleVariant);
        var moduleVariantSource = store.Load(moduleVariant);
        Equal(moduleVariantSettings.ConfigJson, moduleVariantSource.ConfigJson);
        Equal(moduleVariantSettings.DesignPreviewJson, moduleVariantSource.RuntimePreviewJson);
        Equal(module.Id, moduleVariantSource.DesignPreviewOwnerId);

        var componentSettings = database.GetComponentPresetSettings(componentPreset);
        var componentSource = store.Load(componentPreset);
        Equal(componentSettings.ConfigJson, componentSource.ConfigJson);
        Equal(componentSettings.DesignPreviewJson, componentSource.RuntimePreviewJson);
        Equal(RuntimeInputDesignPreviewOwnerKind.ComponentClass, componentSource.DesignPreviewOwnerKind);
        Equal(componentClass.Id, componentSource.DesignPreviewOwnerId);

        var instanceVariant = database.GetModuleInstanceVariantSettings(screen.Id);
        var instanceSource = store.Load(screen);
        Equal(instanceVariant.ConfigJson, instanceSource.ConfigJson);
        Equal(database.GetModuleInstanceRuntimePreviewJson(screen.Id), instanceSource.RuntimePreviewJson);
        True(instanceSource.IsInstance);
        Equal(RuntimeInputDesignPreviewOwnerKind.None, instanceSource.DesignPreviewOwnerKind);

        var selection = database.GetComponentPresetSelectionSettings(componentPreset.Id);
        var selectionSource = store.ComponentPresetSelection(componentPreset.Id);
        Equal(selection.ProjectId, selectionSource.ProjectId);
        Equal(selection.ComponentType, selectionSource.ComponentType);
        Equal(selection.RecordClassId, selectionSource.RecordClassId);
        Equal(selection.ConfigJson, selectionSource.ConfigJson);
        Equal(
            database.GetComponentPresetRuntimeInputs(componentPreset.Id).ToJsonString(),
            store.ComponentPresetRuntimeInputs(componentPreset.Id).ToJsonString());

        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        store.SaveDesignPreviewJson(moduleSource, moduleSource.RuntimePreviewJson);
        Equal(moduleSource.RuntimePreviewJson, database.GetModuleSettings(module.Id).DesignPreviewJson);
        store.SaveDesignPreviewJson(componentSource, componentSource.RuntimePreviewJson);
        Equal(componentSource.RuntimePreviewJson, database.GetComponentClassSettings(componentClass.Id).DesignPreviewJson);
        Throws<InvalidOperationException>(() =>
            store.SaveDesignPreviewJson(instanceSource, instanceSource.RuntimePreviewJson));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RuntimeInputInstanceStorePreservesExplicitWrites()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-runtime-input-instance-store-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var store = new RuntimeInputInstanceDocumentStore(database);
        var screen = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var animationJson = database.GetModuleInstanceSettings(screen.Id).AnimationJson;

        Equal(animationJson, store.AnimationJson(screen.Id));
        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        const string collectionKey = "test_runtime_items";
        store.UpdateRuntimeValue(screen.Id, "test_runtime_scalar", JsonValue.Create("value"));
        store.AddCollectionItem(screen.Id, collectionKey, new JsonObject
        {
            ["id"] = "test_a",
            ["name"] = "A",
        });
        store.InsertCollectionItemAfter(screen.Id, collectionKey, "test_a", new JsonObject
        {
            ["id"] = "test_b",
            ["name"] = "B",
        });
        store.DuplicateCollectionItem(
            screen.Id,
            collectionKey,
            "test_a",
            new JsonObject
            {
                ["id"] = "test_c",
                ["name"] = "C",
            },
            new Dictionary<string, string>());
        store.UpdateCollectionValue(
            screen.Id,
            collectionKey,
            "test_b",
            "name",
            JsonValue.Create("B2"));
        store.MoveCollectionItem(screen.Id, collectionKey, "test_c", 1);
        store.DeleteCollectionItem(screen.Id, collectionKey, "test_a");

        var content = JsonPath.ParseRequiredObject(
            database.GetModuleInstanceSettings(screen.Id).ContentJson,
            $"Module Instance '{screen.Id}' content");
        Equal("value", content["test_runtime_scalar"]?.GetValue<string>() ?? "");
        var items = content[collectionKey]?.AsArray()
            ?? throw new InvalidOperationException("Missing test runtime collection.");
        SequenceEqual(
            new[] { "test_b", "test_c" },
            items.Select((item) => item?["id"]?.GetValue<string>() ?? ""));
        Equal("B2", items[0]?["name"]?.GetValue<string>() ?? "");

        Equal(animationJson, store.SaveAnimationJson(screen.Id, animationJson));
        Equal(animationJson, database.GetModuleInstanceSettings(screen.Id).AnimationJson);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void PreviewVisualContextBoundaryPreservesResolvedResources()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-preview-visual-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new PreviewVisualContextDataSource(database);
        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var device = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.Device);

        SequenceEqual(
            database.GetDeviceOptions(project.Id).Select((option) => option.Value),
            dataSource.DeviceOptions(project.Id).Select((option) => option.Value));
        SequenceEqual(
            database.GetThemeOptions(project.Id).Select((option) => option.Value),
            dataSource.ThemeOptions(project.Id).Select((option) => option.Value));
        Equal(database.GetProjectSettings(project.Id).MediaRoot, dataSource.ProjectMediaRoot(project.Id));
        Equal(database.GetDevicePreviewMetrics(device.Id), dataSource.DeviceMetrics(device.Id));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ProductionPreviewSessionBoundaryPreservesCurrentData()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-preview-session-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new ProductionPreviewSessionDataSource(database);
        var timelineDataSource = new ModuleInstanceTimelineDataSource(database);
        var tree = database.LoadProjectTree();
        var shot = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Shot);
        var screen = shot.Children.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);

        Equal(database.GetModuleInstanceSettings(screen.Id).ShotId, dataSource.ModuleInstanceShotId(screen.Id));
        Equal(database.GetShotSettings(shot.Id).Fps, dataSource.ShotFrameRate(shot.Id));
        Equal(
            database.GetModuleInstanceVariantSettings(screen.Id).ConfigJson,
            dataSource.ModuleInstanceVariantConfigJson(screen.Id));
        SequenceEqual(
            database.GetShotModuleInstanceSlots(shot.Id).Select((slot) => slot.Id),
            timelineDataSource.ShotSlotIds(shot.Id));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ModuleInstanceAnimationStorePreservesCurrentDocuments()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-instance-animation-store-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var timelineDataSource = new ModuleInstanceTimelineDataSource(database);
        var store = new ModuleInstanceAnimationDocumentStore(database, timelineDataSource);
        var screen = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var instance = database.GetModuleInstanceSettings(screen.Id);
        var variant = database.GetModuleInstanceVariantSettings(screen.Id);
        var source = store.Load(screen.Id);

        Equal(variant.ConfigJson, source.VariantConfigJson);
        Equal(instance.AnimationJson, source.AnimationJson);
        Equal(database.GetModuleInstanceRuntimePreviewJson(screen.Id), source.RuntimePreviewJson);
        Equal(database.GetModuleInstanceThemeTokensJson(screen.Id), source.ThemeTokensJson);
        Equal(database.GetModuleInstanceEffectiveContractJson(screen.Id), source.EffectiveContractJson);

        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        var persisted = store.SaveAnimationJson(screen.Id, source.AnimationJson);
        Equal(source.AnimationJson, persisted);
        Equal(source.AnimationJson, database.GetModuleInstanceSettings(screen.Id).AnimationJson);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ThemeRepositoryPreservesFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-theme-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IThemeRepository themeRepository = new ThemeRepository(context);
        IModuleInstanceThemeContextService themeContextService = new ModuleInstanceThemeContextService(context);
        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var theme = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var settings = database.GetThemeSettings(theme.Id);
        var record = themeRepository.Get(theme.Id);

        Equal(settings.ProjectId, record.ProjectId);
        Equal(settings.Name, record.Name);
        Equal(settings.Family, record.Family);
        Equal(settings.IconThemeId, record.IconThemeId);
        Equal(settings.StatusBarId, record.StatusBarId);
        Equal(settings.NavigationBarId, record.NavigationBarId);
        Equal(settings.TokensJson, record.TokensJson);
        Equal(settings.MetadataJson, record.MetadataJson);
        using (var connection = context.OpenConnection())
        {
            SequenceEqual(
                database.GetThemeOptions(project.Id).Select((option) => option.Value),
                themeRepository.QueryAll(connection)
                    .Where((row) => row.ProjectId == project.Id)
                    .OrderBy((row) => row.Name)
                    .Select((row) => row.Id));
        }

        themeRepository.UpdateDirectField(theme.Id, "theme.family", "repository-test");
        Equal("repository-test", database.GetThemeSettings(theme.Id).Family);
        database.UpdateThemeField(theme.Id, "theme.family", settings.Family);
        Equal(settings.Family, themeRepository.Get(theme.Id).Family);

        var originalBackground = database.GetThemeFieldValue(theme.Id, "theme.colors.background");
        database.UpdateThemeField(theme.Id, "theme.colors.background", "gray_010|gray_100");
        Equal("gray_010|gray_100", database.GetThemeFieldValue(theme.Id, "theme.colors.background"));
        database.UpdateThemeField(theme.Id, "theme.colors.background", originalBackground);
        Equal(settings.TokensJson, themeRepository.Get(theme.Id).TokensJson);

        var themesRoot = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.ThemesRoot);
        var created = database.AddTheme(themesRoot, "ios");
        var duplicated = database.Duplicate(created);
        duplicated.Name = "Repository Theme";
        database.UpdateNode(duplicated);
        Equal(duplicated.Name, themeRepository.Get(duplicated.Id).Name);
        database.Delete(duplicated);
        database.Delete(created);

        var moduleInstance = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        Equal(
            database.GetModuleInstanceThemeTokensJson(moduleInstance.Id),
            themeContextService.GetTokensJson(moduleInstance.Id));
        Throws<InvalidOperationException>(() => themeContextService.GetTokensJson("missing_module_instance"));

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => themeRepository.UpdateTokens(theme.Id, "[]"));
        Throws<InvalidOperationException>(() => themeRepository.UpdateDirectField(theme.Id, "theme.unknown", "value"));
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ProductionFontRepositoryPreservesFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-font-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IProductionFontRepository repository = new ProductionFontRepository(context);
        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var fontNode = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.ProductionFont);
        var settings = database.GetProductionFontSettings(fontNode.Id);
        var record = repository.Get(fontNode.Id);

        Equal(settings.FamilyName, record.FamilyName);
        Equal(settings.Category, record.Category);
        Equal(settings.SourceDirectory, record.SourceDirectory);
        Equal(settings.FilesJson, record.FilesJson);
        JsonPath.ParseRequiredObject(record.MetadataJson, $"Production Font '{record.Id}' metadata_json");
        using (var connection = context.OpenConnection())
        {
            SequenceEqual(
                database.GetProductionFontOptions(project.Id).Skip(1).Select((option) => option.Value),
                repository.QueryAll(connection)
                    .Where((font) => font.ProjectId == project.Id)
                    .OrderBy((font) => font.FamilyName)
                    .Select((font) => font.Id));
        }

        repository.UpdateField(fontNode.Id, "font.family", "Repository Font");
        Equal("Repository Font", database.GetProductionFontSettings(fontNode.Id).FamilyName);
        database.UpdateProductionFontField(fontNode.Id, "font.family", settings.FamilyName);
        Equal(settings.FamilyName, repository.Get(fontNode.Id).FamilyName);

        ProductionFontRecord imported;
        using (var connection = context.OpenConnection())
        {
            imported = repository.UpsertImported(
                connection,
                project.Id,
                "Repository Lifecycle Font",
                "text",
                "fonts/repository-lifecycle-font",
                "[]");
        }
        var importedNode = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.ProductionFont && node.Id == imported.Id);
        importedNode.Name = "Renamed Repository Font";
        database.UpdateNode(importedNode);
        Equal(importedNode.Name, repository.Get(imported.Id).FamilyName);
        database.Delete(importedNode);
        Throws<InvalidOperationException>(() => repository.Get(imported.Id));

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        using (var connection = context.OpenConnection())
        {
            Throws<InvalidOperationException>(() => repository.UpsertImported(
                connection,
                project.Id,
                "Invalid Repository Font",
                "text",
                "fonts/invalid-repository-font",
                "{}"));
        }
        Throws<InvalidOperationException>(() => repository.UpdateField(fontNode.Id, "font.unknown", "value"));
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void IconThemeRepositoryPreservesFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-icon-theme-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IIconThemeRepository repository = new IconThemeRepository(context);
        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var iconThemeNode = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.IconTheme);
        var settings = database.GetIconThemeSettings(iconThemeNode.Id);
        var record = repository.Get(iconThemeNode.Id);

        Equal(settings.Name, record.Name);
        Equal(settings.AssetRoot, record.AssetRoot);
        Equal(settings.MappingJson, record.MappingJson);
        Equal(settings.MetadataJson, record.MetadataJson);
        using (var connection = context.OpenConnection())
        {
            SequenceEqual(
                database.GetIconThemeOptions(project.Id).Skip(1).Select((option) => option.Value),
                repository.QueryAll(connection)
                    .Where((iconTheme) => iconTheme.ProjectId == project.Id)
                    .OrderBy((iconTheme) => iconTheme.Name)
                    .Select((iconTheme) => iconTheme.Id));
        }

        var changedMapping = JsonPath.ParseRequiredObject(record.MappingJson, $"Icon Theme '{record.Id}' mapping_json");
        changedMapping["repositoryTest"] = true;
        using (var connection = context.OpenConnection())
        {
            repository.UpdateMapping(connection, record.Id, changedMapping.ToJsonString());
        }
        Equal(changedMapping.ToJsonString(), database.GetIconThemeSettings(record.Id).MappingJson);
        using (var connection = context.OpenConnection())
        {
            repository.UpdateMapping(connection, record.Id, record.MappingJson);
        }

        IconThemeRecord duplicated;
        using (var connection = context.OpenConnection())
        {
            duplicated = repository.CreateDuplicate(
                connection,
                record.Id,
                $"icon_theme_repository_{Guid.NewGuid():N}",
                "Repository Icon Theme",
                "icon-themes/repository-icon-theme",
                record.MetadataJson);
        }
        Equal(record.MappingJson, repository.Get(duplicated.Id).MappingJson);
        using (var connection = context.OpenConnection())
        {
            repository.Delete(connection, duplicated.Id);
        }
        Throws<InvalidOperationException>(() => repository.Get(duplicated.Id));

        var token = database.GetIconThemeTokens(record.Id).First().Token;
        var invalidMapping = JsonPath.ParseRequiredObject(record.MappingJson, $"Icon Theme '{record.Id}' mapping_json");
        var tokenObject = (invalidMapping["tokens"] as JsonObject)?[token] as JsonObject
            ?? throw new InvalidOperationException($"Missing test token '{token}'.");
        tokenObject.Remove("file");
        using (var connection = context.OpenConnection())
        {
            repository.UpdateMapping(connection, record.Id, invalidMapping.ToJsonString());
        }
        var beforeStrictRead = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.ReadIconThemeTokenSvg(record.Id, token));
        var afterStrictRead = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeStrictRead, afterStrictRead);
        using (var connection = context.OpenConnection())
        {
            repository.UpdateMapping(connection, record.Id, record.MappingJson);
        }

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        using (var connection = context.OpenConnection())
        {
            Throws<InvalidOperationException>(() => repository.UpdateMapping(connection, record.Id, "[]"));
            Throws<InvalidOperationException>(() => repository.UpsertDiscovered(
                connection,
                "invalid_icon_theme",
                project.Id,
                "Invalid Icon Theme",
                "icon-themes/invalid",
                "[]"));
        }
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void AppModuleRepositoryPreservesFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-app-module-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IAppModuleRepository repository = new AppModuleRepository(context);
        var tree = database.LoadProjectTree();
        var appNode = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.App);
        var moduleNode = appNode.Children.First((node) => node.Kind == ProjectTreeNodeKind.Module);
        var appSettings = database.GetAppSettings(appNode.Id);
        var moduleSettings = database.GetModuleSettings(moduleNode.Id);
        var app = repository.GetApp(appNode.Id);
        var module = repository.GetModule(moduleNode.Id);

        Equal(appSettings.ProjectId, app.ProjectId);
        Equal(appSettings.BundleKey, app.BundleKey);
        Equal(appSettings.AppType, app.AppType);
        Equal(appSettings.ConfigJson, app.ConfigJson);
        Equal(appSettings.MetadataJson, app.MetadataJson);
        Equal(moduleSettings.ProjectId, module.ProjectId);
        Equal(moduleSettings.RecordClassId, module.RecordClassId);
        Equal(moduleSettings.SortOrder, module.SortOrder);
        Equal(moduleSettings.ConfigJson, module.ConfigJson);
        Equal(moduleSettings.DesignPreviewJson, module.DesignPreviewJson);
        Equal(moduleSettings.MetadataJson, module.MetadataJson);
        Equal(app, repository.GetModuleApp(module.Id));
        using (var connection = context.OpenConnection())
        {
            True(repository.QueryApps(connection).Any((candidate) => candidate.Id == app.Id));
            True(repository.QueryModules(connection).Any((candidate) => candidate.Id == module.Id));
        }

        using (var connection = context.OpenConnection())
        {
            repository.UpdateAppDirectField(connection, app.Id, "app.bundleKey", "repository.bundle");
        }
        Equal("repository.bundle", database.GetAppSettings(app.Id).BundleKey);
        database.UpdateAppField(app.Id, "app.bundleKey", app.BundleKey);

        var appConfig = JsonPath.ParseRequiredObject(app.ConfigJson, $"App '{app.Id}' config_json");
        appConfig["repositoryTest"] = true;
        using (var connection = context.OpenConnection())
        {
            repository.UpdateAppConfig(connection, app.Id, appConfig.ToJsonString());
        }
        Equal(appConfig.ToJsonString(), database.GetAppSettings(app.Id).ConfigJson);
        database.UpdateAppField(app.Id, "app.config", app.ConfigJson);

        var moduleConfig = JsonPath.ParseRequiredObject(module.ConfigJson, $"Module '{module.Id}' config_json");
        moduleConfig["repositoryTest"] = true;
        using (var connection = context.OpenConnection())
        {
            repository.UpdateModuleConfig(connection, module.Id, moduleConfig.ToJsonString());
        }
        Equal(moduleConfig.ToJsonString(), database.GetModuleSettings(module.Id).ConfigJson);
        using (var connection = context.OpenConnection())
        {
            repository.UpdateModuleConfig(connection, module.Id, module.ConfigJson);
        }

        var preview = JsonPath.ParseRequiredObject(module.DesignPreviewJson, $"Module '{module.Id}' design_preview_json");
        preview["repositoryTest"] = true;
        repository.UpdateModuleDesignPreview(module.Id, preview.ToJsonString());
        Equal(preview.ToJsonString(), database.GetModuleSettings(module.Id).DesignPreviewJson);
        database.UpdateModuleDesignPreviewJson(module.Id, module.DesignPreviewJson);

        var renamedApp = database.RenameDirectNode(appNode, "Repository App");
        Equal("Repository App", repository.GetApp(app.Id).Name);
        database.RenameDirectNode(renamedApp, app.Name);
        var renamedModule = database.RenameDirectNode(moduleNode, "Repository Module");
        Equal("Repository Module", repository.GetModule(module.Id).Name);
        database.RenameDirectNode(renamedModule, module.Name);

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        using (var connection = context.OpenConnection())
        {
            Throws<InvalidOperationException>(() => repository.UpdateAppConfig(connection, app.Id, "[]"));
            Throws<InvalidOperationException>(() => repository.UpdateModuleConfig(connection, module.Id, "[]"));
            Throws<InvalidOperationException>(() => repository.UpdateModuleMetadata(connection, module.Id, "{\"variants\":[]}"));
        }
        Throws<InvalidOperationException>(() => repository.UpdateModuleDesignPreview(module.Id, "[]"));
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ComponentClassRepositoryPreservesFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-component-class-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IComponentClassRepository repository = new ComponentClassRepository(context);
        var componentNode = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
        var original = repository.Get(componentNode.Id);
        var settings = database.GetComponentClassSettings(componentNode.Id);

        Equal(original.ProjectId, settings.ProjectId);
        Equal(original.ComponentType, settings.ComponentType);
        Equal(original.RecordClassId, settings.RecordClassId);
        Equal(original.DesignPreviewJson, settings.DesignPreviewJson);
        Equal(original.MetadataJson, settings.MetadataJson);
        using (var connection = context.OpenConnection())
        {
            True(repository.QueryAll(connection).Any((candidate) => candidate.Id == original.Id));
            True(repository.QueryByProject(connection, original.ProjectId).Any((candidate) => candidate.Id == original.Id));
        }

        var preview = JsonPath.ParseRequiredObject(original.DesignPreviewJson, $"Component class '{original.Id}' design_preview_json");
        preview["repositoryTest"] = true;
        repository.UpdateDesignPreview(original.Id, preview.ToJsonString());
        Equal(preview.ToJsonString(), database.GetComponentClassSettings(original.Id).DesignPreviewJson);
        database.UpdateComponentClassDesignPreviewJson(original.Id, original.DesignPreviewJson);

        var config = JsonPath.ParseRequiredObject(original.ConfigJson, $"Component class '{original.Id}' config_json");
        config["repositoryTest"] = true;
        var metadata = JsonPath.ParseRequiredObject(original.MetadataJson, $"Component class '{original.Id}' metadata_json");
        var presets = VariantEnvelopeContract.RequiredArray(metadata, "presets", $"Component class '{original.Id}'");
        var defaultPreset = presets.OfType<JsonObject>()
            .Single((preset) => JsonPath.String(preset, "id", "") == "default");
        defaultPreset["config"] = config.DeepClone();
        using (var connection = context.OpenConnection())
        {
            repository.UpdateConfigAndMetadata(
                connection,
                original.Id,
                config.ToJsonString(),
                metadata.ToJsonString());
        }
        var storedConfig = JsonPath.ParseRequiredObject(
            database.GetComponentClassSettings(original.Id).ConfigJson,
            "repository test config");
        True(storedConfig["repositoryTest"]?.GetValue<bool>() == true);
        using (var connection = context.OpenConnection())
        {
            repository.UpdateConfigAndMetadata(
                connection,
                original.Id,
                original.ConfigJson,
                original.MetadataJson);
        }

        var renamed = database.RenameDirectNode(componentNode, "Repository Component");
        Equal("Repository Component", repository.Get(original.Id).Name);
        database.RenameDirectNode(renamed, original.Name);

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        using (var connection = context.OpenConnection())
        {
            Throws<InvalidOperationException>(() => repository.UpdateConfigAndMetadata(
                connection,
                original.Id,
                "[]",
                original.MetadataJson));
            Throws<InvalidOperationException>(() => repository.UpdateMetadata(
                connection,
                original.Id,
                "{\"presets\":[]}"));
        }
        Throws<InvalidOperationException>(() => repository.UpdateDesignPreview(original.Id, "[]"));
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ModuleInstanceRepositoryPreservesFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-instance-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IModuleInstanceRepository repository = new ModuleInstanceRepository(context);
        var node = Descendants(database.LoadProjectTree())
            .First((candidate) => candidate.Kind == ProjectTreeNodeKind.ModuleInstance);
        var original = repository.Get(node.Id);
        var settings = database.GetModuleInstanceSettings(node.Id);

        Equal(original.ShotId, settings.ShotId);
        Equal(original.AppId, settings.AppId);
        Equal(original.ModuleId, settings.ModuleId);
        Equal(original.DurationFrames, settings.DurationFrames);
        Equal(original.ContentJson, settings.ContentJson);
        Equal(original.AnimationJson, settings.AnimationJson);
        using (var connection = context.OpenConnection())
        {
            True(repository.QueryAll(connection).Any((candidate) => candidate.Id == original.Id));
            True(repository.QueryByShot(connection, original.ShotId).Any((candidate) => candidate.Id == original.Id));
        }

        var content = JsonPath.ParseRequiredObject(original.ContentJson, $"Module instance '{original.Id}' content_json");
        content["repositoryTest"] = true;
        using (var connection = context.OpenConnection())
        {
            repository.UpdateContent(connection, original.Id, content.ToJsonString());
        }
        True(JsonPath.ParseRequiredObject(
            database.GetModuleInstanceSettings(original.Id).ContentJson,
            "repository test content")["repositoryTest"]?.GetValue<bool>() == true);

        var animation = JsonPath.ParseRequiredObject(original.AnimationJson, $"Module instance '{original.Id}' animation_json");
        animation["repositoryTest"] = true;
        using (var connection = context.OpenConnection())
        {
            repository.UpdateContentAndAnimation(
                connection,
                original.Id,
                original.ContentJson,
                animation.ToJsonString());
        }
        True(JsonPath.ParseRequiredObject(
            database.GetModuleInstanceSettings(original.Id).AnimationJson,
            "repository test animation")["repositoryTest"]?.GetValue<bool>() == true);

        var metadata = JsonPath.ParseRequiredObject(original.MetadataJson, $"Module instance '{original.Id}' metadata_json");
        var variantReference = JsonPath.String(metadata, "moduleVariantReference", "");
        metadata["repositoryTest"] = true;
        using (var connection = context.OpenConnection())
        {
            repository.UpdateVariantDocuments(
                connection,
                original.Id,
                metadata.ToJsonString(),
                original.ContentJson,
                original.AnimationJson);
            True(repository.CountVariantReferences(connection, original.ModuleId, variantReference) > 0);
            repository.UpdateDuration(connection, original.Id, original.DurationFrames + 1);
        }
        Equal(original.DurationFrames + 1, repository.Get(original.Id).DurationFrames);

        using (var connection = context.OpenConnection())
        {
            repository.UpdateVariantDocuments(
                connection,
                original.Id,
                original.MetadataJson,
                original.ContentJson,
                original.AnimationJson);
            repository.UpdateDuration(connection, original.Id, original.DurationFrames);
        }

        var renamed = database.RenameDirectNode(node, "Repository Screen");
        Equal("Repository Screen", repository.Get(original.Id).Name);
        database.RenameDirectNode(renamed, original.Name);

        using (var connection = context.OpenConnection())
        {
            var siblings = repository.QueryByShot(connection, original.ShotId);
            if (siblings.Count >= 2)
            {
                var first = siblings[0];
                var second = siblings[1];
                repository.SwapSortOrder(connection, first.Id, first.SortOrder, second.Id, second.SortOrder);
                Equal(second.SortOrder, repository.Get(connection, first.Id).SortOrder);
                Equal(first.SortOrder, repository.Get(connection, second.Id).SortOrder);
                repository.SwapSortOrder(connection, first.Id, second.SortOrder, second.Id, first.SortOrder);
            }

            var duplicateId = $"module_instance_repository_{Guid.NewGuid():N}";
            var duplicateName = repository.UniqueName(connection, original.ShotId, $"{original.Name} copy");
            var duplicate = repository.Duplicate(
                connection,
                original.Id,
                duplicateId,
                duplicateName,
                repository.NextSortOrder(connection, original.ShotId));
            Equal(original.ModuleId, duplicate.ModuleId);
            Equal(original.ContentJson, duplicate.ContentJson);
            repository.Delete(connection, duplicate.Id);
            Throws<InvalidOperationException>(() => repository.Get(connection, duplicate.Id));
        }

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        using (var connection = context.OpenConnection())
        {
            Throws<InvalidOperationException>(() => repository.UpdateContent(connection, original.Id, "[]"));
            Throws<InvalidOperationException>(() => repository.UpdateAnimation(connection, original.Id, "[]"));
            Throws<InvalidOperationException>(() => repository.UpdateContentAndAnimation(
                connection,
                original.Id,
                original.ContentJson,
                "[]"));
            Throws<InvalidOperationException>(() => repository.UpdateVariantDocuments(
                connection,
                original.Id,
                "[]",
                original.ContentJson,
                original.AnimationJson));
            Throws<InvalidOperationException>(() => repository.Insert(
                connection,
                original with
                {
                    Id = $"invalid_module_instance_{Guid.NewGuid():N}",
                    ContentJson = "[]",
                }));
        }
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ShotRepositoryPreservesFacadeContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-shot-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var context = new SqliteProjectContext(temporary);
        IShotRepository repository = new ShotRepository(context);
        IProjectEpisodeRepository episodeRepository = new ProjectEpisodeRepository(context, repository);
        var tree = database.LoadProjectTree();
        var node = Descendants(tree).Single((candidate) => candidate.Id == "shot_001");
        var original = repository.Get(node.Id);
        var settings = database.GetShotSettings(node.Id);

        Equal(original.ProjectId, settings.ProjectId);
        Equal(original.Slug, settings.Slug);
        Equal(original.Version, settings.Version);
        Equal(original.DurationFrames, settings.DurationFrames);
        Equal(original.OwnerActorId, settings.OwnerActorId);
        Equal(original.CanvasJson, settings.CanvasJson);
        Equal(original.MetadataJson, settings.MetadataJson);
        using (var connection = context.OpenConnection())
        {
            True(repository.QueryAll(connection).Any((candidate) => candidate.Id == original.Id));
            True(repository.QueryByEpisode(connection, original.EpisodeId)
                .Any((candidate) => candidate.Id == original.Id));
        }

        using (var connection = context.OpenConnection())
        {
            repository.UpdateField(connection, original.Id, "shot.slug", "repository-shot");
        }
        Equal("repository-shot", database.GetShotSettings(original.Id).Slug);
        using (var connection = context.OpenConnection())
        {
            repository.UpdateField(connection, original.Id, "shot.slug", original.Slug);
            repository.UpdateField(connection, original.Id, "shot.fps", "30");
        }
        Equal(30, database.GetShotSettings(original.Id).FpsOverride);
        using (var connection = context.OpenConnection())
        {
            repository.ClearFpsOverride(connection, original.Id);
            repository.UpdateDuration(connection, original.Id, original.DurationFrames + 1);
        }
        Equal(original.DurationFrames + 1, database.GetShotSettings(original.Id).DurationFrames);
        using (var connection = context.OpenConnection())
        {
            repository.UpdateDuration(connection, original.Id, original.DurationFrames);
        }

        var originalName = node.Name;
        node.Name = "Repository Shot";
        database.UpdateNode(node);
        Equal("Repository Shot", repository.Get(original.Id).Name);
        node.Name = originalName;
        database.UpdateNode(node);

        var renderPreset = Descendants(tree)
            .First((candidate) => candidate.Kind == ProjectTreeNodeKind.RenderPreset);
        using (var connection = context.OpenConnection())
        {
            repository.UpdateField(
                connection,
                original.Id,
                "shot.renderPresetId",
                renderPreset.Id);
            var duplicate = repository.Duplicate(
                connection,
                original.Id,
                $"shot_repository_{Guid.NewGuid():N}",
                $"{original.Name} repository copy");
            Equal(renderPreset.Id, duplicate.RenderPresetId);
            Equal(original.OwnerActorId, duplicate.OwnerActorId);
            Equal(original.CanvasJson, duplicate.CanvasJson);
            Equal(original.MetadataJson, duplicate.MetadataJson);
            repository.Delete(connection, duplicate.Id);

            var duplicatedEpisode = episodeRepository.DuplicateEpisode(
                connection,
                original.EpisodeId,
                "Repository Episode");
            var episodeShot = repository.QueryByEpisode(connection, duplicatedEpisode.Id).Single();
            Equal(renderPreset.Id, episodeShot.RenderPresetId);
            Equal(original.OwnerActorId, episodeShot.OwnerActorId);
            Equal(original.CanvasJson, episodeShot.CanvasJson);
            Equal(original.MetadataJson, episodeShot.MetadataJson);
            episodeRepository.DeleteEpisode(connection, duplicatedEpisode.Id);

            repository.UpdateField(
                connection,
                original.Id,
                "shot.renderPresetId",
                original.RenderPresetId);
        }

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        using (var connection = context.OpenConnection())
        {
            Throws<InvalidOperationException>(() =>
                repository.UpdateField(connection, original.Id, "shot.canvas", "[]"));
            Throws<InvalidOperationException>(() =>
                repository.UpdateField(connection, original.Id, "shot.metadata", "[]"));
            Throws<InvalidOperationException>(() =>
                repository.UpdateDuration(connection, original.Id, 0));
            Throws<InvalidOperationException>(() =>
                repository.UpdateField(connection, "missing_shot", "shot.slug", "missing"));
        }
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ShotActorContextIsExplicit()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-shot-actor-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var tree = database.LoadProjectTree();
        var episode = Descendants(tree)
            .First((node) => node.Kind == ProjectTreeNodeKind.Episode && node.Id == "episode_002");
        Throws<InvalidOperationException>(() => database.AddChild(episode));
        Throws<InvalidOperationException>(() => database.AddShot(episode, ""));

        var shot = database.AddShot(episode, "actor_alex");
        Equal("actor_alex", database.GetShotSettings(shot.Id).OwnerActorId);
        var module = database.GetAvailableShotModules(shot.Id).First();
        var variant = database.GetModuleVariantOptions(module.Id).First();
        var screen = database.AddModuleInstance(
            shot,
            new SpikeDatabase.ShotModuleInstanceDraft(
                module,
                variant.Value,
                variant.Label,
                $"{module.Name} · {variant.Label}"));
        var before = database.GetModuleInstanceSettings(screen.Id);

        database.UpdateShotField(shot.Id, "shot.ownerActorId", "actor_sam");
        Equal("actor_sam", database.GetShotSettings(shot.Id).OwnerActorId);
        var after = database.GetModuleInstanceSettings(screen.Id);
        Equal(before.ContentJson, after.ContentJson);
        Equal(before.BehaviorJson, after.BehaviorJson);
        Equal(before.AnimationJson, after.AnimationJson);
        Equal(before.MetadataJson, after.MetadataJson);

        Throws<InvalidOperationException>(() => database.UpdateShotField(shot.Id, "shot.ownerActorId", ""));
        Throws<InvalidOperationException>(() => database.UpdateShotField(shot.Id, "shot.ownerActorId", "missing_actor"));
        Throws<InvalidOperationException>(() => database.UpdateActorField("actor_sam", "actor.defaultThemeId", ""));

        database.Delete(screen);
        database.Delete(shot);
        True(Descendants(database.LoadProjectTree()).All((node) => node.Id != shot.Id && node.Id != screen.Id));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ProductionShotContextBoundaryPreservesInheritedContext()
{
    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-context-data-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SpikeDatabase(temporary);
        var dataSource = new ProductionShotContextDataSource(database);
        var service = new ProductionShotContextService(dataSource);
        var shot = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.Shot);
        var shotSettings = database.GetShotSettings(shot.Id);
        var actor = database.GetActorSettings(shotSettings.OwnerActorId);
        var context = service.Resolve(shot.Id);

        True(context.IsValid);
        Equal("", context.Error);
        Equal(actor.DisplayName, context.Actor);
        Equal(database.GetDeviceSettings(actor.DefaultDeviceId).Name, context.Device);
        Equal(database.GetThemeSettings(actor.DefaultThemeId).Name, context.Theme);
        Equal(database.GetThemeFieldValue(actor.DefaultThemeId, "theme.defaultMode"), context.ThemeMode);
        True(service.CanExposeChildren(shot));
        True(shot.Children.All(service.IsNavigationNodeEnabled));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

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
        var screen = database.AddModuleInstance(shot, new SpikeDatabase.ShotModuleInstanceDraft(
            new SpikeDatabase.ShotModuleChoice(
                module.Id, module.Name, module.Parent!.Name, appId, module.RecordClassId),
            defaultVariant.Id,
            defaultVariant.Name,
            $"{module.Name} · {defaultVariant.Name}"));
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

static void LabelSubtextPlacementUsesCurrentContract()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var database = new SpikeDatabase(source);
    var settings = database.GetComponentClassSettings("component_project_foqn_s2_label");
    var label = JsonNode.Parse(settings.ConfigJson)?["label"]?.AsObject()
        ?? throw new InvalidOperationException("Missing current Label values.");
    True(label["subtextPlacement"] is null);
    True(label["subtextVerticalPosition"] is JsonValue);
    True(label["subtextHorizontalAlign"] is JsonValue);
    var subtextFields = database.LoadEditorLayout("component.label").Cards
        .SelectMany((card) => card.VisibleGroups)
        .Single((group) => group.Id == "labelSubtext")
        .VisibleFields.OrderBy((field) => field.Order).Select((field) => field.Id).ToList();
    SequenceEqual(
        ["component.label.textGapToken", "component.label.reserveSubtextSpace", "component.label.subtextVerticalPosition", "component.label.subtextHorizontalAlign", "component.label.subtextColorToken", "component.label.subtextTypography"],
        subtextFields);
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
        var forwardedInputs = ComponentPreviewInputSession.ReadRuntimeInputs(effective, config);
        Equal(0, forwardedInputs.Count((input) => input.Label is "Hora" or "Subtext" or "Password" or "Attempt"));
        var collections = ComponentPreviewInputSession.ReadRuntimeCollections(effective, config);
        var slots = collections.Single((collection) => collection.Id == "stackStates");
        var stateInputs = collections.Single((collection) => collection.Id == "stackStateInputs");
        var stateSelection = slots.Fields.Single((input) => input.Id == "runtimeStateId");
        Equal("name", slots.ItemPresentation?.TitleFieldId ?? "");
        Equal("name", stateInputs.ItemPresentation?.TitleFieldId ?? "");
        Equal("Initial", stateInputs.ItemPresentation?.FirstItemBadge ?? "");
        Equal(true, stateSelection.ActionOnly);
        Equal(true, stateSelection.Animation is not null);
        SequenceEqual(["hold"], stateSelection.Animation?.Interpolations.ToList() ?? []);
        Equal(false, stateSelection.Animation?.ExtendsOwnerDuration ?? true);
        Equal("collectionFooter", slots.AnimationPresentation);
        Equal(slots.JsonKey, stateInputs.UiParentCollectionJsonKey);
        Equal("slotId", stateInputs.UiParentItemIdJsonKey);
        Equal("inputs", stateInputs.ItemRuntimeContractJsonKey);
        var stateItems = DesignPreviewTestValues.CollectionItems(effective, stateInputs);
        True(stateItems.All((item) => !string.IsNullOrWhiteSpace(item["name"]?.GetValue<string>())));
        var passwordState = stateItems.Single((item) =>
            item["presetId"]?.GetValue<string>()?.Contains("_password::preset::", StringComparison.Ordinal) == true);
        var passwordContract = passwordState["inputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing projected Password State runtime contract.");
        var passwordInputs = ComponentPreviewInputSession.ReadRuntimeInputs(passwordContract, new JsonObject());
        var passwordTrigger = passwordInputs.Single((input) => input.Label == "Enter password" && input.ActionOnly);
        var passwordFrame = passwordInputs.Single((input) => input.Label == "Entry frame" && input.ActionOnly);
        var passwordTiming = passwordInputs.Single((input) => input.Label == "Entry timing");
        var passwordAttempt = passwordInputs.Single((input) => input.Label == "Attempt");
        Equal(true, passwordTrigger.Animation is not null);
        Equal(true, passwordFrame.Animation is null);
        Equal(passwordAttempt.Id, passwordTiming.BehaviorTiming?.SourceFieldId ?? "");
        var forwardedPasswordAction = ComponentPreviewActions.ReadWithEmbedded(
                effective,
                new ComponentPreviewInputDataSource(database).ComponentPresetRuntimeContract)
            .Single((action) => action.Label == "Enter password");
        Equal(passwordTrigger.JsonKey, forwardedPasswordAction.PlayInputId);
        Equal(passwordFrame.JsonKey, forwardedPasswordAction.TimeJsonKey);
        Equal(passwordTiming.Id, forwardedPasswordAction.DurationBehaviorTimingInputId);
        Equal(passwordState["id"]?.GetValue<string>(), forwardedPasswordAction.CollectionItemId);
        Equal(stateInputs.JsonKey, forwardedPasswordAction.CollectionJsonKey);
        Equal("inputs", forwardedPasswordAction.TargetJsonPath);
        var items = DesignPreviewTestValues.CollectionItems(effective, slots);
        Equal("Clock", items[0]?["name"]?.GetValue<string>() ?? "");
        Equal("Clock", items[0]?["alternatives"]?[0]?["name"]?.GetValue<string>() ?? "");
        Equal(2, (items[0]?["alternatives"] as JsonArray)?.Count ?? 0);
        var actions = ComponentPreviewActions.Read(effective);
        var stateAction = actions.Single((action) => action.CollectionJsonKey == slots.JsonKey
            && action.TargetInputId == "runtimeStateId"
            && action.CollectionItemId == items[0]?["id"]?.GetValue<string>());
        Equal("alternatives", stateAction.DurationStateCollectionJsonKey);
        Equal("enterMotion", stateAction.DurationEnterMotionJsonKey);
        Equal("exitMotion", stateAction.DurationExitMotionJsonKey);
        SequenceEqual(["theme.motion.reflowDurationMs"], stateAction.DurationAdditionalThemeTokens.ToList());

        var theme = database.LoadProjectTree().SelectMany(DescendantsAndSelf)
            .First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var payload = Required(CreatePreviewPayload(database, moduleVariant, theme.Id));
        var session = new ComponentPreviewInputSession(database, () => { });
        session.UpdateForPayload(payload, settings.ProjectId);
        var deletedStateId = items[0]?["alternatives"]?[1]?["id"]?.GetValue<string>() ?? "";
        True(session.TriggerAction(stateAction.Id, deletedStateId));
        var selected = session.ApplyInputs(payload, "light", settings.ProjectId);
        var selectedPreview = DesignPreviewTestValues.Parse(selected.DesignPreviewJson);
        Equal(deletedStateId, selectedPreview[slots.JsonKey]?[0]?["runtimeStateId"]?.GetValue<string>() ?? "");
        var themeTokens = JsonNode.Parse(payload.ThemeTokensJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Theme tokens.");
        var slide = themeTokens["motion"]?["transitions"]?["slide"] as JsonObject
            ?? throw new InvalidOperationException("Missing Slide timing.");
        var expectedDurationMs = Math.Max(
            (slide["delayMs"]?.GetValue<double>() ?? 0) + (slide["durationMs"]?.GetValue<double>() ?? 0),
            themeTokens["motion"]?["reflowDurationMs"]?.GetValue<double>() ?? 0);
        Equal(
            expectedDurationMs,
            ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(
                selectedPreview,
                stateAction,
                payload.ThemeTokensJson));

        var stackItems = config["lockScreen"]?["stackInputs"]?["items"]?.DeepClone() as JsonArray
            ?? throw new InvalidOperationException("Missing Lock Screen Stack items.");
        (stackItems[0]?["alternatives"] as JsonArray)?.RemoveAt(1);
        database.UpdateModuleVariantField(moduleVariant, "module.lockScreen.stackItems", stackItems.ToJsonString());
        var updatedPayload = Required(CreatePreviewPayload(database, moduleVariant, theme.Id));
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

static void ExplicitReferenceUsageIsExactTypedAndShared()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-explicit-usage-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var context = new SqliteProjectContext(temporary);
        IReferenceUsageService usageService = new ReferenceUsageService(context);
        using (var connection = context.OpenConnection())
        {
            var index = usageService.BuildIndex(connection);
            foreach (var node in nodes.Where((candidate) => candidate.Kind is ProjectTreeNodeKind.PaletteColor
                         or ProjectTreeNodeKind.Device
                         or ProjectTreeNodeKind.Actor
                         or ProjectTreeNodeKind.Theme
                         or ProjectTreeNodeKind.ProductionFont
                         or ProjectTreeNodeKind.IconTheme
                         or ProjectTreeNodeKind.RenderPreset
                         or ProjectTreeNodeKind.ComponentPreset
                         or ProjectTreeNodeKind.ModuleVariant))
            {
                Equal(node.IsUsed, index.ContainsKey(new ReferenceTarget(node.Kind, node.Id)));
            }
        }

        var usedDevice = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device && node.IsUsed);
        var deviceUsages = database.GetReferenceUsageDetails(usedDevice);
        True(deviceUsages.Any((usage) => usage.SourceKind == ProjectTreeNodeKind.Actor && usage.IsProduction));
        Throws<InvalidOperationException>(() => database.Delete(usedDevice));

        var actor = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Actor && node.IsUsed);
        var actorUsages = database.GetReferenceUsageDetails(actor);
        True(actorUsages.Any((usage) => usage.SourceKind == ProjectTreeNodeKind.Shot && usage.IsProduction));
        True(actorUsages.Any((usage) =>
            (usage.SourceKind is ProjectTreeNodeKind.ComponentClass
                or ProjectTreeNodeKind.Module
                or ProjectTreeNodeKind.ComponentPreset)
            && !usage.IsProduction));

        var usedComponentVariant = nodes
            .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentPreset)
            .Select((node) => (Node: node, Usages: database.GetReferenceUsageDetails(node)))
            .First((candidate) => candidate.Usages.Any((usage) => usage.SourceKind == ProjectTreeNodeKind.ComponentPreset));
        True(usedComponentVariant.Usages.Any((usage) =>
            usage.SourceKind == ProjectTreeNodeKind.ComponentPreset
            && usage.SourceNodeId.Contains("::preset::", StringComparison.Ordinal)));

        var usedModuleVariant = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleVariant && node.IsUsed);
        True(database.GetReferenceUsageDetails(usedModuleVariant).Any((usage) =>
            usage.SourceKind == ProjectTreeNodeKind.ModuleInstance && usage.IsProduction));

        const string designActorId = "actor_usage_design_only";
        const string productionActorId = "actor_usage_production_only";
        var projectId = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.Project).Id;
        using (var connection = context.OpenConnection())
        {
            SqliteCommandExecutor.Execute(
                connection,
                "INSERT INTO actors (id, project_id, display_name, short_name, metadata_json) VALUES ($id, $projectId, $name, $name, '{}')",
                ("$id", designActorId),
                ("$projectId", projectId),
                ("$name", "Design-only Usage Actor"));
            SqliteCommandExecutor.Execute(
                connection,
                "INSERT INTO actors (id, project_id, display_name, short_name, metadata_json) VALUES ($id, $projectId, $name, $name, '{}')",
                ("$id", productionActorId),
                ("$projectId", projectId),
                ("$name", "Production-only Usage Actor"));
            SqliteCommandExecutor.Execute(
                connection,
                "UPDATE modules SET design_preview_json = json_set(design_preview_json, '$.testValues.actorId', $actorId) WHERE id = 'module_project_foqn_s2_lock_screen'",
                ("$actorId", designActorId));
            SqliteCommandExecutor.Execute(
                connection,
                "UPDATE module_instances SET content_json = json_set(content_json, '$.actorId', $actorId) WHERE id = (SELECT id FROM module_instances WHERE module_id = 'module_project_foqn_s2_lock_screen' ORDER BY id LIMIT 1)",
                ("$actorId", productionActorId));
        }

        var designOnlyUsages = usageService.GetUsages(ProjectTreeNodeKind.Actor, designActorId);
        True(designOnlyUsages.Any((usage) =>
            usage.SourceKind == ProjectTreeNodeKind.Module
            && usage.Scope == ReferenceUsageScope.Design));
        True(designOnlyUsages.All((usage) => usage.Scope == ReferenceUsageScope.Design));
        var productionOnlyUsages = usageService.GetUsages(ProjectTreeNodeKind.Actor, productionActorId);
        True(productionOnlyUsages.Any((usage) =>
            usage.SourceKind == ProjectTreeNodeKind.ModuleInstance
            && usage.Scope == ReferenceUsageScope.Production));
        True(productionOnlyUsages.All((usage) => usage.Scope == ReferenceUsageScope.Production));

        var blue = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.PaletteColor && node.Name == "blue");
        using (var connection = context.OpenConnection())
        {
            SqliteCommandExecutor.Execute(
                connection,
                "UPDATE projects SET notes = $notes, metadata_json = $metadataJson",
                ("$notes", $"Unrelated prose blue plus substring prefix-{blue.Id}-suffix"),
                ("$metadataJson", "{\"comment\":\"blue\"}"));
        }
        var blueUsages = database.GetReferenceUsageDetails(blue);
        True(blueUsages.Count > 0);
        True(blueUsages.All((usage) => usage.SourceKind != ProjectTreeNodeKind.Project));

        var unusedRenderPreset = nodes.First((node) => node.Kind == ProjectTreeNodeKind.RenderPreset && !node.IsUsed);
        using (var connection = context.OpenConnection())
        {
            SqliteCommandExecutor.Execute(
                connection,
                "UPDATE projects SET notes = $notes, metadata_json = $metadataJson",
                ("$notes", $"Unrelated prefix-{unusedRenderPreset.Id}-suffix"),
                ("$metadataJson", $"{{\"comment\":\"{unusedRenderPreset.Id}\"}}"));
        }
        Equal(0, database.GetReferenceUsageDetails(unusedRenderPreset).Count);
        database.Delete(unusedRenderPreset);
        True(Descendants(database.LoadProjectTree()).All((node) => node.Id != unusedRenderPreset.Id));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void UsageNavigationPreservesTypedContext()
{
    var events = new List<string>();
    var messages = new RecordingMessageSink();
    var navigator = new EditorReferenceUsageNavigator(
        (workspace, nodeId) =>
        {
            events.Add($"select:{workspace}:{nodeId}");
            return true;
        },
        (embedded, nodeId) =>
        {
            events.Add($"embedded:{nodeId}:{embedded.SlotFieldId}");
            return Task.CompletedTask;
        },
        messages);
    var embeddedUsage = new SpikeDatabase.EmbeddedComponentUsage(
        "component_parent",
        "Parent",
        "parent",
        "parent.slot",
        "Slot",
        true,
        "component_parent::preset::default");

    navigator.Navigate(new SpikeDatabase.ReferenceUsageDetail(
        "component_parent::preset::default",
        ProjectTreeNodeKind.ComponentPreset,
        "Component Variant",
        "Parent · Default",
        "Slot · overrides",
        ReferenceUsageScope.Design,
        embeddedUsage)).GetAwaiter().GetResult();
    navigator.Navigate(new SpikeDatabase.ReferenceUsageDetail(
        "screen_1",
        ProjectTreeNodeKind.ModuleInstance,
        "Screen",
        "Screen 1",
        "Actor",
        ReferenceUsageScope.Production,
        null)).GetAwaiter().GetResult();

    SequenceEqual(
        new[]
        {
            "select:Design:component_parent::preset::default",
            "embedded:component_parent::preset::default:parent.slot",
            "select:Production:screen_1",
        },
        events);
    Equal(0, messages.Warnings.Count);
}

static void ProductionDataOwnsConcreteResources()
{
    var database = new SpikeDatabase(Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        "desktop-editor-spike.sqlite"));
    var project = database.LoadProjectTree().Single();
    var productionSections = EditorWorkspaceNavigation.SectionRoots(project, EditorWorkspace.Production);
    SequenceEqual(
        new[] { ProjectTreeNodeKind.EpisodesRoot, ProjectTreeNodeKind.ProductionDataRoot },
        productionSections.Select((node) => node.Kind));

    var productionData = productionSections.Single((node) => node.Kind == ProjectTreeNodeKind.ProductionDataRoot);
    SequenceEqual(
        new[]
        {
            ProjectTreeNodeKind.ActorsRoot,
            ProjectTreeNodeKind.DevicesRoot,
            ProjectTreeNodeKind.ProductionFontsRoot,
            ProjectTreeNodeKind.RenderPresetsRoot,
        },
        productionData.Children.Select((node) => node.Kind));
    True(productionData.Children.All((node) =>
        EditorNavigationMetadata.WorkspaceScope(node.Kind) == EditorWorkspaceScope.Production));
    True(productionData.Children.All((node) => EditorNavigationRenderer.ShowsActions(node, null)));

    var designSections = EditorWorkspaceNavigation.SectionRoots(project, EditorWorkspace.Design);
    True(designSections.Any((node) => node.Kind == ProjectTreeNodeKind.ThemesRoot));
    True(designSections.All((node) => node.Kind is not ProjectTreeNodeKind.DevicesRoot
        and not ProjectTreeNodeKind.ProductionFontsRoot
        and not ProjectTreeNodeKind.RenderPresetsRoot
        and not ProjectTreeNodeKind.ActorsRoot));
    var themeRoot = DescendantsAndSelf(project).Single((node) => node.Kind == ProjectTreeNodeKind.ThemesRoot);
    Equal(ProjectTreeNodeKind.SystemDataRoot, Required(themeRoot.Parent).Kind);
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

static void RuntimeControlsResolveActiveFrameValue()
{
    var document = EmptyDocument();
    document.AddTrack("state", "slot-1", JsonValue.Create("clock")!, "hold");
    document.UpsertKeyframe("state", "slot-1", 10, JsonValue.Create("password")!, "hold");
    var state = Required(document.Track("state", "slot-1"));
    Equal("clock", ModuleInstanceAnimationValueResolver.ResolveDisplayValue(
        state, 0, JsonValue.Create("base")!, ValueKind.OptionToken));
    Equal("clock", ModuleInstanceAnimationValueResolver.ResolveDisplayValue(
        state, 9, JsonValue.Create("base")!, ValueKind.OptionToken));
    Equal("password", ModuleInstanceAnimationValueResolver.ResolveDisplayValue(
        state, 10, JsonValue.Create("base")!, ValueKind.OptionToken));
    Equal("password", ModuleInstanceAnimationValueResolver.ResolveDisplayValue(
        state, 40, JsonValue.Create("base")!, ValueKind.OptionToken));

    var numericDocument = EmptyDocument();
    numericDocument.AddTrack("opacity", "", JsonValue.Create(0)!, "linear");
    numericDocument.UpsertKeyframe("opacity", "", 10, JsonValue.Create(1)!, "linear");
    Equal("0.5", ModuleInstanceAnimationValueResolver.ResolveDisplayValue(
        Required(numericDocument.Track("opacity", "")),
        5,
        JsonValue.Create(0)!,
        ValueKind.Decimal));
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

static void KeyframeMovesPreservePayloadAndProtectFrameZero()
{
    var document = EmptyDocument();
    document.AddTrack("value", "slot", JsonValue.Create("initial")!, "hold");
    document.UpsertKeyframe("value", "slot", 10, JsonValue.Create("moved")!, "easeInOut");
    var before = Required(document.Track("value", "slot")).Keyframes.Single((keyframe) => keyframe.Frame == 10);

    True(document.TryMoveKeyframe("value", "slot", 10, 15));
    var after = Required(document.Track("value", "slot")).Keyframes.Single((keyframe) => keyframe.Frame == 15);
    Equal(before.Id, after.Id);
    Equal("moved", after.Value!.GetValue<string>());
    Equal("easeInOut", after.Interpolation);
    True(after.Enabled);
    SequenceEqual([0, 15], Required(document.Track("value", "slot")).Keyframes.Select((keyframe) => keyframe.Frame));
    document.UpsertKeyframe("value", "slot", 20, JsonValue.Create("occupied")!, "hold");
    True(!document.TryMoveKeyframe("value", "slot", 0, 5));
    True(!document.TryMoveKeyframe("value", "slot", 15, 0));
    True(!document.TryMoveKeyframe("value", "slot", 15, 20));
    True(!document.TryMoveKeyframe("value", "slot", 15, 15));
}

static void KeyframeDragSnapsToScreenGrid()
{
    Equal(10, TimelineKeyframeDrag.ResolveScreenFrame(12.1, precise: false, 100, 500, []));
    Equal(12, TimelineKeyframeDrag.ResolveScreenFrame(12.1, precise: true, 100, 500, []));
    Equal(13, TimelineKeyframeDrag.ResolveScreenFrame(12.8, precise: false, 100, 500, [13]));
    Equal(100, TimelineKeyframeDrag.ResolveScreenFrame(99.9, precise: false, 100, 500, []));
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

static void ScreenDurationPolicyIsContractOwned()
{
    Equal(RuntimeDurationPolicy.Calculated, RuntimeDurationContract.Policy("{}"));
    var explicitContract = Object("""
        {"animationTimeline":{"durationPolicy":"explicit","defaultDurationFrames":240}}
        """);
    Equal(RuntimeDurationPolicy.Explicit, RuntimeDurationContract.Policy(explicitContract));
    Equal(240, RuntimeDurationContract.InitialDurationFrames(explicitContract.ToJsonString()));
    Throws<InvalidOperationException>(() => RuntimeDurationContract.InitialDurationFrames(
        "{\"animationTimeline\":{\"durationPolicy\":\"explicit\"}}"));
    Throws<InvalidOperationException>(() => RuntimeDurationContract.Policy(
        "{\"animationTimeline\":{\"durationPolicy\":\"legacy\"}}"));
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

static void ParallelCollectionTargetsShareScreenOrigin()
{
    var contract = Object("""
        {
          "collections": [{
            "jsonKey": "slots",
            "animationTimeline": {"sequenceItems":false},
            "fields": [{
              "id":"state",
              "jsonKey":"state",
              "animationTimeline":{"origin":{"kind":"ownerStart"},"extendsOwnerDuration":false}
            }]
          }]
        }
        """);
    var runtime = Object("""
        {"slots":[
          {"id":"slot-1","state":"clock"},
          {"id":"slot-2","state":"password"}
        ]}
        """);

    Equal(0, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, "state", "slot-1"));
    Equal(0, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, "state", "slot-2"));
}

static void EntityFieldsKeepFirstAppearanceOrigin()
{
    var contract = Object("""
        {
          "collections": [
            {
              "jsonKey": "slots",
              "animationTimeline": {"sequenceItems":false},
              "fields": [{"id":"state","jsonKey":"runtimeStateId","animationTimeline":{"extendsOwnerDuration":false}}]
            },
            {
              "jsonKey": "states",
              "animationTimeline": {
                "sequenceItems": false,
                "ownerOrigin": {
                  "kind": "firstMatchingValue",
                  "sourceCollectionJsonKey": "slots",
                  "sourceTargetIdJsonKey": "slotId",
                  "sourceFieldId": "state",
                  "sourceValueJsonKey": "runtimeStateId",
                  "matchValueJsonKey": "id"
                }
              },
              "fields": [
                {"id":"slotId","jsonKey":"slotId"},
                {"id":"text","jsonKey":"text"}
              ]
            }
          ]
        }
        """);
    var runtime = Object("""
        {
          "slots":[{"id":"slot-1","runtimeStateId":"state-clock"}],
          "states":[
            {"id":"state-password","slotId":"slot-1","text":"Password"},
            {"id":"state-clock","slotId":"slot-1","text":"Clock"}
          ]
        }
        """);
    var animation = Object("""
        {"schemaVersion":2,"tracks":[
          {"id":"selector","fieldId":"state","targetId":"slot-1","keyframes":[
            {"id":"selector-0","frame":0,"value":"state-clock","enabled":true},
            {"id":"selector-10","frame":10,"value":"state-password","enabled":true},
            {"id":"selector-30","frame":30,"value":"state-clock","enabled":true},
            {"id":"selector-40","frame":40,"value":"state-password","enabled":true}
          ]},
          {"id":"password-text","fieldId":"text","targetId":"state-password","keyframes":[
            {"id":"password-text-0","frame":0,"value":"Password","enabled":true},
            {"id":"password-text-5","frame":5,"value":"Ready","enabled":true}
          ]}
        ]}
        """);

    Equal(0, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, animation, "text", "state-clock"));
    Equal(10, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, animation, "text", "state-password"));
    Equal(15, RuntimeAnimationFrameOrigin.ScreenFrame(contract, runtime, animation, "text", "state-password", 5));
    Equal(5d, RuntimeAnimationFrameOrigin.LocalFrame(contract, runtime, animation, "text", "state-password", 15));
    Equal(30d, RuntimeAnimationFrameOrigin.LocalFrame(contract, runtime, animation, "text", "state-password", 40));
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

static void StrictValidationRejectsMissingOrigin()
{
    var animation = Object("""{"schemaVersion":2,"tracks":[{"id":"track","fieldId":"text","targetId":"m1","keyframes":[]}]}""");
    Throws<InvalidOperationException>(() => InvokeDatabaseStatic("ValidateAnimationJson", animation, "instance"));
}

static void LegacyAnimationRequiresExplicitMigration()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-legacy-animation-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE module_instances SET animation_json = '{\"schemaVersion\":1,\"tracks\":[]}' WHERE id = (SELECT id FROM module_instances ORDER BY id LIMIT 1)";
            command.ExecuteNonQuery();
        }

        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => _ = new SpikeDatabase(temporary));
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void AnimatableFieldVocabularyIsConstrained()
{
    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var database = new SpikeDatabase(source);
    var module = database.LoadProjectTree()
        .SelectMany(DescendantsAndSelf)
        .Single((node) => node.Kind == ProjectTreeNodeKind.Module
            && database.GetModuleSettings(node.Id).RecordClassId == "module.core.chat");
    var settings = database.GetModuleSettings(module.Id);
    var preview = JsonPath.ParseRequiredObject(
        settings.DesignPreviewJson,
        $"Module '{module.Id}' design_preview_json");
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

static void TimelineFrameUpdatesSuppressOwnPlaybackFeedback()
{
    var state = new PreviewPlaybackState();
    var gate = new TimelineFrameUpdateGate();
    var externalRefreshes = 0;
    state.Changed += () =>
    {
        if (!gate.IsActive) externalRefreshes++;
    };

    gate.Run(state.NotifyFrameChanged);
    Equal(0, externalRefreshes);
    True(!gate.IsActive);

    state.NotifyFrameChanged();
    Equal(1, externalRefreshes);

    Throws<InvalidOperationException>(() => gate.Run(() => throw new InvalidOperationException("test")));
    True(!gate.IsActive);
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

static void AppAndModuleDefinitionsExposeRenameOnlyLifecycleActions()
{
    var appsRoot = new ProjectTreeNode(ProjectTreeNodeKind.AppsRoot, "apps", "Apps", "", "navigation.apps");
    var app = new ProjectTreeNode(ProjectTreeNodeKind.App, "app", "System", "", "app.system", appsRoot);
    var module = new ProjectTreeNode(
        ProjectTreeNodeKind.Module, "module", "Lock Screen", "", "module.core.lockScreen", app);
    var defaultVariant = new ProjectTreeNode(
        ProjectTreeNodeKind.ModuleVariant, "module::variant::default", "Default", "", "module.variant", module,
        isProtected: true);
    var customVariant = new ProjectTreeNode(
        ProjectTreeNodeKind.ModuleVariant, "module::variant::custom", "Custom", "", "module.variant", module);

    True(!appsRoot.CanAddChild);
    True(app.CanRenameDirectly);
    True(!app.CanAddChild);
    True(!app.CanDuplicate);
    True(!app.CanDelete);
    True(module.CanRenameDirectly);
    True(!module.CanAddChild);
    True(!module.CanDuplicate);
    True(!module.CanDelete);
    True(defaultVariant.CanRenameDirectly);
    True(defaultVariant.CanDuplicate);
    True(!defaultVariant.CanDelete);
    True(customVariant.CanRenameDirectly);
    True(customVariant.CanDuplicate);
    True(customVariant.CanDelete);

    var source = Path.Combine(Directory.GetCurrentDirectory(), "data", "desktop-editor-spike.sqlite");
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-definition-lifecycle-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SpikeDatabase(temporary);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var currentAppsRoot = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.AppsRoot);
        var currentApp = nodes.Single((node) => node.Id == "app_core_chat");
        var currentModule = nodes.Single((node) => node.Id == "module_core_chat");
        var currentDefaultVariant = currentModule.Children.Single((node) => node.IsProtected);

        Throws<InvalidOperationException>(() => database.AddChild(currentAppsRoot));
        Throws<InvalidOperationException>(() => database.AddChild(currentModule));
        Throws<InvalidOperationException>(() => database.Duplicate(currentApp));
        Throws<InvalidOperationException>(() => database.Duplicate(currentModule));
        Throws<InvalidOperationException>(() => database.Delete(currentApp));
        Throws<InvalidOperationException>(() => database.Delete(currentModule));

        var renamedApp = database.RenameDirectNode(currentApp, "Chat renamed");
        var renamedModule = database.RenameDirectNode(currentModule, "Conversation renamed");
        var renamedDefaultVariant = database.RenameDirectNode(currentDefaultVariant, "Primary");
        Equal(currentApp.Id, renamedApp.Id);
        Equal(currentModule.Id, renamedModule.Id);
        Equal(currentDefaultVariant.Id, renamedDefaultVariant.Id);
        Equal("Chat renamed", renamedApp.Name);
        Equal("Conversation renamed", renamedModule.Name);
        Equal("Primary", renamedDefaultVariant.Name);

        var copiedVariant = database.Duplicate(renamedDefaultVariant);
        True(copiedVariant.Id != renamedDefaultVariant.Id);
        True(copiedVariant.CanDelete);
        database.Delete(copiedVariant);
        Throws<InvalidOperationException>(() => database.Delete(renamedDefaultVariant));

        var reloaded = Descendants(database.LoadProjectTree()).ToList();
        Equal("Chat renamed", reloaded.Single((node) => node.Id == currentApp.Id).Name);
        Equal("Conversation renamed", reloaded.Single((node) => node.Id == currentModule.Id).Name);
        Equal("Primary", reloaded.Single((node) => node.Id == currentDefaultVariant.Id).Name);
    }
    finally
    {
        File.Delete(temporary);
    }
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
        var slotCollection = collections.OfType<JsonObject>().Single();
        Equal("items", slotCollection["jsonKey"]?.GetValue<string>() ?? "");
        Equal(false, slotCollection["animationTimeline"]?["sequenceItems"]?.GetValue<bool>() ?? true);
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
        var payload = Required(CreatePreviewPayload(database, defaultVariant, theme.Id));
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
            new RuntimeInputOptionsDataSource(database),
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
        var otherPayload = Required(CreatePreviewPayload(database, childVariantNode, theme.Id));
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
        var embeddedDocuments = new EmbeddedComponentDocumentStore(database);
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
        True(embeddedDocuments.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);
        embeddedDocuments.CommitFieldValue(runtimeContext, "component.audio.padding", "theme.spacing.xl|theme.spacing.l");
        Equal(1, runtimeOverrideChanges);
        True(!embeddedDocuments.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);
        embeddedDocuments.CommitFieldValue(runtimeContext, "component.audio.padding", "inherited");
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
        _ = embeddedDocuments.CreateFieldValue(nestedRuntimeContext, nestedFieldId);
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
            _ = embeddedDocuments.CreateFieldValue(avatarContext, avatarFieldId);
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
        var populatedPayloadSource = Required(CreatePreviewPayload(database, defaultVariant, theme.Id));
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
        var payload = Required(CreatePreviewPayload(database, variants[0], theme.Id));
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
            var payload = Required(CreatePreviewPayload(database, variant, theme.Id));
            var inputSession = new ComponentPreviewInputSession(database, () => { });
            inputSession.UpdateForPayload(payload, database.GetComponentClassSettings(variant.Parent!.Id).ProjectId);
            var html = WebDesignPreviewRenderer.RenderBodyAsync(
                database.GetDevicePreviewMetrics(device.Id), "light", false,
                inputSession.ApplyInputs(payload, "light", database.GetComponentClassSettings(variant.Parent.Id).ProjectId)).GetAwaiter().GetResult();
            True(!html.Contains("preview-error", StringComparison.Ordinal));
        }

        var transitionPayload = Required(CreatePreviewPayload(database, notificationVariant, theme.Id));
        var transitionPreview = JsonNode.Parse(transitionPayload.DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing Notification transition preview.");
        var transitionAction = ComponentPreviewActions.ReadWithEmbedded(
                transitionPreview,
                new ComponentPreviewInputDataSource(database).ComponentPresetRuntimeContract)
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
        var wrappingPayload = Required(CreatePreviewPayload(database, notificationVariant, theme.Id));
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
        var populated = Required(CreatePreviewPayload(database, notificationsVariant, theme.Id));
        var populatedSession = new ComponentPreviewInputSession(database, () => { });
        populatedSession.UpdateForPayload(populated, settings.ProjectId);
        var populatedContract = JsonNode.Parse(populated.DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing populated Notifications contract.");
        var embeddedActions = ComponentPreviewActions.ReadWithEmbedded(
                populatedContract,
                new ComponentPreviewInputDataSource(database).ComponentPresetRuntimeContract)
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
        var payload = Required(CreatePreviewPayload(database, defaultVariant, theme.Id));
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
            ["initialText", "correctText", "incorrectText", "expectedPassword", "attemptPassword", "enabled", "entryTiming", "entryTrigger", "entryFrame"],
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
        True(passwordConfig["initialText"] is null);
        True(passwordConfig["correctText"] is null);
        True(passwordConfig["incorrectText"] is null);
        True(runtimeInputs.Single((input) => input.Id == "entryTrigger").Animation is not null);
        True(runtimeInputs.Single((input) => input.Id == "entryTrigger").ActionOnly);
        True(runtimeInputs.Single((input) => input.Id == "entryFrame").ActionOnly);

        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(CreatePreviewPayload(database, defaultVariant, theme.Id));
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
            var componentPayload = Required(CreatePreviewPayload(database, componentVariant, theme.Id));
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
            var modePayload = Required(CreatePreviewPayload(database, variant, theme.Id));
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
        foreach (var screen in nodes.Where((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance))
        {
            var animation = JsonNode.Parse(database.GetModuleInstanceSettings(screen.Id).AnimationJson) as JsonObject;
            foreach (var track in animation?["tracks"]?.AsArray().OfType<JsonObject>()
                         .Where((track) => track["fieldId"]?.GetValue<string>() == "runtimeStateId") ?? [])
            {
                var frameZero = track["keyframes"]?.AsArray().OfType<JsonObject>()
                    .SingleOrDefault((keyframe) => keyframe["frame"]?.GetValue<int>() == 0);
                True(!string.IsNullOrWhiteSpace(frameZero?["value"]?.GetValue<string>()));
            }
        }
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
        Equal("explicit", preview["animationTimeline"]?["durationPolicy"]?.GetValue<string>() ?? "");
        Equal(240, preview["animationTimeline"]?["defaultDurationFrames"]?.GetValue<int>() ?? 0);
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
        var forwardedSlots = stackInputs[RuntimeInputForwardingContract.StorageKey]?["items"]?["collection"]
            ?? throw new InvalidOperationException("Missing forwarded Lock Screen slot contract.");
        Equal(false, forwardedSlots["animationTimeline"]?["sequenceItems"]?.GetValue<bool>() ?? true);
        var stateOwnerOrigin = stackInputs[RuntimeInputForwardingContract.StorageKey]?["items"]?["projection"]?["childCollection"]?["animationTimeline"]?["ownerOrigin"]
            ?? throw new InvalidOperationException("Missing forwarded Lock Screen State owner origin.");
        Equal("firstMatchingValue", stateOwnerOrigin["kind"]?.GetValue<string>() ?? "");
        Equal("forwarded_module_lockScreen_stackStates", stateOwnerOrigin["sourceCollectionJsonKey"]?.GetValue<string>() ?? "");
        Equal("runtimeStateId", stateOwnerOrigin["sourceFieldId"]?.GetValue<string>() ?? "");
        var defaultVariant = module.Children.Single((child) => child.Kind == ProjectTreeNodeKind.ModuleVariant && child.IsProtected);
        var variantConfig = JsonNode.Parse(database.GetModuleVariantSettings(defaultVariant).ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen Variant config.");
        Equal(false,
            variantConfig["lockScreen"]?["stackInputs"]?[RuntimeInputForwardingContract.StorageKey]?["items"]?["collection"]?["animationTimeline"]?["sequenceItems"]?.GetValue<bool>()
            ?? true);
        Equal("firstMatchingValue",
            variantConfig["lockScreen"]?["stackInputs"]?[RuntimeInputForwardingContract.StorageKey]?["items"]?["projection"]?["childCollection"]?["animationTimeline"]?["ownerOrigin"]?["kind"]?.GetValue<string>()
            ?? "");
        var lockScreenFields = database.LoadEditorLayout("module.core.lockScreen").Cards
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .Select((field) => field.Id)
            .ToHashSet(StringComparer.Ordinal);
        True(lockScreenFields.Contains("module.lockScreen.stackInputs"));
        True(lockScreenFields.Contains("module.lockScreen.stackItems"));

        var lockScreenInstance = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance
            && database.GetModuleInstanceSettings(node.Id).ModuleId == module.Id);
        var values = new RecordClassFieldValueService(database);
        True(values.CreateFieldValue(lockScreenInstance, "moduleInstance.durationFrames").Definition.IsEditable);
        Equal(240, ModuleInstanceTimeline.DurationFrames(new ModuleInstanceTimelineDataSource(database), lockScreenInstance.Id));
        database.UpdateModuleInstanceField(lockScreenInstance.Id, "moduleInstance.durationFrames", "180");
        Equal(180, ModuleInstanceTimeline.DurationFrames(new ModuleInstanceTimelineDataSource(database), lockScreenInstance.Id));

        var conversationInstance = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance
            && database.GetModuleSettings(database.GetModuleInstanceSettings(node.Id).ModuleId).RecordClassId == "module.core.chat");
        True(!values.CreateFieldValue(conversationInstance, "moduleInstance.durationFrames").Definition.IsEditable);
        Throws<InvalidOperationException>(() => database.UpdateModuleInstanceField(
            conversationInstance.Id,
            "moduleInstance.durationFrames",
            "180"));

        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var instanceVariantConfig = JsonNode.Parse(
            database.GetModuleInstanceVariantSettings(lockScreenInstance.Id).ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen instance Variant config.");
        var instanceStackInputs = instanceVariantConfig["lockScreen"]?["stackInputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen instance Stack inputs.");
        var configuredStackSlots = instanceStackInputs["items"] as JsonArray
            ?? throw new InvalidOperationException("Missing configured Lock Screen Stack slots.");
        var passwordState = configuredStackSlots.OfType<JsonObject>()
            .SelectMany((slot) => slot["alternatives"]?.AsArray().OfType<JsonObject>()
                .Select((state) => (Slot: slot, State: state)) ?? [])
            .Single((candidate) => (candidate.State["presetId"]?.GetValue<string>() ?? "")
                .Contains("_password::preset::", StringComparison.Ordinal));
        var passwordSlotId = passwordState.Slot["id"]?.GetValue<string>() ?? "";
        var passwordStateId = passwordState.State["id"]?.GetValue<string>() ?? "";
        var instancePreview = DesignPreviewTestValues.Parse(
            database.GetModuleInstanceRuntimePreviewJson(lockScreenInstance.Id));
        var stateInputsCollection = ComponentPreviewInputSession
            .ReadRuntimeCollections(instancePreview, instanceVariantConfig)
            .Single((collection) => collection.Id == "stackStateInputs");
        var projectedPasswordState = DesignPreviewTestValues
            .CollectionItems(instancePreview, stateInputsCollection)
            .Single((state) => state["id"]?.GetValue<string>() == passwordStateId);
        var projectedPasswordInputs = projectedPasswordState["inputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing projected Password runtime inputs.");
        var passwordEntryTrigger = ComponentPreviewInputSession
            .ReadRuntimeInputs(projectedPasswordInputs, new JsonObject())
            .Single((input) => input.Label == "Enter password" && input.ActionOnly);
        var instanceAnimation = new ModuleInstanceAnimationDocument(
            database.GetModuleInstanceSettings(lockScreenInstance.Id).AnimationJson);
        if (!instanceAnimation.HasTrack("runtimeStateId", passwordSlotId))
        {
            var initialStateId = passwordState.Slot["alternatives"]?.AsArray()
                .OfType<JsonObject>()
                .First()["id"]?.GetValue<string>() ?? "";
            instanceAnimation.AddTrack(
                "runtimeStateId",
                passwordSlotId,
                JsonValue.Create(initialStateId)!,
                "hold");
        }
        instanceAnimation.UpsertKeyframe(
            "runtimeStateId",
            passwordSlotId,
            24,
            JsonValue.Create(passwordStateId)!,
            "hold");
        instanceAnimation.RemoveTrack(passwordEntryTrigger.Id, passwordStateId);
        instanceAnimation.AddTrack(
            passwordEntryTrigger.Id,
            passwordStateId,
            JsonValue.Create(true)!,
            "hold");
        database.UpdateModuleInstanceAnimationJson(lockScreenInstance.Id, instanceAnimation.ToJson());
        var passwordFramePayload = Required(CreatePreviewPayload(
            database,
            lockScreenInstance,
            theme.Id,
            timelineFrame: ModuleInstanceTimeline.ScreenStartFrame(new ModuleInstanceTimelineDataSource(database), lockScreenInstance.Id) + 30));
        var passwordFrameHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            passwordFramePayload).GetAwaiter().GetResult();
        if (passwordFrameHtml.Contains("preview-error", StringComparison.Ordinal))
            throw new InvalidOperationException("Password transition frame contains a preview error.");
        if (!passwordFrameHtml.Contains("Enter password", StringComparison.Ordinal))
            throw new InvalidOperationException("Password action reached its final state before its declared duration.");
        Equal(
            1,
            passwordFrameHtml.Split(
                "data-renderable-id=\"component.password.indicator.initial.filled\"",
                StringSplitOptions.None).Length - 1);
        var completedPasswordPayload = Required(CreatePreviewPayload(
            database,
            lockScreenInstance,
            theme.Id,
            timelineFrame: ModuleInstanceTimeline.ScreenStartFrame(new ModuleInstanceTimelineDataSource(database), lockScreenInstance.Id) + 40));
        var completedPasswordHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            "light",
            false,
            completedPasswordPayload).GetAwaiter().GetResult();
        if (completedPasswordHtml.Contains("preview-error", StringComparison.Ordinal))
            throw new InvalidOperationException("Completed Password frame contains a preview error.");
        if (!completedPasswordHtml.Contains("Password correct", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Completed Password frame does not contain the correct-password state " +
                $"(initial={completedPasswordHtml.Contains("Enter password", StringComparison.Ordinal)}, " +
                $"incorrect={completedPasswordHtml.Contains("Password incorrect", StringComparison.Ordinal)}).");

        var payload = Required(CreatePreviewPayload(database, module, theme.Id));
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
                ["name"] = "Clock",
                ["alternatives"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "lock_screen_label_default",
                        ["name"] = "Clock",
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
        var populatedPayload = Required(CreatePreviewPayload(database, module, theme.Id));
        var populatedSession = new ComponentPreviewInputSession(database, () => { });
        populatedSession.UpdateForPayload(populatedPayload, settings.ProjectId);
        var populatedPreview = JsonNode.Parse(populatedPayload.DesignPreviewJson) as JsonObject ?? new JsonObject();
        var populatedConfig = JsonNode.Parse(populatedPayload.ConfigJson) as JsonObject ?? new JsonObject();
        var populatedCollections = ComponentPreviewInputSession.ReadRuntimeCollections(populatedPreview, populatedConfig);
        var populatedStateInputs = populatedCollections.Single((collection) => collection.Id == "stackStateInputs");
        var populatedStateItems = DesignPreviewTestValues.CollectionItems(populatedPreview, populatedStateInputs)
            .Select((item) => item.DeepClone() as JsonObject ?? new JsonObject())
            .ToList();
        var populatedState = populatedStateItems.Single((item) => item["id"]?.GetValue<string>() == "lock_screen_label_default");
        var populatedStateContract = populatedState["inputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing populated State runtime contract.");
        var populatedInputs = ComponentPreviewInputSession.ReadRuntimeInputs(populatedStateContract, new JsonObject());
        var forwardedSubtitle = populatedInputs.Single((input) => input.Label == "Lock subtitle");
        populatedStateContract[forwardedSubtitle.JsonKey] = "Forwarded subtitle";
        populatedSession.SetExternalCollectionItems(populatedStateInputs.JsonKey, populatedStateItems);
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
          {"id":"name","label":"Name","jsonKey":"name","kind":"text","valueKind":"StringSingleLine","defaultValue":""},
          {"id":"direction","label":"Direction","jsonKey":"direction","kind":"option","valueKind":"OptionToken","defaultValue":"incoming","options":[{"value":"incoming","label":"Incoming"}]},
          {"id":"text","label":"Text","jsonKey":"text","kind":"text","valueKind":"StringSingleLine","defaultValue":""},
          {"id":"mediaType","label":"Media","jsonKey":"mediaType","kind":"option","valueKind":"OptionToken","defaultValue":"none"}
        ],"itemPresentation":{"titleFieldId":"name","firstItemBadge":"Initial","subtitleFieldIds":["direction","text"],"subtitleMaxCharacters":24,"iconFieldId":"mediaType","fallbackIcon":"message","iconValueMap":{"image":"image"}}}]}
        """);
    var collection = ComponentPreviewInputSession.ReadRuntimeCollections(preview, new JsonObject()).Single();
    var presentation = RuntimeCollectionItemPresentation.Resolve(
        collection,
        Object("""{"name":"Welcome","direction":"incoming","text":"A message with enough words to be abbreviated","mediaType":"image"}"""),
        0,
        "Message 1",
        "Payload item 1",
        EditorIcons.Component);

    Equal("Welcome · Initial", presentation.Title);
    Equal("Incoming · A message wi…", presentation.Subtitle);
    Equal(EditorIcons.Image, presentation.Icon);
}

static void ScreenTreeNodesKeepActionsInEditor()
{
    var screen = new ProjectTreeNode(ProjectTreeNodeKind.ModuleInstance, "screen", "Screen", "", "module_instance");
    True(screen.CanDuplicate);
    True(screen.CanDelete);
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
static DesignPreviewPayload? CreatePreviewPayload(
    SpikeDatabase database,
    ProjectTreeNode? node,
    string? themeId,
    string themeMode = "light",
    int timelineFrame = 0) =>
    DesignPreviewPayloadFactory.Create(
        new DesignPreviewPayloadDataSource(database),
        node,
        themeId,
        themeMode,
        timelineFrame);
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

internal sealed class RecordingMessageSink : IEditorShellMessageSink
{
    public List<string> Warnings { get; } = [];

    public void Clear() { }

    public void Info(string area, string message) { }

    public void Warning(string area, string message)
    {
        Warnings.Add($"{area}: {message}");
    }

    public void Error(string area, Exception exception) { }

    public void Error(string area, string message) { }
}
