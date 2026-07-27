using Microsoft.Data.Sqlite;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using Mockups.DesktopEditorShell.Integrations.ProductionOutput;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

if (args.Length == 2
    && args[0].Equals(
        "--desktop-instance-lifetime-probe",
        StringComparison.Ordinal))
{
    var visualLifetimeStarted = false;
    var ran = DesktopVisualInstanceLease.TryRun(
        () => visualLifetimeStarted = true,
        args[1]);
    Environment.ExitCode = (ran, visualLifetimeStarted) switch
    {
        (false, false) => 23,
        (true, true) => 0,
        _ => 24,
    };
    return;
}

var selectedManifestOwners = new HashSet<string>(
    StringComparer.Ordinal);

var tests = new (string Name, Action Run)[]
{
    ("v2 document rejects malformed roots", RejectsMalformedDocuments),
    ("opening an existing desktop database is byte-for-byte read-only", ExistingDatabaseOpenIsReadOnly),
    ("rejected databases remain byte-for-byte unchanged", RejectedDatabaseOpenIsReadOnly),
    ("Project-owned references reject cross-Project reads and writes", ProjectOwnedReferencesRejectCrossProjectValues),
    ("current editor layouts reject retired or incomplete roots read-only", CurrentEditorLayoutContractFailsReadOnly),
    ("persisted JSON roots reject blank malformed and wrong shapes", PersistedJsonRootsAreStrict),
    ("incomplete Component and Module Variants fail read-only", IncompleteVariantsFailReadOnly),
    ("Status and Navigation Bar configs fail strictly read-only", SystemBarComponentContractsFailReadOnly),
    ("List and List Item fixed contracts fail strictly read-only", ListComponentContractsFailReadOnly),
    ("Module definitions and Variants use owner config contracts", ModuleConfigsUseOwnerContracts),
    ("Variant writes never repair missing Variant arrays", VariantWritesDoNotRepairMissingArrays),
    ("system bar items use fixed dictionary collections on every Variant", SystemBarItemsUseFixedDictionaryCollections),
    ("editor layout saves only authored card metadata", EditorLayoutSaveKeepsOnlyAuthoredCardMetadata),
    ("extracted repositories preserve the focused port contract", ExtractedRepositoriesPreserveFocusedContract),
    ("resource repositories preserve Palette Device and Actor contracts", ResourceRepositoriesPreserveFocusedContract),
    ("Actor preview data boundary preserves current values read-only", ActorPreviewDataBoundaryPreservesCurrentValues),
    ("Actor preview surfaces share initials identity", ActorPreviewSurfacesShareInitialsIdentity),
    ("Runtime Input option boundary preserves dictionary options read-only", RuntimeInputOptionBoundaryPreservesDictionaryOptions),
    ("fixed Component boundaries use one exact class and its Default Variant", FixedComponentBoundariesUseExactDefaultVariant),
    ("Runtime Input kind and ValueKind share one exact contract", RuntimeInputKindAndValueKindShareOneContract),
    ("Runtime Input defaults use their exact ValueKind owner", RuntimeInputDefaultsUseValueKindOwner),
    ("Text Box Preview resolves Variant-owned Icon Row slots", TextBoxPreviewResolvesVariantOwnedIconRowSlots),
    ("Runtime Input readers reject filtered definition metadata", RuntimeInputDefinitionReadersAreStrict),
    ("pair fields require explicit presentation labels", PairFieldsRequireExplicitLabels),
    ("numeric dictionary fields separate current values from drafts", NumericDictionaryFieldsSeparateCurrentValuesFromDrafts),
    ("valid integer pairs commit after a short editing pause", ValidIntegerPairsCommitAfterEditingPause),
    ("Icon Row preserves sequential nested Button Override commits", IconRowPreservesSequentialNestedButtonOverrideCommits),
    ("Design Preview actions reject incomplete declarative contracts", PreviewActionContractsAreStrict),
    ("Runtime Input forwarding envelopes reject invalid current shapes", RuntimeInputForwardingEnvelopesAreStrict),
    ("Design Test Values preserve strict transient documents", DesignTestValuesPreserveStrictDocuments),
    ("dictionary field context boundary preserves current data read-only", DictionaryFieldContextBoundaryPreservesCurrentData),
    ("Typography Style keeps only its explicit inherited sentinels", TypographyStyleKeepsOnlyExplicitSentinels),
    ("embedded Component document store preserves Variant and local Override ownership", EmbeddedComponentDocumentStorePreservesOwnership),
    ("failed Runtime Override persistence restores the confirmed document", FailedRuntimeOverridePersistenceRestoresConfirmedDocument),
    ("editor presentation context boundary preserves current data read-only", EditorPresentationContextBoundaryPreservesCurrentData),
    ("Production Screen presentation boundary preserves exact current data read-only", ProductionScreenPresentationBoundaryPreservesCurrentData),
    ("Production active Screen presentation follows exact Shot frame ranges", ProductionActiveScreenPresentationFollowsShotFrames),
    ("Component Preview input boundary preserves current contracts read-only", ComponentPreviewInputBoundaryPreservesCurrentContracts),
    ("Runtime Input owner store preserves current documents and explicit Preview writes", RuntimeInputOwnerStorePreservesCurrentDocuments),
    ("Runtime Input instance store preserves explicit scalar collection and animation writes", RuntimeInputInstanceStorePreservesExplicitWrites),
    ("Preview visual context boundary preserves options metrics and media root read-only", PreviewVisualContextBoundaryPreservesResolvedResources),
    ("Production Preview session boundary preserves Shot and Screen data read-only", ProductionPreviewSessionBoundaryPreservesCurrentData),
    ("Module Instance animation store preserves current documents and explicit writes", ModuleInstanceAnimationStorePreservesCurrentDocuments),
    ("failed animation commands restore the last confirmed document", FailedAnimationCommandRestoresConfirmedDocument),
    ("rapid animation commands serialize against the latest confirmed document", RapidAnimationCommandsUseLatestConfirmedDocument),
    ("Theme repository preserves current documents and lifecycle", ThemeRepositoryPreservesFocusedContract),
    ("Production Font repository preserves current rows and lifecycle", ProductionFontRepositoryPreservesFocusedContract),
    ("Production Font file documents reject filtered or inferred values", ProductionFontFileDocumentsAreStrict),
    ("Icon Theme repository preserves rows and strict token files", IconThemeRepositoryPreservesFocusedContract),
    ("App and Module repository preserves definitions and Rename-only lifecycle", AppModuleRepositoryPreservesFocusedContract),
    ("Component Class repository preserves current definitions and Variants", ComponentClassRepositoryPreservesFocusedContract),
    ("Component dictionary fields use exact ValueKind documents", ComponentDictionaryFieldsUseExactValueKinds),
    ("record scalar writes reject invalid booleans and numbers", RecordScalarWritesRejectInvalidValues),
    ("resource scalar reads reject wrong current JSON shapes", ResourceScalarReadsRejectWrongShapes),
    ("Module Instance repository preserves Screen rows and prepared documents", ModuleInstanceRepositoryPreservesFocusedContract),
    ("Shot repository preserves its focused Production contract", ShotRepositoryPreservesFocusedContract),
    ("Production Output generates exact Shot names and portable render routes", ProductionOutputGeneratesExactShotPlans),
    ("Render output naming reserves one version for Light and Dark", RenderOutputNamingReservesOneBatchVersion),
    ("MOV H.264 modes match the Créditos encoding profiles", MovH264ModesMatchCreditosProfiles),
    ("Production render overrides Device and Theme while respecting forced Screen appearance", ProductionRenderOverridesRespectScreenAppearance),
    ("Render snapshot store interns repeated font assets", RenderSnapshotStoreInternsAssets),
    ("Render Queue persists and completes batch children independently", RenderQueueChildrenAreIndependent),
    ("Render Queue is a permanent Production surface and Shot action stays available", RenderQueueNavigationAndSurfaceAreAlwaysAvailable),
    ("Render executor publishes a clean PNG sequence", RenderExecutorPublishesCleanPngSequence),
    ("Shots require an explicit replaceable owner Actor", ShotActorContextIsExplicit),
    ("Production Shot context boundary preserves explicit inherited context read-only", ProductionShotContextBoundaryPreservesInheritedContext),
    ("Preview payload rejects incomplete Production context without selector fallbacks", PreviewPayloadRejectsIncompleteProductionContext),
    ("Production payload preserves its explicit Actor and animation documents", ProductionPayloadPreservesActorAndAnimation),
    ("Production playback selects exact owner frames from its prepared snapshot", ProductionPlaybackSelectsPreparedOwnerFrames),
    ("Conversation Play messages advances the root Module owner frame", ConversationPlayMessagesAdvancesRootOwnerFrame),
    ("Preview Theme mode has one strict payload owner", PreviewThemeModeHasOneStrictPayloadOwner),
    ("animated Conversation text keeps Keyboard and Text Input Bar visible", AnimatedConversationComposerRemainsVisible),
    ("Conversation message Actors follow their exact direction contract", ConversationMessageActorsFollowDirectionContract),
    ("invalid Conversation message Actor documents fail read-only", InvalidConversationMessageActorsFailReadOnly),
    ("explicit Usage references are exact typed and shared", ExplicitReferenceUsageIsExactTypedAndShared),
    ("Usage navigation preserves workspace node and embedded context", UsageNavigationPreservesTypedContext),
    ("Production Data owns actors devices and fonts", ProductionDataOwnsConcreteResources),
    ("Production Output action reveals the project-owned configuration", ProductionOutputActionOwnsConfiguration),
    ("external Node processes share one executable resolution", ExternalNodeProcessesShareExecutableResolution),
    ("Desktop Preview startup rejects missing and stale bundle artifacts", DesktopPreviewBundleValidationIsStrict),
    ("startup classifies missing and invalid Preview bundles", StartupClassifiesPreviewBundleFailures),
    ("startup classifies missing empty and invalid databases", StartupClassifiesDatabaseFailures),
    ("startup prepares a read-only session and honors cancellation", StartupPreparesReadOnlySessionAndHonorsCancellation),
    ("closing the editor cancels Preview work and releases its lifetime", ClosingEditorCancelsPreviewLifetime),
    ("desktop visual instance lease excludes a second editor and recovers after exit", DesktopVisualInstanceLeaseIsExclusive),
    ("SQLite write coordination is isolated per database context", SqliteWriteCoordinationIsPerContext),
    ("Component and Module Variants share one full-reference grammar", ComponentAndModuleVariantsShareReferenceGrammar),
    ("Component and Module Variants share envelope lookup and id generation", ComponentAndModuleVariantsShareEnvelopeOperations),
    ("exact Component Variant Slots replace inherited boundaries atomically", ExactComponentVariantSlotsReplaceInheritedBoundaries),
    ("Default Variant editing unlock is session-only", DefaultVariantEditingUnlockIsSessionOnly),
    ("fixed structural Runtime collections reconcile by stable ids", FixedStructuralRuntimeCollectionsReconcileByStableIds),
    ("Incoming Call exposes exact Avatar and Icon Row Runtime boundaries", IncomingCallExposesExactChildRuntimeBoundaries),
    ("Preview references share Project media path resolution", PreviewReferencesShareProjectMediaPathResolution),
    ("SQLite contexts retain independent Project roots", SqliteContextsRetainIndependentProjectRoots),
    ("SQLite session exposes distinct focused application ports", SqliteSessionExposesDistinctFocusedPorts),
    ("visual persistence writers require operation coordination", VisualPersistenceWritersRequireOperationCoordination),
    ("MainWindow retains only shell-owned services", MainWindowRetainsOnlyShellServices),
    ("post-commit presentation reads run through operation coordination", PostCommitPresentationReadsUseOperationCoordination),
    ("Preview authoring preparation is task-based cancellable and snapshot-owned", PreviewAuthoringPreparationUsesOperationBoundary),
    ("Variant history reads persistence through the operation boundary", VariantHistoryReadsThroughOperationBoundary),
    ("collapsed editor cards defer their snapshot until expansion", CollapsedEditorCardsDeferSnapshots),
    ("editor visual cards require prepared field snapshots", EditorVisualCardsRequirePreparedFieldSnapshots),
    ("rapid visual selection commits only the latest prepared editor", RapidVisualSelectionCommitsLatestPreparedEditor),
    ("new Shot reload prepares Preview before selection", NewShotReloadPreparesPreviewBeforeSelection),
    ("failed Preview preparation keeps the prior tree catalog and selection", FailedPreviewPreparationKeepsPriorSession),
    ("obsolete Preview authoring preparation cannot replace the latest selection", ObsoletePreviewAuthoringPreparationCannotCommit),
    ("obsolete interactive Preview render results are discarded", ObsoleteInteractivePreviewRenderResultsAreDiscarded),
    ("Preview resource selection has one session rule", PreviewResourceSelectionHasOneSessionRule),
    ("editor view state follows the exact record class across records", EditorViewStateFollowsRecordClass),
    ("editor view state round-trips per class and clamps scroll", EditorViewStateRoundTripsPerClass),
    ("editor view state survives real editor and breadcrumb navigation", EditorViewStateSurvivesRealNavigation),
    ("Preview shell remains usable at 1040 and 1440 widths", PreviewShellLayoutIsResponsive),
    ("real Preview shell layout remains usable at 1040 and 1440", PreviewShellVisualTreeIsResponsive),
    ("List Item and List expose their runtime model in the real editor", ListRuntimeEditorVisualTreeExposesDynamicSetsAndState),
    ("Conversation Module exposes its Test Values Runtime in the real editor", ConversationModuleEditorVisualTreeExposesTestValues),
    ("pinned Module Variant Preview survives changing editor selection", PinnedModuleVariantPreviewSurvivesEditorSelection),
    ("Chat List Module exposes its fixed List boundary and exact Runtime in the real editor", ChatListModuleEditorVisualTreeExposesExactListRuntime),
    ("Design Preview transient snapshots remain immutable across later edits", DesignPreviewTransientSnapshotsRemainImmutable),
    ("List Runtime updates follow stable item identity after reorder", ListRuntimeUpdatesFollowStableIdentityAfterReorder),
    ("List Presence replays the same initial-to-final action and restores its origin", ListPresenceReplaysAndRestoresItsOrigin),
    ("manifest owners render their committed fixtures and Modules advance time", ManifestOwnersRenderCommittedFixturesAndModulesAdvanceTime),
    ("Design authoring context exposes exact Variant state without a fake save mode", DesignAuthoringContextExposesExactVariantState),
    ("track activation creates frame-zero state", TrackActivationCreatesInitialKeyframe),
    ("runtime controls resolve their value at the active owner frame", RuntimeControlsResolveActiveFrameValue),
    ("track targets persist and round-trip", TrackTargetsRoundTrip),
    ("nested collection duplication and deletion preserve animation targets", NestedCollectionTargetsFollowIdentity),
    ("keyframe upsert updates and orders", KeyframeUpsertUpdatesAndOrders),
    ("keyframe moves preserve payload and protect frame zero", KeyframeMovesPreservePayloadAndProtectFrameZero),
    ("keyframe drag snaps to the Screen authoring grid", KeyframeDragSnapsToScreenGrid),
    ("keyframes and tracks can be removed", KeyframesAndTracksCanBeRemoved),
    ("Screen-owned fields start at Screen zero", ScreenFieldsStartAtZero),
    ("runtime owner timeline rejects filtered contract envelopes", RuntimeOwnerTimelineRejectsFilteredEnvelopes),
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
    ("strict validation rejects malformed entries and unsorted keyframes", StrictValidationRejectsMalformedEntriesAndOrder),
    ("strict validation rejects invalid target durations", StrictValidationRejectsInvalidTargetDurations),
    ("strict validation rejects tracks without an origin keyframe", StrictValidationRejectsMissingOrigin),
    ("legacy animation requires explicit migration", LegacyAnimationRequiresExplicitMigration),
    ("initial animatable field vocabulary is constrained", AnimatableFieldVocabularyIsConstrained),
    ("playback state publishes play, busy and frame changes", PlaybackStatePublishesChanges),
    ("Runtime action controls reactivate after playback and visual-tree reattachment", RuntimeActionControlsReactivateAfterPlaybackAndReattachment),
    ("Preview preparation cancellation retains only the latest operation", PreviewPreparationCancellationRetainsLatestOperation),
    ("prepared playback reuse requires an exact current signature", PreparedPlaybackReuseRequiresExactSignature),
    ("prepared playback owners retain their combined frame cache", PreparedPlaybackOwnersRetainCombinedFrameCache),
    ("timeline frame updates suppress their own playback feedback", TimelineFrameUpdatesSuppressOwnPlaybackFeedback),
    ("collection item reorder persists stable ids", CollectionItemReorderPersistsStableIds),
    ("new collection items become the only expanded item", NewCollectionItemBecomesOnlyExpanded),
    ("active component variants expose parent class actions", ActiveVariantExposesParentClassActions),
    ("App and Module definitions expose rename-only lifecycle actions", AppAndModuleDefinitionsExposeRenameOnlyLifecycleActions),
    ("module parents open Default then remember the session Variant", ModuleParentsFollowComponentVariantSelection),
    ("only Default system bar variants are protected", OnlyDefaultSystemBarVariantsAreProtected),
    ("collection item presentation summarizes configured fields", CollectionItemPresentationSummarizesConfiguredFields),
    ("lifecycle actions stay consistent across navigation and editors", LifecycleActionsStayConsistentAcrossNavigationAndEditors),
    ("natural behavior timing uses graphemes and Theme pace", NaturalBehaviorTimingUsesGraphemesAndThemePace),
    ("timeline reference bands use contract-owned durations", TimelineReferenceBandsUseContractDurations),
    ("Component Stack opens from Atoms and renders its empty seed", ComponentStackSeedOpensAndRenders),
    ("Collection Stack exposes one runtime-owned Default Variant", CollectionStackSeedOpensAndRenders),
    ("Notifications composes Notification items through Collection Stack", NotificationsSeedOpensAndRenders),
    ("Keypad exposes Variant keys and renders from System", KeypadSeedOpensAndRenders),
    ("dictionary fields contract labels before stacking compound actions", DictionaryFieldsRespondToCompactWidths),
    ("Forward actions use one compact right-pointing presentation", ForwardActionsUseSharedPresentation),
    ("Label subtext placement uses the current explicit alignment contract", LabelSubtextPlacementUsesCurrentContract),
    ("Password composes stateful atoms and BehaviorTiming", PasswordSeedOpensAndRenders),
    ("Lock Screen composes its runtime Stack and optional system bars", LockScreenComposesRuntimeStack),
    ("forwarded child inputs become effective parent runtime inputs", ForwardedChildInputsBecomeParentRuntimeInputs),
    ("forwarded runtime collections expose slot state actions", ForwardedRuntimeCollectionsExposeSlotStateActions),
    ("module variants are explicit and selected by Screen instances", ModuleVariantsAreExplicit),
    ("Render Queue keeps one stable monotonic progress control", RenderQueueProgressControlIsStable),
};

static void ExactComponentVariantSlotsReplaceInheritedBoundaries()
{
    var target = JsonNode.Parse(
        """
        {
          "button": {
            "states": {
              "normal": {
                "surfaceSlot": {
                  "variantReference": "surface::variant::negative",
                  "overrides": {
                    "style": {
                      "cornerRadiusToken": "theme.radii.full"
                    },
                    "surface": {
                      "backgroundColorToken": "theme.colors.negative",
                      "backgroundAlpha": 1
                    }
                  }
                }
              }
            }
          }
        }
        """)?.AsObject()
        ?? throw new InvalidOperationException("Missing Component config target.");
    var exactSlotOverride = JsonNode.Parse(
        """
        {
          "button": {
            "states": {
              "normal": {
                "surfaceSlot": {
                  "variantReference": "surface::variant::neutral",
                  "overrides": {
                    "surface": {
                      "backgroundAlpha": 0.8
                    }
                  }
                }
              }
            }
          }
        }
        """)?.AsObject()
        ?? throw new InvalidOperationException("Missing exact Component Variant Slot Override.");

    ComponentConfigOverrideMerger.MergeInto(target, exactSlotOverride);
    var selectedSlot = target["button"]?["states"]?["normal"]?["surfaceSlot"]?.AsObject()
        ?? throw new InvalidOperationException("Missing selected Component Variant Slot.");
    Equal("surface::variant::neutral", selectedSlot["variantReference"]?.GetValue<string>());
    Equal(1, selectedSlot["overrides"]?["surface"]?.AsObject().Count ?? -1);
    Equal(0.8, selectedSlot["overrides"]?["surface"]?["backgroundAlpha"]?.GetValue<double>() ?? -1);

    var partialOverride = JsonNode.Parse(
        """
        {
          "button": {
            "states": {
              "normal": {
                "surfaceSlot": {
                  "overrides": {
                    "surface": {
                      "backgroundAlpha": 0.5
                    }
                  }
                }
              }
            }
          }
        }
        """)?.AsObject()
        ?? throw new InvalidOperationException("Missing partial Component Override.");
    var partialTarget = JsonNode.Parse(
        """
        {
          "button": {
            "states": {
              "normal": {
                "surfaceSlot": {
                  "variantReference": "surface::variant::negative",
                  "overrides": {
                    "surface": {
                      "backgroundColorToken": "theme.colors.negative",
                      "backgroundAlpha": 1
                    }
                  }
                }
              }
            }
          }
        }
        """)?.AsObject()
        ?? throw new InvalidOperationException("Missing partial Component config target.");

    ComponentConfigOverrideMerger.MergeInto(partialTarget, partialOverride);
    var partialSlot = partialTarget["button"]?["states"]?["normal"]?["surfaceSlot"]?.AsObject()
        ?? throw new InvalidOperationException("Missing merged Component Variant Slot.");
    Equal("surface::variant::negative", partialSlot["variantReference"]?.GetValue<string>());
    Equal(
        "theme.colors.negative",
        partialSlot["overrides"]?["surface"]?["backgroundColorToken"]?.GetValue<string>());
    Equal(0.5, partialSlot["overrides"]?["surface"]?["backgroundAlpha"]?.GetValue<double>() ?? -1);
}

static void DesignPreviewTransientSnapshotsRemainImmutable()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var nodes = database.LoadProjectTree()
        .SelectMany(DescendantsAndSelf)
        .ToList();
    var listVariant = nodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Id ==
        "component_project_foqn_s2_list::variant::default");
    var theme = nodes.First((node) =>
        node.Kind == ProjectTreeNodeKind.Theme);
    var payload = Required(
        CreatePreviewPayload(database, listVariant, theme.Id));
    var settings = database.GetComponentClassSettings(
        "component_project_foqn_s2_list");
    var session = new ComponentPreviewInputSession(
        database.Design,
        database.DictionaryContext,
        database.Resources,
        database.ProjectPaths,
        () => { });
    session.UpdateForPayload(payload, settings.ProjectId);

    var sourcePreview = JsonPath.ParseRequiredObject(
        payload.DesignPreviewJson,
        "List Design Preview");
    var sourceItems = JsonPath.RequiredArray(
            sourcePreview,
            "items",
            "List Design Preview")
        .OfType<JsonObject>()
        .Select((item) => item.DeepClone().AsObject())
        .ToList();
    True(sourceItems.Count >= 2);

    session.SetExternalCollectionItems(
        payload,
        "items",
        [sourceItems[0]]);
    var firstSnapshot =
        session.CaptureTransientState(payload);
    Equal(
        ComponentPreviewTransientValues.ScopeKey(payload),
        ComponentPreviewTransientValues.ScopeKey(
            listVariant,
            isInstance: false));
    session.SetExternalCollectionItems(
        payload,
        "items",
        [sourceItems[1]]);

    var previewInputData = new ComponentPreviewInputDataSource(
        database.Design,
        database.Resources);
    var firstEffective =
        ComponentPreviewTransientValues.Apply(
            sourcePreview,
            JsonPath.ParseRequiredObject(
                payload.ConfigJson,
                "List config"),
            firstSnapshot,
            previewInputData.ComponentVariantConfig);
    var currentEffective =
        session.ApplyTransientTestValues(sourcePreview, payload);
    var firstId = JsonPath.RequiredString(
        sourceItems[0],
        "id",
        "List Runtime item 1");
    var secondId = JsonPath.RequiredString(
        sourceItems[1],
        "id",
        "List Runtime item 2");
    Equal(
        firstId,
        JsonPath.RequiredString(
            JsonPath.RequiredArray(
                    firstEffective,
                    "items",
                    "Prepared List Runtime")
                .Single()!.AsObject(),
            "id",
            "Prepared List Runtime item"));
    Equal(
        secondId,
        JsonPath.RequiredString(
            JsonPath.RequiredArray(
                    currentEffective,
                    "items",
                    "Current List Runtime")
                .Single()!.AsObject(),
            "id",
            "Current List Runtime item"));
}

static void ListRuntimeUpdatesFollowStableIdentityAfterReorder()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
    var listVariant = nodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Id == "component_project_foqn_s2_list::variant::default");
    var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
    var payload = Required(CreatePreviewPayload(database, listVariant, theme.Id));
    var settings = database.GetComponentClassSettings(
        "component_project_foqn_s2_list");
    var session = new ComponentPreviewInputSession(
        database.Design,
        database.DictionaryContext,
        database.Resources,
        database.ProjectPaths,
        () => { });
    session.UpdateForPayload(payload, settings.ProjectId);

    var sourcePreview = JsonPath.ParseRequiredObject(
        payload.DesignPreviewJson,
        "List Design Preview");
    var sourceItems = JsonPath.RequiredArray(
            sourcePreview,
            "items",
            "List Design Preview")
        .OfType<JsonObject>()
        .Select((item) => item.DeepClone().AsObject())
        .ToList();
    True(sourceItems.Count >= 2);
    var first = sourceItems[0];
    var second = sourceItems[1];
    var firstId = JsonPath.RequiredString(first, "id", "List Runtime item 1");
    var secondId = JsonPath.RequiredString(second, "id", "List Runtime item 2");
    var originalSecond = second.DeepClone();
    sourceItems.RemoveAt(0);
    sourceItems.Insert(1, first);
    session.SetExternalCollectionItems(payload, "items", sourceItems);

    var firstRuntime = JsonPath.RequiredObject(
            first,
            "listItemInputs",
            "List Runtime item 1")
        .DeepClone()
        .AsObject();
    firstRuntime["state"] = "inactive";
    session.SetExternalCollectionItemValues(
        "items",
        firstId,
        new Dictionary<string, JsonNode?>
        {
            ["listItemInputs"] = firstRuntime,
        });

    var effective = session.ApplyTransientTestValues(sourcePreview, payload);
    var effectiveItems = JsonPath.RequiredArray(
            effective,
            "items",
            "Effective List Design Preview")
        .OfType<JsonObject>()
        .ToList();
    Equal(secondId, JsonPath.RequiredString(
        effectiveItems[0],
        "id",
        "Effective List Runtime item 1"));
    Equal(firstId, JsonPath.RequiredString(
        effectiveItems[1],
        "id",
        "Effective List Runtime item 2"));
    True(JsonNode.DeepEquals(originalSecond, effectiveItems[0]));
    Equal(
        "inactive",
        JsonPath.RequiredString(
            JsonPath.RequiredObject(
                effectiveItems[1],
                "listItemInputs",
                "Effective moved List Runtime item"),
            "state",
            "Effective moved List Runtime item"));
}

static void ListPresenceReplaysAndRestoresItsOrigin()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
    var listVariant = nodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Id == "component_project_foqn_s2_list::variant::default");
    var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
    var payload = Required(CreatePreviewPayload(database, listVariant, theme.Id));
    var settings = database.GetComponentClassSettings(
        "component_project_foqn_s2_list");
    var preview = JsonPath.ParseRequiredObject(
        payload.DesignPreviewJson,
        "List Design Preview");
    var firstItem = JsonPath.RequiredArray(preview, "items", "List Design Preview")
        .OfType<JsonObject>()
        .First();
    var firstItemId = JsonPath.RequiredString(
        firstItem,
        "id",
        "List Runtime item 1");
    var action = ComponentPreviewActions.ReadWithEmbedded(
            preview,
            new ComponentPreviewInputDataSource(database.Design, database.Resources).ComponentVariantRuntimeContract)
        .Single((candidate) =>
            candidate.CollectionItemId == firstItemId
            && candidate.Label == "Presence");
    var session = new ComponentPreviewInputSession(
        database.Design,
        database.DictionaryContext,
        database.Resources,
        database.ProjectPaths,
        () => { })
    {
        PresentEveryPlaybackFrame = true,
    };
    session.UpdateForPayload(payload, settings.ProjectId);
    var durationMethod = typeof(ComponentPreviewInputSession).GetMethod(
        "DurationFrames",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing shared preview action duration resolver.");
    var advanceMethod = typeof(ComponentPreviewInputSession).GetMethod(
        "AdvancePlaybackFrame",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing shared preview frame advance.");
    var durationFrames = (int)(durationMethod.Invoke(session, [action]) ?? -1);

    void CompleteAction()
    {
        session.NotifyPlaybackFramePresented();
        for (var frame = 1; frame <= durationFrames; frame++)
        {
            advanceMethod.Invoke(session, null);
            session.NotifyPlaybackFramePresented();
        }
    }

    JsonObject EffectiveItem()
    {
        var effectivePayload = session.ApplyInputs(payload, "light", settings.ProjectId);
        var effectivePreview = JsonPath.ParseRequiredObject(
            effectivePayload.DesignPreviewJson,
            "Effective List Design Preview");
        return JsonPath.RequiredArray(
                effectivePreview,
                "items",
                "Effective List Design Preview")
            .OfType<JsonObject>()
            .Single((item) =>
                JsonPath.RequiredString(item, "id", "Effective List item") == firstItemId);
    }

    Equal(true, JsonPath.RequiredBoolean(EffectiveItem(), "present", "Initial List item"));
    True(session.TriggerAction(action.Id));
    CompleteAction();
    True(!session.IsPlaybackActive);
    Equal(durationFrames, session.CurrentPreviewFrame);
    Equal(false, JsonPath.RequiredBoolean(EffectiveItem(), "present", "Completed List item"));
    True(session.CanRestoreAction(action.Id));

    True(session.TriggerAction(action.Id));
    True(session.IsPlaybackActive);
    Equal(0, session.CurrentPreviewFrame);
    var replayItem = EffectiveItem();
    Equal(false, JsonPath.RequiredBoolean(replayItem, "present", "Replayed List item target"));
    Equal(true, JsonPath.RequiredBoolean(
        replayItem,
        action.PlayInputId,
        "Replayed List item action state"));
    CompleteAction();
    True(!session.IsPlaybackActive);
    Equal(false, JsonPath.RequiredBoolean(EffectiveItem(), "present", "Recompleted List item"));

    True(session.RestoreAction(action.Id));
    True(!session.CanRestoreAction(action.Id));
    Equal(0, session.CurrentPreviewFrame);
    Equal(true, JsonPath.RequiredBoolean(EffectiveItem(), "present", "Restored List item"));
}

static void ExternalNodeProcessesShareExecutableResolution()
{
    var executable = DesktopChildProcess.ResolveNodeExecutable();
    True(!string.IsNullOrWhiteSpace(executable));
    Equal(OperatingSystem.IsWindows() ? "node.exe" : "node", Path.GetFileName(executable));
}

static void DesktopPreviewBundleValidationIsStrict()
{
    var source = Path.Combine(AppContext.BaseDirectory, "desktop-preview");
    DesktopPreviewBundle.RequireCurrent(source);
    var temporary = Path.Combine(
        Path.GetTempPath(),
        $"mockups-preview-bundle-{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporary);
    try
    {
        foreach (var sourceFile in Directory.EnumerateFiles(source))
        {
            File.Copy(
                sourceFile,
                Path.Combine(temporary, Path.GetFileName(sourceFile)));
        }
        DesktopPreviewBundle.RequireCurrent(temporary);

        File.AppendAllText(
            Path.Combine(temporary, "renderDesignPreviewHtml.cjs"),
            Environment.NewLine);
        Throws<InvalidDataException>(() =>
            DesktopPreviewBundle.RequireCurrent(temporary));

        File.Delete(Path.Combine(temporary, "manifest.json"));
        Throws<FileNotFoundException>(() =>
            DesktopPreviewBundle.RequireCurrent(temporary));
    }
    finally
    {
        Directory.Delete(temporary, recursive: true);
    }
}

static void StartupClassifiesPreviewBundleFailures()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-startup-preview-{Guid.NewGuid():N}");
    var missingBundle = Path.Combine(root, "missing");
    var copiedBundle = Path.Combine(root, "invalid");
    Directory.CreateDirectory(copiedBundle);
    try
    {
        var missing = new ApplicationStartupCoordinator(
            missingBundle).Start(ParityDatabasePath());
        True(missing is StartupResult.PreviewBundleMissing);

        var source = Path.Combine(
            AppContext.BaseDirectory,
            "desktop-preview");
        foreach (var sourceFile in Directory.EnumerateFiles(source))
        {
            File.Copy(
                sourceFile,
                Path.Combine(
                    copiedBundle,
                    Path.GetFileName(sourceFile)));
        }
        File.AppendAllText(
            Path.Combine(
                copiedBundle,
                "renderDesignPreviewHtml.cjs"),
            Environment.NewLine);

        var invalid = new ApplicationStartupCoordinator(
            copiedBundle).Start(ParityDatabasePath());
        True(invalid is StartupResult.PreviewBundleInvalid);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

static void StartupClassifiesDatabaseFailures()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-startup-database-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var bundle = Path.Combine(
        AppContext.BaseDirectory,
        "desktop-preview");
    var coordinator = new ApplicationStartupCoordinator(bundle);
    var missingPath = Path.Combine(root, "missing.sqlite");
    var emptyPath = Path.Combine(root, "empty.sqlite");
    var invalidPath = Path.Combine(root, "invalid.sqlite");
    try
    {
        var missing = coordinator.Start(missingPath);
        True(missing is StartupResult.DatabaseMissing);

        File.WriteAllBytes(emptyPath, []);
        var empty = coordinator.Start(emptyPath);
        True(empty is StartupResult.DatabaseInvalid);

        File.Copy(
            ParityDatabasePath(),
            invalidPath,
            overwrite: true);
        using (var connection = new SqliteConnection(
                   $"Data Source={invalidPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 999;";
            command.ExecuteNonQuery();
        }
        var invalid = coordinator.Start(invalidPath);
        True(invalid is StartupResult.DatabaseInvalid);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void StartupPreparesReadOnlySessionAndHonorsCancellation()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-startup-success-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var databasePath = Path.Combine(root, "current.sqlite");
    File.Copy(
        ParityDatabasePath(),
        databasePath,
        overwrite: true);
    try
    {
        var before = SHA256.HashData(
            File.ReadAllBytes(databasePath));
        var coordinator = new ApplicationStartupCoordinator(
            Path.Combine(
                AppContext.BaseDirectory,
                "desktop-preview"));

        var result = coordinator.Start(databasePath);

        True(result is StartupResult.Success);
        SequenceEqual(
            before,
            SHA256.HashData(
                File.ReadAllBytes(databasePath)));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = coordinator.StartAsync(
                databasePath,
                cancellation.Token)
            .GetAwaiter()
            .GetResult();
        True(canceled is StartupResult.Canceled);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void SqliteWriteCoordinationIsPerContext()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-sqlite-write-context-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var firstPath = Path.Combine(root, "first.sqlite");
    var secondPath = Path.Combine(root, "second.sqlite");
    try
    {
        var first = new SqliteProjectContext(firstPath);
        var second = new SqliteProjectContext(secondPath);
        True(!ReferenceEquals(first.WriteGate, second.WriteGate));

        using (var connection = first.OpenConnection())
        {
            first.ExecuteScript(
                connection,
                "CREATE TABLE writes (id INTEGER PRIMARY KEY, value TEXT NOT NULL);");
        }
        using (var connection = second.OpenConnection())
        {
            second.ExecuteScript(
                connection,
                "CREATE TABLE writes (id INTEGER PRIMARY KEY, value TEXT NOT NULL);");
        }

        using var firstGateHeld = new ManualResetEventSlim();
        using var releaseFirstGate = new ManualResetEventSlim();
        var blockedFirstWrite = Task.Run(() =>
        {
            lock (first.WriteGate)
            {
                firstGateHeld.Set();
                if (!releaseFirstGate.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException(
                        "Timed out while holding the first database write gate.");
                }
                using var connection = first.OpenConnection();
                first.Execute(
                    connection,
                    "INSERT INTO writes(value) VALUES ($value)",
                    ("$value", "first"));
            }
        });

        True(firstGateHeld.Wait(TimeSpan.FromSeconds(3)));
        try
        {
            var independentSecondWrite = Task.Run(() =>
            {
                using var connection = second.OpenConnection();
                second.Execute(
                    connection,
                    "INSERT INTO writes(value) VALUES ($value)",
                    ("$value", "second"));
            });
            True(independentSecondWrite.Wait(TimeSpan.FromSeconds(3)));
            independentSecondWrite.GetAwaiter().GetResult();
        }
        finally
        {
            releaseFirstGate.Set();
        }
        blockedFirstWrite.GetAwaiter().GetResult();

        var concurrentWrites = Enumerable.Range(0, 12)
            .Select((index) => Task.Run(() =>
            {
                using var connection = first.OpenConnection();
                first.Execute(
                    connection,
                    "INSERT INTO writes(value) VALUES ($value)",
                    ("$value", $"concurrent-{index}"));
            }))
            .ToArray();
        Task.WaitAll(concurrentWrites);

        using var firstRead = first.OpenConnection();
        using var secondRead = second.OpenConnection();
        Equal(
            13L,
            SqliteCommandExecutor.ScalarLong(
                firstRead,
                "SELECT COUNT(*) FROM writes"));
        Equal(
            1L,
            SqliteCommandExecutor.ScalarLong(
                secondRead,
                "SELECT COUNT(*) FROM writes"));
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void ClosingEditorCancelsPreviewLifetime()
{
    var source = ParityDatabasePath();
    var windowStatePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "data",
            "window-state.json"));
    var priorWindowState = File.Exists(windowStatePath)
        ? File.ReadAllBytes(windowStatePath)
        : null;
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-preview-close-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var window = DesktopHost.CreateWindow(temporary);
            window.Show();
            var controller = typeof(MainWindow)
                .GetField(
                    "_previewController",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(window) as EditorPreviewController
                ?? throw new InvalidOperationException(
                    "Missing MainWindow Preview lifetime owner.");
            var designPreparation = typeof(EditorPreviewController)
                .GetField(
                    "_designPlaybackPreparation",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(controller) as PreviewPreparationCancellation
                ?? throw new InvalidOperationException(
                    "Missing Design Preview preparation lifetime.");
            var shotPreparation = typeof(EditorPreviewController)
                .GetField(
                    "_shotPlaybackPreparation",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(controller) as PreviewPreparationCancellation
                ?? throw new InvalidOperationException(
                    "Missing Production Preview preparation lifetime.");
            var visualContextPreparation =
                typeof(EditorPreviewController)
                    .GetField(
                        "_visualContextPreparation",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.GetValue(controller)
                    as PreviewPreparationCancellation
                ?? throw new InvalidOperationException(
                    "Missing Preview visual-context preparation lifetime.");
            var productionPayloadPreparation =
                typeof(EditorPreviewController)
                    .GetField(
                        "_productionPayloadPreparation",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.GetValue(controller)
                    as PreviewPreparationCancellation
                ?? throw new InvalidOperationException(
                    "Missing Production payload preparation lifetime.");
            var designOperation = designPreparation.Begin();
            var shotOperation = shotPreparation.Begin();
            var visualContextOperation =
                visualContextPreparation.Begin();
            var productionPayloadOperation =
                productionPayloadPreparation.Begin();
            var aheadOperation = new CancellationTokenSource();
            typeof(EditorPreviewController)
                .GetField(
                    "_aheadPreloadCancellation",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, aheadOperation);
            var timer = typeof(EditorPreviewController)
                .GetField(
                    "_shotPlaybackTimer",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(controller) as DispatcherTimer
                ?? throw new InvalidOperationException(
                    "Missing Production Preview timer lifetime.");
            timer.Start();

            window.Close();

            True(designOperation.IsCancellationRequested);
            True(shotOperation.IsCancellationRequested);
            True(visualContextOperation.IsCancellationRequested);
            True(productionPayloadOperation.IsCancellationRequested);
            True(aheadOperation.IsCancellationRequested);
            True(!timer.IsEnabled);
            controller.Dispose();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(temporary);
        if (priorWindowState is null)
        {
            File.Delete(windowStatePath);
        }
        else
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(windowStatePath)
                ?? throw new InvalidOperationException(
                    "Window state path has no directory."));
            File.WriteAllBytes(
                windowStatePath,
                priorWindowState);
        }
    }
}

static void DesktopVisualInstanceLeaseIsExclusive()
{
    var identity =
        $"mockups-desktop-instance-{Guid.NewGuid():N}";
    using (var first =
           DesktopVisualInstanceLease.TryAcquire(identity))
    {
        True(first is not null);
        Equal(
            23,
            RunDesktopInstanceLifetimeProbe(identity));
    }

    Equal(
        0,
        RunDesktopInstanceLifetimeProbe(identity));
}

static int RunDesktopInstanceLifetimeProbe(
    string identity)
{
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException(
            "The desktop test executable path is unavailable.");
    var start = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
    };
    start.ArgumentList.Add(
        "--desktop-instance-lifetime-probe");
    start.ArgumentList.Add(identity);
    using var process = Process.Start(start)
        ?? throw new InvalidOperationException(
            "The desktop instance probe could not start.");
    if (!process.WaitForExit(
            milliseconds: 5000))
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException(
            "The desktop instance probe did not exit.");
    }
    return process.ExitCode;
}

static void ActorPreviewSurfacesShareInitialsIdentity()
{
    Equal("AT", ActorIdentityText.Initials("Alex Torrens", "Ignored Name"));
    Equal("JN", ActorIdentityText.Initials("", "  Jorge   Navarro  "));
    Equal("A", ActorIdentityText.Initials("Alex", "Ignored Name"));
    Equal("", ActorIdentityText.Initials("", ""));
}

static void DesignAuthoringContextExposesExactVariantState()
{
    var selectedVariantId = "";
    var metadata = new EditorContextStripMetadata(
        [new EditorContextIdentity("Component", "Text Box")],
        new EditorContextVariantSelector(
            [
                new FieldOption("component.text_box::variant::default", "Default"),
                new FieldOption("component.text_box::variant::search", "Search"),
            ],
            "component.text_box::variant::search",
            (variantId) => selectedVariantId = variantId),
        2,
        IsUsed: true,
        IsProtected: true,
        IsLocked: true);

    True(metadata.AccessibleText.Contains("Component: Text Box", StringComparison.Ordinal));
    True(metadata.AccessibleText.Contains("Variant: Search", StringComparison.Ordinal));
    True(metadata.AccessibleText.Contains("2 overrides", StringComparison.Ordinal));
    True(metadata.AccessibleText.Contains("Used", StringComparison.Ordinal));
    True(metadata.AccessibleText.Contains("Protected", StringComparison.Ordinal));
    True(metadata.AccessibleText.Contains("Locked", StringComparison.Ordinal));
    True(!metadata.AccessibleText.Contains("Saved", StringComparison.Ordinal));

    metadata.VariantSelector!.Select("component.text_box::variant::default");
    Equal("component.text_box::variant::default", selectedVariantId);

    var rootVariantMetadata = metadata with { Identities = [] };
    True(rootVariantMetadata.AccessibleText.StartsWith("Variant: Search", StringComparison.Ordinal));
}

static void TypographyStyleKeepsOnlyExplicitSentinels()
{
    Equal(0, TypographyStyleValue.Parse("").Count);
    Equal(0, TypographyStyleValue.Parse("inherited").Count);
    Equal(
        "theme.typography.sizes.s",
        TypographyStyleValue.String(
            TypographyStyleValue.Parse(TypographyStyleValue.CreateDefault("theme.typography.sizes.s")),
            TypographyStyleValue.SizeToken));
    Throws<InvalidOperationException>(() => TypographyStyleValue.Parse("not-json"));
    Throws<InvalidOperationException>(() => TypographyStyleValue.Parse("[]"));
    Throws<InvalidOperationException>(() => TypographyStyleValue.Parse("4"));
    Throws<InvalidOperationException>(() => TypographyStyleValue.Parse(JsonNode.Parse("[]")!));

    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-typography-owner-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var keyboard = Descendants(database.LoadProjectTree()).Single((node) =>
            node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "keyboard");
        var beforeRejectedWrite = database.GetComponentClassSettings(keyboard.Id).ConfigJson;

        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            keyboard.Id,
            "component.keyboard.typography",
            "[]"));
        Equal(beforeRejectedWrite, database.GetComponentClassSettings(keyboard.Id).ConfigJson);

        var validStyle = TypographyStyleValue.CreateDefault(
            "theme.typography.sizes.m",
            "theme.system");
        database.UpdateComponentClassField(
            keyboard.Id,
            "component.keyboard.typography",
            validStyle);
        var savedConfig = JsonPath.ParseRequiredObject(
            database.GetComponentClassSettings(keyboard.Id).ConfigJson,
            "Saved Keyboard Variant config");
        True(savedConfig["keyboard"]?["typography"] is JsonObject);
        Equal(
            "theme.typography.sizes.m",
            savedConfig["keyboard"]?["typography"]?[TypographyStyleValue.SizeToken]?.GetValue<string>());
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RuntimeInputKindAndValueKindShareOneContract()
{
    Equal(
        ValueKind.StringSingleLine,
        RuntimeInputValueKindContract.RequireCompatible(
            "text",
            "StringSingleLine",
            "Test Runtime Input"));
    Equal(
        ValueKind.MediaFilePath,
        RuntimeInputValueKindContract.RequireCompatible(
            "mediaFilePath",
            "MediaFilePath",
            "Test Runtime Input"));
    Equal(
        ValueKind.StructuredCollection,
        RuntimeInputValueKindContract.RequireCompatible(
            "collection",
            "StructuredCollection",
            "Test Runtime Input"));
    Equal(
        ValueKind.ComponentVariantSlot,
        RuntimeInputValueKindContract.RequireCompatible(
            "componentVariantSlot",
            "ComponentVariantSlot",
            "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.RequireCompatible(
        "text",
        "MediaFilePath",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.RequireCompatible(
        "collection",
        "UnknownValueKind",
        "Test Runtime Input"));
}

static void RuntimeInputDefinitionReadersAreStrict()
{
    static JsonObject Input() => new()
    {
        ["id"] = "title",
        ["label"] = "Title",
        ["jsonKey"] = "title",
        ["kind"] = "text",
        ["valueKind"] = "StringSingleLine",
        ["defaultValue"] = "Default",
    };

    static JsonObject Preview(JsonNode? input) => new()
    {
        ["inputs"] = new JsonArray(input),
    };

    var valid = RuntimeInputDefinitionReader.ReadInputs(Preview(Input()), new JsonObject()).Single();
    Equal(ComponentInputSource.Runtime, valid.Source);
    Equal(ComponentInputUiOrigin.Self, valid.UiOrigin);
    var dynamic = Input();
    dynamic["optionsSourceCollectionJsonKey"] = "contentSets";
    dynamic["optionsSourceValueJsonKey"] = "id";
    dynamic["optionsSourceLabelJsonKey"] = "name";
    dynamic["optionsSourceFirstItemBadge"] = "Default";
    var dynamicDefinition = RuntimeInputDefinitionReader
        .ReadInputs(Preview(dynamic), new JsonObject())
        .Single();
    Equal("contentSets", dynamicDefinition.OptionsSourceCollectionJsonKey);
    Equal("id", dynamicDefinition.OptionsSourceValueJsonKey);
    Equal("name", dynamicDefinition.OptionsSourceLabelJsonKey);
    Equal("Default", dynamicDefinition.OptionsSourceFirstItemBadge);
    Equal(0, RuntimeInputDefinitionReader.ReadInputs(new JsonObject(), new JsonObject()).Count);

    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        new JsonObject { ["inputs"] = new JsonObject() },
        new JsonObject()));
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(JsonValue.Create("invalid")),
        new JsonObject()));
    var missingId = Input();
    missingId.Remove("id");
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(missingId),
        new JsonObject()));
    var unknownSource = Input();
    unknownSource["source"] = "automatic";
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(unknownSource),
        new JsonObject()));
    var unknownOrigin = Input();
    unknownOrigin["uiOrigin"] = "automatic";
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(unknownOrigin),
        new JsonObject()));
    var wrongOptionsRoot = Input();
    wrongOptionsRoot["options"] = new JsonObject();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(wrongOptionsRoot),
        new JsonObject()));
    var filteredOption = Input();
    filteredOption["options"] = new JsonArray(JsonValue.Create("invalid"));
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(filteredOption),
        new JsonObject()));
    var duplicateOptions = Input();
    duplicateOptions["options"] = new JsonArray
    {
        new JsonObject { ["value"] = "same", ["label"] = "One" },
        new JsonObject { ["value"] = "same", ["label"] = "Two" },
    };
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(duplicateOptions),
        new JsonObject()));
    var invalidList = Input();
    invalidList["allowEmptyWhenItemValues"] = new JsonArray("valid", 4);
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(invalidList),
        new JsonObject()));
    var incompleteVisibility = Input();
    incompleteVisibility["visibleWhenPath"] = "mode";
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(incompleteVisibility),
        new JsonObject()));
    var invalidAnimation = Input();
    invalidAnimation["animatable"] = "true";
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(invalidAnimation),
        new JsonObject()));
    var invalidTimeline = Input();
    invalidTimeline["animatable"] = true;
    invalidTimeline["animationTimeline"] = new JsonArray();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(invalidTimeline),
        new JsonObject()));
    var invalidTransition = Input();
    invalidTransition["transition"] = new JsonArray();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadInputs(
        Preview(invalidTransition),
        new JsonObject()));

    static JsonObject Collection() => new()
    {
        ["id"] = "items",
        ["label"] = "Items",
        ["jsonKey"] = "items",
        ["itemLabel"] = "Item",
        ["fields"] = new JsonArray(Input()),
    };

    static JsonObject CollectionPreview(JsonNode? collection) => new()
    {
        ["collections"] = new JsonArray(collection),
    };

    Equal(
        "items",
        RuntimeInputDefinitionReader.ReadCollections(
            CollectionPreview(Collection()),
            new JsonObject()).Single().Id);
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        new JsonObject { ["collections"] = new JsonObject() },
        new JsonObject()));
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(JsonValue.Create("invalid")),
        new JsonObject()));
    var missingItemLabel = Collection();
    missingItemLabel.Remove("itemLabel");
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(missingItemLabel),
        new JsonObject()));
    var wrongFieldsRoot = Collection();
    wrongFieldsRoot["fields"] = new JsonObject();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(wrongFieldsRoot),
        new JsonObject()));
    var filteredField = Collection();
    filteredField["fields"] = new JsonArray(JsonValue.Create("invalid"));
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(filteredField),
        new JsonObject()));
    var wrongComponentItems = Collection();
    wrongComponentItems["componentItems"] = new JsonArray();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(wrongComponentItems),
        new JsonObject()));
    var incompleteComponentItems = Collection();
    incompleteComponentItems["componentItems"] = new JsonObject
    {
        ["variantReferenceJsonKey"] = "variantReference",
    };
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(incompleteComponentItems),
        new JsonObject()));
    var nullComponentItems = Collection();
    nullComponentItems["componentItems"] = null;
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(nullComponentItems),
        new JsonObject()));
    static JsonObject ComponentField() => new()
    {
        ["id"] = "componentVariant",
        ["label"] = "Component Variant",
        ["jsonKey"] = "variantReference",
        ["kind"] = "componentVariant",
        ["valueKind"] = "ComponentVariant",
        ["defaultValue"] = "component_example::variant::default",
        ["componentType"] = "*",
    };
    static JsonObject ComponentItems() => new()
    {
        ["variantReferenceJsonKey"] = "variantReference",
        ["overridesJsonKey"] = "overrides",
        ["inputsJsonKey"] = "inputs",
    };
    var validComponentCollection = Collection();
    validComponentCollection["fields"] = new JsonArray(ComponentField());
    validComponentCollection["componentItems"] = ComponentItems();
    Equal(
        "variantReference",
        RuntimeInputDefinitionReader.ReadCollections(
            CollectionPreview(validComponentCollection),
            new JsonObject()).Single().ComponentItems?.VariantReferenceJsonKey ?? "");
    var missingComponentField = Collection();
    missingComponentField["componentItems"] = ComponentItems();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(missingComponentField),
        new JsonObject()));
    var wrongComponentField = ComponentField();
    wrongComponentField["kind"] = "text";
    wrongComponentField["valueKind"] = "StringSingleLine";
    var wrongComponentFieldCollection = Collection();
    wrongComponentFieldCollection["fields"] = new JsonArray(wrongComponentField);
    wrongComponentFieldCollection["componentItems"] = ComponentItems();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(wrongComponentFieldCollection),
        new JsonObject()));
    var overlappingComponentKeys = ComponentItems();
    overlappingComponentKeys["inputsJsonKey"] = "overrides";
    var overlappingComponentCollection = Collection();
    overlappingComponentCollection["fields"] = new JsonArray(ComponentField());
    overlappingComponentCollection["componentItems"] = overlappingComponentKeys;
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(overlappingComponentCollection),
        new JsonObject()));
    var wrongPresentation = Collection();
    wrongPresentation["itemPresentation"] = new JsonArray();
    Throws<InvalidOperationException>(() => RuntimeInputDefinitionReader.ReadCollections(
        CollectionPreview(wrongPresentation),
        new JsonObject()));
}

static void ComponentDictionaryFieldsUseExactValueKinds()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-component-value-kinds-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var components = Descendants(database.LoadProjectTree())
            .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentClass)
            .ToList();
        var componentFieldIds = ComponentClassFieldCatalog.All()
            .Select((field) => field.Id)
            .ToHashSet(StringComparer.Ordinal);
        var beforeReads = SHA256.HashData(File.ReadAllBytes(temporary));
        var invalidFields = new List<string>();
        foreach (var component in components)
        {
            var fields = EditorLayouts(database).LoadEditorLayout(component.RecordClassId).Cards
                .SelectMany((card) => card.VisibleGroups)
                .SelectMany((group) => group.VisibleFields)
                .Select((field) => field.Id)
                .Where(componentFieldIds.Contains)
                .Distinct(StringComparer.Ordinal);
            foreach (var owner in new[] { component }.Concat(
                         component.Children.Where((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant)))
            {
                foreach (var fieldId in fields)
                {
                    try
                    {
                        var fieldValue = owner.Kind == ProjectTreeNodeKind.ComponentClass
                            ? database.CreateComponentClassFieldValue(owner.Id, fieldId)
                            : database.CreateComponentVariantFieldValue(owner, fieldId);
                        if (fieldValue.Definition.ValueKind is ValueKind.Integer or ValueKind.Decimal)
                        {
                            _ = DictionaryNumericValueContract.ParseRequired(
                                fieldValue.Definition,
                                fieldValue.Value);
                        }
                    }
                    catch (InvalidOperationException exception)
                    {
                        invalidFields.Add($"{owner.Id} / {fieldId}: {exception.Message}");
                    }
                }
            }
        }
        if (invalidFields.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, invalidFields));
        }
        SequenceEqual(beforeReads, SHA256.HashData(File.ReadAllBytes(temporary)));

        ProjectTreeNode Component(string recordClassId) => components.Single((node) =>
            node.RecordClassId.Equals(recordClassId, StringComparison.Ordinal));
        var beforeRejectedWrites = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.avatar").Id,
            "component.avatar.label.showLabel",
            "perhaps"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.cursor").Id,
            "component.cursor.width",
            "1.5"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.notification").Id,
            "component.notification.avatarPlacement",
            "[]"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.notification").Id,
            "component.notification.avatar.inputs",
            "[]"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.keypad").Id,
            "component.keypad.keys",
            "[{\"id\":\"key_1\"},{\"id\":\"key_1\"}]"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.surface").Id,
            "component.surface.tail.size",
            "18|invalid"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.textBox").Id,
            "component.textBox.padding",
            "theme.spacing.m|"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.bubble").Id,
            "component.bubble.incomingBackground",
            "gray_080"));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            Component("component.surface").Id,
            "component.surface.backgroundAlpha",
            "1.5"));
        SequenceEqual(beforeRejectedWrites, SHA256.HashData(File.ReadAllBytes(temporary)));

        var cursor = Component("component.cursor");
        database.UpdateComponentClassField(cursor.Id, "component.cursor.width", "3");
        Equal("3", database.CreateComponentClassFieldValue(cursor.Id, "component.cursor.width").Value);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RecordScalarWritesRejectInvalidValues()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-record-scalar-values-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var tree = Descendants(database.LoadProjectTree()).ToList();
        var device = tree.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var actor = tree.First((node) => node.Kind == ProjectTreeNodeKind.Actor);
        var palette = tree.First((node) => node.Kind == ProjectTreeNodeKind.PaletteColor);
        var theme = tree.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var app = tree.First((node) => node.Kind == ProjectTreeNodeKind.App
            && EditorLayouts(database).LoadEditorLayout(node.RecordClassId).Cards
                .SelectMany((card) => card.VisibleGroups)
                .SelectMany((group) => group.VisibleFields)
                .Any((field) => field.Id == "app.wallpaper.opacity"));

        var beforeRejectedWrites = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.UpdateDeviceField(
            device.Id,
            "device.metrics.scaleToPixels",
            "not-a-number"));
        Throws<InvalidOperationException>(() => database.UpdateDeviceField(
            device.Id,
            "device.metrics.screen.size",
            "100|not-a-number"));
        Throws<InvalidOperationException>(() => database.UpdateActorField(
            actor.Id,
            "actor.wallpaper.opacity",
            "not-a-number"));
        Throws<InvalidOperationException>(() => database.UpdateActorField(
            actor.Id,
            "actor.avatar.useInitials",
            "perhaps"));
        Throws<InvalidOperationException>(() => database.UpdateThemeField(
            theme.Id,
            "theme.neutralTint.saturation",
            "not-a-number"));
        Throws<InvalidOperationException>(() => database.UpdateAppField(
            app.Id,
            "app.wallpaper.opacity",
            "not-a-number"));
        Throws<InvalidOperationException>(() => database.UpdateAppField(
            app.Id,
            "app.icon.offset",
            "1|not-a-number"));
        Throws<InvalidOperationException>(() => database.UpdatePaletteColorField(
            palette.Id,
            "palette.isNeutral",
            "perhaps"));
        Throws<InvalidOperationException>(() => database.UpdatePaletteColorField(
            palette.Id,
            "palette.protected",
            "perhaps"));
        Throws<InvalidOperationException>(() => database.UpdatePaletteColorField(
            palette.Id,
            "palette.hiddenFromPickers",
            "perhaps"));
        SequenceEqual(beforeRejectedWrites, SHA256.HashData(File.ReadAllBytes(temporary)));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ResourceScalarReadsRejectWrongShapes()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-resource-scalar-reads-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var context = new SqliteProjectContext(temporary);
        var fields = new RecordClassFieldValueService(
            ProductionRecordFields(database),
            DesignRecordFields(database),
            ResourceRecordFields(database),
            database.Production,
            database.Resources);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var checkedKinds = new HashSet<ProjectTreeNodeKind>
        {
            ProjectTreeNodeKind.App,
            ProjectTreeNodeKind.Device,
            ProjectTreeNodeKind.Actor,
            ProjectTreeNodeKind.Theme,
            ProjectTreeNodeKind.PaletteColor,
        };
        var beforeValidReads = SHA256.HashData(File.ReadAllBytes(temporary));
        foreach (var node in nodes.Where((candidate) => checkedKinds.Contains(candidate.Kind)))
        {
            foreach (var fieldId in EditorLayouts(database).LoadEditorLayout(node.RecordClassId).Cards
                         .SelectMany((card) => card.VisibleGroups)
                         .SelectMany((group) => group.VisibleFields)
                         .Select((field) => field.Id)
                         .Distinct(StringComparer.Ordinal))
            {
                if (fields.CanHandle(node.Kind, fieldId))
                {
                    var fieldValue = fields.CreateFieldValue(node, fieldId);
                    if (fieldValue.Definition.ValueKind is ValueKind.Integer or ValueKind.Decimal)
                    {
                        _ = DictionaryNumericValueContract.ParseRequired(
                            fieldValue.Definition,
                            fieldValue.Value);
                    }
                }
            }
        }
        SequenceEqual(beforeValidReads, SHA256.HashData(File.ReadAllBytes(temporary)));

        void ReplaceJson(string table, string column, string id, string json)
        {
            using var connection = context.OpenConnection();
            context.Execute(
                connection,
                $"UPDATE {table} SET {column} = $json WHERE id = $id",
                ("$json", json),
                ("$id", id));
        }

        void RejectsReadWithoutMutation(Action read)
        {
            var before = SHA256.HashData(File.ReadAllBytes(temporary));
            Throws<InvalidOperationException>(read);
            SequenceEqual(before, SHA256.HashData(File.ReadAllBytes(temporary)));
        }

        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var deviceMetricsJson = database.GetDeviceSettings(device.Id).MetricsJson;
        var invalidDeviceMetrics = JsonPath.ParseRequiredObject(deviceMetricsJson, "Device test metrics");
        invalidDeviceMetrics["scaleToPixels"] = "3";
        ReplaceJson("devices", "metrics_json", device.Id, invalidDeviceMetrics.ToJsonString());
        RejectsReadWithoutMutation(() => database.GetDeviceMetricFieldValue(device.Id, "device.metrics.scaleToPixels"));
        RejectsReadWithoutMutation(() => database.GetDevicePreviewMetrics(device.Id));
        ReplaceJson("devices", "metrics_json", device.Id, deviceMetricsJson);
        var invalidDynamicIsland = JsonPath.ParseRequiredObject(deviceMetricsJson, "Device Dynamic Island test metrics");
        invalidDynamicIsland["dynamicIsland"] = "present-but-invalid";
        ReplaceJson("devices", "metrics_json", device.Id, invalidDynamicIsland.ToJsonString());
        RejectsReadWithoutMutation(() => database.GetDeviceMetricFieldValue(device.Id, "device.metrics.dynamicIsland.position"));
        ReplaceJson("devices", "metrics_json", device.Id, deviceMetricsJson);

        var actor = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Actor);
        var actorMetadataJson = database.GetActorSettings(actor.Id).MetadataJson;
        var invalidActorMetadata = JsonPath.ParseRequiredObject(actorMetadataJson, "Actor test metadata");
        invalidActorMetadata["avatar"]!.AsObject()["useInitials"] = "false";
        ReplaceJson("actors", "metadata_json", actor.Id, invalidActorMetadata.ToJsonString());
        RejectsReadWithoutMutation(() => database.GetActorFieldValue(actor.Id, "actor.avatar.useInitials"));
        ReplaceJson("actors", "metadata_json", actor.Id, actorMetadataJson);

        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var themeTokensJson = database.GetThemeSettings(theme.Id).TokensJson;
        var invalidThemeTokens = JsonPath.ParseRequiredObject(themeTokensJson, "Theme test tokens");
        invalidThemeTokens["defaultMode"] = 1;
        ReplaceJson("themes", "tokens_json", theme.Id, invalidThemeTokens.ToJsonString());
        RejectsReadWithoutMutation(() => database.GetThemeFieldValue(theme.Id, "theme.defaultMode"));
        ReplaceJson("themes", "tokens_json", theme.Id, themeTokensJson);

        var app = nodes.First((node) => node.Kind == ProjectTreeNodeKind.App
            && EditorLayouts(database).LoadEditorLayout(node.RecordClassId).Cards
                .SelectMany((card) => card.VisibleGroups)
                .SelectMany((group) => group.VisibleFields)
                .Any((field) => field.Id == "app.wallpaper.opacity"));
        var appConfigJson = database.GetAppSettings(app.Id).ConfigJson;
        var invalidAppConfig = JsonPath.ParseRequiredObject(appConfigJson, "App test config");
        invalidAppConfig["wallpaper"]!.AsObject()["opacity"] = "1";
        ReplaceJson("apps", "config_json", app.Id, invalidAppConfig.ToJsonString());
        RejectsReadWithoutMutation(() => database.GetAppConfigFieldValue(app.Id, "app.wallpaper.opacity"));
        ReplaceJson("apps", "config_json", app.Id, appConfigJson);

        var palette = nodes.First((node) => node.Kind == ProjectTreeNodeKind.PaletteColor
            && database.GetPaletteColorSettings(node.Id).IsProtected);
        string paletteMetadataJson;
        using (var connection = context.OpenConnection())
        {
            paletteMetadataJson = SqliteCommandExecutor.ScalarString(
                connection,
                "SELECT metadata_json FROM palette_colors WHERE id = $id",
                ("$id", palette.Id)) ?? throw new InvalidOperationException("Missing Palette test metadata.");
        }
        var invalidPaletteMetadata = JsonPath.ParseRequiredObject(paletteMetadataJson, "Palette test metadata");
        invalidPaletteMetadata["protected"] = "true";
        ReplaceJson("palette_colors", "metadata_json", palette.Id, invalidPaletteMetadata.ToJsonString());
        RejectsReadWithoutMutation(() => database.GetPaletteColorSettings(palette.Id));
        ReplaceJson("palette_colors", "metadata_json", palette.Id, paletteMetadataJson);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RuntimeInputDefaultsUseValueKindOwner()
{
    static JsonObject Definition(string kind, string valueKind, string? defaultValue) => new()
    {
        ["id"] = "test",
        ["label"] = "Test",
        ["jsonKey"] = "test",
        ["kind"] = kind,
        ["valueKind"] = valueKind,
        ["defaultValue"] = defaultValue,
    };

    Equal(
        true,
        RuntimeInputValueKindContract.CreateDefaultValue(
            Definition("boolean", "Boolean", "true"),
            "Test Runtime Input").GetValue<bool>());
    Equal(
        12,
        RuntimeInputValueKindContract.CreateDefaultValue(
            Definition("number", "Integer", "12"),
            "Test Runtime Input").GetValue<int>());
    Equal(
        2,
        RuntimeInputValueKindContract.CreateDefaultValue(
            Definition("iconList", "IconTokenList", "[\"first\",\"second\"]"),
            "Test Runtime Input").AsArray().Count);
    const string iconSlot = """
        [{"id":"button_001","buttonVariantReference":"component_button::variant::default","state":"normal","iconToken":"media_mic","text":"","iconSizeToken":"theme.iconSizes.m","textSizeToken":"theme.typography.sizes.s","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{}}]
        """;
    Equal(
        1,
        RuntimeInputValueKindContract.CreateDefaultValue(
            Definition("iconList", "IconSlots", iconSlot),
            "Test Runtime Input").AsArray().Count);
    Equal(
        "natural",
        RuntimeInputValueKindContract.CreateDefaultValue(
            Definition(
                "behaviorTiming",
                "BehaviorTiming",
                "{\"mode\":\"natural\",\"fixedFrames\":20,\"paceToken\":\"theme.motion.naturalPace.normal\"}"),
            "Test Runtime Input")["mode"]?.GetValue<string>());
    const string componentVariantSlot = """
        {"variantReference":"component_icon_row::variant::default","overrides":{}}
        """;
    Equal(
        "component_icon_row::variant::default",
        ComponentVariantSlotDocumentContract.VariantReference(
            RuntimeInputValueKindContract.CreateDefaultValue(
                Definition(
                    "componentVariantSlot",
                    "ComponentVariantSlot",
                    componentVariantSlot),
                "Test Runtime Input").AsObject(),
            "Test Runtime Input"));

    var behaviorSource = Definition("text", "StringSingleLine", "Sample");
    behaviorSource["id"] = "text";
    behaviorSource["jsonKey"] = "text";
    var behaviorTiming = Definition(
        "behaviorTiming",
        "BehaviorTiming",
        "{\"mode\":\"natural\",\"fixedFrames\":20,\"paceToken\":\"theme.motion.naturalPace.normal\"}");
    behaviorTiming["id"] = "writeOn";
    behaviorTiming["jsonKey"] = "writeOnTiming";
    behaviorTiming["naturalTiming"] = new JsonObject
    {
        ["sourceFieldId"] = "text",
        ["unit"] = "grapheme",
        ["baseFramesPerUnit"] = 7.0,
    };
    RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
        [behaviorSource, behaviorTiming],
        "Test Runtime Inputs");

    var projectedCollection = Definition("collection", "StructuredCollection", null);
    projectedCollection["collection"] = new JsonObject { ["id"] = "items" };
    Equal(
        0,
        RuntimeInputValueKindContract.CreateDefaultValue(
            projectedCollection,
            "Test Runtime Input").AsArray().Count);
    Equal(
        1,
        RuntimeInputValueKindContract.CreateDefaultValue(
            Definition("collection", "StructuredCollection", "[{\"id\":\"item_1\"}]"),
            "Test Runtime Input").AsArray().Count);
    Equal(
        "slide",
        RuntimeInputValueKindContract.ParseValue(
            ValueKind.Motion,
            "{\"transition\":\"slide\",\"direction\":\"bottom\",\"bounds\":\"parent\",\"fade\":false,\"translate\":true,\"scale\":false}",
            "Test Runtime Input")["transition"]?.GetValue<string>());
    Equal(
        "center",
        RuntimeInputValueKindContract.ParseValue(
            ValueKind.AlignmentPlacement,
            "{\"mode\":\"center\",\"alignX\":0.5,\"alignY\":0.5,\"offsetX\":0,\"offsetY\":0}",
            "Test Runtime Input")["mode"]?.GetValue<string>());
    Equal(
        "10|20",
        RuntimeInputValueKindContract.ParseValue(
            ValueKind.IntegerPair,
            "10|20",
            "Test Runtime Input").GetValue<string>());
    Equal(
        "theme.spacing.m|theme.spacing.s",
        RuntimeInputValueKindContract.ParseValue(
            ValueKind.ThemeTokenPair,
            "theme.spacing.m|theme.spacing.s",
            "Test Runtime Input").GetValue<string>());
    Equal(
        "gray_100|gray_000",
        RuntimeInputValueKindContract.ParseValue(
            ValueKind.PaletteColorPair,
            "gray_100|gray_000",
            "Test Runtime Input").GetValue<string>());
    Equal(
        "gray_100|gray_000||1|0.5",
        RuntimeInputValueKindContract.ParseValue(
            ValueKind.PaletteColorAlphaPair,
            "gray_100|gray_000||1|0.5",
            "Test Runtime Input").GetValue<string>());

    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.CreateDefaultValue(
        Definition("boolean", "Boolean", "perhaps"),
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.CreateDefaultValue(
        Definition("number", "Integer", "1.5"),
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.CreateDefaultValue(
        Definition("iconList", "IconSlots", "{}"),
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.CreateDefaultValue(
        Definition("collection", "StructuredCollection", null),
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.Motion,
        "{}",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.ComponentInputBindings,
        "[]",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.ComponentInputBindings,
        "{\"$forwardedInputs\":[]}",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.StructuredCollection,
        "[{\"id\":\"item_1\"},{\"id\":\"item_1\"}]",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.IconSlots,
        "[{\"contentMode\":\"icon\"}]",
        "Test Runtime Input"));
    foreach (var invalidIconSlot in new[]
    {
        iconSlot.Replace("component_button::variant::default", "default", StringComparison.Ordinal),
        iconSlot.Replace("\"buttonOverrides\":{}", "\"buttonOverrides\":null", StringComparison.Ordinal),
        iconSlot.Replace("\"pushElapsedMs\":0", "\"pushElapsedMs\":-1", StringComparison.Ordinal),
        iconSlot.Replace("\"state\":\"normal\"", "\"contentMode\":\"icon\",\"state\":\"normal\"", StringComparison.Ordinal),
        iconSlot.Replace("\"buttonOverrides\":{}", "\"buttonOverrides\":{},\"position\":1", StringComparison.Ordinal),
    })
    {
        Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
            ValueKind.IconSlots,
            invalidIconSlot,
            "Test Runtime Input"));
    }
    foreach (var invalidComponentVariantSlot in new[]
    {
        "\"component_icon_row::variant::default\"",
        "{\"variantReference\":\"component_icon_row::variant::default\"}",
        "{\"variantReference\":\"default\",\"overrides\":{}}",
        "{\"variantReference\":\"component_icon_row::variant::default\",\"overrides\":null}",
        "{\"variantReference\":\"component_icon_row::variant::default\",\"overrides\":{},\"componentType\":\"iconRow\"}",
    })
    {
        Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
            ValueKind.ComponentVariantSlot,
            invalidComponentVariantSlot,
            "Test Runtime Input"));
    }
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.IntegerPair,
        "10|1.5",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.ThemeTokenPair,
        "theme.spacing.m|",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.PaletteColorPair,
        "gray_100",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.PaletteColorAlphaPair,
        "gray_100|gray_000||1|2",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.Alpha,
        "1.01",
        "Test Runtime Input"));
    Throws<InvalidOperationException>(() => RuntimeInputValueKindContract.ParseValue(
        ValueKind.HueDegrees,
        "361",
        "Test Runtime Input"));

    foreach (var invalid in new[]
    {
        "",
        "[]",
        "{}",
        "{\"mode\":\"automatic\",\"fixedFrames\":0,\"paceToken\":\"theme.motion.naturalPace.normal\"}",
        "{\"mode\":\"fixed\",\"fixedFrames\":-1,\"paceToken\":\"theme.motion.naturalPace.normal\"}",
        "{\"mode\":\"fixed\",\"fixedFrames\":12,\"paceToken\":\"theme.motion.other\"}",
    })
    {
        Throws<InvalidOperationException>(() => BehaviorTimingValue.Parse(invalid));
    }

    var missingNaturalTiming = behaviorTiming.DeepClone().AsObject();
    missingNaturalTiming.Remove("naturalTiming");
    Throws<InvalidOperationException>(() =>
        RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
            [behaviorSource, missingNaturalTiming],
            "Test Runtime Inputs"));
    var missingSource = behaviorTiming.DeepClone().AsObject();
    missingSource["naturalTiming"]!["sourceFieldId"] = "missing";
    Throws<InvalidOperationException>(() =>
        RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
            [behaviorSource, missingSource],
            "Test Runtime Inputs"));
    var invalidBaseRate = behaviorTiming.DeepClone().AsObject();
    invalidBaseRate["naturalTiming"]!["baseFramesPerUnit"] = 0;
    Throws<InvalidOperationException>(() =>
        RuntimeInputValueKindContract.ValidateBehaviorTimingDefinitions(
            [behaviorSource, invalidBaseRate],
            "Test Runtime Inputs"));

    AssertRejectedDatabaseIsReadOnly("runtime-boolean-default", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET design_preview_json = json_set(design_preview_json, '$.inputs[6].defaultValue', 'perhaps') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-behavior-timing-default", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET design_preview_json = json_set(design_preview_json, '$.collections[0].fields[4].defaultValue', '{}') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-behavior-timing-metadata", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET design_preview_json = json_remove(design_preview_json, '$.collections[0].fields[4].naturalTiming') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-behavior-timing-source", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET design_preview_json = json_set(design_preview_json, '$.collections[0].fields[4].naturalTiming.sourceFieldId', 'missing') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-behavior-timing-pace", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE module_instances SET content_json = json_set(content_json, '$.messages[0].writeOnTiming.paceToken', 'theme.motion.other') WHERE id = 'module_instance_900f1616432d4f63a97f2a74dd647e08'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-pair-label", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE modules SET design_preview_json = json_remove(design_preview_json, '$.collections[0].fields[11].pairFirstLabel') WHERE id = 'module_core_chat'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-component-variant-slot-string", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET design_preview_json = json_set(
                design_preview_json,
                '$.placeholder',
                'retired-runtime-placeholder')
            WHERE component_type = 'textBox'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-component-variant-slot-missing-overrides", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET config_json = json_remove(
                config_json,
                '$.textBox.rightIconRowSlot.overrides')
            WHERE component_type = 'textBox'
            """;
        command.ExecuteNonQuery();
    });
}

static void TextBoxPreviewResolvesVariantOwnedIconRowSlots()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
    var textBox = nodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentClass
        && database.GetComponentClassSettings(node.Id).ComponentType == "textBox");
    var textBoxVariant = textBox.Children.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.IsProtected);
    var settings = database.GetComponentClassSettings(textBox.Id);
    var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
    var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
    var payload = Required(CreatePreviewPayload(database, textBoxVariant, theme.Id));
    var session = new ComponentPreviewInputSession(
        database.Design,
        database.DictionaryContext,
        database.Resources,
        database.ProjectPaths,
        () => { });
    session.UpdateForPayload(payload, settings.ProjectId);
    var resolved = session.ApplyInputs(payload, "light", settings.ProjectId);
    var preview = DesignPreviewTestValues.Parse(resolved.DesignPreviewJson);
    var config = JsonPath.ParseRequiredObject(resolved.ConfigJson, "Text Box Preview config");
    var textBoxConfig = JsonPath.RequiredObject(config, "textBox", "Text Box Preview config");

    foreach (var side in new[] { "left", "right" })
    {
        var key = $"{side}IconRowSlot";
        True(!preview.ContainsKey(key));
        var slot = JsonPath.RequiredObject(textBoxConfig, key, "Text Box Preview config.textBox");
        ComponentVariantSlotDocumentContract.Validate(slot, $"Text Box Variant '{key}'");
        Equal(
            "component_project_foqn_s2_iconRow::variant::default",
            ComponentVariantSlotDocumentContract.VariantReference(slot, $"Text Box Variant '{key}'"));
        Equal(0, ComponentVariantSlotDocumentContract.Overrides(slot, $"Text Box Variant '{key}'").Count);
    }
    var iconRowVariant = nodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Id == "component_project_foqn_s2_iconRow::variant::default");
    True(database.ReferenceUsages.GetReferenceUsageDetails(iconRowVariant).Any((usage) =>
        usage.SourceKind == ProjectTreeNodeKind.ComponentVariant
        && usage.SourceNodeId == textBoxVariant.Id
        && usage.Field == "Left icon row"));

    var html = WebDesignPreviewRenderer.RenderBodyAsync(
        database.GetDevicePreviewMetrics(device.Id),
        false,
        resolved).GetAwaiter().GetResult();
    True(!string.IsNullOrWhiteSpace(html));
    True(!html.Contains("preview-error", StringComparison.Ordinal));
}

static void PairFieldsRequireExplicitLabels()
{
    static void AssertExplicit(ValueKind valueKind, PairFieldLabels? labels, string owner)
    {
        if (!PairFieldLabelsContract.IsPair(valueKind))
        {
            return;
        }

        var required = PairFieldLabelsContract.Require(labels, owner);
        True(!string.IsNullOrWhiteSpace(required.First));
        True(!string.IsNullOrWhiteSpace(required.Second));
    }

    foreach (var field in RecordClassFieldCatalog.All)
    {
        AssertExplicit(field.ValueKind, field.PairLabels, $"Record field '{field.Id}'");
    }

    foreach (var field in ComponentClassFieldCatalog.All())
    {
        AssertExplicit(field.ValueKind, field.PairLabels, $"Component field '{field.Id}'");
        foreach (var input in field.ComponentInputBindings ?? [])
        {
            AssertExplicit(
                input.ValueKind,
                input.PairLabels,
                $"Component field '{field.Id}' input '{input.Id}'");
        }
    }

    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
    foreach (var variant in nodes.Where((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant))
    {
        foreach (var input in database.GetComponentVariantRuntimeInputBindings(variant.Id))
        {
            AssertExplicit(
                input.ValueKind,
                input.PairLabels,
                $"Component Variant '{variant.Id}' input '{input.Id}'");
        }
    }

    var media = nodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentClass
        && database.GetComponentClassSettings(node.Id).ComponentType == "media");
    var mediaDefaultVariant = media.Children.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.IsProtected);
    var viewportSize = database.GetComponentVariantRuntimeInputBindings(mediaDefaultVariant.Id)
        .Single((input) => input.Id == "viewportSize");
    Equal("W", Required(viewportSize.PairLabels).First);
    Equal("H", Required(viewportSize.PairLabels).Second);

    var labels = PairFieldLabelsContract.Require(new PairFieldLabels("X", "Y"), "Test pair");
    Equal("X", labels.First);
    Equal("Y", labels.Second);
    Throws<InvalidOperationException>(() => PairFieldLabelsContract.Require(null, "Missing pair"));
    Throws<InvalidOperationException>(() => PairFieldLabelsContract.Require(new PairFieldLabels("", "Y"), "Incomplete pair"));
    Throws<InvalidOperationException>(() => DictionaryFieldPairText.Labels(new FieldDefinition(
        "looks.like.size",
        "Size",
        ValueKind.IntegerPair)));
}

static void NumericDictionaryFieldsSeparateCurrentValuesFromDrafts()
{
    var integer = new FieldDefinition(
        "test.integer",
        "Integer",
        ValueKind.Integer,
        Number: new NumberDefinition(0, 10, 1, 0));
    var decimalField = new FieldDefinition(
        "test.decimal",
        "Decimal",
        ValueKind.Decimal,
        Number: new NumberDefinition(0, 1, 0.05m, 2));

    Equal(5m, DictionaryNumericValueContract.ParseRequired(integer, "5"));
    Equal(0.35m, DictionaryNumericValueContract.ParseRequired(decimalField, "0.35"));
    Throws<InvalidOperationException>(() => DictionaryNumericValueContract.ParseRequired(integer, "1.5"));
    Throws<InvalidOperationException>(() => DictionaryNumericValueContract.ParseRequired(integer, "invalid"));
    Throws<InvalidOperationException>(() => DictionaryNumericValueContract.ParseRequired(integer, "11"));
    Throws<InvalidOperationException>(() => DictionaryNumericValueContract.ParseRequired(decimalField, ""));
    Throws<InvalidOperationException>(() => DictionaryNumericValueContract.ParseRequired(decimalField, "1.01"));

    True(DictionaryNumericValueContract.TryParseDraft(integer, "6", out var integerDraft));
    Equal(6m, integerDraft);
    True(!DictionaryNumericValueContract.TryParseDraft(integer, "6.5", out _));
    True(!DictionaryNumericValueContract.TryParseDraft(integer, "", out _));
    True(!DictionaryNumericValueContract.TryParseDraft(integer, "12", out _));
    True(DictionaryNumericValueContract.TryParseDraft(decimalField, "0.4", out var decimalDraft));
    Equal(0.4m, decimalDraft);
    True(!DictionaryNumericValueContract.TryParseDraft(decimalField, "draft", out _));
    True(!DictionaryNumericValueContract.TryParseDraft(decimalField, "2", out _));
}

static void ValidIntegerPairsCommitAfterEditingPause()
{
    var control = new DictionaryIntegerPairControl(
        new FieldDefinition(
            "test.size",
            "Size",
            ValueKind.IntegerPair,
            PairLabels: new PairFieldLabels("W", "H")),
        "112|48");
    var committed = "";
    var commitCount = 0;
    control.ValueCommitted += (_, value) =>
    {
        committed = value;
        commitCount++;
    };
    var first = typeof(DictionaryIntegerPairControl)
        .GetField("_firstTextBox", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.GetValue(control) as TextBox
        ?? throw new InvalidOperationException("Missing first Integer Pair editor.");

    first.Text = "300";
    for (var attempt = 0; attempt < 10 && committed.Length == 0; attempt++)
    {
        Thread.Sleep(100);
        Dispatcher.UIThread.RunJobs();
    }
    Equal("300|48", committed);
    Equal(1, commitCount);

    first.Text = "";
    for (var attempt = 0; attempt < 5; attempt++)
    {
        Thread.Sleep(100);
        Dispatcher.UIThread.RunJobs();
    }
    Equal(1, commitCount);
}

static void IconRowPreservesSequentialNestedButtonOverrideCommits()
{
    const string iconSlots = """
        [{"id":"decline","buttonVariantReference":"component_project_button::variant::default","state":"normal","iconToken":"phone_hangup","text":"Decline","iconSizeToken":"theme.iconSizes.m","textSizeToken":"theme.typography.sizes.s","pushTrigger":false,"pushElapsedMs":0,"buttonOverrides":{"button":{"dimensionMode":"fixed","size":"112|48"}}}]
        """;
    var items = RuntimeInputValueKindContract.ParseValue(
            ValueKind.IconSlots,
            iconSlots,
            "Sequential Icon Row Overrides")
        .AsArray()
        .OfType<JsonObject>()
        .Select((item) => item.DeepClone().AsObject())
        .ToList();
    static string Slot(string size) => new JsonObject
    {
        ["variantReference"] = "component_project_button::variant::default",
        ["overrides"] = new JsonObject
        {
            ["button"] = new JsonObject
            {
                ["dimensionMode"] = "fixed",
                ["size"] = size,
                ["padding"] = "theme.spacing.s|theme.spacing.s",
            },
        },
    }.ToJsonString();

    IconSlotsDocumentContract.ReplaceButtonVariantSlot(
        items,
        "decline",
        Slot("112|48"),
        "Sequential Icon Row");
    items = RuntimeInputValueKindContract.ParseValue(
            ValueKind.IconSlots,
            new JsonArray(items.Select((item) => (JsonNode?)item.DeepClone()).ToArray()).ToJsonString(),
            "Rebuilt Sequential Icon Row Overrides")
        .AsArray()
        .OfType<JsonObject>()
        .Select((item) => item.DeepClone().AsObject())
        .ToList();
    IconSlotsDocumentContract.ReplaceButtonVariantSlot(
        items,
        "decline",
        Slot("300|300"),
        "Sequential Icon Row");

    var persisted = items.Single();
    var buttonOverrides = JsonPath.RequiredObject(
        persisted,
        "buttonOverrides",
        "Sequential Icon Row Button");
    var button = JsonPath.RequiredObject(
        buttonOverrides,
        "button",
        "Sequential Icon Row Button Overrides");
    Equal("fixed", JsonPath.RequiredString(
        button,
        "dimensionMode",
        "Sequential Icon Row Button Overrides"));
    Equal("300|300", JsonPath.RequiredString(
        button,
        "size",
        "Sequential Icon Row Button Overrides"));
    Equal("theme.spacing.s|theme.spacing.s", JsonPath.RequiredString(
        button,
        "padding",
        "Sequential Icon Row Button Overrides"));
}

static void PreviewActionContractsAreStrict()
{
    static JsonObject Action() => new()
    {
        ["id"] = "play",
        ["label"] = "Play",
        ["playInputId"] = "isPlaying",
        ["durationSeconds"] = 1,
        ["timeJsonKey"] = "currentTimeSeconds",
        ["timeUnit"] = "seconds",
        ["prewarmFrames"] = false,
        ["completionBehavior"] = "reset",
    };

    static JsonObject Preview(JsonNode? action) => new()
    {
        ["actions"] = new JsonArray(action),
    };

    var valid = Preview(Action());
    var parsed = ComponentPreviewActions.Read(valid).Single();
    Equal("play", parsed.Id);
    Equal("Play", parsed.Label);
    Equal(ComponentPreviewActionTimeUnit.Seconds, parsed.TimeUnit);
    True(!ComponentPreviewActionRuntimeValue.BooleanOrDefault(
        valid,
        parsed,
        parsed.PlayInputId,
        absentValue: false));
    Equal(
        0d,
        ComponentPreviewActionRuntimeValue.TimeOrDefault(
            valid,
            parsed,
            absentValue: 0));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.Read(
        new JsonObject { ["collections"] = null }));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        new JsonObject { ["actions"] = null },
        "Null action root"));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        new JsonObject
        {
            ["collections"] = new JsonArray
            {
                new JsonObject { ["itemActions"] = null },
            },
        },
        "Null item action root"));
    foreach (var nullOptionalKey in new[]
    {
        "durationThemeToken",
        "durationBaseFrames",
        "durationEnabledInputId",
        "playFieldId",
        "prewarmFrames",
        "prewarmWhenJsonKey",
        "activateInputIds",
        "targetOptions",
    })
    {
        var nullOptional = Action();
        nullOptional[nullOptionalKey] = null;
        Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
            Preview(nullOptional),
            $"Null optional action member {nullOptionalKey}"));
    }

    static JsonObject EmbeddedRuntimeCollection() => new()
    {
        ["id"] = "states",
        ["label"] = "States",
        ["jsonKey"] = "states",
        ["itemLabel"] = "State",
        ["fields"] = new JsonArray(),
        ["itemRuntimeContractJsonKey"] = "runtimeContract",
    };
    var validEmbeddedRuntime = new JsonObject
    {
        ["collections"] = new JsonArray(EmbeddedRuntimeCollection()),
        ["states"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "state_1",
                ["runtimeContract"] = new JsonObject(),
            },
        },
    };
    Equal(
        0,
        ComponentPreviewActions.ReadWithEmbedded(
            validEmbeddedRuntime,
            (_) => new JsonObject()).Count);
    var wrongEmbeddedRuntime = validEmbeddedRuntime.DeepClone().AsObject();
    wrongEmbeddedRuntime["states"]![0]!["runtimeContract"] = new JsonArray();
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ReadWithEmbedded(
        wrongEmbeddedRuntime,
        (_) => new JsonObject()));
    var missingEmbeddedRuntime = validEmbeddedRuntime.DeepClone().AsObject();
    missingEmbeddedRuntime["states"]![0]!.AsObject().Remove("runtimeContract");
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ReadWithEmbedded(
        missingEmbeddedRuntime,
        (_) => new JsonObject()));

    var validThemeDuration = Action();
    validThemeDuration.Remove("durationSeconds");
    validThemeDuration["durationThemeToken"] = "theme.motion.buttonPushedDurationMs";
    Equal(
        "theme.motion.buttonPushedDurationMs",
        ComponentPreviewActions.Read(Preview(validThemeDuration)).Single().DurationThemeToken);

    var durationInputAction = Action();
    durationInputAction.Remove("durationSeconds");
    durationInputAction["durationInputId"] = "durationField";
    var durationInputPreview = Preview(durationInputAction);
    durationInputPreview["inputs"] = new JsonArray
    {
        new JsonObject
        {
            ["id"] = "durationField",
            ["jsonKey"] = "durationValue",
        },
    };
    durationInputPreview["durationValue"] = 2.5;
    durationInputPreview["currentTimeSeconds"] = 0;
    durationInputPreview["isPlaying"] = false;
    var parsedDurationInputAction = ComponentPreviewActions.Read(durationInputPreview).Single();
    Equal(
        2.5d,
        ComponentPreviewActionRuntimeValue.RequireDurationInput(
            durationInputPreview,
            parsedDurationInputAction));
    Equal(
        2.5d,
        ComponentPreviewActionRuntimeValue.RequireDurationInput(
            "2,5",
            parsedDurationInputAction));
    Equal(
        0d,
        ComponentPreviewActionRuntimeValue.RequireTime(
            durationInputPreview,
            parsedDurationInputAction));
    True(!ComponentPreviewActionRuntimeValue.RequireBoolean(
        durationInputPreview,
        parsedDurationInputAction,
        parsedDurationInputAction.PlayInputId));
    var wrongDurationReference = durationInputPreview.DeepClone().AsObject();
    wrongDurationReference["actions"]![0]!["durationInputId"] = "durationValue";
    Throws<InvalidOperationException>(() => ComponentPreviewActions.Read(wrongDurationReference));
    durationInputPreview["durationValue"] = "2.5";
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.RequireDurationInput(
        durationInputPreview,
        parsedDurationInputAction));
    durationInputPreview["durationValue"] = 0;
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.RequireDurationInput(
        durationInputPreview,
        parsedDurationInputAction));
    durationInputPreview["durationValue"] = 2.5;
    durationInputPreview["currentTimeSeconds"] = "0";
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.RequireTime(
        durationInputPreview,
        parsedDurationInputAction));
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.TimeOrDefault(
        durationInputPreview,
        parsedDurationInputAction,
        absentValue: 0));
    durationInputPreview["currentTimeSeconds"] = 0;
    durationInputPreview["isPlaying"] = "false";
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.RequireBoolean(
        durationInputPreview,
        parsedDurationInputAction,
        parsedDurationInputAction.PlayInputId));
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.BooleanOrDefault(
        durationInputPreview,
        parsedDurationInputAction,
        parsedDurationInputAction.PlayInputId,
        absentValue: false));

    var collectionDurationAction = Action();
    collectionDurationAction.Remove("durationSeconds");
    collectionDurationAction["durationCollectionJsonKey"] = "items";
    collectionDurationAction["durationBaseFrames"] = 1;
    collectionDurationAction["durationItemNumberKeys"] = new JsonArray("frames");
    collectionDurationAction["durationCollectionMultiplierNumberKeys"] = new JsonArray("gap");
    var collectionDurationPreview = Preview(collectionDurationAction);
    collectionDurationPreview["items"] = new JsonArray
    {
        new JsonObject { ["id"] = "a", ["frames"] = 2 },
        new JsonObject { ["id"] = "b", ["frames"] = 3 },
    };
    collectionDurationPreview["gap"] = 1;
    var parsedCollectionDurationAction = ComponentPreviewActions.Read(collectionDurationPreview).Single();
    Equal(
        8,
        ComponentPreviewActionRuntimeValue.CollectionDurationFrames(
            collectionDurationPreview,
            parsedCollectionDurationAction));
    ((JsonObject)((JsonArray)collectionDurationPreview["items"]!)[0]!)["frames"] = "2";
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.CollectionDurationFrames(
        collectionDurationPreview,
        parsedCollectionDurationAction));

    var behaviorDurationAction = Action();
    behaviorDurationAction.Remove("durationSeconds");
    behaviorDurationAction["durationBehaviorTimingInputId"] = "timing";
    var behaviorDurationPreview = Preview(behaviorDurationAction);
    behaviorDurationPreview["inputs"] = new JsonArray("invalid");
    var parsedBehaviorDurationAction = ComponentPreviewActions.Read(behaviorDurationPreview).Single();
    Throws<InvalidOperationException>(() => ComponentPreviewActionRuntimeValue.RequireInputDefinitions(
        behaviorDurationPreview,
        parsedBehaviorDurationAction));

    var missingId = Action();
    missingId.Remove("id");
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(missingId),
        "Missing id"));
    var missingLabel = Action();
    missingLabel.Remove("label");
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(missingLabel),
        "Missing label"));
    var missingDuration = Action();
    missingDuration.Remove("durationSeconds");
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(missingDuration),
        "Missing duration"));
    var invalidUnit = Action();
    invalidUnit["timeUnit"] = "automatic";
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(invalidUnit),
        "Invalid time unit"));
    var invalidBoolean = Action();
    invalidBoolean["prewarmFrames"] = "false";
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(invalidBoolean),
        "Invalid boolean"));
    var invalidList = Action();
    invalidList["activateInputIds"] = new JsonArray("enabled", 4);
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(invalidList),
        "Invalid string list"));
    var invalidOptions = Action();
    invalidOptions["targetInputId"] = "state";
    invalidOptions["targetMode"] = "option";
    invalidOptions["targetOptions"] = new JsonArray("invalid");
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(invalidOptions),
        "Invalid options"));
    var unknownThemeDuration = Action();
    unknownThemeDuration.Remove("durationSeconds");
    unknownThemeDuration["durationThemeToken"] = "theme.motion.missing";
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(unknownThemeDuration),
        "Unknown Theme duration"));
    var unknownAdditionalThemeDuration = Action();
    unknownAdditionalThemeDuration["durationAdditionalThemeTokens"] = new JsonArray("theme.motion.missing");
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(unknownAdditionalThemeDuration),
        "Unknown additional Theme duration"));
    var incompleteStateDuration = Action();
    incompleteStateDuration["durationStateCollectionJsonKey"] = "states";
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        Preview(incompleteStateDuration),
        "Incomplete State duration"));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        new JsonObject { ["actions"] = new JsonObject() },
        "Wrong action root"));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        new JsonObject { ["actions"] = new JsonArray(Action(), Action()) },
        "Duplicate action"));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.ValidateContract(
        new JsonObject
        {
            ["collections"] = new JsonArray
            {
                new JsonObject { ["itemActions"] = new JsonObject() },
            },
        },
        "Wrong item action root"));

    var storedValues = new JsonObject { ["enabled"] = true, ["count"] = 2, ["progress"] = 0.5 };
    Throws<InvalidOperationException>(() => ComponentPreviewActions.SetStoredValue(
        storedValues,
        parsed,
        "enabled",
        "perhaps"));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.SetStoredValue(
        storedValues,
        parsed,
        "count",
        "2.5"));
    Throws<InvalidOperationException>(() => ComponentPreviewActions.SetStoredValue(
        storedValues,
        parsed,
        "progress",
        "invalid"));

    var motionTheme = Object("""
        {"motion":{"transitions":{"fade":{"delayMs":0,"durationMs":180},"slide":{"delayMs":100,"durationMs":260}}}}
        """);
    var slideMotion = Object("""
        {"transition":"slide","direction":"bottom","bounds":"screen","fade":false,"translate":true,"scale":false}
        """);
    Equal(
        360d,
        MotionTimingDuration.RequirePositiveMilliseconds(
            motionTheme,
            slideMotion,
            "Slide action"));
    Equal(
        180d,
        MotionTimingDuration.ResolveMilliseconds(
            motionTheme,
            Object("""{"transition":"none","direction":"bottom","bounds":"screen","fade":true,"translate":false,"scale":false}"""),
            "Fade action"));
    Equal(
        0d,
        MotionTimingDuration.ResolveMilliseconds(
            motionTheme,
            Object("""{"transition":"none","direction":"bottom","bounds":"screen","fade":false,"translate":false,"scale":false}"""),
            "No Motion"));
    Throws<InvalidOperationException>(() => MotionTimingDuration.RequirePositiveMilliseconds(
        motionTheme,
        Object("""{"transition":"none","direction":"bottom","bounds":"screen","fade":false,"translate":false,"scale":false}"""),
        "Missing finite Motion"));
    Throws<InvalidOperationException>(() => MotionTimingDuration.ResolveMilliseconds(
        Object("""{"motion":{"transitions":{"slide":{"delayMs":0}}}}"""),
        slideMotion,
        "Missing duration"));
    Throws<InvalidOperationException>(() => MotionTimingDuration.ResolveMilliseconds(
        Object("""{"motion":{"transitions":{"slide":{"delayMs":0,"durationMs":"260"}}}}"""),
        slideMotion,
        "Wrong duration type"));

    AssertRejectedDatabaseIsReadOnly("preview-action-id", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET design_preview_json = json_remove(design_preview_json, '$.actions[0].id') WHERE id = 'component_project_foqn_s2_audio'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("preview-action-theme-token", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET design_preview_json = json_set(design_preview_json, '$.actions[0].durationThemeToken', 'theme.motion.missing') WHERE id = 'component_project_foqn_s2_button'";
        command.ExecuteNonQuery();
    });

    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-action-motion-path-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var writeContext = new SqliteProjectContext(temporary);
        using (var connection = writeContext.OpenConnection())
        {
            writeContext.Execute(
                connection,
                "UPDATE component_classes SET design_preview_json = json_set(design_preview_json, '$.actions[0].durationMotionConfigPath', 'keyboard.missing') WHERE id = 'component_project_foqn_s2_keyboard'");
        }
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var keyboardVariant = nodes.Single((node) =>
            node.Kind == ProjectTreeNodeKind.ComponentVariant
            && node.Parent?.Id == "component_project_foqn_s2_keyboard"
            && node.IsProtected);
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        Throws<InvalidOperationException>(() => CreatePreviewPayload(database, keyboardVariant, theme.Id));
        SequenceEqual(before, SHA256.HashData(File.ReadAllBytes(temporary)));
    }
    finally
    {
        File.Delete(temporary);
    }

    var currentDatabase = new SqliteProjectTestContext(sourcePath);
    var currentNodes = Descendants(currentDatabase.LoadProjectTree()).ToList();
    var bubbleVariant = currentNodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Parent?.Id == "component_project_foqn_s2_bubble"
        && node.IsProtected);
    var currentTheme = currentNodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
    var bubblePayload = Required(CreatePreviewPayload(
        currentDatabase,
        bubbleVariant,
        currentTheme.Id));
    var bubblePreview = Object(bubblePayload.DesignPreviewJson);
    var bubbleFullScreenAction = bubblePreview["actions"]?.AsArray()
        .OfType<JsonObject>()
        .Single((action) => action["id"]?.GetValue<string>() == "fullScreen")
        ?? throw new InvalidOperationException("Missing Bubble Full screen action.");
    Equal(0.3d, bubbleFullScreenAction["durationSeconds"]?.GetValue<double>() ?? 0);
    True(bubbleFullScreenAction["durationMotionConfigPath"] is null);
}

static void DesignTestValuesPreserveStrictDocuments()
{
    var input = new ComponentInputDefinition(
        "title",
        "Title",
        "title",
        ComponentInputKind.Text,
        ValueKind.StringSingleLine,
        "Default");
    var collection = new RuntimeInputCollectionDefinition(
        "items",
        "Items",
        "items",
        "Item",
        [input]);

    Throws<InvalidOperationException>(() => DesignPreviewTestValues.RuntimeJson(
        new JsonObject { ["testValues"] = new JsonArray() }.ToJsonString()));
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.SetValue(
        new JsonObject { ["testValues"] = JsonValue.Create(false) },
        input,
        "Value"));
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionItems(
        new JsonObject { ["items"] = new JsonObject() },
        collection));
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionItems(
        new JsonObject
        {
            ["testValues"] = new JsonObject
            {
                ["items"] = new JsonArray
                {
                    new JsonObject { ["id"] = "item_1" },
                    new JsonObject { ["id"] = "item_1" },
                },
            },
        },
        collection));
    var invalidCollectionSourceDefinition = new JsonObject
    {
        ["collections"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "items",
                ["label"] = "Items",
                ["jsonKey"] = "items",
                ["itemLabel"] = "Item",
                ["fields"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "title",
                        ["label"] = "Title",
                        ["jsonKey"] = "title",
                        ["kind"] = "text",
                        ["valueKind"] = "StringSingleLine",
                        ["defaultValue"] = "Default",
                    },
                },
                ["sourceCollectionJsonKey"] = false,
            },
        },
    };
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.RuntimeJson(
        invalidCollectionSourceDefinition.ToJsonString()));
    Equal("Default", DesignPreviewTestValues.Value(new JsonObject(), input));
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.Value(
        new JsonObject { ["title"] = false },
        input));
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.Value(
        new JsonObject
        {
            ["title"] = "Persisted",
            ["testValues"] = new JsonObject { ["title"] = false },
        },
        input));
    Equal("Default", DesignPreviewTestValues.CollectionValue(new JsonObject(), input));
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionValue(
        new JsonObject { ["title"] = false },
        input));

    var decimalInput = new ComponentInputDefinition(
        "opacity",
        "Opacity",
        "opacity",
        ComponentInputKind.Number,
        ValueKind.Decimal,
        "1");
    Equal(
        "0.35",
        DesignPreviewTestValues.Value(
            new JsonObject { ["opacity"] = 0.35 },
            decimalInput));
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.Value(
        new JsonObject { ["opacity"] = "0.35" },
        decimalInput));

    var variantInput = new ComponentInputDefinition(
        "componentVariant",
        "Component Variant",
        "variantReference",
        ComponentInputKind.ComponentVariant,
        ValueKind.ComponentVariant,
        "component_example::variant::default");
    var componentItems = new RuntimeComponentCollectionItemDefinition(
        "variantReference",
        "overrides",
        "inputs");
    var componentCollection = new RuntimeInputCollectionDefinition(
        "components",
        "Components",
        "components",
        "Component",
        [variantInput],
        ComponentItems: componentItems);
    static JsonObject ComponentItem() => new()
    {
        ["id"] = "component_item_1",
        ["variantReference"] = "component_example::variant::default",
        ["overrides"] = new JsonObject(),
        ["inputs"] = new JsonObject(),
    };
    var componentPreview = new JsonObject
    {
        ["components"] = new JsonArray(ComponentItem()),
    };
    Equal(
        1,
        DesignPreviewTestValues.CollectionItems(componentPreview, componentCollection).Count);
    var explicitEmptyComponent = ComponentItem();
    explicitEmptyComponent["variantReference"] = "";
    Equal(
        "",
        DesignPreviewTestValues.CollectionItems(
            new JsonObject { ["components"] = new JsonArray(explicitEmptyComponent) },
            componentCollection).Single()["variantReference"]?.GetValue<string>() ?? "missing");
    var currentComponentItem = DesignPreviewTestValues.CurrentCollectionItems(
        componentPreview,
        componentCollection).Single();
    currentComponentItem["resolved"] = true;
    True(componentPreview["components"]?[0]?["resolved"]?.GetValue<bool>() == true);
    var shortReference = ComponentItem();
    shortReference["variantReference"] = "default";
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionItems(
        new JsonObject { ["components"] = new JsonArray(shortReference) },
        componentCollection));
    var missingOverrides = ComponentItem();
    missingOverrides.Remove("overrides");
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionItems(
        new JsonObject { ["components"] = new JsonArray(missingOverrides) },
        componentCollection));
    var wrongInputs = ComponentItem();
    wrongInputs["inputs"] = new JsonArray();
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionItems(
        new JsonObject { ["components"] = new JsonArray(wrongInputs) },
        componentCollection));

    var projectedCollection = new RuntimeInputCollectionDefinition(
        "states",
        "States",
        "states",
        "State",
        [],
        ItemRuntimeContractJsonKey: "runtimeContract");
    var projectedItem = new JsonObject
    {
        ["id"] = "state_1",
        ["runtimeContract"] = new JsonObject(),
    };
    Equal(
        1,
        DesignPreviewTestValues.CollectionItems(
            new JsonObject { ["states"] = new JsonArray(projectedItem.DeepClone()) },
            projectedCollection).Count);
    var missingProjectedContract = projectedItem.DeepClone().AsObject();
    missingProjectedContract.Remove("runtimeContract");
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionItems(
        new JsonObject { ["states"] = new JsonArray(missingProjectedContract) },
        projectedCollection));
    var wrongProjectedContract = projectedItem.DeepClone().AsObject();
    wrongProjectedContract["runtimeContract"] = new JsonArray();
    Throws<InvalidOperationException>(() => DesignPreviewTestValues.CollectionItems(
        new JsonObject { ["states"] = new JsonArray(wrongProjectedContract) },
        projectedCollection));

    var preview = new JsonObject { ["title"] = "Default" };
    DesignPreviewTestValues.SetValue(preview, input, "Test");
    Equal("Test", DesignPreviewTestValues.Value(preview, input));
    Equal(
        "Test",
        DesignPreviewTestValues.Parse(
            DesignPreviewTestValues.RuntimeJson(preview.ToJsonString()))["title"]?.GetValue<string>());
}

static void ComponentAndModuleVariantsShareReferenceGrammar()
{
    var reference = VariantReferenceId.Format("owner_001", "variant_001");
    Equal("owner_001::variant::variant_001", reference);
    True(VariantReferenceId.TryParse(reference, out var ownerId, out var variantId));
    Equal("owner_001", ownerId);
    Equal("variant_001", variantId);
    True(VariantReferenceId.HasVariantId(
        VariantReferenceId.Format("owner_001", "default"),
        "default"));
    True(!VariantReferenceId.HasVariantId(reference, "default"));

    foreach (var malformed in new[] { "", "owner_001", "::variant::default", "owner_001::variant::" })
    {
        True(!VariantReferenceId.TryParse(malformed, out _, out _));
    }
}

static void ComponentAndModuleVariantsShareEnvelopeOperations()
{
    Equal("default", VariantEnvelopeContract.DefaultId);
    var variants = new JsonArray
    {
        new JsonObject { ["id"] = "default" },
        new JsonObject { ["id"] = "new_variant" },
        new JsonObject { ["id"] = "new_variant_2" },
        new JsonObject { ["id"] = "variant" },
    };
    Equal("new_variant", VariantEnvelopeContract.FindSource(variants, "new_variant")?["id"]?.GetValue<string>());
    True(VariantEnvelopeContract.FindSource(variants, "missing") is null);
    Equal("new_variant_3", VariantEnvelopeContract.UniqueId(variants, "New Variant"));
    Equal("variant_2", VariantEnvelopeContract.UniqueId(variants, "---"));

    var config = new JsonObject { ["value"] = 7 };
    var source = VariantEnvelopeContract.CreateSource("new", "New", config);
    Equal("new", source["id"]?.GetValue<string>());
    Equal("New", source["name"]?.GetValue<string>());
    Equal(false, source["protected"]?.GetValue<bool>());
    Equal(false, source["locked"]?.GetValue<bool>());
    Equal(7, source["config"]?["value"]?.GetValue<int>());
}

static void DefaultVariantEditingUnlockIsSessionOnly()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Path.GetTempPath(),
        $"mockups-default-variant-session-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree()
            .SelectMany(DescendantsAndSelf)
            .ToList();
        var componentDefault = nodes.Single((node) =>
            node.Id == "component_project_foqn_s2_label::variant::default");
        var moduleDefault = nodes.Single((node) =>
            node.Id == "module_core_chat::variant::default");
        True(componentDefault.IsProtected);
        True(componentDefault.IsLocked);
        True(moduleDefault.IsProtected);
        True(moduleDefault.IsLocked);

        var beforeUnlock = SHA256.HashData(File.ReadAllBytes(temporary));
        componentDefault = NodeCommands(database)
            .ToggleComponentVariantLock(componentDefault);
        moduleDefault = NodeCommands(database)
            .ToggleModuleVariantLock(moduleDefault);
        True(!componentDefault.IsLocked);
        True(!moduleDefault.IsLocked);
        SequenceEqual(beforeUnlock, SHA256.HashData(File.ReadAllBytes(temporary)));

        database.UpdateComponentVariantField(
            componentDefault,
            "component.label.padding",
            "theme.spacing.s|theme.spacing.s");
        database.UpdateModuleVariantField(
            moduleDefault,
            "module.conversation.showHeader",
            "false");
        Equal(true, PersistedDefaultLock(temporary, "component_classes", "component_project_foqn_s2_label"));
        Equal(true, PersistedDefaultLock(temporary, "modules", "module_core_chat"));

        var nextSession = new SqliteProjectTestContext(temporary);
        var nextNodes = nextSession.LoadProjectTree()
            .SelectMany(DescendantsAndSelf)
            .ToList();
        var nextComponentDefault = nextNodes.Single((node) =>
            node.Id == componentDefault.Id);
        var nextModuleDefault = nextNodes.Single((node) =>
            node.Id == moduleDefault.Id);
        True(nextComponentDefault.IsLocked);
        True(nextModuleDefault.IsLocked);
        Throws<InvalidOperationException>(() => nextSession.UpdateComponentVariantField(
            nextComponentDefault,
            "component.label.padding",
            "theme.spacing.m|theme.spacing.m"));
        Throws<InvalidOperationException>(() => nextSession.UpdateModuleVariantField(
            nextModuleDefault,
            "module.conversation.showHeader",
            "true"));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static bool PersistedDefaultLock(
    string databasePath,
    string table,
    string ownerId)
{
    using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = $"""
        SELECT json_extract(value, '$.locked')
        FROM {table} owner, json_each(owner.metadata_json, '$.variants')
        WHERE owner.id = $id
          AND json_extract(value, '$.id') = 'default'
        """;
    command.Parameters.AddWithValue("$id", ownerId);
    return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
}

static void FixedStructuralRuntimeCollectionsReconcileByStableIds()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var iconRow = database.LoadProjectTree()
        .SelectMany(DescendantsAndSelf)
        .Single((node) =>
            node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "iconRow");
    var references = iconRow.Children
        .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant)
        .ToDictionary(
            (node) => node.Id[(node.Id.LastIndexOf("::variant::", StringComparison.Ordinal) + "::variant::".Length)..],
            (node) => node.Id,
            StringComparer.Ordinal);
    var defaultContract = database.GetComponentVariantRuntimeContract(references["default"]);
    var iosContract = database.GetComponentVariantRuntimeContract(references["incoming_call_ios"]);
    var androidContract = database.GetComponentVariantRuntimeContract(references["incoming_call_android"]);
    Equal(
        "content",
        JsonPath.RequiredString(
            JsonPath.RequiredObject(
                database.GetComponentVariantConfig(references["default"]),
                "iconRow",
                "Default Icon Row config"),
            "itemSizingMode",
            "Default Icon Row config"));
    Equal(
        "content",
        JsonPath.RequiredString(
            JsonPath.RequiredObject(
                database.GetComponentVariantConfig(references["incoming_call_ios"]),
                "iconRow",
                "iOS Icon Row config"),
            "itemSizingMode",
            "iOS Icon Row config"));
    Equal(
        "fillParent",
        JsonPath.RequiredString(
            JsonPath.RequiredObject(
                database.GetComponentVariantConfig(references["incoming_call_android"]),
                "iconRow",
                "Android Icon Row config"),
            "itemSizingMode",
            "Android Icon Row config"));
    True(EditorLayouts(database).LoadEditorLayout("component.iconRow").Cards
        .SelectMany((card) => card.Groups)
        .SelectMany((group) => group.Fields)
        .Any((field) => field.Id == "component.iconRow.itemSizingMode"));
    var missingItemSizing = AndroidConfigWithoutItemSizing(
        database.GetComponentVariantConfig(references["incoming_call_android"]));
    Throws<InvalidOperationException>(() =>
        ComponentIconRowCompositionContract.ValidateConfig(
            "iconRow",
            missingItemSizing,
            "Icon Row without item sizing"));
    var invalidItemSizing = database.GetComponentVariantConfig(references["incoming_call_android"]);
    JsonPath.RequiredObject(
        invalidItemSizing,
        "iconRow",
        "Invalid Icon Row item sizing")["itemSizingMode"] = "stretch";
    Throws<InvalidOperationException>(() =>
        ComponentIconRowCompositionContract.ValidateConfig(
            "iconRow",
            invalidItemSizing,
            "Icon Row with invalid item sizing"));
    Equal(0, JsonPath.RequiredArray(defaultContract, "buttonInputs", "Default Icon Row Runtime").Count);
    SequenceEqual(
        ["decline", "answer"],
        JsonPath.ObjectItems(
                JsonPath.RequiredArray(iosContract, "buttonInputs", "iOS Icon Row Runtime"),
                "iOS Icon Row Runtime buttons")
            .Select((item) => JsonPath.RequiredString(item, "id", "iOS Icon Row button")));
    SequenceEqual(
        ["decline", "answer"],
        JsonPath.ObjectItems(
                JsonPath.RequiredArray(androidContract, "buttonInputs", "Android Icon Row Runtime"),
                "Android Icon Row Runtime buttons")
            .Select((item) => JsonPath.RequiredString(item, "id", "Android Icon Row button")));

    var preview = JsonPath.ParseRequiredObject(
        database.GetComponentClassSettings(iconRow.Id).DesignPreviewJson,
        "Icon Row Runtime projection");
    var iosConfig = database.GetComponentVariantConfig(references["incoming_call_ios"]);
    StructuredRuntimeCollectionProjection.Apply(preview, iosConfig);
    var buttons = JsonPath.RequiredArray(preview, "buttonInputs", "Projected Icon Row Runtime");
    buttons[1]!["state"] = "pushed";

    var androidConfig = database.GetComponentVariantConfig(references["incoming_call_android"]);
    StructuredRuntimeCollectionProjection.Apply(preview, androidConfig);
    buttons = JsonPath.RequiredArray(preview, "buttonInputs", "Reprojected Icon Row Runtime");
    Equal("pushed", buttons[1]?["state"]?.GetValue<string>() ?? "");

    var reorderedAndroid = androidConfig.DeepClone().AsObject();
    var structuralItems = JsonPath.RequiredArray(
        JsonPath.RequiredObject(reorderedAndroid, "iconRow", "Android Icon Row config"),
        "items",
        "Android Icon Row config");
    var first = structuralItems[0]!.DeepClone();
    structuralItems[0] = structuralItems[1]!.DeepClone();
    structuralItems[1] = first;
    StructuredRuntimeCollectionProjection.Apply(preview, reorderedAndroid);
    buttons = JsonPath.RequiredArray(preview, "buttonInputs", "Reordered Icon Row Runtime");
    SequenceEqual(
        ["answer", "decline"],
        buttons.OfType<JsonObject>().Select((item) => JsonPath.RequiredString(item, "id", "Reordered button")));
    Equal("pushed", buttons[0]?["state"]?.GetValue<string>() ?? "");

    StructuredRuntimeCollectionProjection.Apply(
        preview,
        database.GetComponentVariantConfig(references["default"]));
    Equal(0, JsonPath.RequiredArray(preview, "buttonInputs", "Empty Icon Row Runtime").Count);

    var duplicateConfig = iosConfig.DeepClone().AsObject();
    var duplicateItems = JsonPath.RequiredArray(
        JsonPath.RequiredObject(duplicateConfig, "iconRow", "Duplicate Icon Row config"),
        "items",
        "Duplicate Icon Row config");
    duplicateItems.Add(duplicateItems[0]!.DeepClone());
    Throws<InvalidOperationException>(() =>
        StructuredRuntimeCollectionProjection.Apply(
            JsonPath.ParseRequiredObject(
                database.GetComponentClassSettings(iconRow.Id).DesignPreviewJson,
                "Duplicate Runtime projection"),
            duplicateConfig));

    static JsonObject AndroidConfigWithoutItemSizing(JsonObject config)
    {
        var clone = config.DeepClone().AsObject();
        JsonPath.RequiredObject(
            clone,
            "iconRow",
            "Icon Row without item sizing").Remove("itemSizingMode");
        return clone;
    }
}

static void IncomingCallExposesExactChildRuntimeBoundaries()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var nodes = database.LoadProjectTree()
        .SelectMany(DescendantsAndSelf)
        .ToList();
    var incomingCall = nodes.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentClass
        && database.GetComponentClassSettings(node.Id).ComponentType == "incomingCallNotification");
    var ios = incomingCall.Children.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Id.EndsWith("::variant::default", StringComparison.Ordinal));
    var android = incomingCall.Children.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Id.EndsWith("::variant::android", StringComparison.Ordinal));
    var iosRuntime = database.GetComponentVariantRuntimeContract(ios.Id);
    True(iosRuntime["labelRuntime"] is null);
    var avatarRuntime = JsonPath.ObjectItems(
            JsonPath.RequiredArray(iosRuntime, "avatarRuntime", "Incoming Call Runtime"),
            "Incoming Call Avatar Runtime")
        .Single();
    Equal("avatar", JsonPath.RequiredString(avatarRuntime, "id", "Incoming Call Avatar Runtime"));
    var avatarInputs = JsonPath.RequiredObject(
        avatarRuntime,
        "runtimeInputs",
        "Incoming Call Avatar Runtime");
    var avatarDefinitions = RuntimeInputDefinitionReader.ReadInputs(
        avatarInputs,
        new JsonObject());
    SequenceEqual(
        ["actorId", "sampleSubtext"],
        avatarDefinitions
            .Where((input) => input.UiGroupId == "identity")
            .Select((input) => input.Id));

    var iconRowRuntime = JsonPath.ObjectItems(
            JsonPath.RequiredArray(iosRuntime, "iconRowRuntime", "Incoming Call Runtime"),
            "Incoming Call Icon Row Runtime")
        .Single();
    Equal("iconRow", JsonPath.RequiredString(iconRowRuntime, "id", "Incoming Call Icon Row Runtime"));
    var buttonInputs = JsonPath.RequiredArray(
        JsonPath.RequiredObject(iconRowRuntime, "runtimeInputs", "Incoming Call Icon Row Runtime"),
        "buttonInputs",
        "Incoming Call Icon Row Runtime");
    SequenceEqual(
        ["decline", "answer"],
        buttonInputs.OfType<JsonObject>()
            .Select((item) => JsonPath.RequiredString(item, "id", "Incoming Call button")));
    True(buttonInputs.OfType<JsonObject>().All((item) => item["state"] is JsonValue));

    var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
    var payload = Required(CreatePreviewPayload(database, ios, theme.Id));
    var changedConfig = JsonPath.ParseRequiredObject(
        payload.ConfigJson,
        "Incoming Call changed config");
    var owner = JsonPath.RequiredObject(
        changedConfig,
        "incomingCallNotification",
        "Incoming Call changed config");
    var iconRowSlot = JsonPath.RequiredObject(
        owner,
        "iconRowSlot",
        "Incoming Call changed config");
    iconRowSlot["variantReference"] =
        "component_project_foqn_s2_iconRow::variant::default";
    var session = new ComponentPreviewInputSession(
        database.Design,
        database.DictionaryContext,
        database.Resources,
        database.ProjectPaths,
        () => { });
    var emptyPayload = payload with { ConfigJson = changedConfig.ToJsonString() };
    session.UpdateForPayload(emptyPayload, database.GetComponentClassSettings(incomingCall.Id).ProjectId);
    var emptyResolved = session.ApplyInputs(
        emptyPayload,
        "light",
        database.GetComponentClassSettings(incomingCall.Id).ProjectId);
    var emptyRuntime = JsonPath.ParseRequiredObject(
        emptyResolved.DesignPreviewJson,
        "Incoming Call empty Icon Row Runtime");
    var emptyIconRow = JsonPath.ObjectItems(
            JsonPath.RequiredArray(emptyRuntime, "iconRowRuntime", "Incoming Call empty Runtime"),
            "Incoming Call empty Icon Row Runtime")
        .Single();
    Equal(
        0,
        JsonPath.RequiredArray(
            JsonPath.RequiredObject(emptyIconRow, "runtimeInputs", "Incoming Call empty Icon Row Runtime"),
            "buttonInputs",
            "Incoming Call empty Icon Row Runtime").Count);

    var androidPayload = Required(CreatePreviewPayload(database, android, theme.Id));
    session.UpdateForPayload(
        androidPayload,
        database.GetComponentClassSettings(incomingCall.Id).ProjectId);
    var androidResolved = session.ApplyInputs(
        androidPayload,
        "light",
        database.GetComponentClassSettings(incomingCall.Id).ProjectId);
    var androidPreview = JsonPath.ParseRequiredObject(
        androidResolved.DesignPreviewJson,
        "Incoming Call Android Runtime");
    var androidIconRow = JsonPath.ObjectItems(
            JsonPath.RequiredArray(androidPreview, "iconRowRuntime", "Incoming Call Android Runtime"),
            "Incoming Call Android Icon Row Runtime")
        .Single();
    SequenceEqual(
        ["decline", "answer"],
        JsonPath.RequiredArray(
                JsonPath.RequiredObject(androidIconRow, "runtimeInputs", "Incoming Call Android Icon Row Runtime"),
                "buttonInputs",
                "Incoming Call Android Icon Row Runtime")
            .OfType<JsonObject>()
            .Select((item) => JsonPath.RequiredString(item, "id", "Incoming Call Android button")));
}

static void PreviewReferencesShareProjectMediaPathResolution()
{
    var mediaRoot = Path.Combine(Path.GetTempPath(), "mockups-media-root");
    var projectPaths = new ProjectPathResolver(
        Path.GetTempPath());
    Equal(
        Path.GetFullPath(Path.Combine(mediaRoot, "references", "frame.png")),
        projectPaths.ResolveLocalPath(
            Path.Combine("references", "frame.png"),
            mediaRoot));

    var absolute = Path.GetFullPath(Path.Combine(mediaRoot, "absolute.png"));
    Equal(absolute, projectPaths.ResolveLocalPath(absolute, mediaRoot));
}

static void SqliteContextsRetainIndependentProjectRoots()
{
    var rootA = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        $"mockups-path-context-a-{Guid.NewGuid():N}"));
    var rootB = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        $"mockups-path-context-b-{Guid.NewGuid():N}"));
    var contextA = new SqliteProjectContext(
        Path.Combine(rootA, "data", "project-a.sqlite"));
    var resolvedBeforeB =
        contextA.ProjectPaths.ResolveProjectPath(
            Path.Combine("assets", "a.png"));
    var contextB = new SqliteProjectContext(
        Path.Combine(rootB, "data", "project-b.sqlite"));

    Equal(
        Path.Combine(rootA, "assets", "a.png"),
        resolvedBeforeB);
    Equal(
        Path.Combine(rootB, "assets", "b.png"),
        contextB.ProjectPaths.ResolveProjectPath(
            Path.Combine("assets", "b.png")));
    Equal(
        resolvedBeforeB,
        contextA.ProjectPaths.ResolveProjectPath(
            Path.Combine("assets", "a.png")));
}

static void SqliteSessionExposesDistinctFocusedPorts()
{
    var persistenceAssembly = typeof(SqlitePersistence).Assembly;
    True(
        persistenceAssembly.GetType(
            "Mockups.DesktopEditorShell.Data.SqliteProjectEngine")
        is null);
    True(
        persistenceAssembly.GetType(
            "Mockups.DesktopEditorShell.Data.SqliteRecordClassFieldStore")
        is null);
    SequenceEqual(
        new[] { typeof(IProductionRecordFieldStore) },
        typeof(SqliteProductionRecordFieldStore)
            .GetInterfaces());
    SequenceEqual(
        new[] { typeof(IDesignRecordFieldStore) },
        typeof(SqliteDesignRecordFieldStore)
            .GetInterfaces());
    SequenceEqual(
        new[] { typeof(IResourceRecordFieldStore) },
        typeof(SqliteResourceRecordFieldStore)
            .GetInterfaces());
    True(typeof(SqliteProjectSessionFactory).IsNotPublic);
    Equal(
        0,
        typeof(SqliteProjectSessionFactory).GetInterfaces().Length);
    Equal(
        0,
        typeof(SqliteProjectSessionFactory)
            .GetMembers(
                BindingFlags.Public
                | BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.DeclaredOnly)
            .Length);
    True(
        typeof(SqliteProjectSessionFactory).GetMethod(
            "Create",
            BindingFlags.Static
            | BindingFlags.NonPublic) is not null);
    True(typeof(SqliteCurrentDatabaseValidator).IsNotPublic);
    Equal(
        0,
        typeof(SqliteCurrentDatabaseValidator).GetInterfaces().Length);
    Equal(
        0,
        typeof(SqliteCurrentDatabaseValidator)
            .GetMembers(
                BindingFlags.Public
                | BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.DeclaredOnly)
            .Length);
    True(
        typeof(SqliteCurrentDatabaseValidator).GetMethod(
            "Validate",
            BindingFlags.Instance
            | BindingFlags.NonPublic) is not null);
    var project = SqlitePersistence.OpenCurrent(
        ParityDatabasePath());
    (object Port, Type Contract)[] capabilities =
    [
        (project.ProjectPaths, typeof(IProjectPathResolver)),
        (project.Navigation, typeof(IEditorNavigationDataSource)),
        (project.CoreFields, typeof(ICoreFieldStore)),
        (project.ProductionRecordFields,
            typeof(IProductionRecordFieldStore)),
        (project.DesignRecordFields,
            typeof(IDesignRecordFieldStore)),
        (project.ResourceRecordFields,
            typeof(IResourceRecordFieldStore)),
        (project.ComponentFields, typeof(IComponentClassFieldStore)),
        (project.VariantHistory, typeof(IVariantHistoryStore)),
        (project.Preview, typeof(IPreviewInputRepository)),
        (project.ComponentPreview,
            typeof(IComponentPreviewInputRepository)),
        (project.Timeline, typeof(IModuleInstanceTimelineStore)),
        (project.ModuleInstanceThemes,
            typeof(IModuleInstanceThemeTokenQuery)),
        (project.Dictionary,
            typeof(IDictionaryFieldContextRepository)),
        (project.Children, typeof(IEditorChildStore)),
        (project.NodeCommands, typeof(IEditorNodeCommandStore)),
        (project.RenderSnapshots, typeof(IRenderSnapshotDataSource)),
        (project.Presentation,
            typeof(IEditorPresentationContextRepository)),
        (project.ModuleInstances,
            typeof(IModuleInstanceCollectionStore)),
        (project.IconThemes, typeof(IIconThemeAssetStore)),
        (project.ThemeTokens, typeof(IThemeTokenQuery)),
        (project.Components, typeof(IComponentDocumentStore)),
        (project.RuntimeInputOwners, typeof(IRuntimeInputOwnerStore)),
        (project.RuntimeInputInstances,
            typeof(IRuntimeInputInstanceStore)),
        (project.Animation, typeof(IModuleInstanceAnimationStore)),
        (project.ReferenceUsage, typeof(IReferenceUsageQuery)),
        (project.Layouts, typeof(IEditorLayoutStore)),
        (project.ActorPreview, typeof(IActorPreviewRepository)),
    ];
    var ports = capabilities
        .Select((capability) => capability.Port)
        .ToArray();

    Equal(ports.Length, ports.Distinct().Count());
    var capabilityLeaks = capabilities
        .Select((capability) =>
        {
            var expected = capability.Contract
                .GetInterfaces()
                .Append(capability.Contract)
                .SelectMany((contract) => contract.GetMethods(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly))
                .Select(MethodSignature)
                .Distinct(StringComparer.Ordinal)
                .OrderBy((signature) => signature, StringComparer.Ordinal)
                .ToArray();
            var actual = capability.Port.GetType()
                .GetMethods(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly)
                .Select(MethodSignature)
                .OrderBy((signature) => signature, StringComparer.Ordinal)
                .ToArray();
            return (
                capability.Contract.Name,
                Expected: string.Join("|", expected),
                Actual: string.Join("|", actual));
        })
        .Where((capability) =>
            !capability.Expected.Equals(
                capability.Actual,
                StringComparison.Ordinal))
        .ToList();
    if (capabilityLeaks.Count > 0)
    {
        throw new InvalidOperationException(
            "SQLite session capability membranes expose undeclared members:\n"
            + string.Join(
                "\n",
                capabilityLeaks.Select((capability) =>
                    $"{capability.Name}\nexpected: {capability.Expected}\nactual: {capability.Actual}")));
    }
    True(project.Navigation is not IPreviewInputRepository);
    True(project.Preview is not IActorPreviewRepository);
    True(project.Preview is not IComponentPreviewInputRepository);
    True(project.Preview is not IModuleInstanceTimelineStore);
    True(project.Dictionary is not IPreviewInputRepository);
    True(project.ComponentPreview is not IPreviewInputRepository);
    True(project.Timeline is not IPreviewInputRepository);
    True(project.Timeline is not IModuleInstanceThemeTokenQuery);
    True(project.ModuleInstanceThemes is not IModuleInstanceTimelineStore);
    True(project.Layouts is not IProductionRecordFieldStore);
    True(project.Layouts is not IDesignRecordFieldStore);
    True(project.Layouts is not IResourceRecordFieldStore);
    True(project.ProductionRecordFields is not
        IDesignRecordFieldStore);
    True(project.ProductionRecordFields is not
        IResourceRecordFieldStore);
    True(project.DesignRecordFields is not
        IProductionRecordFieldStore);
    True(project.DesignRecordFields is not
        IResourceRecordFieldStore);
    True(project.ResourceRecordFields is not
        IProductionRecordFieldStore);
    True(project.ResourceRecordFields is not
        IDesignRecordFieldStore);
    True(project.Presentation is not IPreviewInputRepository);
    True(project.ActorPreview is not IEditorNodeCommandStore);
    True(project.Children is not IEditorNodeCommandStore);
    True(project.NodeCommands is not IEditorChildStore);
    True(project.NodeCommands is not IReferenceUsageQuery);
    True(project.ModuleInstances is not IIconThemeAssetStore);
    True(project.ModuleInstances is not IModuleInstanceTimelineStore);
    True(project.IconThemes is not IThemeTokenQuery);
    True(project.ThemeTokens is not IModuleInstanceCollectionStore);
    True(project.ComponentFields is not IComponentDocumentStore);
    True(project.Components is not IPreviewInputRepository);
    True(project.RuntimeInputOwners is not IRuntimeInputInstanceStore);
    True(project.RuntimeInputOwners is not
        IModuleInstanceTimelineStore);
    True(project.RuntimeInputInstances is not
        IModuleInstanceAnimationStore);
    True(project.Animation is not IRuntimeInputInstanceStore);
    True(project.Animation is not IModuleInstanceTimelineStore);
    True(project.ReferenceUsage is not IRuntimeInputOwnerStore);
    True(project.ReferenceUsage is not IEditorNodeCommandStore);
}

static void VisualPersistenceWritersRequireOperationCoordination()
{
    foreach (var writerType in new[]
             {
                 typeof(EditorFieldCommitCoordinator),
                 typeof(EditorFieldPostCommitEffects),
                 typeof(EditorVariantHistoryService),
                 typeof(EditorContentPreparationService),
                 typeof(EditorDictionaryFieldServices),
                 typeof(EditorAddChildWorkflow),
                 typeof(EditorNodeCommandController),
                 typeof(EditorDomainDialogService),
                 typeof(EditorCollectionCardFactory),
                 typeof(ShotCreationDialog),
                 typeof(ShotModulePickerDialog),
                 typeof(ShotModuleInstancesCollectionEditor),
                 typeof(IconThemeTokensCollectionEditor),
                 typeof(IconThemeSearchDialog),
                 typeof(IconThemeSvgReplaceDialog),
                 typeof(IconTokenPickerDialog),
                 typeof(ThemeTokenPickerDialog),
                 typeof(ReferenceUsageCollectionEditor),
                 typeof(RuntimeInputsCollectionEditor),
                 typeof(RuntimeInputInstanceDocumentStore),
                 typeof(RuntimeInputOwnerDocumentStore),
                 typeof(ModuleInstanceAnimationEditor),
                 typeof(ModuleInstanceAnimationDocumentStore),
             })
    {
        var constructors = writerType.GetConstructors(
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic);
        True(constructors.Length > 0);
        True(
            constructors.All(
                (constructor) => constructor.GetParameters()
                    .Any(
                        (parameter) =>
                            parameter.ParameterType
                            == typeof(EditorOperationCoordinator))));
    }

    foreach (var (writerType, methodName) in new[]
             {
                 (typeof(RuntimeInputInstanceDocumentStore), "UpdateRuntimeValueAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "AddCollectionItemAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "InsertCollectionItemAfterAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "DuplicateCollectionItemAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "MoveCollectionItemAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "DeleteCollectionItemAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "UpdateCollectionValueAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "UpdateCollectionValuesAsync"),
                 (typeof(RuntimeInputInstanceDocumentStore), "SaveAnimationJsonAsync"),
                 (typeof(RuntimeInputOwnerDocumentStore), "SaveDesignPreviewJsonAsync"),
                 (typeof(ModuleInstanceAnimationDocumentStore), "SaveAnimationJsonAsync"),
                 (typeof(ModuleInstanceAnimationDocumentStore), "SaveAnimationSnapshotAsync"),
                 (typeof(EditorFieldPostCommitEffects), "ApplyAsync"),
             })
    {
        var method = writerType.GetMethod(
            methodName,
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic);
        True(method is not null);
        True(typeof(Task).IsAssignableFrom(method!.ReturnType));
    }
}

static void MainWindowRetainsOnlyShellServices()
{
    var retainedTypes = typeof(MainWindow)
        .GetFields(
            BindingFlags.Instance
            | BindingFlags.NonPublic)
        .Select((field) => field.FieldType)
        .ToHashSet();
    foreach (var constructionOnlyType in new[]
             {
                 typeof(CoreFieldValueService),
                 typeof(RecordClassFieldValueService),
                 typeof(ComponentClassFieldValueService),
                 typeof(IEditorInlinePreviewController),
                 typeof(ProductionShotContextService),
                 typeof(EditorFieldPostCommitEffects),
                 typeof(EditorPathBrowser),
                 typeof(EditorDomainDialogService),
                 typeof(EditorDictionaryFieldServices),
                 typeof(EditorFieldValueRouter),
                 typeof(EditorLayoutCardFactory),
                 typeof(EditorFieldCommitCoordinator),
             })
    {
        True(!retainedTypes.Contains(constructionOnlyType));
    }

    True(retainedTypes.Contains(
        typeof(EditorWorkspaceCoordinator)));
}

static void PostCommitPresentationReadsUseOperationCoordination()
{
    var repository =
        new RecordingPresentationContextRepository();
    using var operations = new EditorOperationCoordinator();
    var navigationRefreshes = 0;
    var effects = new EditorFieldPostCommitEffects(
        repository,
        operations,
        () => null,
        (_) => { },
        () => navigationRefreshes++,
        () => { },
        () => { },
        () => { });
    var callerThread = Environment.CurrentManagedThreadId;
    var theme = new ProjectTreeNode(
        ProjectTreeNodeKind.Theme,
        "theme_test",
        "Theme",
        "",
        "theme");
    effects.ApplyAsync(
            theme,
            "theme.family",
            "Editorial")
        .GetAwaiter()
        .GetResult();
    Equal("Editorial · 2/3 refs", theme.Notes);

    var font = new ProjectTreeNode(
        ProjectTreeNodeKind.ProductionFont,
        "font_test",
        "Font",
        "",
        "productionFont");
    effects.ApplyAsync(
            font,
            "font.category",
            "Display")
        .GetAwaiter()
        .GetResult();
    Equal("Display · 2 files", font.Notes);
    Equal(2, navigationRefreshes);
    True(repository.ReadThreadIds.Count == 2);
    True(repository.ReadThreadIds.All((threadId) =>
        threadId != callerThread));
}

static void PreviewAuthoringPreparationUsesOperationBoundary()
{
    var factoryMethod = typeof(EditorCollectionCardFactory)
        .GetMethod(
            "PreparePreviewAuthoringSurfaceAsync",
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Missing Preview authoring preparation boundary.");
    True(typeof(Task).IsAssignableFrom(
        factoryMethod.ReturnType));
    True(factoryMethod.GetParameters().Any((parameter) =>
        parameter.ParameterType
        == typeof(ComponentPreviewTransientState)));
    True(typeof(IDisposable).IsAssignableFrom(
        typeof(EditorCollectionCardFactory)));

    var surfaceMethod = typeof(RuntimeInputsCollectionEditor)
        .GetMethod(
            "PrepareSurface",
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Missing Runtime Input surface preparation boundary.");
    True(surfaceMethod.GetParameters().Any((parameter) =>
        parameter.ParameterType
        == typeof(ComponentPreviewTransientState)));
    True(surfaceMethod.GetParameters().Any((parameter) =>
        parameter.ParameterType == typeof(CancellationToken)));
    True(surfaceMethod.GetParameters().Any((parameter) =>
        parameter.Name?.Equals(
            "selectedThemeId",
            StringComparison.Ordinal) == true));
    True(typeof(RuntimeInputsCollectionEditor).GetMethod(
        "Create",
        BindingFlags.Instance
        | BindingFlags.Public
        | BindingFlags.NonPublic) is null);
    True(typeof(RuntimeInputsCollectionEditor).GetMethod(
        "LoadSurface",
        BindingFlags.Instance
        | BindingFlags.Public
        | BindingFlags.NonPublic) is null);
    True(typeof(RuntimeInputSurface)
        .GetProperty("DictionaryContext")?
        .PropertyType
        == typeof(EditorDictionaryContextSnapshot));
    True(typeof(RuntimeInputSurface)
        .GetProperty("AnimationSnapshot")?
        .PropertyType
        == typeof(ModuleInstanceAnimationSnapshot));
    True(typeof(IRuntimeInputOptionsDataSource)
        .IsAssignableFrom(
            typeof(PreparedRuntimeInputOptionsDataSource)));
    var contextMethod = typeof(EditorDictionaryFieldServices)
        .GetMethod(
            "PrepareRuntimeContext",
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Missing prepared Runtime dictionary context.");
    Equal(
        typeof(EditorDictionaryContextSnapshot),
        contextMethod.ReturnType);
    True(contextMethod.GetParameters().Any((parameter) =>
        parameter.ParameterType
        == typeof(RuntimeInputSurface)));

    var preparedAnimationContext =
        typeof(ModuleInstanceAnimationEditor)
            .GetMethod(
                "UsePreparedContext",
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Missing prepared animation context.");
    SequenceEqual(
        new[]
        {
            typeof(EditorDictionaryContextSnapshot),
            typeof(ModuleInstanceAnimationSnapshot),
        },
        preparedAnimationContext.GetParameters()
            .Select((parameter) => parameter.ParameterType));
    True(typeof(RuntimeInputInstanceDocumentStore)
        .GetMethod(
            "AnimationJson",
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic) is null);

    var shellMethod = typeof(MainWindow)
        .GetMethod(
            "RefreshPreviewAuthoringSurfaceAsync",
            BindingFlags.Instance
            | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Missing revisioned Preview authoring shell adapter.");
    Equal(typeof(Task), shellMethod.ReturnType);
}

static void VariantHistoryReadsThroughOperationBoundary()
{
    var owner = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentClass,
        "component.history",
        "History",
        "",
        "component.history");
    var first = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentVariant,
        "component.history::variant::first",
        "First",
        "",
        "component.variant",
        owner);
    var second = new ProjectTreeNode(
        ProjectTreeNodeKind.ComponentVariant,
        "component.history::variant::second",
        "Second",
        "",
        "component.variant",
        owner);
    var store = new RecordingVariantHistoryStore();
    store.ConfigByVariant[first.Id] = """{"value":"initial"}""";
    store.ConfigByVariant[second.Id] = """{"value":"second"}""";
    using var operations = new EditorOperationCoordinator();
    var history = new EditorVariantHistoryService(
        store,
        operations);
    var callingThread = Environment.CurrentManagedThreadId;

    history.TrackTransitionAsync(null, first).GetAwaiter().GetResult();
    store.ConfigByVariant[first.Id] = """{"value":"changed"}""";
    history.TrackTransitionAsync(first, second).GetAwaiter().GetResult();

    var snapshot = history.Snapshots(first).Single();
    Equal("""{"value":"changed"}""", snapshot.ConfigJson);
    True(store.ReadThreadIds.Count >= 3);
    True(store.ReadThreadIds.All((threadId) => threadId != callingThread));
}

static void CollapsedEditorCardsDeferSnapshots()
{
    using var session = HeadlessUnitTestSession.StartNew(
        typeof(HeadlessTestApplication));
    session.Dispatch(
        () =>
        {
            var loadCount = 0;
            var presented = false;
            var card = DeferredEditorCard.Create(
                "Deferred",
                "Load on expand",
                () => EditorIcons.Create(EditorIcons.Structure, 18),
                "deferred:test",
                (_) =>
                {
                    loadCount++;
                    return Task.FromResult(42);
                },
                (value) =>
                {
                    Equal(42, value);
                    presented = true;
                    return new DeferredEditorCardContent(
                        "Loaded",
                        new TextBlock { Text = "Ready" });
                });

            Equal(0, loadCount);
            True(!presented);
            card.IsExpanded = true;
            Equal(1, loadCount);
            True(presented);
            card.IsExpanded = false;
            card.IsExpanded = true;
            Equal(1, loadCount);
        },
        CancellationToken.None);
}

static void EditorVisualCardsRequirePreparedFieldSnapshots()
{
    var preparedFieldType =
        typeof(IReadOnlyDictionary<string, FieldValue>);
    foreach (var methodName in new[]
             {
                 "CreateDirectFieldControl",
                 "CreateEmbeddedFieldControl",
             })
    {
        var methods = typeof(EditorLayoutCardFactory).GetMethods(
                BindingFlags.Instance
                | BindingFlags.NonPublic)
            .Where((method) =>
                method.Name.Equals(
                    methodName,
                    StringComparison.Ordinal))
            .ToList();
        Equal(1, methods.Count);
        True(methods[0].GetParameters().Any((parameter) =>
            parameter.ParameterType == typeof(FieldValue)));
        True(methods[0].GetParameters().Any((parameter) =>
            parameter.ParameterType == preparedFieldType));
        True(methods[0].GetParameters().Any((parameter) =>
            parameter.ParameterType
            == typeof(EditorDictionaryContextSnapshot)));
        True(!methods[0].GetParameters().Any((parameter) =>
            parameter.Name?.Equals(
                "fieldId",
                StringComparison.Ordinal) == true));
    }

    True(!typeof(EditorContentController)
        .GetConstructors(
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic)
        .SelectMany((constructor) => constructor.GetParameters())
        .Any((parameter) =>
            parameter.ParameterType == typeof(IEditorLayoutStore)));

    var headerConstructorParameters =
        typeof(EditorHeaderController)
            .GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            .SelectMany((constructor) =>
                constructor.GetParameters())
            .Select((parameter) =>
                parameter.ParameterType)
            .ToHashSet();
    True(!headerConstructorParameters.Contains(
        typeof(IComponentDocumentStore)));
    True(!headerConstructorParameters.Contains(
        typeof(IPreviewInputRepository)));
    True(!headerConstructorParameters.Contains(
        typeof(IModuleInstanceTimelineStore)));
    True(!headerConstructorParameters.Contains(
        typeof(IModuleInstanceThemeTokenQuery)));
    foreach (var methodName in new[]
             {
                 "SetRootTitle",
                 "SetEmbeddedTitle",
             })
    {
        var method = typeof(EditorHeaderController)
            .GetMethod(
                methodName,
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing prepared header method '{methodName}'.");
        True(method.GetParameters().Any((parameter) =>
            parameter.ParameterType
            == typeof(EditorPreparedHeader)));
    }

    var preparedServices = typeof(EditorDictionaryFieldServices)
        .GetMethod(
            "ForPreparedNode",
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Missing prepared dictionary-context boundary.");
    True(preparedServices.GetParameters().Any((parameter) =>
        parameter.ParameterType
        == typeof(EditorDictionaryContextSnapshot)));
}

static void RapidVisualSelectionCommitsLatestPreparedEditor()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-prepared-editor-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(
            () =>
            {
                var window = DesktopHost.CreateWindow(temporary);
                window.Show();
                var roots = WindowSession(window).TreeRoots;
                var first = Descendants(roots)
                    .First((node) =>
                        node.Kind == ProjectTreeNodeKind.Theme);
                var latest = Descendants(roots)
                    .First((node) =>
                        node.Kind == ProjectTreeNodeKind.Actor);
                var selectNode = typeof(MainWindow).GetMethod(
                    "SelectNodeById",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(string)],
                    modifiers: null)
                    ?? throw new InvalidOperationException(
                        "Missing MainWindow node selection boundary.");
                var content = typeof(MainWindow)
                    .GetField(
                        "_editorContent",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.GetValue(window) as EditorContentController
                    ?? throw new InvalidOperationException(
                        "Missing prepared editor content owner.");

                True((bool)selectNode.Invoke(window, [first.Id])!);
                True((bool)selectNode.Invoke(window, [latest.Id])!);
                var committed = SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return content.CommittedOwnerId.Equals(
                            latest.Id,
                            StringComparison.Ordinal);
                    },
                    TimeSpan.FromSeconds(10));
                True(committed);
                Equal(latest.Id, WindowSession(window).SelectedNode?.Id);
                Equal(latest.Id, content.CommittedOwnerId);
                window.Close();
            },
            CancellationToken.None);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void NewShotReloadPreparesPreviewBeforeSelection()
{
    var source = ParityDatabasePath();
    var windowStatePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "data",
            "window-state.json"));
    var priorWindowState = File.Exists(windowStatePath)
        ? File.ReadAllBytes(windowStatePath)
        : null;
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-new-shot-preview-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(
            () =>
            {
                var window = DesktopHost.CreateWindow(temporary);
                window.Show();
                var setWorkspace = typeof(MainWindow).GetMethod(
                    "SetWorkspace",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "Missing MainWindow workspace transition.");
                setWorkspace.Invoke(
                    window,
                    [EditorWorkspace.Production]);
                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return WindowSession(window).Workspace
                            == EditorWorkspace.Production;
                    },
                    TimeSpan.FromSeconds(10)));

                var preview = typeof(MainWindow)
                    .GetField(
                        "_previewController",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.GetValue(window) as EditorPreviewController
                    ?? throw new InvalidOperationException(
                        "Missing Preview controller.");
                var preparedSession = typeof(EditorPreviewController)
                    .GetField(
                        "_productionSessionSnapshot",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "Missing prepared Production Preview session.");
                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return preparedSession.GetValue(preview) is not null;
                    },
                    TimeSpan.FromSeconds(10)));

                var database =
                    new SqliteProjectTestContext(temporary);
                var episode = Descendants(
                        database.LoadProjectTree())
                    .Single((node) =>
                        node.Id == "episode_002");
                var shot = database.AddShot(
                    episode,
                    "actor_alex",
                    321);
                var reloadAndSelect = typeof(MainWindow)
                    .GetMethod(
                        "ReloadAndSelect",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "Missing reload-and-select transition.");
                reloadAndSelect.Invoke(window, [shot]);

                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return WindowSession(window)
                            .SelectedNode?.Id.Equals(
                                shot.Id,
                                StringComparison.Ordinal)
                            == true;
                    },
                    TimeSpan.FromSeconds(10)));
                Equal(
                    "",
                    preview.ActiveNavigationNodeId);
                var snapshot =
                    preparedSession.GetValue(preview)
                    as ProductionPreviewSessionSnapshot
                    ?? throw new InvalidOperationException(
                        "Missing refreshed Production Preview session.");
                Equal(
                    shot.Id,
                    snapshot.Shot(shot.Id).ShotId);
                window.Close();
            },
            CancellationToken.None);
    }
    finally
    {
        File.Delete(temporary);
        if (priorWindowState is null)
        {
            File.Delete(windowStatePath);
        }
        else
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(windowStatePath)
                ?? throw new InvalidOperationException(
                    "Window state path has no directory."));
            File.WriteAllBytes(
                windowStatePath,
                priorWindowState);
        }
    }
}

static void FailedPreviewPreparationKeepsPriorSession()
{
    var source = ParityDatabasePath();
    var windowStatePath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "data",
            "window-state.json"));
    var priorWindowState = File.Exists(windowStatePath)
        ? File.ReadAllBytes(windowStatePath)
        : null;
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-preview-failure-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(
            () =>
            {
                var window = DesktopHost.CreateWindow(
                    temporary);
                window.Show();
                var setWorkspace = typeof(MainWindow)
                    .GetMethod(
                        "SetWorkspace",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "Missing MainWindow workspace transition.");
                setWorkspace.Invoke(
                    window,
                    [EditorWorkspace.Production]);
                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return WindowSession(window).Workspace
                            == EditorWorkspace.Production;
                    },
                    TimeSpan.FromSeconds(10)));

                var preview = typeof(MainWindow)
                    .GetField(
                        "_previewController",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.GetValue(window)
                    as EditorPreviewController
                    ?? throw new InvalidOperationException(
                        "Missing Preview controller.");
                var preparedSession = typeof(EditorPreviewController)
                    .GetField(
                        "_productionSessionSnapshot",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "Missing prepared Production Preview session.");
                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return preparedSession.GetValue(
                            preview) is not null;
                    },
                    TimeSpan.FromSeconds(10)));
                var priorState = WindowSession(window);
                var priorCatalog =
                    preparedSession.GetValue(preview);

                var database =
                    new SqliteProjectTestContext(
                        temporary);
                var episode = Descendants(
                        database.LoadProjectTree())
                    .Single((node) =>
                        node.Id == "episode_002");
                var shot = database.AddShot(
                    episode,
                    "actor_alex",
                    322);
                using (var connection =
                       new SqliteConnection(
                           $"Data Source={temporary}"))
                {
                    connection.Open();
                    using var command =
                        connection.CreateCommand();
                    command.CommandText = """
                        PRAGMA foreign_keys = OFF;
                        UPDATE shots
                        SET owner_actor_id = 'missing-actor'
                        WHERE id = $shotId;
                        """;
                    command.Parameters.AddWithValue(
                        "$shotId",
                        shot.Id);
                    command.ExecuteNonQuery();
                }

                var reload = typeof(MainWindow)
                    .GetMethod(
                        "LoadProjectTreeAsync",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.Invoke(window, null)
                    as Task<bool>
                    ?? throw new InvalidOperationException(
                        "Missing transactional tree reload.");
                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return reload.IsCompleted;
                    },
                    TimeSpan.FromSeconds(10)));
                True(!reload.GetAwaiter().GetResult());

                var current = WindowSession(window);
                Equal(
                    priorState.Revision,
                    current.Revision);
                Equal(
                    priorState.SelectedNode?.Id,
                    current.SelectedNode?.Id);
                True(EditorNodeSelectionState.FindNodeById(
                    current.TreeRoots,
                    shot.Id) is null);
                True(ReferenceEquals(
                    priorCatalog,
                    preparedSession.GetValue(preview)));
                window.Close();
            },
            CancellationToken.None);
    }
    finally
    {
        File.Delete(temporary);
        if (priorWindowState is null)
        {
            File.Delete(windowStatePath);
        }
        else
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(windowStatePath)
                ?? throw new InvalidOperationException(
                    "Window state path has no directory."));
            File.WriteAllBytes(
                windowStatePath,
                priorWindowState);
        }
    }
}

static void ObsoletePreviewAuthoringPreparationCannotCommit()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-preview-authoring-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(
            () =>
            {
                var window = DesktopHost.CreateWindow(temporary);
                window.Show();
                var roots = WindowSession(window).TreeRoots;
                var previewOwner = Descendants(roots)
                    .First((node) =>
                        node.Kind
                        == ProjectTreeNodeKind.ComponentVariant);
                var latest = Descendants(roots)
                    .First((node) =>
                        node.Kind == ProjectTreeNodeKind.Actor);
                var selectNode = typeof(MainWindow).GetMethod(
                    "SelectNodeById",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(string)],
                    modifiers: null)
                    ?? throw new InvalidOperationException(
                        "Missing MainWindow node selection boundary.");

                True((bool)selectNode.Invoke(
                    window,
                    [previewOwner.Id])!);
                True((bool)selectNode.Invoke(
                    window,
                    [latest.Id])!);
                var content = window.FindControl<ContentControl>(
                    "PreviewAuthoringDataHost")
                    ?? throw new InvalidOperationException(
                        "Missing Preview authoring host.");
                var tab = window.FindControl<TabItem>(
                    "PreviewAuthoringDataTab")
                    ?? throw new InvalidOperationException(
                        "Missing Preview authoring tab.");
                var editor = typeof(MainWindow)
                    .GetField(
                        "_editorContent",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.GetValue(window) as EditorContentController
                    ?? throw new InvalidOperationException(
                        "Missing prepared editor content owner.");
                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return editor.CommittedOwnerId.Equals(
                            latest.Id,
                            StringComparison.Ordinal);
                    },
                    TimeSpan.FromSeconds(10)));
                var settle = Stopwatch.StartNew();
                while (settle.Elapsed
                       < TimeSpan.FromMilliseconds(500))
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(10);
                }

                Equal(
                    latest.Id,
                    WindowSession(window).SelectedNode?.Id);
                True(content.Content is null);
                True(!tab.IsVisible);
                window.Close();
            },
            CancellationToken.None);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static string MethodSignature(MethodInfo method) =>
    $"{method.Name}({string.Join(",", method.GetParameters().Select(
        (parameter) => parameter.ParameterType.FullName))})";

static IRenderSnapshotDataSource RenderSnapshots(
    SqliteProjectTestContext database) =>
    new SqliteRenderSnapshotPort(
        database.PreviewInputs,
        database.Resources,
        database.Design,
        database.Production,
        database.Resources,
        database.Production);

static IComponentClassFieldStore ComponentFields(
    SqliteProjectTestContext database) =>
    new SqliteComponentClassFieldPort(database.ComponentDocuments);

static ICoreFieldStore CoreFields(SqliteProjectTestContext database) =>
    new SqliteCoreFieldPort(database.CoreFields);

static IComponentDocumentStore ComponentDocuments(
    SqliteProjectTestContext database) =>
    new SqliteComponentDocumentPort(database.ComponentDocuments);

static IModuleInstanceCollectionStore ModuleInstances(
    SqliteProjectTestContext database) =>
    new SqliteModuleInstanceCollectionPort(
        database.ModuleInstanceCollection);

static IEditorNodeCommandStore NodeCommands(
    SqliteProjectTestContext database) =>
    new SqliteEditorNodeCommandPort(database.NodeCommands);

static IProductionRecordFieldStore ProductionRecordFields(
    SqliteProjectTestContext database) =>
    new SqliteProductionRecordFieldPort(
        database.ProductionRecordFields);

static IDesignRecordFieldStore DesignRecordFields(
    SqliteProjectTestContext database) =>
    new SqliteDesignRecordFieldPort(
        database.DesignRecordFields);

static IResourceRecordFieldStore ResourceRecordFields(
    SqliteProjectTestContext database) =>
    new SqliteResourceRecordFieldPort(
        database.ResourceRecordFields);

static void PreviewResourceSelectionHasOneSessionRule()
{
    var options = new[]
    {
        new FieldOption("first", "First"),
        new FieldOption("second", "Second"),
    };
    Equal("second", EditorPreviewController.PreferredResourceOption(options, "second")?.Value);
    Equal("first", EditorPreviewController.PreferredResourceOption(options, "missing")?.Value);
    Equal("first", EditorPreviewController.PreferredResourceOption(options, "")?.Value);
    True(EditorPreviewController.PreferredResourceOption([], "missing") is null);
}

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
        ProjectTreeNodeKind.ComponentVariant,
        "component-a::variant::default",
        "Default",
        "",
        "component.variant",
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

static void EditorViewStateSurvivesRealNavigation()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-headless-editor-view-state-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var window = DesktopHost.CreateWindow(temporary);
            window.Width = 1440;
            window.Height = 480;
            window.Show();

            var treeRoots = WindowSession(window).TreeRoots;
            var selectNode = typeof(MainWindow).GetMethod(
                "SelectNodeById",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string)],
                modifiers: null)
                ?? throw new InvalidOperationException("Missing MainWindow node selection boundary.");
            var showEmbedded = typeof(MainWindow).GetMethod(
                "ShowEmbeddedContext",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing MainWindow embedded navigation boundary.");
            var returnToOwner = typeof(MainWindow).GetMethod(
                "ReturnToEmbeddedOwner",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing MainWindow breadcrumb owner boundary.");
            var editorContent = typeof(MainWindow)
                .GetField("_editorContent", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(window) as EditorContentController
                ?? throw new InvalidOperationException("Missing MainWindow editor content owner.");
            var scroll = Required(window.FindControl<ScrollViewer>("EditorScrollViewer"));

            ProjectTreeNode Component(string recordClassId) => treeRoots
                .SelectMany(DescendantsAndSelf)
                .Single((node) =>
                    node.Kind == ProjectTreeNodeKind.ComponentClass
                    && node.RecordClassId == recordClassId);

            ProjectTreeNode SelectedNode() =>
                Required(WindowSession(window).SelectedNode);

            void Layout()
            {
                Dispatcher.UIThread.RunJobs();
                var size = new Size(window.Width, window.Height);
                window.Measure(size);
                window.Arrange(new Rect(size));
                Dispatcher.UIThread.RunJobs();
            }

            void Select(ProjectTreeNode node)
            {
                try
                {
                    if (!(bool)(selectNode.Invoke(window, [node.Id]) ?? false))
                    {
                        throw new InvalidOperationException($"Could not select editor fixture '{node.RecordClassId}'.");
                    }
                }
                catch (TargetInvocationException error) when (error.InnerException is not null)
                {
                    throw error.InnerException;
                }
                Layout();
            }

            double ScrollToWorkingPoint()
            {
                var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                if (maximum < 24)
                {
                    throw new InvalidOperationException(
                        $"Editor fixture is not scrollable (extent={scroll.Extent.Height:0.##}, "
                        + $"viewport={scroll.Viewport.Height:0.##}).");
                }
                scroll.Offset = new Vector(0, Math.Min(96, maximum));
                Dispatcher.UIThread.RunJobs();
                return scroll.Offset.Y;
            }

            void EqualOffset(double expected, string context)
            {
                if (Math.Abs(scroll.Offset.Y - expected) > 0.5)
                {
                    throw new InvalidOperationException(
                        $"{context} restored scroll {scroll.Offset.Y:0.##} instead of {expected:0.##}.");
                }
            }

            Button HistoryButton(
                string name) =>
                window.GetVisualDescendants()
                    .OfType<Button>()
                    .Single((button) =>
                        Avalonia.Automation
                            .AutomationProperties
                            .GetName(button)
                        == name);

            var button = Component("component.button");
            var avatar = Component("component.avatar");
            Select(button);
            var buttonCard = editorContent.Cards.Single((card) => card.SessionStateId == "layout:button");
            buttonCard.RestoreExpansion(true);
            Layout();
            var buttonOffset = ScrollToWorkingPoint();

            Select(avatar);
            Select(button);
            buttonCard = editorContent.Cards.Single((card) => card.SessionStateId == "layout:button");
            if (!buttonCard.IsExpanded)
            {
                throw new InvalidOperationException("Editor-class navigation did not restore the expanded card.");
            }
            EqualOffset(buttonOffset, "Editor-class navigation");

            Select(avatar);
            var avatarCard = editorContent.Cards.Single((card) => card.SessionStateId == "layout:avatar");
            avatarCard.RestoreExpansion(true);
            Layout();
            var avatarOffset = ScrollToWorkingPoint();
            var ownerNode = SelectedNode();

            var labelContext = new EditorEmbeddedContext(
                ownerNode,
                [EmbeddedComponentSlotCatalog.Get("component.avatar.label.editor")]);
            showEmbedded.Invoke(window, [labelContext]);
            Layout();
            var labelCard = editorContent.Cards.Single((card) => card.SessionStateId == "embedded:label");
            labelCard.RestoreExpansion(true);
            Layout();
            var labelOffset = ScrollToWorkingPoint();

            var surfaceContext = labelContext.Nested(
                EmbeddedComponentSlotCatalog.Get("component.label.surface.editor"));
            showEmbedded.Invoke(window, [surfaceContext]);
            Layout();

            showEmbedded.Invoke(window, [labelContext]);
            Layout();
            labelCard = editorContent.Cards.Single((card) => card.SessionStateId == "embedded:label");
            if (!labelCard.IsExpanded)
            {
                throw new InvalidOperationException("Embedded breadcrumb did not restore the expanded card.");
            }
            EqualOffset(labelOffset, "Embedded breadcrumb navigation");

            returnToOwner.Invoke(window, [ownerNode]);
            Layout();
            avatarCard = editorContent.Cards.Single((card) => card.SessionStateId == "layout:avatar");
            if (!avatarCard.IsExpanded)
            {
                throw new InvalidOperationException("Owner breadcrumb did not restore the expanded card.");
            }
            EqualOffset(avatarOffset, "Owner breadcrumb navigation");

            var back = HistoryButton(
                "Back in Design editor history");
            var forward = HistoryButton(
                "Forward in Design editor history");
            True(back.IsEnabled);
            True(!forward.IsEnabled);
            back.RaiseEvent(
                new RoutedEventArgs(
                    Button.ClickEvent));
            Layout();
            var restoredEmbedded =
                Required(
                    WindowSession(window)
                        .EmbeddedEditor);
            Equal(
                labelContext.Slot.FieldId,
                restoredEmbedded.Slot.FieldId);
            labelCard = editorContent.Cards.Single(
                (card) =>
                    card.SessionStateId
                    == "embedded:label");
            True(labelCard.IsExpanded);
            EqualOffset(
                labelOffset,
                "Design history embedded navigation");

            forward = HistoryButton(
                "Forward in Design editor history");
            True(forward.IsEnabled);
            forward.RaiseEvent(
                new RoutedEventArgs(
                    Button.ClickEvent));
            Layout();
            True(
                WindowSession(window)
                    .EmbeddedEditor is null);
            Equal(
                ownerNode.Id,
                WindowSession(window)
                    .SelectedNode?.Id);
            avatarCard = editorContent.Cards.Single(
                (card) =>
                    card.SessionStateId
                    == "layout:avatar");
            True(avatarCard.IsExpanded);
            EqualOffset(
                avatarOffset,
                "Design history forward navigation");

            window.Hide();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ExistingDatabaseOpenIsReadOnly()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-read-only-open-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        _ = new SqliteProjectTestContext(temporary);
        _ = new SqliteProjectTestContext(temporary);
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void PreviewShellLayoutIsResponsive()
{
    foreach (var width in new[]
             {
                 PreviewPanelLayoutPolicy.SupportedMinimumWindowWidth,
                 PreviewPanelLayoutPolicy.DefaultWindowWidth,
             })
    {
        var layout = PreviewPanelLayoutPolicy.ForWindow(width);
        True(layout.PreviewPanelWidth >= PreviewPanelLayoutPolicy.MinimumPreviewColumnWidth);
        True(layout.HeaderStripWidth >= PreviewPanelLayoutPolicy.MinimumHeaderStripWidth);
        True(layout.EditorPanelWidth >= PreviewPanelLayoutPolicy.MinimumEditorColumnWidth);
        Equal(PreviewSetupLayoutMode.TwoColumns, layout.SetupMode);
    }

    Equal(
        PreviewSetupLayoutMode.FourColumns,
        PreviewPanelLayoutPolicy.SetupMode(PreviewPanelLayoutPolicy.FourColumnSetupWidth));
    Equal(
        PreviewSetupLayoutMode.TwoColumns,
        PreviewPanelLayoutPolicy.SetupMode(PreviewPanelLayoutPolicy.TwoColumnSetupWidth));
    Equal(
        PreviewSetupLayoutMode.OneColumn,
        PreviewPanelLayoutPolicy.SetupMode(PreviewPanelLayoutPolicy.TwoColumnSetupWidth - 1));
    Equal(PreviewSetupLayoutMode.OneColumn, PreviewPanelLayoutPolicy.SetupMode(-1));
    Equal(PreviewSetupLayoutMode.OneColumn, PreviewPanelLayoutPolicy.SetupMode(0));
    Equal(PreviewSetupLayoutMode.OneColumn, PreviewPanelLayoutPolicy.SetupMode(1));
    Equal(PreviewSetupLayoutMode.OneColumn, PreviewPanelLayoutPolicy.SetupMode(279));
    Equal(PreviewSetupLayoutMode.TwoColumns, PreviewPanelLayoutPolicy.SetupMode(280));
    Equal(PreviewSetupLayoutMode.TwoColumns, PreviewPanelLayoutPolicy.SetupMode(579));
    Equal(PreviewSetupLayoutMode.FourColumns, PreviewPanelLayoutPolicy.SetupMode(580));

    var restored = PreviewPanelLayoutPolicy.ClampRestoredColumns(
        PreviewPanelLayoutPolicy.SupportedMinimumWindowWidth,
        requestedLeftWidth: 800,
        requestedEditorWidth: 800);
    True(restored.LeftPanelWidth >= PreviewPanelLayoutPolicy.MinimumLeftColumnWidth);
    True(restored.EditorPanelWidth >= PreviewPanelLayoutPolicy.MinimumEditorColumnWidth);
    True(restored.LeftPanelWidth
        + restored.EditorPanelWidth
        + PreviewPanelLayoutPolicy.MinimumPreviewColumnWidth
        <= PreviewPanelLayoutPolicy.SupportedMinimumWindowWidth - 32);
}

static void PreviewShellVisualTreeIsResponsive()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-headless-layout-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var window = DesktopHost.CreateWindow(temporary);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var previewController =
                typeof(MainWindow)
                    .GetField(
                        "_previewController",
                        BindingFlags.Instance
                        | BindingFlags.NonPublic)
                    ?.GetValue(window)
                    as EditorPreviewController
                ?? throw new InvalidOperationException(
                    "Missing Preview controller.");
            var optionsTimeout = Stopwatch.StartNew();
            while (previewController.SelectedDeviceId is null
                   && optionsTimeout.Elapsed
                   < TimeSpan.FromSeconds(5))
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            True(previewController.SelectedDeviceId is not null);
            Dispatcher.UIThread.RunJobs();

            var shell = Required(window.FindControl<Grid>("ShellColumns"));
            var navigation = Required(window.FindControl<Border>("NavigationPanelBorder"));
            var editor = Required(window.FindControl<Border>("EditorPanelBorder"));
            var preview = Required(window.FindControl<Border>("PreviewPanelBorder"));
            var tabs = Required(window.FindControl<TabControl>("PreviewUtilityTabs"));
            var authoringTab = Required(window.FindControl<TabItem>("PreviewAuthoringDataTab"));
            var setupTab = Required(window.FindControl<TabItem>("PreviewSetupTab"));
            var controlsTab = Required(window.FindControl<TabItem>("PreviewControlsTab"));
            var setupGrid = Required(window.FindControl<Grid>("PreviewSetupGrid"));
            var setupHost = Required(window.FindControl<ContentControl>("PreviewSetupHost"));
            var setupScroll = setupHost.GetVisualAncestors().OfType<ScrollViewer>().First();

            if (!authoringTab.IsVisible)
            {
                var treeRoots = WindowSession(window).TreeRoots;
                var component = treeRoots
                    .SelectMany(DescendantsAndSelf)
                    .First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
                var selectNode = typeof(MainWindow).GetMethod(
                    "SelectNodeById",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(string)],
                    modifiers: null)
                    ?? throw new InvalidOperationException("Missing MainWindow node selection boundary.");
                LayoutCheck(
                    (bool)(selectNode.Invoke(window, [component.Id]) ?? false),
                    "could not select a Design Component fixture");
                Dispatcher.UIThread.RunJobs();
            }

            tabs.SelectedItem = setupTab;
            Dispatcher.UIThread.RunJobs();
            var selectedTab = tabs.SelectedItem;

            foreach (var size in new[]
                     {
                         new Size(1040, 680),
                         new Size(1440, 900),
                     })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                Dispatcher.UIThread.RunJobs();
                window.Measure(size);
                window.Arrange(new Rect(size));
                Dispatcher.UIThread.RunJobs();

                LayoutCheck(window.ClientSize.Width <= size.Width + 0.5, $"{size}: Client width escaped the window");
                LayoutCheck(shell.Bounds.Width > 0, $"{size}: shell has no width");
                LayoutCheck(navigation.Bounds.Width >= navigation.MinWidth, $"{size}: Navigation is below its minimum");
                LayoutCheck(editor.Bounds.Width >= editor.MinWidth, $"{size}: Editor is below its minimum");
                LayoutCheck(preview.Bounds.Width >= preview.MinWidth, $"{size}: Preview is below its visual minimum");
                LayoutCheck(
                    shell.ColumnDefinitions[4].ActualWidth >= PreviewPanelLayoutPolicy.MinimumPreviewColumnWidth,
                    $"{size}: Preview column is below its shell minimum "
                    + $"({shell.ColumnDefinitions[4].ActualWidth:0.##} < {PreviewPanelLayoutPolicy.MinimumPreviewColumnWidth:0.##})");

                var navigationRect = BoundsInWindow(navigation, window);
                var editorRect = BoundsInWindow(editor, window);
                var previewRect = BoundsInWindow(preview, window);
                LayoutCheck(
                    navigationRect.Width >= 0 && editorRect.Width >= 0 && previewRect.Width >= 0,
                    $"{size}: a shell panel has negative width");
                LayoutCheck(navigationRect.Right <= editorRect.Left + 0.5, $"{size}: Navigation overlaps Editor");
                LayoutCheck(editorRect.Right <= previewRect.Left + 0.5, $"{size}: Editor overlaps Preview");
                LayoutCheck(previewRect.Left >= -0.5, $"{size}: Preview starts outside the window");
                LayoutCheck(
                    previewRect.Right <= window.ClientSize.Width + 0.5,
                    $"{size}: Preview ends outside the window ({previewRect.Right:0.##} > {window.ClientSize.Width:0.##})");

                var visibleTabs = new[] { authoringTab, setupTab, controlsTab };
                LayoutCheck(
                    visibleTabs.All((tab) => tab.IsVisible && tab.Bounds.Width > 0),
                    $"{size}: one of the three Preview tabs is not visible");
                var tabRects = visibleTabs.Select((tab) => BoundsInWindow(tab, window)).ToList();
                LayoutCheck(
                    tabRects.Max((rect) => rect.Top) - tabRects.Min((rect) => rect.Top) <= 0.5,
                    $"{size}: Preview tabs do not share one row "
                    + $"({string.Join("; ", tabRects.Select((rect) => $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}"))})");
                LayoutCheck(tabRects[0].Right <= tabRects[1].Left + 0.5, $"{size}: first and second tabs overlap");
                LayoutCheck(tabRects[1].Right <= tabRects[2].Left + 0.5, $"{size}: second and third tabs overlap");
                var tabsRect = BoundsInWindow(tabs, window);
                LayoutCheck(
                    tabRects.All((rect) =>
                        rect.Left >= tabsRect.Left - 0.5
                        && rect.Right <= tabsRect.Right + 0.5),
                    $"{size}: Preview tabs clip horizontally "
                    + $"(host={tabsRect.X:0.##},{tabsRect.Y:0.##},{tabsRect.Width:0.##},{tabsRect.Height:0.##}; "
                    + $"tabs={string.Join("; ", tabRects.Select((rect) => $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}"))})");

                var expectedSetupLayout =
                    PreviewPanelLayoutPolicy.SetupMode(
                        setupGrid.Bounds.Width);
                var expectedSetupShape =
                    expectedSetupLayout switch
                    {
                        PreviewSetupLayoutMode.FourColumns =>
                            (Columns: 4, Rows: 1),
                        PreviewSetupLayoutMode.TwoColumns =>
                            (Columns: 2, Rows: 2),
                        PreviewSetupLayoutMode.OneColumn =>
                            (Columns: 1, Rows: 4),
                        _ => throw new InvalidOperationException(
                            $"Unsupported Preview Setup layout '{expectedSetupLayout}'."),
                    };
                LayoutCheck(
                    setupGrid.ColumnDefinitions.Count
                        == expectedSetupShape.Columns
                    && setupGrid.RowDefinitions.Count
                        == expectedSetupShape.Rows,
                    $"{size}: Preview Setup does not match its measured-width policy "
                    + $"(width={setupGrid.Bounds.Width:0.##}, "
                    + $"mode={expectedSetupLayout}, "
                    + $"columns={setupGrid.ColumnDefinitions.Count}, "
                    + $"rows={setupGrid.RowDefinitions.Count})");
                Equal(ScrollBarVisibility.Auto, setupScroll.VerticalScrollBarVisibility);
                Equal(ScrollBarVisibility.Disabled, setupScroll.HorizontalScrollBarVisibility);
                LayoutCheck(
                    setupScroll.Extent.Width <= setupScroll.Viewport.Width + 0.5,
                    $"{size}: Preview Setup content clips horizontally");
                Equal(selectedTab, tabs.SelectedItem);
            }

            shell.ColumnDefinitions[0].Width = new GridLength(240);
            shell.ColumnDefinitions[2].Width = new GridLength(280);
            shell.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            Dispatcher.UIThread.RunJobs();
            Equal(4, setupGrid.ColumnDefinitions.Count);
            Equal(1, setupGrid.RowDefinitions.Count);
            setupGrid.Width = 260;
            setupGrid.HorizontalAlignment = HorizontalAlignment.Left;
            Dispatcher.UIThread.RunJobs();
            Equal(1, setupGrid.ColumnDefinitions.Count);
            Equal(4, setupGrid.RowDefinitions.Count);
            setupGrid.Width = 400;
            Dispatcher.UIThread.RunJobs();
            LayoutCheck(
                setupGrid.ColumnDefinitions.Count == 2
                && setupGrid.RowDefinitions.Count == 2,
                $"resized Preview Setup did not return to two columns "
                + $"(width={setupGrid.Bounds.Width:0.##}, columns={setupGrid.ColumnDefinitions.Count}, rows={setupGrid.RowDefinitions.Count})");

            Required(window.FindControl<Button>("ProductionWorkspaceButton"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WaitForWorkspace(EditorWorkspace.Production);
            True(!Required(Required(window.FindControl<Control>("PreviewDeviceComboBox")).Parent as Visual).IsVisible);
            True(!Required(Required(window.FindControl<Control>("PreviewThemeComboBox")).Parent as Visual).IsVisible);
            True(!Required(Required(window.FindControl<Control>("PreviewModeComboBox")).Parent as Visual).IsVisible);
            True(Required(Required(window.FindControl<Control>("PreviewOrientationComboBox")).Parent as Visual).IsVisible);
            Equal(selectedTab, tabs.SelectedItem);

            Required(window.FindControl<Button>("DesignWorkspaceButton"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WaitForWorkspace(EditorWorkspace.Design);
            True(Required(Required(window.FindControl<Control>("PreviewDeviceComboBox")).Parent as Visual).IsVisible);
            True(Required(Required(window.FindControl<Control>("PreviewThemeComboBox")).Parent as Visual).IsVisible);
            True(Required(Required(window.FindControl<Control>("PreviewModeComboBox")).Parent as Visual).IsVisible);
            True(Required(Required(window.FindControl<Control>("PreviewOrientationComboBox")).Parent as Visual).IsVisible);
            LayoutCheck(
                setupGrid.ColumnDefinitions.Count == 2
                && setupGrid.RowDefinitions.Count == 2,
                "Design Preview Setup did not restore two columns after Production "
                + $"(width={setupGrid.Bounds.Width:0.##}, "
                + $"columns={setupGrid.ColumnDefinitions.Count}, "
                + $"rows={setupGrid.RowDefinitions.Count})");
            Equal(selectedTab, tabs.SelectedItem);
            setupGrid.Width = double.NaN;
            setupGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            window.Hide();

            void WaitForWorkspace(EditorWorkspace workspace)
            {
                var timeout = Stopwatch.StartNew();
                while (WindowSession(window).Workspace != workspace
                       && timeout.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(5);
                }
                Equal(
                    workspace,
                    WindowSession(window).Workspace);
                Dispatcher.UIThread.RunJobs();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(temporary);
    }

    static Rect BoundsInWindow(Control control, Visual window)
    {
        var origin = control.TranslatePoint(default, window)
            ?? throw new InvalidOperationException($"Could not translate {control.Name ?? control.GetType().Name} bounds.");
        return new Rect(origin, control.Bounds.Size);
    }

    static void LayoutCheck(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

static void ListRuntimeEditorVisualTreeExposesDynamicSetsAndState()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-headless-list-editor-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var window = DesktopHost.CreateWindow(temporary);
            window.Width = 3000;
            window.Height = 900;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var treeRoots = WindowSession(window).TreeRoots;
            var selectNode = typeof(MainWindow).GetMethod(
                "SelectNodeById",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string)],
                modifiers: null)
                ?? throw new InvalidOperationException("Missing MainWindow node selection boundary.");
            var tabs = Required(window.FindControl<TabControl>("PreviewUtilityTabs"));
            var authoringTab = Required(window.FindControl<TabItem>("PreviewAuthoringDataTab"));
            var authoringHost = Required(window.FindControl<ContentControl>("PreviewAuthoringDataHost"));
            var contextHost = Required(window.FindControl<StackPanel>("EditorContextStripHost"));

            Control SelectComponent(string componentId)
            {
                var component = treeRoots
                    .SelectMany(DescendantsAndSelf)
                    .Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass && node.Id == componentId);
                try
                {
                    True((bool)(selectNode.Invoke(window, [component.Id]) ?? false));
                }
                catch (TargetInvocationException error) when (error.InnerException is not null)
                {
                    throw error.InnerException;
                }
                Dispatcher.UIThread.RunJobs();
                True(authoringTab.IsVisible);
                tabs.SelectedItem = authoringTab;
                Dispatcher.UIThread.RunJobs();
                return Required(authoringHost.Content as Control);
            }

            static IReadOnlyList<FieldOption> VariantOptions(Control context)
            {
                return context.GetVisualDescendants()
                    .OfType<EditorInstantComboBox>()
                    .Select((combo) => combo.ItemsSource?.ToList() ?? [])
                    .Single((options) => options.Any((option) => option.Value.Contains("::variant::", StringComparison.Ordinal)));
            }

            static DictionaryFieldControl RequiredField(Control root, string fieldId)
            {
                return root.GetVisualDescendants()
                    .OfType<DictionaryFieldControl>()
                    .First((field) => field.FieldId == fieldId);
            }

            static IReadOnlyList<Button> ActionButtons(Control root, string accessibleName)
            {
                return root.GetVisualDescendants()
                    .OfType<Button>()
                    .Where((button) => ToolTip.GetTip(button) as string == accessibleName)
                    .ToList();
            }

            var listItemSurface = SelectComponent("component_project_foqn_s2_list_item");
            SequenceEqual(
                ["Default", "Calls", "Chats"],
                VariantOptions(contextHost).Select((option) => option.Label));
            Equal("360", RequiredField(listItemSurface, "width").Value);
            Equal("84", RequiredField(listItemSurface, "height").Value);
            Equal("1", RequiredField(listItemSurface, "activeSet").Value);
            Equal("normal", RequiredField(listItemSurface, "state").Value);
            var runtimeSectionLabels = listItemSurface.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select((text) => text.Text ?? "")
                .ToList();
            True(runtimeSectionLabels.Contains("General"));
            SequenceEqual(
                ["Set 1", "Set 2", "Set 3"],
                runtimeSectionLabels.Where((label) => label.StartsWith("Set ", StringComparison.Ordinal)));
            True(!runtimeSectionLabels.Contains("Content Sets"));

            var setOneButton = listItemSurface.GetVisualDescendants()
                .OfType<Button>()
                .Single((button) => button.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Any((text) => text.Text == "Set 1"));
            setOneButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var componentRows = listItemSurface.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select((text) => text.Text ?? "")
                .Where((text) => text is "Avatar" or "Label" or "Icon Row")
                .ToList();
            SequenceEqual(["Avatar", "Label", "Icon Row"], componentRows);
            var runtimeButtons = listItemSurface.GetVisualDescendants()
                .OfType<Button>()
                .Where((button) => button.Content as string == "···")
                .ToList();
            Equal(3, runtimeButtons.Count);

            runtimeButtons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            _ = RequiredField(listItemSurface, "actorId");
            runtimeButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            _ = RequiredField(listItemSurface, "sampleText");
            var finalLabelField = RequiredField(listItemSurface, "subtextColorToken");
            var runtimeScroll = listItemSurface.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Single((scroll) => scroll.Name == "PreviewTestValuesEditorScroll");
            runtimeScroll.Offset = new Vector(0, double.MaxValue);
            Dispatcher.UIThread.RunJobs();
            var finalFieldTransform = finalLabelField.TransformToVisual(runtimeScroll)
                ?? throw new InvalidOperationException("Final Label Runtime field has no scroll transform.");
            var finalFieldBottom = finalFieldTransform.Transform(
                new Point(0, finalLabelField.Bounds.Height)).Y;
            if (finalFieldBottom > runtimeScroll.Viewport.Height + 0.5)
            {
                throw new InvalidOperationException(
                    $"Final Label Runtime field remains below the scroll viewport "
                    + $"(bottom={finalFieldBottom:0.##}, viewport={runtimeScroll.Viewport.Height:0.##}, "
                    + $"extent={runtimeScroll.Extent.Height:0.##}, offset={runtimeScroll.Offset.Y:0.##}).");
            }
            runtimeButtons[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            _ = RequiredField(listItemSurface, "buttonInputs");

            var listSurface = SelectComponent("component_project_foqn_s2_list");
            SequenceEqual(
                ["Default", "Calls", "Chats"],
                VariantOptions(contextHost).Select((option) => option.Label));
            Equal("360", RequiredField(listSurface, "itemWidth").Value);
            Equal("84", RequiredField(listSurface, "itemHeight").Value);
            var database = new SqliteProjectTestContext(temporary);
            var listPreview = JsonPath.ParseRequiredObject(
                database.GetComponentClassSettings("component_project_foqn_s2_list").DesignPreviewJson,
                "List Design Preview");
            var listItems = JsonPath.RequiredArray(listPreview, "items", "List Design Preview");
            if (listItems.Count < 5)
            {
                throw new InvalidOperationException(
                    $"List exposes {listItems.Count} Runtime items instead of at least five.");
            }
            foreach (var node in listItems)
            {
                var item = node as JsonObject
                    ?? throw new InvalidOperationException("List Runtime item must be an object.");
                True(item["name"] is null);
                var runtime = JsonPath.RequiredObject(item, "listItemInputs", "List Runtime item");
                var activeSet = JsonPath.RequiredNumber(runtime, "activeSet", "List Item Runtime");
                True(activeSet >= 1 && activeSet <= 3);
                True(new[] { "normal", "pressed", "inactive" }.Contains(
                    JsonPath.RequiredString(runtime, "state", "List Item Runtime"),
                    StringComparer.Ordinal));
                Equal(360d, JsonPath.RequiredNumber(runtime, "width", "List Item Runtime"));
                Equal(84d, JsonPath.RequiredNumber(runtime, "height", "List Item Runtime"));
                Equal(4, JsonPath.RequiredArray(runtime, "collections", "List Item Runtime").Count);
            }
            var listSettings = database.GetComponentClassSettings(
                "component_project_foqn_s2_list");
            var listCollectionDefinition = RuntimeInputDefinitionReader.ReadCollections(
                    listPreview,
                    JsonPath.ParseRequiredObject(listSettings.ConfigJson, "List config"))
                .Single((collection) => collection.Id == "items");
            var rebasedItem = listItems[0] is JsonObject originalListItem
                ? originalListItem.DeepClone().AsObject()
                : throw new InvalidOperationException("List Runtime item must be an object.");
            var idMappings = StructuredCollectionItemIdentity.RebaseNestedItems(
                rebasedItem,
                listCollectionDefinition);
            True(idMappings.Count > 0);
            Equal(idMappings.Count, idMappings.Values.Distinct(StringComparer.Ordinal).Count());
            var rebasedTargetIds = StructuredCollectionItemIdentity.TargetIds(rebasedItem);
            foreach (var (previous, next) in idMappings)
            {
                True(previous != next);
                True(!rebasedTargetIds.Contains(previous, StringComparer.Ordinal));
                True(rebasedTargetIds.Contains(next, StringComparer.Ordinal));
            }
            var rebasedRuntime = JsonPath.RequiredObject(
                rebasedItem,
                "listItemInputs",
                "Rebased List Item Runtime");
            var rebasedSetIds = JsonPath.RequiredArray(
                    rebasedRuntime,
                    "contentSets",
                    "Rebased List Item Runtime")
                .OfType<JsonObject>()
                .Select((set) => JsonPath.RequiredString(set, "id", "Rebased Content Set"))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var collectionKey in new[] { "avatarContent", "labelContent", "iconRowContent" })
            {
                foreach (var child in JsonPath.RequiredArray(
                             rebasedRuntime,
                             collectionKey,
                             "Rebased List Item Runtime").OfType<JsonObject>())
                {
                    True(rebasedSetIds.Contains(JsonPath.RequiredString(
                        child,
                        "contentSetId",
                        $"Rebased {collectionKey} item")));
                }
            }

            var listRuntimeLabels = listSurface.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select((text) => text.Text ?? "")
                .ToList();
            SequenceEqual(
                Enumerable.Range(1, listItems.Count).Select((index) => $"Item {index}"),
                listRuntimeLabels.Where((label) =>
                    label.StartsWith("Item ", StringComparison.Ordinal)
                    && int.TryParse(label.AsSpan(5), out _)));
            True(!listRuntimeLabels.Contains("Diana"));
            True(!listRuntimeLabels.Contains("Missed call"));

            Button ItemButton(string label) => listSurface.GetVisualDescendants()
                .OfType<Button>()
                .Single((button) => button.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Any((text) => text.Text == label));

            for (var itemIndex = 1; itemIndex >= 0; itemIndex--)
            {
                ItemButton($"Item {itemIndex + 1}").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                var sourceItem = JsonPath.RequiredObject(
                    listItems[itemIndex]!.AsObject(),
                    "listItemInputs",
                    $"List Runtime Item {itemIndex + 1}");
                Equal(
                    JsonPath.RequiredNumber(
                        sourceItem,
                        "activeSet",
                        $"List Runtime Item {itemIndex + 1}").ToString(CultureInfo.InvariantCulture),
                    RequiredField(listSurface, "activeSet").Value);
            }

            var itemOneButton = ItemButton("Item 1");
            itemOneButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Equal("true", RequiredField(listSurface, "present").Value);
            Equal(1, ActionButtons(listSurface, "Play Presence").Count);
            Equal(1, ActionButtons(listSurface, "Restore Presence").Count);
            Equal("1", RequiredField(listSurface, "activeSet").Value);
            Equal("normal", RequiredField(listSurface, "state").Value);
            Equal(0, listSurface.GetVisualDescendants()
                .OfType<DictionaryFieldControl>()
                .Count((field) => field.FieldId is "width" or "height"));

            listRuntimeLabels = listSurface.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select((text) => text.Text ?? "")
                .ToList();
            True(listRuntimeLabels.Contains("General"));
            SequenceEqual(
                ["Set 1", "Set 2", "Set 3"],
                listRuntimeLabels.Where((label) => label.StartsWith("Set ", StringComparison.Ordinal)));
            True(!listRuntimeLabels.Contains("Content Sets"));

            RequiredField(listSurface, "activeSet").SetValue("2", commit: true);
            Dispatcher.UIThread.RunJobs();
            var nestedSetTwoButton = listSurface.GetVisualDescendants()
                .OfType<Button>()
                .Single((button) => button.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Any((text) => text.Text == "Set 2"));
            nestedSetTwoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var nestedComponentRows = listSurface.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select((text) => text.Text ?? "")
                .Where((text) => text is "Avatar" or "Label" or "Icon Row")
                .ToList();
            SequenceEqual(["Avatar", "Label", "Icon Row"], nestedComponentRows);
            var nestedRuntimeButtons = listSurface.GetVisualDescendants()
                .OfType<Button>()
                .Where((button) => button.Content as string == "···")
                .ToList();
            Equal(3, nestedRuntimeButtons.Count);
            nestedRuntimeButtons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var nestedActor = RequiredField(listSurface, "actorId");
            var listRuntimeScroll = listSurface.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Single((scroll) => scroll.Name == "PreviewTestValuesEditorScroll");
            if (listRuntimeScroll.Extent.Width > listRuntimeScroll.Viewport.Width + 0.5)
            {
                throw new InvalidOperationException(
                    $"List Runtime editor overflows horizontally "
                    + $"(extent={listRuntimeScroll.Extent.Width:0.##}, "
                    + $"viewport={listRuntimeScroll.Viewport.Width:0.##}).");
            }
            var visibleNavigationWidths = listSurface.GetVisualDescendants()
                .OfType<EditorInternalNavigation>()
                .Where((navigation) => navigation.ColumnDefinitions.Count == 3)
                .Select((navigation) => navigation.ColumnDefinitions[0].ActualWidth)
                .ToList();
            True(visibleNavigationWidths.Count >= 2);
            True(visibleNavigationWidths.All((width) => width <= 160.5));
            var actorTransform = nestedActor.TransformToVisual(listRuntimeScroll)
                ?? throw new InvalidOperationException("Nested Actor field has no scroll transform.");
            var actorRight = actorTransform.Transform(
                new Point(nestedActor.Bounds.Width, 0)).X;
            var runtimeContent = listRuntimeScroll.Content as Control
                ?? throw new InvalidOperationException("List Runtime scroll has no content.");
            var runtimeContentOrigin = runtimeContent.TransformToVisual(listRuntimeScroll)
                ?.Transform(default).X
                ?? throw new InvalidOperationException("List Runtime content has no scroll transform.");
            var viewportRight = runtimeContentOrigin + listRuntimeScroll.Viewport.Width;
            if (actorRight > viewportRight + 1.5)
            {
                throw new InvalidOperationException(
                    $"Nested Actor field is clipped on the right "
                    + $"(right={actorRight:0.##}, viewportRight={viewportRight:0.##}).");
            }
            nestedActor.SetValue("actor_alex_b", commit: true);
            Dispatcher.UIThread.RunJobs();
            var listPreviewController = typeof(MainWindow)
                .GetField("_previewController", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(window) as EditorPreviewController
                ?? throw new InvalidOperationException("Missing Preview controller.");
            var selectedListNode = Required(
                WindowSession(window).SelectedNode);
            var selectedTheme = treeRoots
                .SelectMany(DescendantsAndSelf)
                .First((node) => node.Kind == ProjectTreeNodeKind.Theme);
            var listPayload = Required(DesignPreviewPayloadFactory.Create(
                new DesignPreviewPayloadDataSource(
                    database.PreviewInputs,
                    database.Production,
                    database.Resources,
                    database.Resources,
                    database.ProjectPaths),
                selectedListNode,
                selectedTheme.Id,
                "light"));
            var listInputSession = typeof(EditorPreviewController)
                .GetField("_designInputsPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(listPreviewController) as ComponentPreviewInputSession
                ?? throw new InvalidOperationException("Missing List Preview input session.");
            var effectiveListPayload = listInputSession.ApplyInputs(
                listPayload,
                "light",
                listSettings.ProjectId);
            var effectiveListRuntime = JsonPath.ParseRequiredObject(
                effectiveListPayload.DesignPreviewJson,
                "Effective List Runtime");
            var effectiveItemOne = JsonPath.RequiredArray(
                    effectiveListRuntime,
                    "items",
                    "Effective List Runtime")[0]!.AsObject();
            var effectiveItemOneRuntime = JsonPath.RequiredObject(
                effectiveItemOne,
                "listItemInputs",
                "Effective List Runtime Item 1");
            Equal(
                2d,
                JsonPath.RequiredNumber(
                    effectiveItemOneRuntime,
                    "activeSet",
                    "Effective List Runtime Item 1"));
            var effectiveSetTwoId = JsonPath.RequiredString(
                JsonPath.RequiredArray(
                    effectiveItemOneRuntime,
                    "contentSets",
                    "Effective List Runtime Item 1")[1]!.AsObject(),
                "id",
                "Effective List Runtime Item 1 Set 2");
            var effectiveSetTwoAvatar = JsonPath.RequiredArray(
                    effectiveItemOneRuntime,
                    "avatarContent",
                    "Effective List Runtime Item 1")
                .OfType<JsonObject>()
                .Single((avatar) =>
                    JsonPath.RequiredString(
                        avatar,
                        "contentSetId",
                        "Effective List Runtime Item 1 Avatar") == effectiveSetTwoId);
            var effectiveSetTwoAvatarRuntime = JsonPath.RequiredObject(
                effectiveSetTwoAvatar,
                "runtimeInputs",
                "Effective List Runtime Item 1 Avatar");
            Equal(
                "actor_alex_b",
                JsonPath.RequiredString(
                    effectiveSetTwoAvatarRuntime,
                    "actorId",
                    "Effective List Runtime Item 1 Avatar"));
            Equal(
                "Asia",
                JsonPath.RequiredString(
                    JsonPath.RequiredObject(
                        effectiveSetTwoAvatarRuntime,
                        "actor",
                        "Effective List Runtime Item 1 Avatar"),
                    "displayName",
                    "Effective List Runtime Item 1 Avatar"));
            nestedRuntimeButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            _ = RequiredField(listSurface, "sampleText");
            nestedRuntimeButtons[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            _ = RequiredField(listSurface, "buttonInputs");

            Equal(1, ActionButtons(listSurface, "Duplicate item").Count);
            Equal(1, ActionButtons(listSurface, "Delete").Count);
            Equal(1, ActionButtons(listSurface, "Add item").Count);
            ActionButtons(listSurface, "Add item").Single()
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var previewController = typeof(MainWindow)
                .GetField("_previewController", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(window) as EditorPreviewController
                ?? throw new InvalidOperationException("Missing Preview controller.");
            var selectedNode = Required(WindowSession(window).SelectedNode);
            var effectiveListPreview = previewController.ApplyDesignPreviewTransientTestValues(
                selectedNode,
                JsonPath.ParseRequiredObject(listSettings.DesignPreviewJson, "List Design Preview"));
            var effectiveListItemCount = DesignPreviewTestValues.CollectionItems(
                effectiveListPreview,
                listCollectionDefinition).Count;
            if (effectiveListItemCount != listItems.Count + 1)
            {
                var inputSession = typeof(EditorPreviewController)
                    .GetField("_designInputsPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(previewController)
                    ?? throw new InvalidOperationException("Missing Design Preview input session.");
                var transientScopes = typeof(ComponentPreviewInputSession)
                    .GetField(
                        "_transientCollectionTestValuesByScope",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(inputSession) as System.Collections.IDictionary;
                var scopeKeys = transientScopes is null
                    ? ""
                    : string.Join(
                        ",",
                        transientScopes.Keys.Cast<object>().Select((key) => key.ToString()));
                throw new InvalidOperationException(
                    $"List add stored {effectiveListItemCount} items instead of {listItems.Count + 1}; "
                    + $"selected={selectedNode?.Kind}:{selectedNode?.Id}; scopes={scopeKeys}.");
            }
            listSurface = SelectComponent("component_project_foqn_s2_list");
            var addedItemLabels = listSurface.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select((text) => text.Text ?? "")
                .Where((label) =>
                    label.StartsWith("Item ", StringComparison.Ordinal)
                    && int.TryParse(label.AsSpan(5), out _))
                .ToList();
            SequenceEqual(
                Enumerable.Range(1, listItems.Count + 1).Select((index) => $"Item {index}"),
                addedItemLabels);
            Equal("1", RequiredField(listSurface, "activeSet").Value);
            Equal("normal", RequiredField(listSurface, "state").Value);
            SequenceEqual(
                ["Set 1", "Set 2", "Set 3"],
                listSurface.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select((text) => text.Text ?? "")
                    .Where((label) => label.StartsWith("Set ", StringComparison.Ordinal)));

            ActionButtons(listSurface, "Duplicate item").Last()
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            listSurface = SelectComponent("component_project_foqn_s2_list");
            Equal(
                listItems.Count + 2,
                listSurface.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Count((text) =>
                        text.Text?.StartsWith("Item ", StringComparison.Ordinal) == true
                        && int.TryParse(text.Text.AsSpan(5), out _)));
            Equal("1", RequiredField(listSurface, "activeSet").Value);
            Equal("normal", RequiredField(listSurface, "state").Value);
            Equal(1, ActionButtons(listSurface, "Delete").Count);
            True(ActionButtons(listSurface, "Move up").Last().IsEnabled);
            ActionButtons(listSurface, "Move up").Last()
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            window.Hide();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ChatListModuleEditorVisualTreeExposesExactListRuntime()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-headless-chat-list-module-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var window = DesktopHost.CreateWindow(temporary);
            window.Width = 3000;
            window.Height = 900;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var treeRoots = WindowSession(window).TreeRoots;
            var chatApp = treeRoots
                .SelectMany(DescendantsAndSelf)
                .Single((node) =>
                    node.Kind == ProjectTreeNodeKind.App
                    && node.Id == "app_core_chat");
            var chatList = DescendantsAndSelf(chatApp)
                .Single((node) =>
                    node.Kind == ProjectTreeNodeKind.Module
                    && node.Id == "module_project_foqn_s2_chat_list");
            Equal("module.core.chatList", chatList.RecordClassId);

            var selectNode = typeof(MainWindow).GetMethod(
                "SelectNodeById",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string)],
                modifiers: null)
                ?? throw new InvalidOperationException("Missing MainWindow node selection boundary.");
            Check(
                (bool)(selectNode.Invoke(window, [chatList.Id]) ?? false),
                "Chat List Module could not be selected.");
            Dispatcher.UIThread.RunJobs();

            var selected = Required(WindowSession(window).SelectedNode);
            Check(
                selected.Kind == ProjectTreeNodeKind.ModuleVariant,
                $"Chat List selection resolved to {selected.Kind} instead of its Default Variant.");
            Check(
                selected.Id.EndsWith("::variant::default", StringComparison.Ordinal),
                $"Chat List selection resolved to unexpected Variant '{selected.Id}'.");
            var editorContent = typeof(MainWindow)
                .GetField(
                    "_editorContent",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic)
                ?.GetValue(window) as EditorContentController
                ?? throw new InvalidOperationException(
                    "Missing prepared editor content owner.");
            Check(
                SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return editorContent.CommittedOwnerId.Equals(
                            selected.Id,
                            StringComparison.Ordinal);
                    },
                    TimeSpan.FromSeconds(10)),
                "Chat List editor preparation did not commit its selected owner. "
                + Required(window.FindControl<TextBox>("ShellMessagesTextBox")).Text);

            var database = new SqliteProjectTestContext(temporary);
            var fieldValues = new RecordClassFieldValueService(
                ProductionRecordFields(database),
                DesignRecordFields(database),
                ResourceRecordFields(database),
                database.Production,
                database.Resources);
            var projectId = treeRoots
                .SelectMany(DescendantsAndSelf)
                .Single((node) => node.Kind == ProjectTreeNodeKind.Project)
                .Id;
            foreach (var (fieldId, componentType) in new[]
            {
                ("module.core.chatList.stack", "componentStack"),
                ("module.core.chatList.topIconBar", "iconBar"),
                ("module.core.chatList.list", "list"),
                ("module.core.chatList.bottomIconBar", "iconBar"),
                ("module.core.chatList.statusBar", "status_bar"),
                ("module.core.chatList.navigationBar", "navigation_bar"),
            })
            {
                var actual = fieldValues.CreateFieldValue(selected, fieldId)
                    .Definition.Options
                    ?? [];
                var expected = database.GetComponentVariantReferenceOptionsByType(
                    projectId,
                    componentType);
                SequenceEqual(
                    expected.Select((option) => option.Value),
                    actual.Select((option) => option.Value));
            }
            var listField = fieldValues.CreateFieldValue(
                selected,
                "module.core.chatList.list");
            var listReferences = listField.Definition.Options ?? [];
            Check(
                listReferences.Count >= 2,
                "Chat List List boundary does not expose every exact List Variant.");
            var nextListReference = listReferences
                .Select((option) => option.Value)
                .First((reference) =>
                    !reference.Equals(
                        "component_project_foqn_s2_list::variant::chats",
                        StringComparison.Ordinal));
            var slotControl = new DictionaryComponentVariantSlotControl(
                listField.Definition,
                """
                {"variantReference":"component_project_foqn_s2_list::variant::chats","overrides":{"marker":"preserved"}}
                """,
                null,
                null);
            typeof(DictionaryComponentVariantSlotControl)
                .GetMethod("SetReference", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(slotControl, [nextListReference]);
            var serializedSlot = (string)typeof(DictionaryComponentVariantSlotControl)
                .GetMethod("Serialize", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(slotControl, null)!;
            var preservedSlot = JsonPath.ParseRequiredObject(
                serializedSlot,
                "Changed fixed Component slot");
            Equal(
                nextListReference,
                JsonPath.RequiredString(
                    preservedSlot,
                    "variantReference",
                    "Changed fixed Component slot"));
            Equal(
                "preserved",
                JsonPath.RequiredString(
                    JsonPath.RequiredObject(
                        preservedSlot,
                        "overrides",
                        "Changed fixed Component slot"),
                    "marker",
                    "Changed fixed Component slot Overrides"));

            var editableVariant = NodeCommands(database).SaveModuleVariant(
                selected,
                "Variant selection test");
            database.UpdateModuleVariantField(
                editableVariant,
                "module.core.chatList.list",
                new JsonObject
                {
                    ["variantReference"] = nextListReference,
                    ["overrides"] = new JsonObject
                    {
                        ["marker"] = "atomic",
                    },
                }.ToJsonString());
            var editedConfig = JsonPath.ParseRequiredObject(
                database.GetModuleVariantSettings(editableVariant).ConfigJson,
                "Edited Chat List Variant");
            var editedChatList = JsonPath.RequiredObject(
                editedConfig,
                "chatList",
                "Edited Chat List Variant");
            Equal(
                nextListReference,
                ComponentVariantSlotDocumentContract.VariantReference(
                    JsonPath.RequiredObject(
                        editedChatList,
                        "listSlot",
                        "Edited Chat List Variant.chatList"),
                    "Edited Chat List Variant.chatList.listSlot"));
            Equal(
                nextListReference,
                JsonPath.RequiredString(
                    JsonPath.RequiredObject(
                        editedChatList,
                        "runtimeContract",
                        "Edited Chat List Variant.chatList"),
                    "variantReference",
                    "Edited Chat List Variant.chatList.runtimeContract"));

            var configurationFields = window.GetVisualDescendants()
                .OfType<DictionaryFieldControl>()
                .Select((field) => field.FieldId)
                .ToHashSet(StringComparer.Ordinal);
            Check(
                configurationFields.Contains("module.core.chatList.list"),
                "Chat List editor is missing its fixed List boundary.");
            Check(
                configurationFields.Contains("module.core.chatList.wallpaperEnabled"),
                "Chat List editor is missing its Wallpaper toggle.");
            Check(
                configurationFields.Contains("module.core.chatList.stack"),
                "Chat List editor is missing its fixed Content Stack boundary.");
            Check(
                configurationFields.Contains("module.core.chatList.topIconBar"),
                "Chat List editor is missing its fixed top Icon Bar boundary.");
            Check(
                configurationFields.Contains("module.core.chatList.bottomIconBar"),
                "Chat List editor is missing its fixed bottom Icon Bar boundary.");
            Check(
                configurationFields.Contains("module.core.chatList.statusBar"),
                "Chat List editor is missing its fixed Status Bar boundary.");
            Check(
                configurationFields.Contains("module.core.chatList.navigationBar"),
                "Chat List editor is missing its fixed Navigation Bar boundary.");
            Check(
                window.GetVisualDescendants()
                    .OfType<Button>()
                    .Any((button) =>
                        Avalonia.Automation.AutomationProperties.GetName(button)
                            == "Edit overrides for List"),
                "Chat List fixed List boundary is missing Overrides.");

            var tabs = Required(window.FindControl<TabControl>("PreviewUtilityTabs"));
            var authoringTab = Required(window.FindControl<TabItem>("PreviewAuthoringDataTab"));
            var authoringHost = Required(window.FindControl<ContentControl>("PreviewAuthoringDataHost"));
            Check(authoringTab.IsVisible, "Chat List Runtime tab is not visible.");
            tabs.SelectedItem = authoringTab;
            Dispatcher.UIThread.RunJobs();
            var runtime = Required(authoringHost.Content as Control);
            var runtimeFields = runtime.GetVisualDescendants()
                .OfType<DictionaryFieldControl>()
                .ToList();
            Equal("360", runtimeFields.Single((field) => field.FieldId == "itemWidth").Value);
            Equal("84", runtimeFields.Single((field) => field.FieldId == "itemHeight").Value);
            var itemLabels = runtime.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select((text) => text.Text ?? "")
                .Where((label) =>
                    label.StartsWith("Item ", StringComparison.Ordinal)
                    && int.TryParse(label.AsSpan(5), out _))
                .ToList();
            Check(
                itemLabels.Count >= 5,
                $"Chat List Runtime exposes only {itemLabels.Count} items.");
            SequenceEqual(
                Enumerable.Range(1, itemLabels.Count).Select((index) => $"Item {index}"),
                itemLabels);
            Equal(
                1,
                runtime.GetVisualDescendants()
                    .OfType<Button>()
                    .Count((button) => ToolTip.GetTip(button) as string == "Add item"));

            var previewHost = Required(window.FindControl<ContentControl>("DesignPreviewHost"));
            Check(
                previewHost.Content is DesignWebPreviewPane,
                "Chat List Module is not connected to the generic Design Preview host.");
            Check(
                previewHost.Bounds.Width > 0 && previewHost.Bounds.Height > 0,
                $"Chat List Module Preview has unusable bounds {previewHost.Bounds}.");

            var previewController = typeof(MainWindow)
                .GetField("_previewController", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(window) as EditorPreviewController
                ?? throw new InvalidOperationException("Missing Preview controller.");
            var selectedPayload = typeof(EditorPreviewController)
                .GetMethod(
                    "DesignPreviewPayloadForSelection",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(previewController, null) as DesignPreviewPayload
                ?? throw new InvalidOperationException(
                    "Chat List Module selection did not produce a Design Preview payload.");
            Equal("module", selectedPayload.Kind);
            Equal("module.core.chatList", selectedPayload.ComponentType);
            var previewPane = previewHost.Content as DesignWebPreviewPane
                ?? throw new InvalidOperationException("Missing generic Design Preview pane.");
            var initialPreviewSequence = PreviewUpdateSequence(previewPane);
            Check(
                initialPreviewSequence > 0,
                "Chat List Module selection did not request a render from the Preview host.");

            runtimeFields.Single((field) => field.FieldId == "itemWidth")
                .SetValue("320", commit: true);
            Dispatcher.UIThread.RunJobs();
            var inputSession = typeof(EditorPreviewController)
                .GetField("_designInputsPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(previewController) as ComponentPreviewInputSession
                ?? throw new InvalidOperationException("Missing Module Preview input session.");
            var effectivePayload = inputSession.ApplyInputs(
                selectedPayload,
                selectedPayload.ThemeMode,
                projectId);
            Equal(
                320d,
                JsonPath.RequiredNumber(
                    JsonPath.ParseRequiredObject(
                        effectivePayload.DesignPreviewJson,
                        "Chat List effective Design Preview"),
                    "itemWidth",
                    "Chat List effective Design Preview"));
            Check(
                PreviewUpdateSequence(previewPane) > initialPreviewSequence,
                "Changing a Chat List Runtime Input did not request a new Preview render.");

            window.Hide();

            static long PreviewUpdateSequence(DesignWebPreviewPane pane)
            {
                return typeof(DesignWebPreviewPane)
                    .GetField("_latestUpdateSequence", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(pane) is long sequence
                        ? sequence
                        : 0;
            }

            static void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ConversationModuleEditorVisualTreeExposesTestValues()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-headless-conversation-editor-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(
            () =>
            {
                var window = DesktopHost.CreateWindow(temporary);
                window.Width = 3000;
                window.Height = 900;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var conversation = WindowSession(window).TreeRoots
                    .SelectMany(DescendantsAndSelf)
                    .Single((node) =>
                        node.Kind == ProjectTreeNodeKind.Module
                        && node.Id == "module_core_chat");
                var selectNode = typeof(MainWindow).GetMethod(
                    "SelectNodeById",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(string)],
                    modifiers: null)
                    ?? throw new InvalidOperationException(
                        "Missing MainWindow node selection boundary.");
                True((bool)(selectNode.Invoke(
                    window,
                    [conversation.Id]) ?? false));

                var selected = Required(
                    WindowSession(window).SelectedNode);
                Equal(
                    ProjectTreeNodeKind.ModuleVariant,
                    selected.Kind);
                True(selected.Id.EndsWith(
                    "::variant::default",
                    StringComparison.Ordinal));

                var tab = Required(
                    window.FindControl<TabItem>(
                        "PreviewAuthoringDataTab"));
                var host = Required(
                    window.FindControl<ContentControl>(
                        "PreviewAuthoringDataHost"));
                var messages = Required(
                    window.FindControl<TextBox>(
                        "ShellMessagesTextBox"));
                True(SpinWait.SpinUntil(
                    () =>
                    {
                        Dispatcher.UIThread.RunJobs();
                        return tab.IsVisible
                            && host.Content is Control;
                    },
                    TimeSpan.FromSeconds(10)),
                    "Conversation Test Values did not become visible. "
                    + messages.Text);
                Equal("Test Values", tab.Header as string);

                var runtime = Required(host.Content as Control);
                var fieldIds = runtime
                    .GetVisualDescendants()
                    .OfType<DictionaryFieldControl>()
                    .Select((field) => field.FieldId)
                    .ToHashSet(StringComparer.Ordinal);
                True(fieldIds.Contains("conversationType"));
                True(fieldIds.Contains("actor"));
                True(fieldIds.Contains("headerSubtitle"));
                True(runtime.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Any((text) => text.Text == "Messages"));

                window.Hide();
            },
            CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ObsoleteInteractivePreviewRenderResultsAreDiscarded()
{
    True(DesignWebPreviewPane.ShouldDiscardRenderedUpdate(
        sequence: 4,
        latestSequence: 5,
        isPlaybackUpdate: false));
    True(!DesignWebPreviewPane.ShouldDiscardRenderedUpdate(
        sequence: 5,
        latestSequence: 5,
        isPlaybackUpdate: false));
    True(!DesignWebPreviewPane.ShouldDiscardRenderedUpdate(
        sequence: 4,
        latestSequence: 5,
        isPlaybackUpdate: true));
}

static void PinnedModuleVariantPreviewSurvivesEditorSelection()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-headless-pinned-preview-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(
            () =>
            {
                var window = DesktopHost.CreateWindow(temporary);
                window.Width = 3000;
                window.Height = 900;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var nodes = WindowSession(window).TreeRoots
                    .SelectMany(DescendantsAndSelf)
                    .ToList();
                var conversation = nodes.Single((node) =>
                    node.Kind == ProjectTreeNodeKind.Module
                    && node.Id == "module_core_chat");
                var other = nodes.First((node) =>
                    node.Kind == ProjectTreeNodeKind.ComponentClass);
                var selectNode = typeof(MainWindow).GetMethod(
                    "SelectNodeById",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(string)],
                    modifiers: null)
                    ?? throw new InvalidOperationException(
                        "Missing MainWindow node selection boundary.");
                True((bool)(selectNode.Invoke(
                    window,
                    [conversation.Id]) ?? false));
                Dispatcher.UIThread.RunJobs();

                var pinned = Required(WindowSession(window).SelectedNode);
                Equal(ProjectTreeNodeKind.ModuleVariant, pinned.Kind);
                var lockButton = Required(
                    window.FindControl<Button>(
                        "PreviewContextLockButton"));
                var messages = Required(
                    window.FindControl<TextBox>(
                        "ShellMessagesTextBox"));
                True(lockButton.IsEnabled);
                lockButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                True(!(messages.Text ?? "").Contains(
                    "Module variant has no parent module",
                    StringComparison.Ordinal));

                True((bool)(selectNode.Invoke(
                    window,
                    [other.Id]) ?? false));
                Dispatcher.UIThread.RunJobs();
                True(WindowSession(window).SelectedNode?.Id != pinned.Id);
                True(!(messages.Text ?? "").Contains(
                    "Module variant has no parent module",
                    StringComparison.Ordinal));

                window.Hide();
            },
            CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(temporary);
    }
}

void ManifestOwnersRenderCommittedFixturesAndModulesAdvanceTime()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-manifest-owner-coverage-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var metrics = database.GetDevicePreviewMetrics(device.Id);

        foreach (var componentType in DesktopPreviewManifest.Components.Keys.Order(StringComparer.Ordinal))
        {
            var ownerSelector = $"component:{componentType}";
            if (selectedManifestOwners.Count > 0
                && !selectedManifestOwners.Contains(ownerSelector))
            {
                continue;
            }
            try
            {
                var components = nodes.Where((node) =>
                        node.Kind == ProjectTreeNodeKind.ComponentClass
                        && database.GetComponentClassSettings(node.Id).ComponentType == componentType)
                    .ToList();
                True(components.Count > 0);
                foreach (var component in components)
                {
                    var fixtures = component.Children
                        .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant)
                        .ToList();
                    True(fixtures.Count > 0);
                    var projectId = database.GetComponentClassSettings(component.Id).ProjectId;
                    foreach (var fixture in fixtures)
                    {
                        foreach (var themeMode in new[] { "light", "dark" })
                        {
                            foreach (var frame in new[] { 0, 1, 12, 60 })
                            {
                                var payload = Required(CreatePreviewPayload(
                                    database,
                                    fixture,
                                    theme.Id,
                                    themeMode: themeMode,
                                    timelineFrame: frame));
                                Equal(frame, payload.LocalFrame);
                                var inputSession =
                                    new ComponentPreviewInputSession(
                                        database.Design,
                                        database.DictionaryContext,
                                        database.Resources,
                                        database.ProjectPaths,
                                        () => { });
                                inputSession.UpdateForPayload(payload, projectId);
                                payload = inputSession.ApplyInputs(payload, themeMode, projectId);
                                Equal(themeMode, payload.ThemeMode);
                                var html = WebDesignPreviewRenderer.RenderBodyAsync(metrics, false, payload)
                                    .GetAwaiter()
                                    .GetResult();
                                True(!string.IsNullOrWhiteSpace(html));
                                True(!html.Contains("preview-error", StringComparison.Ordinal));
                                True(html.Contains("data-renderable-id=", StringComparison.Ordinal));
                            }
                        }
                    }
                }
                Console.WriteLine($"PASS OWNER {ownerSelector}");
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Manifest Component '{componentType}' has no observable committed fixture coverage: {exception.Message}",
                    exception);
            }
        }

        foreach (var moduleClass in DesktopPreviewManifest.Modules.Keys.Order(StringComparer.Ordinal))
        {
            var ownerSelector = $"module:{moduleClass}";
            if (selectedManifestOwners.Count > 0
                && !selectedManifestOwners.Contains(ownerSelector))
            {
                continue;
            }
            try
            {
                var modules = nodes.Where((node) =>
                        node.Kind == ProjectTreeNodeKind.Module
                        && node.RecordClassId == moduleClass)
                    .ToList();
                True(modules.Count > 0);
                foreach (var module in modules)
                {
                    var fixtures = module.Children
                        .Where((node) => node.Kind == ProjectTreeNodeKind.ModuleVariant)
                        .ToList();
                    True(fixtures.Count > 0);
                    foreach (var fixture in fixtures)
                    {
                        foreach (var frame in new[] { 0, 1, 12, 60 })
                        {
                            var payload = Required(CreatePreviewPayload(
                                database,
                                fixture,
                                theme.Id,
                                timelineFrame: frame));
                            Equal(frame, payload.LocalFrame);
                            var html = WebDesignPreviewRenderer.RenderBodyAsync(metrics, false, payload)
                                .GetAwaiter()
                                .GetResult();
                            True(!string.IsNullOrWhiteSpace(html));
                            True(!html.Contains("preview-error", StringComparison.Ordinal));
                            True(html.Contains("data-renderable-id=", StringComparison.Ordinal));
                        }
                    }
                }
                Console.WriteLine($"PASS OWNER {ownerSelector}");
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Manifest Module '{moduleClass}' has no observable committed timing fixture coverage: {exception.Message}",
                    exception);
            }
        }
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

static void ProjectOwnedReferencesRejectCrossProjectValues()
{
    AssertRejectedDatabaseIsReadOnly("cross-project-actor-device", (connection) =>
    {
        InsertCrossProject(connection);
        Execute(connection, """
            UPDATE devices
            SET project_id = 'project_cross'
            WHERE id = (SELECT default_device_id FROM actors WHERE id = 'actor_alex')
            """);
    });
    AssertRejectedDatabaseIsReadOnly("cross-project-actor-theme", (connection) =>
    {
        InsertCrossProject(connection);
        Execute(connection, """
            UPDATE themes
            SET project_id = 'project_cross'
            WHERE id = (SELECT default_theme_id FROM actors WHERE id = 'actor_alex')
            """);
    });
    AssertRejectedDatabaseIsReadOnly("cross-project-shot-actor", (connection) =>
    {
        InsertCrossProject(connection);
        InsertCrossProjectActor(connection);
        Execute(connection, "UPDATE shots SET owner_actor_id = 'actor_cross' WHERE id = 'shot_001'");
    });
    AssertRejectedDatabaseIsReadOnly("cross-project-theme-icon-theme", (connection) =>
    {
        InsertCrossProject(connection);
        Execute(connection, """
            UPDATE icon_themes
            SET project_id = 'project_cross'
            WHERE id = (
                SELECT icon_theme_id
                FROM themes
                WHERE icon_theme_id <> ''
                ORDER BY id
                LIMIT 1)
            """);
    });
    AssertRejectedDatabaseIsReadOnly("cross-project-theme-status-bar", (connection) =>
    {
        InsertCrossProject(connection);
        Execute(connection, """
            UPDATE component_classes
            SET project_id = 'project_cross'
            WHERE id = 'component_project_foqn_s2_status_bar'
            """);
    });
    AssertRejectedDatabaseIsReadOnly("cross-project-theme-navigation-bar", (connection) =>
    {
        InsertCrossProject(connection);
        Execute(connection, """
            UPDATE component_classes
            SET project_id = 'project_cross'
            WHERE id = 'component_project_foqn_s2_navigation_bar'
            """);
    });

    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-cross-project-writes-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        _ = new SqliteProjectTestContext(temporary);
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            InsertCrossProject(connection);
            InsertCrossProjectActor(connection);
            Execute(connection, """
                INSERT INTO devices (
                    id, project_id, name, manufacturer, model, os_family, metrics_json)
                SELECT
                    'device_cross', 'project_cross', 'Cross Device',
                    manufacturer, model, os_family, metrics_json
                FROM devices
                ORDER BY id
                LIMIT 1
                """);
            Execute(connection, """
                INSERT INTO themes (
                    id, project_id, name, family, icon_theme_id,
                    status_bar_id, navigation_bar_id, tokens_json, metadata_json)
                SELECT
                    'theme_cross', 'project_cross', 'Cross Theme', family, '',
                    '', '', tokens_json, metadata_json
                FROM themes
                ORDER BY id
                LIMIT 1
                """);
            Execute(connection, """
                INSERT INTO icon_themes (
                    id, project_id, name, asset_root, mapping_json, metadata_json)
                SELECT
                    'icon_theme_cross', 'project_cross', 'Cross Icon Theme',
                    asset_root, mapping_json, metadata_json
                FROM icon_themes
                ORDER BY id
                LIMIT 1
                """);
            InsertCrossProjectComponent(
                connection,
                "component_cross_status_bar",
                StatusBarComponentConfigContract.ComponentType,
                "Cross Status Bar");
            InsertCrossProjectComponent(
                connection,
                "component_cross_navigation_bar",
                NavigationBarComponentConfigContract.ComponentType,
                "Cross Navigation Bar");
        }

        var context = new SqliteProjectContext(temporary);
        IActorRepository actorRepository = new ActorRepository(context);
        IShotRepository shotRepository = new ShotRepository(context);
        IThemeRepository themeRepository = new ThemeRepository(context);
        const string actorId = "actor_alex";
        const string shotId = "shot_001";
        const string themeId = "theme_88126480cb044ecdbdee380aea764a2d";

        var actorBefore = actorRepository.GetSettings(actorId);
        var shotBefore = shotRepository.Get(shotId);
        var themeBefore = themeRepository.Get(themeId);
        Throws<InvalidOperationException>(() =>
            actorRepository.UpdateField(actorId, "actor.defaultDeviceId", "device_cross"));
        Throws<InvalidOperationException>(() =>
            actorRepository.UpdateField(actorId, "actor.defaultThemeId", "theme_cross"));
        using (var connection = context.OpenConnection())
        {
            Throws<InvalidOperationException>(() =>
                shotRepository.UpdateField(connection, shotId, "shot.ownerActorId", "actor_cross"));
        }
        Throws<InvalidOperationException>(() =>
            themeRepository.UpdateDirectField(themeId, "theme.iconThemeId", "icon_theme_cross"));
        Throws<InvalidOperationException>(() =>
            themeRepository.UpdateDirectField(
                themeId,
                "theme.statusBarId",
                "component_cross_status_bar::variant::default"));
        Throws<InvalidOperationException>(() =>
            themeRepository.UpdateDirectField(
                themeId,
                "theme.navigationBarId",
                "component_cross_navigation_bar::variant::default"));

        Equal(actorBefore, actorRepository.GetSettings(actorId));
        Equal(shotBefore, shotRepository.Get(shotId));
        Equal(themeBefore, themeRepository.Get(themeId));

        using (var connection = context.OpenConnection())
        {
            var actorCountBefore = SqliteCommandExecutor.ScalarLong(
                connection,
                "SELECT COUNT(*) FROM actors");
            var duplicate = actorRepository.Duplicate(
                connection,
                actorId,
                "Valid Actor copy");
            Equal(actorBefore.DefaultDeviceId, duplicate.DefaultDeviceId);
            Equal(actorBefore.DefaultThemeId, duplicate.DefaultThemeId);
            Equal(actorBefore, actorRepository.GetSettings(actorId));
            actorRepository.Delete(connection, duplicate.Id);
            Equal(
                actorCountBefore,
                SqliteCommandExecutor.ScalarLong(connection, "SELECT COUNT(*) FROM actors"));

            Execute(
                connection,
                "UPDATE actors SET default_device_id = 'device_cross' WHERE id = 'actor_alex'");
            var invalidDeviceSource = actorRepository.GetSettings(actorId);
            Throws<InvalidOperationException>(() =>
                actorRepository.Duplicate(connection, actorId, "Invalid Device Actor copy"));
            Equal(invalidDeviceSource, actorRepository.GetSettings(actorId));
            Equal(
                actorCountBefore,
                SqliteCommandExecutor.ScalarLong(connection, "SELECT COUNT(*) FROM actors"));

            Execute(
                connection,
                "UPDATE actors SET default_device_id = $defaultDeviceId, default_theme_id = 'theme_cross' WHERE id = 'actor_alex'",
                ("$defaultDeviceId", actorBefore.DefaultDeviceId));
            var invalidThemeSource = actorRepository.GetSettings(actorId);
            Throws<InvalidOperationException>(() =>
                actorRepository.Duplicate(connection, actorId, "Invalid Theme Actor copy"));
            Equal(invalidThemeSource, actorRepository.GetSettings(actorId));
            Equal(
                actorCountBefore,
                SqliteCommandExecutor.ScalarLong(connection, "SELECT COUNT(*) FROM actors"));

            Execute(
                connection,
                "UPDATE actors SET default_theme_id = $defaultThemeId WHERE id = 'actor_alex'",
                ("$defaultThemeId", actorBefore.DefaultThemeId));
            Equal(actorBefore, actorRepository.GetSettings(actorId));
        }
    }
    finally
    {
        File.Delete(temporary);
    }

    static void InsertCrossProject(SqliteConnection connection)
    {
        Execute(connection, """
            INSERT INTO projects (
                id, name, slug, default_fps, notes, media_root,
                production_code, production_season_code,
                output_name_separator, shot_prefix, shot_number_padding,
                output_version_padding, output_frame_padding,
                output_relative_directory_template, metadata_json)
            VALUES (
                'project_cross', 'Cross Project', 'cross-project', 25, '', '',
                'CROSS', 'S01', '_', 'SH', 4, 3, 8,
                '{{SEASON_CODE}}/{{EPISODE_CODE}}/{{SHOT_NAME}}/comp', '{}')
            """);
    }

    static void InsertCrossProjectActor(SqliteConnection connection)
    {
        Execute(connection, """
            INSERT INTO actors (
                id, project_id, display_name, short_name,
                default_device_id, default_theme_id, metadata_json)
            SELECT
                'actor_cross', 'project_cross', 'Cross Actor', 'CA',
                '', '', metadata_json
            FROM actors
            ORDER BY id
            LIMIT 1
            """);
    }

    static void InsertCrossProjectComponent(
        SqliteConnection connection,
        string id,
        string componentType,
        string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO component_classes (
                id, project_id, component_type, record_class_id, name, notes,
                config_json, design_preview_json, metadata_json)
            SELECT
                $id, 'project_cross', component_type, record_class_id, $name, notes,
                config_json, design_preview_json, metadata_json
            FROM component_classes
            WHERE component_type = $componentType
            ORDER BY id
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$componentType", componentType);
        command.Parameters.AddWithValue("$name", name);
        Equal(1, command.ExecuteNonQuery());
    }

    static void Execute(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        command.ExecuteNonQuery();
    }
}

static void CurrentEditorLayoutContractFailsReadOnly()
{
    AssertRejectedDatabaseIsReadOnly("retired-simplified-editor", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE editor_layouts
            SET layout_json = json_set(
                layout_json,
                '$.simplified',
                json_object('groups', json_array(), 'capturedSlots', json_array()))
            WHERE record_class_id = 'component.keypad'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("editor-layout-without-cards", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE editor_layouts
            SET layout_json = json_remove(layout_json, '$.cards')
            WHERE record_class_id = 'component.keypad'
            """;
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
    AssertRejectedDatabaseIsReadOnly("component-default-unlocked", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET metadata_json = json_set(metadata_json, '$.variants[0].locked', json('false'))
            WHERE id = 'component_project_foqn_s2_label'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-locked", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET metadata_json = json_remove(metadata_json, '$.variants[0].locked')
            WHERE id = 'component_project_foqn_s2_label'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-config", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET metadata_json = json_remove(metadata_json, '$.variants[0].config')
            WHERE id = 'component_project_foqn_s2_label'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-name", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_remove(metadata_json, '$.variants[0].name') WHERE id = 'component_project_foqn_s2_label'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-protected", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_remove(metadata_json, '$.variants[0].protected') WHERE id = 'component_project_foqn_s2_label'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-variant-duplicate-id", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_insert(metadata_json, '$.variants[#]', json_extract(metadata_json, '$.variants[0]')) WHERE id = 'component_project_foqn_s2_label'";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("component-default-unprotected", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE component_classes SET metadata_json = json_set(metadata_json, '$.variants[0].protected', json('false')) WHERE id = 'component_project_foqn_s2_label'";
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
    AssertRejectedDatabaseIsReadOnly("module-default-unlocked", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE modules
            SET metadata_json = json_set(metadata_json, '$.variants[0].locked', json('false'))
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

static void SystemBarComponentContractsFailReadOnly()
{
    AssertRejectedDatabaseIsReadOnly("status-bar-missing-items", (connection) =>
    {
        MutateComponentClassAndDefaultVariant(
            connection,
            "component_project_foqn_s2_status_bar",
            (config) => config.Remove("items"));
    });
    AssertRejectedDatabaseIsReadOnly("status-bar-duplicate-item-id", (connection) =>
    {
        MutateComponentClassAndDefaultVariant(
            connection,
            "component_project_foqn_s2_status_bar",
            (config) =>
            {
                var items = config["items"]?.AsArray()
                    ?? throw new InvalidOperationException("Missing fixture Status Bar items.");
                items[1]!["id"] = items[0]!["id"]!.DeepClone();
            });
    });
    AssertRejectedDatabaseIsReadOnly("navigation-bar-invalid-zone", (connection) =>
    {
        MutateComponentClassAndDefaultVariant(
            connection,
            "component_project_foqn_s2_navigation_bar",
            (config) =>
            {
                var items = config["items"]?.AsArray()
                    ?? throw new InvalidOperationException("Missing fixture Navigation Bar items.");
                items[0]!["zone"] = "automatic";
            });
    });
}

static void ListComponentContractsFailReadOnly()
{
    AssertRejectedDatabaseIsReadOnly("list-missing-boundary-motion", (connection) =>
    {
        MutateComponentClassAndDefaultVariant(
            connection,
            "component_project_foqn_s2_list",
            (config) => config.Remove("boundaryMotion"));
    });
    AssertRejectedDatabaseIsReadOnly("list-item-missing-boundary-motion", (connection) =>
    {
        MutateComponentClassAndDefaultVariant(
            connection,
            "component_project_foqn_s2_list_item",
            (config) => config.Remove("boundaryMotion"));
    });
    AssertRejectedDatabaseIsReadOnly("list-wrong-stack-type", (connection) =>
    {
        MutateComponentClassAndDefaultVariant(
            connection,
            "component_project_foqn_s2_list",
            (config) =>
            {
                config["list"]!["collectionStackSlot"]!["variantReference"] =
                    "component_project_foqn_s2_button::variant::default";
            });
    });
    AssertRejectedDatabaseIsReadOnly("list-item-extra-content-set", (connection) =>
    {
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT design_preview_json
            FROM component_classes
            WHERE id = 'component_project_foqn_s2_list_item'
            """;
        var preview = JsonPath.ParseRequiredObject(
            select.ExecuteScalar() as string ?? "",
            "List Item Design Preview fixture");
        JsonPath.RequiredArray(preview, "contentSets", "List Item Design Preview fixture")
            .Add(new JsonObject { ["id"] = "unexpected_extra_set" });
        using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE component_classes
            SET design_preview_json = $preview
            WHERE id = 'component_project_foqn_s2_list_item'
            """;
        update.Parameters.AddWithValue("$preview", preview.ToJsonString());
        update.ExecuteNonQuery();
    });

    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Path.GetTempPath(),
        $"mockups-list-contracts-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.UpdateComponentClassField(
            "component_project_foqn_s2_list",
            "component.list.collectionStack",
            """
            {"variantReference":"component_project_foqn_s2_button::variant::default","overrides":{}}
            """));
        var preview = JsonPath.ParseRequiredObject(
            database.GetComponentClassSettings(
                "component_project_foqn_s2_list_item").DesignPreviewJson,
            "List Item Design Preview");
        JsonPath.RequiredArray(preview, "contentSets", "List Item Design Preview")
            .Add(new JsonObject { ["id"] = "unexpected_extra_set" });
        Throws<InvalidOperationException>(() =>
            database.UpdateComponentClassDesignPreviewJson(
                "component_project_foqn_s2_list_item",
                preview.ToJsonString()));
        SequenceEqual(before, SHA256.HashData(File.ReadAllBytes(temporary)));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ModuleConfigsUseOwnerContracts()
{
    AssertRejectedDatabaseIsReadOnly("conversation-module-input-root", (connection) =>
    {
        MutateModuleAndDefaultVariant(
            connection,
            "module_core_chat",
            (config) => config["conversation"]!["headerLeftIconRowInputs"] = new JsonArray());
    });
    AssertRejectedDatabaseIsReadOnly("lock-screen-module-items-root", (connection) =>
    {
        MutateModuleAndDefaultVariant(
            connection,
            "module_project_foqn_s2_lock_screen",
            (config) => config["lockScreen"]!["stackInputs"]!["items"] = new JsonObject());
    });

    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-config-owner-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var conversation = nodes.Single((node) => node.Id == "module_core_chat");
        var conversationVariant = nodes.Single((node) => node.Id == "module_core_chat::variant::default");
        var lockScreen = nodes.Single((node) => node.Id == "module_project_foqn_s2_lock_screen");
        var beforeRejectedWrites = SHA256.HashData(File.ReadAllBytes(temporary));

        Throws<InvalidOperationException>(() => database.UpdateModuleField(
            conversation.Id,
            "module.conversation.headerLeftIconRow.inputs",
            "[]"));
        Throws<InvalidOperationException>(() => database.UpdateModuleField(
            conversation.Id,
            "module.conversation.showHeader",
            "perhaps"));
        Throws<InvalidOperationException>(() => database.UpdateModuleField(
            conversation.Id,
            "module.conversation.headerHeight",
            "many"));
        Throws<InvalidOperationException>(() => database.UpdateModuleVariantField(
            conversationVariant,
            "module.conversation.headerAvatarAlignment",
            "automatic"));
        Throws<InvalidOperationException>(() => database.UpdateModuleField(
            lockScreen.Id,
            "module.lockScreen.stackItems",
            "{}"));
        SequenceEqual(beforeRejectedWrites, SHA256.HashData(File.ReadAllBytes(temporary)));

        conversationVariant = NodeCommands(database)
            .ToggleModuleVariantLock(conversationVariant);
        database.UpdateModuleVariantField(
            conversationVariant,
            "module.conversation.showHeader",
            "false");
        Equal(
            "false",
            database.GetModuleVariantConfigFieldValue(
                conversationVariant,
                "module.conversation.showHeader"));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void SystemBarItemsUseFixedDictionaryCollections()
{
    var statusField = ComponentClassFieldCatalog.Get("component.statusBar.items");
    var navigationField = ComponentClassFieldCatalog.Get("component.navigationBar.items");
    Equal(ValueKind.StructuredCollection, statusField.ValueKind);
    Equal(ValueKind.StructuredCollection, navigationField.ValueKind);
    True(statusField.StructuredCollection is { CanEditStructure: false });
    True(navigationField.StructuredCollection is { CanEditStructure: false });
    SequenceEqual(
        ["textValue", "signalValue", "batteryValue", "token", "charging", "zone", "order"],
        statusField.StructuredCollection!.Fields.Where((field) => field.ShowInEditor).Select((field) => field.Id));
    SequenceEqual(
        ["zone", "order"],
        navigationField.StructuredCollection!.Fields.Where((field) => field.ShowInEditor).Select((field) => field.Id));

    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-system-bar-items-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var statusClass = nodes.Single((node) => node.Id == "component_project_foqn_s2_status_bar");
        var statusDefault = nodes.Single((node) => node.Id == $"{statusClass.Id}::variant::default");
        True(!new ComponentClassFieldValueService(
                ComponentFields(database),
                ComponentDocuments(database))
            .CreateFieldValue(statusDefault, statusField.Id)
            .Definition.IsEditable);
        var statusVariant = nodes.Single((node) => node.Id == $"{statusClass.Id}::variant::lock_screen");
        var classConfigBefore = database.GetComponentClassSettings(statusClass.Id).ConfigJson;
        var statusConfig = JsonPath.ParseRequiredObject(
            database.GetComponentVariantSettings(statusVariant).ConfigJson,
            "Status Bar test Variant");
        var statusItems = statusConfig["items"]?.AsArray().DeepClone().AsArray()
            ?? throw new InvalidOperationException("Missing Status Bar items.");
        statusItems[0]!["zone"] = "right";
        database.UpdateComponentVariantField(statusVariant, statusField.Id, statusItems.ToJsonString());
        var statusAfter = JsonPath.ParseRequiredObject(
            database.GetComponentVariantSettings(statusVariant).ConfigJson,
            "Updated Status Bar test Variant");
        Equal("right", statusAfter["items"]?[0]?["zone"]?.GetValue<string>() ?? "");
        Equal(
            "theme.iconSizes.m",
            statusAfter["items"]?[0]?["iconSizeToken"]?.GetValue<string>() ?? "");
        Equal(classConfigBefore, database.GetComponentClassSettings(statusClass.Id).ConfigJson);

        var navigationClass = nodes.Single((node) => node.Id == "component_project_foqn_s2_navigation_bar");
        var navigationVariant = nodes.Single((node) => node.Id == $"{navigationClass.Id}::variant::default_copy");
        navigationVariant = NodeCommands(database)
            .ToggleComponentVariantLock(navigationVariant);
        True(!navigationVariant.IsLocked);
        var navigationConfig = JsonPath.ParseRequiredObject(
            database.GetComponentVariantSettings(navigationVariant).ConfigJson,
            "Navigation Bar test Variant");
        var navigationItems = navigationConfig["items"]?.AsArray().DeepClone().AsArray()
            ?? throw new InvalidOperationException("Missing Navigation Bar items.");
        navigationItems[0]!["zone"] = "right";
        navigationItems[0]!["order"] = 90;
        database.UpdateComponentVariantField(navigationVariant, navigationField.Id, navigationItems.ToJsonString());
        var navigationAfter = JsonPath.ParseRequiredObject(
            database.GetComponentVariantSettings(navigationVariant).ConfigJson,
            "Updated Navigation Bar test Variant");
        Equal("right", navigationAfter["items"]?[0]?["zone"]?.GetValue<string>() ?? "");
        Equal(90, navigationAfter["items"]?[0]?["order"]?.GetValue<int>() ?? -1);
        Equal(
            "theme.iconSizes.m",
            navigationAfter["items"]?[0]?["iconSizeToken"]?.GetValue<string>() ?? "");

        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        navigationItems[1]!["id"] = navigationItems[0]!["id"]!.DeepClone();
        Throws<InvalidOperationException>(() =>
            database.UpdateComponentVariantField(navigationVariant, navigationField.Id, navigationItems.ToJsonString()));
        var afterRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(beforeRejectedWrite, afterRejectedWrite);

        foreach (var (recordClassId, fieldId) in new[]
        {
            ("component.status_bar", statusField.Id),
            ("component.navigation_bar", navigationField.Id),
        })
        {
            var layout = EditorLayouts(database).LoadEditorLayout(recordClassId);
            True(layout.Cards.SelectMany((card) => card.Groups)
                .SelectMany((group) => group.Fields)
                .Any((field) => field.Id == fieldId && field.Visible));
        }
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void MutateComponentClassAndDefaultVariant(
    SqliteConnection connection,
    string componentClassId,
    Action<JsonObject> mutate)
{
    using var select = connection.CreateCommand();
    select.CommandText = "SELECT config_json, metadata_json FROM component_classes WHERE id = $id";
    select.Parameters.AddWithValue("$id", componentClassId);
    using var reader = select.ExecuteReader();
    if (!reader.Read()) throw new InvalidOperationException($"Missing fixture Component class '{componentClassId}'.");
    var config = JsonPath.ParseRequiredObject(reader.GetString(0), $"{componentClassId} config");
    var metadata = JsonPath.ParseRequiredObject(reader.GetString(1), $"{componentClassId} metadata");
    reader.Close();
    var defaultVariant = VariantEnvelopeContract.Read(metadata, "variants", componentClassId)
        .Single((variant) => variant.Id == "default");
    mutate(config);
    mutate(defaultVariant.Config);

    using var update = connection.CreateCommand();
    update.CommandText = "UPDATE component_classes SET config_json = $config, metadata_json = $metadata WHERE id = $id";
    update.Parameters.AddWithValue("$config", config.ToJsonString());
    update.Parameters.AddWithValue("$metadata", metadata.ToJsonString());
    update.Parameters.AddWithValue("$id", componentClassId);
    update.ExecuteNonQuery();
}

static void MutateModuleAndDefaultVariant(
    SqliteConnection connection,
    string moduleId,
    Action<JsonObject> mutate)
{
    using var select = connection.CreateCommand();
    select.CommandText = "SELECT config_json, metadata_json FROM modules WHERE id = $id";
    select.Parameters.AddWithValue("$id", moduleId);
    using var reader = select.ExecuteReader();
    if (!reader.Read()) throw new InvalidOperationException($"Missing fixture Module '{moduleId}'.");
    var config = JsonPath.ParseRequiredObject(reader.GetString(0), $"{moduleId} config");
    var metadata = JsonPath.ParseRequiredObject(reader.GetString(1), $"{moduleId} metadata");
    reader.Close();
    var defaultVariant = VariantEnvelopeContract.Read(metadata, "variants", moduleId)
        .Single((variant) => variant.Id == "default");
    mutate(config);
    mutate(defaultVariant.Config);

    using var update = connection.CreateCommand();
    update.CommandText = "UPDATE modules SET config_json = $config, metadata_json = $metadata WHERE id = $id";
    update.Parameters.AddWithValue("$config", config.ToJsonString());
    update.Parameters.AddWithValue("$metadata", metadata.ToJsonString());
    update.Parameters.AddWithValue("$id", moduleId);
    update.ExecuteNonQuery();
}

static void VariantWritesDoNotRepairMissingArrays()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-variant-write-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var defaultVariant = Descendants(database.LoadProjectTree()).Single((node) =>
            node.Id == "component_project_foqn_s2_label::variant::default");
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE component_classes
                SET metadata_json = json_remove(metadata_json, '$.variants')
                WHERE id = 'component_project_foqn_s2_label'
                """;
            command.ExecuteNonQuery();
        }

        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() =>
            NodeCommands(database).SaveComponentVariant(
                defaultVariant,
                "Must fail"));
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
        var database = new SqliteProjectTestContext(temporary);
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
        Throws<InvalidOperationException>(() =>
            NodeCommands(database).SaveModuleVariant(
                defaultVariant,
                "Must fail"));
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
    var source = ParityDatabasePath();
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
        Throws<InvalidOperationException>(() => _ = new SqliteProjectTestContext(temporary));
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void EditorLayoutSaveKeepsOnlyAuthoredCardMetadata()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-layout-serialization-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var layout = EditorLayouts(database).LoadEditorLayout("component.keypad");
        EditorLayouts(database).SaveEditorLayout("component.keypad", layout);
        using var connection = new SqliteConnection($"Data Source={temporary}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT layout_json FROM editor_layouts WHERE record_class_id = 'component.keypad'";
        var json = command.ExecuteScalar() as string ?? throw new InvalidOperationException("Missing Keypad editor layout.");
        True(!json.Contains("\"VisibleGroups\"", StringComparison.Ordinal));
        True(!json.Contains("\"VisibleFields\"", StringComparison.Ordinal));
        True(!json.Contains("\"Entries\"", StringComparison.Ordinal));
        True(!json.Contains("\"simplified\"", StringComparison.Ordinal));
        Equal(layout.Cards.Count, JsonNode.Parse(json)?["cards"]?.AsArray().Count ?? -1);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ExtractedRepositoriesPreserveFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-repository-contract-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var context = new SqliteProjectContext(temporary);
        IEditorLayoutStore layoutRepository = new SqliteEditorLayoutStore(context);
        IShotRepository shotRepository = new ShotRepository(context);
        IProjectEpisodeRepository projectEpisodeRepository = new ProjectEpisodeRepository(context, shotRepository);

        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var episode = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.Episode);

        Equal(database.GetProjectSettings(project.Id), projectEpisodeRepository.GetProjectSettings(project.Id));
        Equal(database.GetEpisodeSettings(episode.Id), projectEpisodeRepository.GetEpisodeSettings(episode.Id));

        var facadeLayout = EditorLayouts(database).LoadEditorLayout("component.keypad");
        var repositoryLayout = layoutRepository.LoadEditorLayout("component.keypad");
        Equal(facadeLayout.Cards.Count, repositoryLayout.Cards.Count);
        layoutRepository.SaveEditorLayout("component.keypad", repositoryLayout);
        Equal(repositoryLayout.Cards.Count, EditorLayouts(database).LoadEditorLayout("component.keypad").Cards.Count);

        using (var connection = context.OpenConnection())
        {
            True(projectEpisodeRepository.QueryProjects(connection).Any((row) => row.Id == project.Id));
            True(projectEpisodeRepository.QueryEpisodes(connection).Any((row) => row.Id == episode.Id));
        }

        var originalProject = database.GetProjectSettings(project.Id);
        projectEpisodeRepository.UpdateProjectField(project.Id, "project.slug", $"{originalProject.Slug}-repository");
        Equal($"{originalProject.Slug}-repository", database.GetProjectSettings(project.Id).Slug);
        database.UpdateProjectField(project.Id, "project.slug", originalProject.Slug);
        Equal(originalProject, projectEpisodeRepository.GetProjectSettings(project.Id));

        var originalEpisode = database.GetEpisodeSettings(episode.Id);
        projectEpisodeRepository.UpdateEpisodeField(
            episode.Id,
            "episode.slug",
            "EP_99");
        Equal("EP_99", database.GetEpisodeSettings(episode.Id).Slug);
        database.UpdateEpisodeField(episode.Id, "episode.slug", originalEpisode.Slug);
        Equal(originalEpisode, projectEpisodeRepository.GetEpisodeSettings(episode.Id));

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

    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ResourceRepositoriesPreserveFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-resource-repositories-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-actor-preview-data-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new ActorPreviewDataSource(database.Resources);
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
        var lightPayload = ActorPreviewInputFactory.Create(
            dataSource,
            database.ProjectPaths,
            actor.Id,
            "light",
            paletteColors);
        var darkPayload = ActorPreviewInputFactory.Create(
            dataSource,
            database.ProjectPaths,
            actor.Id,
            "dark",
            paletteColors);
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
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-runtime-input-options-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new RuntimeInputOptionsDataSource(database.DictionaryContext, database.Resources);
        var project = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.Project);

        var actorInput = new ComponentInputDefinition(
            "actor", "Actor", "actorId", ComponentInputKind.RecordReference,
            ValueKind.RecordReference, "", TableId: "actors");
        var actorDefinition = RuntimeInputFieldDefinitionFactory.Create(dataSource, project, actorInput);
        SequenceEqual(
            database.GetRequiredActorOptions(project.Id).Select((option) => option.Value),
            actorDefinition.Options!.Select((option) => option.Value));
        var optionalActorDefinition = RuntimeInputFieldDefinitionFactory.Create(
            dataSource,
            project,
            actorInput with { AllowEmpty = true });
        SequenceEqual(
            database.GetActorOptions(project.Id).Select((option) => option.Value),
            optionalActorDefinition.Options!.Select((option) => option.Value));

        var paletteInput = new ComponentInputDefinition(
            "color", "Color", "color", ComponentInputKind.Option,
            ValueKind.PaletteColorToken, "");
        var paletteDefinition = RuntimeInputFieldDefinitionFactory.Create(dataSource, project, paletteInput);
        SequenceEqual(
            database.GetPaletteColorOptions(project.Id).Select((option) => option.Value),
            paletteDefinition.Options!.Select((option) => option.Value));

        var variantInput = new ComponentInputDefinition(
            "audio", "Audio", "variantReference", ComponentInputKind.ComponentVariant,
            ValueKind.ComponentVariant, "", ComponentType: "audio");
        var variantDefinition = RuntimeInputFieldDefinitionFactory.Create(dataSource, project, variantInput);
        SequenceEqual(
            database.GetComponentVariantReferenceOptions(project.Id, "audio", false).Select((option) => option.Value),
            variantDefinition.Options!.Select((option) => option.Value));

        var variantReference = variantDefinition.Options!.First().Value;
        var dynamicInput = new ComponentInputDefinition(
            "state", "State", "stateId", ComponentInputKind.Option,
            ValueKind.OptionToken, "",
            OptionsSourceCollectionJsonKey: "states",
            OptionsSourceValueJsonKey: "id",
            OptionsSourceLabelJsonKey: "variantReference");
        var values = new JsonObject
        {
            ["states"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "state_default",
                    ["variantReference"] = variantReference,
                },
            },
        };
        var dynamicOptions = RuntimeInputDynamicOptions.Resolve(dataSource, dynamicInput, values)!;
        Equal(1, dynamicOptions.Count);
        Equal("state_default", dynamicOptions[0].Value);
        Equal(
            database.GetRuntimeComponentVariantName(variantReference, new JsonObject(), []),
            dynamicOptions[0].Label);

        var namedInput = dynamicInput with
        {
            OptionsSourceLabelJsonKey = "name",
            OptionsSourceFirstItemBadge = "Initial",
        };
        var namedValues = new JsonObject
        {
            ["states"] = new JsonArray
            {
                new JsonObject { ["id"] = "state_clock", ["name"] = "Clock" },
            },
        };
        Equal(
            "Clock · Initial",
            RuntimeInputDynamicOptions.Resolve(dataSource, namedInput, namedValues)!.Single().Label);

        Throws<InvalidOperationException>(() => RuntimeInputDynamicOptions.Resolve(
            dataSource,
            namedInput,
            new JsonObject()));
        Throws<InvalidOperationException>(() => RuntimeInputDynamicOptions.Resolve(
            dataSource,
            namedInput,
            new JsonObject { ["states"] = new JsonObject() }));
        Throws<InvalidOperationException>(() => RuntimeInputDynamicOptions.Resolve(
            dataSource,
            namedInput,
            new JsonObject { ["states"] = new JsonArray("invalid") }));
        Throws<InvalidOperationException>(() => RuntimeInputDynamicOptions.Resolve(
            dataSource,
            namedInput,
            new JsonObject
            {
                ["states"] = new JsonArray(new JsonObject { ["id"] = "state_missing_name" }),
            }));
        Throws<InvalidOperationException>(() => RuntimeInputDynamicOptions.Resolve(
            dataSource,
            namedInput with { OptionsSourceValueJsonKey = "code" },
            new JsonObject
            {
                ["states"] = new JsonArray(new JsonObject { ["id"] = "state_missing_code", ["name"] = "Missing code" }),
            }));
        Throws<InvalidOperationException>(() => RuntimeInputDynamicOptions.Resolve(
            dataSource,
            namedInput with { OptionsSourceValueJsonKey = "code" },
            new JsonObject
            {
                ["states"] = new JsonArray
                {
                    new JsonObject { ["id"] = "state_1", ["code"] = "duplicate", ["name"] = "First" },
                    new JsonObject { ["id"] = "state_2", ["code"] = "duplicate", ["name"] = "Second" },
                },
            }));
        Throws<InvalidOperationException>(() => RuntimeInputDynamicOptions.Resolve(
            dataSource,
            dynamicInput,
            new JsonObject
            {
                ["states"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "state_missing_variant",
                        ["variantReference"] = "component_missing::variant::default",
                    },
                },
            }));

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void FixedComponentBoundariesUseExactDefaultVariant()
{
    var componentClassId = "component_project_button";
    var options = new[]
    {
        new FieldOption(
            $"{componentClassId}::variant::default",
            "Default",
            GroupValue: componentClassId,
            GroupLabel: "Button",
            LocalLabel: "Default"),
        new FieldOption(
            $"{componentClassId}::variant::compact",
            "Compact",
            GroupValue: componentClassId,
            GroupLabel: "Button",
            LocalLabel: "Compact"),
    };
    var boundary = ComponentVariantOptionContract.RequireFixedBoundary(options, "Test Button boundary");
    Equal(componentClassId, boundary.ComponentClassId);
    Equal($"{componentClassId}::variant::default", boundary.DefaultVariantReference);
    Equal(2, boundary.VariantOptions.Count);
    True(!ComponentVariantOptionContract.SelectsComponentClass("button"));
    True(ComponentVariantOptionContract.SelectsComponentClass("*,-componentStack"));

    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var list = database.LoadProjectTree()
        .SelectMany(DescendantsAndSelf)
        .Single((node) =>
            node.Kind == ProjectTreeNodeKind.ComponentClass
            && node.Id == "component_project_foqn_s2_list");
    var listVariant = list.Children.Single((node) =>
        node.Kind == ProjectTreeNodeKind.ComponentVariant
        && node.Id.EndsWith("::variant::default", StringComparison.Ordinal));
    var projectId = database.GetComponentClassSettings(list.Id).ProjectId;
    foreach (var (fieldId, componentType) in new[]
    {
        ("component.list.collectionStack", "collectionStack"),
        ("component.list.listItem", "listItem"),
        ("component.list.surface", "surface"),
    })
    {
        var actual = database.CreateComponentVariantFieldValue(listVariant, fieldId)
            .Definition.Options
            ?? [];
        var expected = database.GetComponentVariantReferenceOptionsByType(
            projectId,
            componentType);
        SequenceEqual(
            expected.Select((option) => option.Value),
            actual.Select((option) => option.Value));
    }

    Throws<InvalidOperationException>(() => ComponentVariantOptionContract.RequireFixedBoundary(
        options.Where((option) => !option.Value.EndsWith("::default", StringComparison.Ordinal)).ToList(),
        "Missing Default boundary"));
    Throws<InvalidOperationException>(() => ComponentVariantOptionContract.RequireFixedBoundary(
        [
            .. options,
            new FieldOption(
                "component_project_badge::variant::default",
                "Badge · Default",
                GroupValue: "component_project_badge",
                GroupLabel: "Badge",
                LocalLabel: "Default"),
        ],
        "Ambiguous boundary"));
}

static void DictionaryFieldContextBoundaryPreservesCurrentData()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-dictionary-field-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new DictionaryFieldContextDataSource(
            database.DictionaryContext,
            database.PreviewInputs,
            database.Production,
            database.Resources,
            database.Resources,
            database.ProjectPaths);
        var payloadData = new DesignPreviewPayloadDataSource(
            database.PreviewInputs,
            database.Production,
            database.Resources,
            database.Resources,
            database.ProjectPaths);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var project = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var componentClass = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
        var variant = componentClass.Children.First((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
        var componentSettings = database.GetComponentClassSettings(componentClass.Id);
        var screen = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var theme = nodes
            .Where((node) => node.Kind == ProjectTreeNodeKind.Theme)
            .First((node) => !string.IsNullOrWhiteSpace(database.GetThemeSettings(node.Id).IconThemeId));
        var themeSettings = database.GetThemeSettings(theme.Id);

        Equal(themeSettings.IconThemeId, dataSource.IconThemeId(variant, theme.Id));
        True(JsonNode.DeepEquals(
            DesignPreviewTestValues.Parse(themeSettings.TokensJson),
            dataSource.ThemeTokens(variant, theme.Id)));

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
            database.GetComponentVariantReferenceOptionsByType(
                project.Id,
                componentSettings.ComponentType).Select((option) => option.Value),
            dataSource.ComponentVariantOptions(
                project.Id,
                componentSettings.ComponentType).Select((option) => option.Value));

        SequenceEqual(
            database.GetComponentVariantRuntimeInputBindings(variant.Id)
                .Select((input) => $"{input.Id}\u001f{input.JsonKey}\u001f{input.ValueKind}"),
            dataSource.ComponentVariantRuntimeInputBindings(variant.Id)
                .Select((input) => $"{input.Id}\u001f{input.JsonKey}\u001f{input.ValueKind}"));
        Equal(
            database.GetComponentVariantRuntimeInputs(variant.Id).ToJsonString(),
            dataSource.ComponentVariantRuntimeValues(variant.Id).ToJsonString());
        SequenceEqual(
            database.GetComponentVariantRuntimeCollections(variant.Id)
                .Select((collection) => $"{collection.Id}\u001f{collection.JsonKey}\u001f{collection.Fields.Count}"),
            dataSource.ComponentVariantRuntimeCollections(variant.Id)
                .Select((collection) => $"{collection.Id}\u001f{collection.JsonKey}\u001f{collection.Fields.Count}"));

        var expectedSelection = database.GetComponentVariantSelectionSettings(variant.Id);
        var selection = dataSource.ComponentVariantSelection(variant.Id);
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
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-embedded-component-documents-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var store = new EmbeddedComponentDocumentStore(
            ComponentDocuments(database));
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var audioClass = nodes
            .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentClass)
            .First((node) => database.GetComponentClassSettings(node.Id).ComponentType == "audio");
        var audioVariant = audioClass.Children
            .First((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
        var surfaceSlot = EmbeddedComponentSlotCatalog.Get("component.audio.surface.editor");
        var designContext = new EditorEmbeddedContext(audioVariant, [surfaceSlot]);
        var embeddedFieldId = EditorLayouts(database).LoadEditorLayout(surfaceSlot.RecordClassId).Cards
            .Where((card) => card.Visible)
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .Select((field) => field.Id)
            .First(ComponentClassFieldCatalog.IsRuntimeOverrideField);

        Equal(
            database.GetEmbeddedComponentVariantName(audioVariant, [surfaceSlot]),
            store.ActiveVariantName(designContext));
        var expectedField = database.CreateEmbeddedComponentFieldValue(
            audioVariant,
            [surfaceSlot],
            embeddedFieldId);
        var storedField = store.CreateFieldValue(designContext, embeddedFieldId);
        Equal(expectedField.Value, storedField.Value);
        Equal(expectedField.IsInherited, storedField.IsInherited);

        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        var editableVariant = NodeCommands(database).SaveComponentVariant(
            audioVariant,
            "Embedded boundary test");
        var editableContext = new EditorEmbeddedContext(editableVariant, [surfaceSlot]);
        var editableField = store.CreateFieldValue(editableContext, embeddedFieldId);
        store.CommitFieldValue(editableContext, embeddedFieldId, editableField.Value);
        Equal(
            editableField.Value,
            database.CreateEmbeddedComponentFieldValue(
                editableVariant,
                [surfaceSlot],
                embeddedFieldId).Value);

        var selection = database.GetComponentVariantSelectionSettings(audioVariant.Id);
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
                (_) =>
                {
                    overrideChanges++;
                    return Task.CompletedTask;
                }));
        Equal(
            database.GetRuntimeComponentVariantName(audioVariant.Id, overrides, []),
            store.ActiveVariantName(runtimeContext));
        True(store.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);

        var beforeRuntimeOverride = SHA256.HashData(File.ReadAllBytes(temporary));
        store.CommitFieldValueAsync(
            runtimeContext,
            "component.audio.padding",
            "theme.spacing.xl|theme.spacing.l")
            .GetAwaiter()
            .GetResult();
        Equal(1, overrideChanges);
        True(!store.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);
        store.CommitFieldValueAsync(
                runtimeContext,
                "component.audio.padding",
                "inherited")
            .GetAwaiter()
            .GetResult();
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

static void FailedRuntimeOverridePersistenceRestoresConfirmedDocument()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(
        Path.GetTempPath(),
        $"mockups-runtime-override-failure-{Guid.NewGuid():N}.sqlite");
    File.Copy(
        sourcePath,
        temporary,
        overwrite: true);
    try
    {
        var before =
            SHA256.HashData(
                File.ReadAllBytes(temporary));
        var database =
            new SqliteProjectTestContext(
                temporary);
        var store =
            new EmbeddedComponentDocumentStore(
                ComponentDocuments(database));
        var nodes = Descendants(
                database.LoadProjectTree())
            .ToList();
        var audioClass = nodes
            .Where((node) =>
                node.Kind
                == ProjectTreeNodeKind.ComponentClass)
            .First((node) =>
                database.GetComponentClassSettings(
                    node.Id).ComponentType
                == "audio");
        var audioVariant = audioClass.Children
            .First((node) =>
                node.Kind
                == ProjectTreeNodeKind
                    .ComponentVariant);
        var selection =
            database
                .GetComponentVariantSelectionSettings(
                    audioVariant.Id);
        var confirmedOverrides =
            new JsonObject();
        var context = new EditorEmbeddedContext(
            audioVariant,
            [],
            new RuntimeComponentOverrideSource(
                selection.ProjectId,
                audioVariant.Id,
                selection.ComponentType,
                selection.RecordClassId,
                selection.ConfigJson,
                confirmedOverrides,
                (_) => Task.FromException(
                    new InvalidOperationException(
                        "persistence failed"))));

        Throws<InvalidOperationException>(
            () => store.CommitFieldValueAsync(
                    context,
                    "component.audio.padding",
                    "theme.spacing.xl|theme.spacing.l")
                .GetAwaiter()
                .GetResult());

        Equal(
            0,
            confirmedOverrides.Count);
        True(store.CreateFieldValue(
                context,
                "component.audio.padding")
            .IsInherited);
        SequenceEqual(
            before,
            SHA256.HashData(
                File.ReadAllBytes(temporary)));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void EditorPresentationContextBoundaryPreservesCurrentData()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-editor-presentation-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource =
            new EditorPresentationContextDataSource(database.Resources);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var project = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var productionFont = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ProductionFont);
        var themeSettings = database.Resources.GetThemeSettings(theme.Id);

        Equal(
            database.Resources.GetProjectSettings(project.Id).MediaRoot,
            dataSource.ProjectMediaRoot(project.Id));
        var themeSource = dataSource.ThemeNavigation(theme.Id);
        Equal(themeSettings.Family, themeSource.Family);
        Equal(themeSettings.IconThemeId, themeSource.IconThemeId);
        Equal(themeSettings.StatusBarId, themeSource.StatusBarId);
        Equal(themeSettings.NavigationBarId, themeSource.NavigationBarId);
        Equal(
            database.Resources.GetProductionFontFieldValue(
                productionFont.Id,
                "font.files"),
            dataSource.ProductionFontFiles(productionFont.Id));
        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ProductionScreenPresentationBoundaryPreservesCurrentData()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-screen-presentation-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var screen = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var source = new ProductionScreenPresentationDataSource(database.PreviewInputs, database.Production, database.Resources).Load(screen.Id);

        Equal(database.GetModuleInstanceModuleName(screen.Id), source.Module);
        Equal(database.GetModuleInstanceVariantName(screen.Id), source.Variant);
        Equal(
            ModuleInstanceTimeline.DurationFrames(new ModuleInstanceTimelineDataSource(database.Production, database.Resources), screen.Id),
            source.DurationFrames);
        Equal(database.GetModuleInstanceTransitionType(screen.Id), source.Transition);

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ProductionActiveScreenPresentationFollowsShotFrames()
{
    IReadOnlyList<ProductionScreenFrameRange> ranges =
    [
        new("screen_a", 0, 5),
        new("screen_b", 5, 3),
        new("screen_c", 8, 4),
    ];

    Equal("screen_a", ProductionScreenPlaybackState.ActiveScreenId(ranges, 0));
    Equal("screen_a", ProductionScreenPlaybackState.ActiveScreenId(ranges, 4));
    Equal("screen_b", ProductionScreenPlaybackState.ActiveScreenId(ranges, 5));
    Equal("screen_b", ProductionScreenPlaybackState.ActiveScreenId(ranges, 7));
    Equal("screen_c", ProductionScreenPlaybackState.ActiveScreenId(ranges, 8));
    Equal("screen_c", ProductionScreenPlaybackState.ActiveScreenId(ranges, 99));
    Equal(1, ProductionScreenPlaybackState.ActiveScreenIndex(ranges, 5));
    Equal("", ProductionScreenPlaybackState.ActiveScreenId([], 0));
    Equal(-1, ProductionScreenPlaybackState.ActiveScreenIndex([], 0));
}

static void ComponentPreviewInputBoundaryPreservesCurrentContracts()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-component-preview-input-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new ComponentPreviewInputDataSource(database.Design, database.Resources);
        var componentClass = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
        var variant = componentClass.Children
            .First((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
        var settings = database.GetComponentClassSettings(componentClass.Id);

        Equal(
            database.GetProjectSettings(settings.ProjectId).DefaultFps,
            dataSource.ProjectDefaultFrameRate(settings.ProjectId));
        Equal(
            database.GetComponentVariantConfig(variant.Id).ToJsonString(),
            dataSource.ComponentVariantConfig(variant.Id).ToJsonString());
        Equal(
            database.GetComponentVariantRuntimeContract(variant.Id).ToJsonString(),
            dataSource.ComponentVariantRuntimeContract(variant.Id).ToJsonString());
        Equal(
            database.ValidateComponentVariantReferenceValue(
                settings.ProjectId,
                settings.ComponentType,
                variant.Id),
            dataSource.ValidateComponentVariantReference(
                settings.ProjectId,
                settings.ComponentType,
                variant.Id));

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
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-runtime-input-owner-store-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        using var operations = new EditorOperationCoordinator();
        var store = new RuntimeInputOwnerDocumentStore(
            database.Design,
            database.Production,
            operations);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var module = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Module);
        var moduleVariant = module.Children.First((node) => node.Kind == ProjectTreeNodeKind.ModuleVariant);
        var componentClass = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ComponentClass);
        var componentVariant = componentClass.Children.First((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
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

        var componentSettings = database.GetComponentVariantSettings(componentVariant);
        var componentSource = store.Load(componentVariant);
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

        var selection = database.GetComponentVariantSelectionSettings(componentVariant.Id);
        var selectionSource = store.ComponentVariantSelection(componentVariant.Id);
        Equal(selection.ProjectId, selectionSource.ProjectId);
        Equal(selection.ComponentType, selectionSource.ComponentType);
        Equal(selection.RecordClassId, selectionSource.RecordClassId);
        Equal(selection.ConfigJson, selectionSource.ConfigJson);
        Equal(
            database.GetComponentVariantRuntimeInputs(componentVariant.Id).ToJsonString(),
            store.ComponentVariantRuntimeInputs(componentVariant.Id).ToJsonString());

        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        store.SaveDesignPreviewJsonAsync(
            moduleSource,
            moduleSource.RuntimePreviewJson).GetAwaiter().GetResult();
        Equal(moduleSource.RuntimePreviewJson, database.GetModuleSettings(module.Id).DesignPreviewJson);
        store.SaveDesignPreviewJsonAsync(
            componentSource,
            componentSource.RuntimePreviewJson).GetAwaiter().GetResult();
        Equal(componentSource.RuntimePreviewJson, database.GetComponentClassSettings(componentClass.Id).DesignPreviewJson);
        Throws<InvalidOperationException>(() =>
            store.SaveDesignPreviewJsonAsync(
                instanceSource,
                instanceSource.RuntimePreviewJson).GetAwaiter().GetResult());
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RuntimeInputInstanceStorePreservesExplicitWrites()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-runtime-input-instance-store-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        using var operations = new EditorOperationCoordinator();
        var store = new RuntimeInputInstanceDocumentStore(
            new SqliteRuntimeInputInstanceStore(
                database.Context,
                database.Design,
                database.Production,
                database.Resources),
            database.Production,
            database.Production,
            database.Resources,
            operations);
        var screen = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance
                && database.GetModuleInstanceVariantSettings(node.Id).RecordClassId == "module.core.chat");
        var animationJson =
            database.GetModuleInstanceSettings(
                screen.Id).AnimationJson;
        var messagePrototype = JsonPath.ParseRequiredObject(
            database.GetModuleInstanceSettings(screen.Id).ContentJson,
            "Conversation test content")["messages"]?[0]?.DeepClone().AsObject()
            ?? throw new InvalidOperationException("Missing Conversation test message.");
        JsonObject TestMessage(string id, string name)
        {
            var message = messagePrototype.DeepClone().AsObject();
            message["id"] = id;
            message["name"] = name;
            message["direction"] = "system";
            message["actorId"] = "";
            return message;
        }

        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        const string collectionKey = "messages";
        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => store.UpdateRuntimeValueAsync(
            screen.Id,
            "undeclared_scalar",
            JsonValue.Create("value")).GetAwaiter().GetResult());
        Throws<InvalidOperationException>(() => store.UpdateRuntimeValueAsync(
            screen.Id,
            "headerSubtitle",
            JsonValue.Create(42)).GetAwaiter().GetResult());
        Throws<InvalidOperationException>(() => store.AddCollectionItemAsync(
            screen.Id,
            "undeclared_items",
            TestMessage("undeclared", "Undeclared")).GetAwaiter().GetResult());
        var missingId = TestMessage("missing", "Missing");
        missingId.Remove("id");
        Throws<InvalidOperationException>(() => store.AddCollectionItemAsync(
            screen.Id,
            collectionKey,
            missingId).GetAwaiter().GetResult());
        SequenceEqual(beforeRejectedWrite, SHA256.HashData(File.ReadAllBytes(temporary)));

        store.UpdateRuntimeValueAsync(
            screen.Id,
            "headerSubtitle",
            JsonValue.Create("value")).GetAwaiter().GetResult();
        using var queuedWriteStarted = new ManualResetEventSlim();
        using var releaseQueuedWrite = new ManualResetEventSlim();
        var blockingWrite = operations.ExecuteAsync(
            () =>
            {
                queuedWriteStarted.Set();
                releaseQueuedWrite.Wait();
            });
        True(queuedWriteStarted.Wait(TimeSpan.FromSeconds(5)));
        var queuedItem = TestMessage("test_a", "A");
        var queuedAdd = store.AddCollectionItemAsync(
            screen.Id,
            collectionKey,
            queuedItem);
        queuedItem["name"] = "Mutated after submission";
        releaseQueuedWrite.Set();
        blockingWrite.GetAwaiter().GetResult();
        queuedAdd.GetAwaiter().GetResult();
        var queuedContent = JsonPath.ParseRequiredObject(
            database.GetModuleInstanceSettings(screen.Id).ContentJson,
            $"Module Instance '{screen.Id}' queued snapshot content");
        Equal(
            "A",
            queuedContent[collectionKey]?
                .AsArray()
                .OfType<JsonObject>()
                .Single((item) => item["id"]?.GetValue<string>() == "test_a")["name"]?
                .GetValue<string>()
            ?? "");
        var afterFirstItem = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => store.AddCollectionItemAsync(
            screen.Id,
            collectionKey,
            TestMessage("test_a", "Duplicate")).GetAwaiter().GetResult());
        Throws<InvalidOperationException>(() => store.InsertCollectionItemAfterAsync(
            screen.Id,
            collectionKey,
            "missing_item",
            TestMessage("test_missing_anchor", "Missing anchor")).GetAwaiter().GetResult());
        SequenceEqual(afterFirstItem, SHA256.HashData(File.ReadAllBytes(temporary)));
        store.InsertCollectionItemAfterAsync(
            screen.Id,
            collectionKey,
            "test_a",
            TestMessage("test_b", "B")).GetAwaiter().GetResult();
        store.DuplicateCollectionItemAsync(
            screen.Id,
            collectionKey,
            "test_a",
            TestMessage("test_c", "C"),
            new Dictionary<string, string>()).GetAwaiter().GetResult();
        var beforeRejectedField = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => store.UpdateCollectionValueAsync(
            screen.Id,
            collectionKey,
            "test_b",
            "undeclared_field",
            JsonValue.Create("value")).GetAwaiter().GetResult());
        Throws<InvalidOperationException>(() => store.UpdateCollectionValueAsync(
            screen.Id,
            collectionKey,
            "test_b",
            "text",
            JsonValue.Create(42)).GetAwaiter().GetResult());
        SequenceEqual(beforeRejectedField, SHA256.HashData(File.ReadAllBytes(temporary)));
        store.UpdateCollectionValueAsync(
            screen.Id,
            collectionKey,
            "test_b",
            "text",
            JsonValue.Create("B2")).GetAwaiter().GetResult();
        store.MoveCollectionItemAsync(
            screen.Id,
            collectionKey,
            "test_c",
            1).GetAwaiter().GetResult();
        store.DeleteCollectionItemAsync(
            screen.Id,
            collectionKey,
            "test_a").GetAwaiter().GetResult();

        var content = JsonPath.ParseRequiredObject(
            database.GetModuleInstanceSettings(screen.Id).ContentJson,
            $"Module Instance '{screen.Id}' content");
        Equal("value", content["headerSubtitle"]?.GetValue<string>() ?? "");
        var items = content[collectionKey]?.AsArray()
            ?? throw new InvalidOperationException("Missing test runtime collection.");
        SequenceEqual(
            new[] { "test_b", "test_c" },
            items.Where((item) => item?["id"]?.GetValue<string>()?.StartsWith("test_", StringComparison.Ordinal) == true)
                .Select((item) => item?["id"]?.GetValue<string>() ?? ""));
        Equal(
            "B2",
            items.OfType<JsonObject>().Single((item) => item["id"]?.GetValue<string>() == "test_b")["text"]?.GetValue<string>() ?? "");
        Equal(
            animationJson,
            store.SaveAnimationJsonAsync(
                screen.Id,
                animationJson).GetAwaiter().GetResult());
        Equal(animationJson, database.GetModuleInstanceSettings(screen.Id).AnimationJson);
    }
    finally
    {
        File.Delete(temporary);
    }

    AssertRejectedDatabaseIsReadOnly("runtime-collection-duplicate-id", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_insert(content_json, '$.messages[#]', json_extract(content_json, '$.messages[0]'))
            WHERE module_id = 'module_core_chat'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-collection-wrong-root", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_set(content_json, '$.forwarded_module_lockScreen_stackStates', json('{}'))
            WHERE module_id = 'module_project_foqn_s2_lock_screen'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-collection-missing-stable-id", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_remove(content_json, '$.messages[0].id')
            WHERE module_id = 'module_core_chat'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-scalar-wrong-type", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_set(content_json, '$.headerSubtitle', 42)
            WHERE module_id = 'module_core_chat'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-scalar-present-null", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_set(content_json, '$.headerSubtitle', json('null'))
            WHERE module_id = 'module_core_chat'
            """;
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("runtime-collection-field-wrong-type", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_set(content_json, '$.messages[0].text', json('false'))
            WHERE module_id = 'module_core_chat'
            """;
        command.ExecuteNonQuery();
    });
}

static void PreviewVisualContextBoundaryPreservesResolvedResources()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-preview-visual-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new PreviewVisualContextDataSource(database.PreviewInputs, database.Resources);
        var tree = database.LoadProjectTree();
        var project = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Project);
        var device = Descendants(tree).First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var snapshot = dataSource.LoadSnapshot(project.Id);

        Equal(project.Id, snapshot.ProjectId);
        SequenceEqual(
            database.GetDeviceOptions(project.Id).Select((option) => option.Value),
            snapshot.DeviceOptions.Select((option) => option.Value));
        SequenceEqual(
            database.GetThemeOptions(project.Id).Select((option) => option.Value),
            snapshot.ThemeOptions.Select((option) => option.Value));
        Equal(
            database.GetProjectSettings(project.Id).MediaRoot,
            snapshot.MediaRoot);
        Equal(
            database.GetDevicePreviewMetrics(device.Id),
            snapshot.DeviceMetrics(device.Id));
        Throws<InvalidOperationException>(
            () => snapshot.DeviceMetrics("missing_device"));

        True(typeof(EditorPreviewController)
            .GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            .All((constructor) =>
                constructor.GetParameters().Any(
                    (parameter) =>
                        parameter.ParameterType
                        == typeof(EditorOperationCoordinator))));
        var refreshOptions =
            typeof(EditorPreviewController).GetMethod(
                "RefreshOptionsAsync",
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Missing prepared Preview visual-context boundary.");
        True(typeof(Task).IsAssignableFrom(
            refreshOptions.ReturnType));

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
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-preview-session-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new ProductionPreviewSessionDataSource(
            database.PreviewInputs,
            database.Production,
            database.Resources,
            database.Resources);
        var timelineDataSource = new ModuleInstanceTimelineDataSource(
            database.Production,
            database.Resources);
        var tree = database.LoadProjectTree();
        var shot = Descendants(tree).Single((node) => node.Kind == ProjectTreeNodeKind.Shot);
        var screen = shot.Children.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var snapshot = dataSource.LoadSnapshot(tree);
        var preparedShot = snapshot.Shot(shot.Id);
        var preparedScreen = snapshot.Screen(screen.Id);
        var expectedContext =
            new ProductionShotContextService(
                new ProductionShotContextDataSource(
                    database.PreviewInputs,
                    database.Resources))
                .Resolve(shot.Id);

        Equal(
            database.GetModuleInstanceSettings(screen.Id).ShotId,
            preparedScreen.ShotId);
        Equal(
            database.GetShotSettings(shot.Id).Fps,
            preparedShot.FrameRate);
        Equal(
            expectedContext,
            preparedShot.Context);
        Equal(
            database.GetModuleInstanceVariantSettings(screen.Id).ConfigJson,
            preparedScreen.VariantConfigJson);
        SequenceEqual(
            database.GetShotModuleInstanceSlots(shot.Id).Select((slot) => slot.Id),
            preparedShot.Screens.Select(
                (prepared) => prepared.ScreenId));
        Equal(
            ModuleInstanceTimeline.ScreenStartFrame(
                timelineDataSource,
                screen.Id),
            preparedScreen.StartFrame);
        Equal(
            Math.Max(
                1,
                ModuleInstanceTimeline.DurationFrames(
                    timelineDataSource,
                    screen.Id)),
            preparedScreen.DurationFrames);
        SequenceEqual(
            ModuleInstanceTimeline.ShotKeyframeFrames(
                timelineDataSource,
                shot.Id),
            preparedShot.KeyframeFrames);
        Throws<InvalidOperationException>(
            () => snapshot.Shot("missing_shot"));
        Throws<InvalidOperationException>(
            () => snapshot.Screen("missing_screen"));
        True(typeof(EditorPreviewController)
            .GetField(
                "_timelineDataSource",
                BindingFlags.Instance
                | BindingFlags.NonPublic) is null);
        True(typeof(EditorPreviewController)
            .GetField(
                "_productionShotContext",
                BindingFlags.Instance
                | BindingFlags.NonPublic) is null);

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
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-instance-animation-store-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var timelineDataSource = new ModuleInstanceTimelineDataSource(
            database.Production,
            database.Resources);
        using var operations = new EditorOperationCoordinator();
        var store = new ModuleInstanceAnimationDocumentStore(
            database.Production,
            database.Production,
            database.Resources,
            timelineDataSource,
            operations);
        var screen = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var instance = database.GetModuleInstanceSettings(screen.Id);
        var variant = database.GetModuleInstanceVariantSettings(screen.Id);
        var snapshot = store.LoadSnapshot(screen.Id);
        var source = snapshot.Source;

        Equal(screen.Id, snapshot.ModuleInstanceId);
        Equal(variant.ConfigJson, source.VariantConfigJson);
        Equal(instance.AnimationJson, source.AnimationJson);
        Equal(database.GetModuleInstanceRuntimePreviewJson(screen.Id), source.RuntimePreviewJson);
        Equal(database.GetModuleInstanceThemeTokensJson(screen.Id), source.ThemeTokensJson);
        Equal(database.GetModuleInstanceEffectiveContractJson(screen.Id), source.EffectiveContractJson);
        Equal(
            ModuleInstanceTimeline.ScreenStartFrame(
                timelineDataSource,
                screen.Id),
            snapshot.ScreenStartFrame);
        Equal(
            Math.Max(
                1,
                ModuleInstanceTimeline.DurationFrames(
                    timelineDataSource,
                    screen.Id)),
            snapshot.DurationFrames);
        var currentAnimation = ModuleInstanceAnimationDocumentContract.Parse(
            source.AnimationJson,
            $"Module Instance '{screen.Id}' animation_json");
        foreach (var track in currentAnimation["tracks"]!.AsArray().OfType<JsonObject>())
        {
            var frames = track["keyframes"]!.AsArray()
                .OfType<JsonObject>()
                .Select((keyframe) => keyframe["frame"]!.GetValue<int>())
                .ToList();
            SequenceEqual(frames.OrderBy((frame) => frame), frames);
        }

        var afterReads = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, afterReads);

        var persisted = store.SaveAnimationSnapshotAsync(
            screen.Id,
            source.AnimationJson).GetAwaiter().GetResult();
        Equal(screen.Id, persisted.ModuleInstanceId);
        Equal(source.AnimationJson, persisted.Source.AnimationJson);
        Equal(snapshot.ScreenStartFrame, persisted.ScreenStartFrame);
        Equal(snapshot.DurationFrames, persisted.DurationFrames);
        Equal(source.AnimationJson, database.GetModuleInstanceSettings(screen.Id).AnimationJson);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void FailedAnimationCommandRestoresConfirmedDocument()
{
    const string confirmed =
        """{"schemaVersion":2,"tracks":[]}""";
    var coordinator =
        new ModuleInstanceAnimationCommandCoordinator(
            confirmed,
            (_) => Task.FromException<
                ModuleInstanceAnimationSnapshot>(
                new InvalidOperationException(
                    "persistence failed")));

    var result = coordinator.ExecuteAsync(
            (candidate) =>
            {
                candidate.SetTargetDurationFrames(
                    "target-a",
                    12);
                return true;
            })
        .GetAwaiter()
        .GetResult();

    True(!result.Succeeded);
    Equal(
        confirmed,
        result.ConfirmedAnimationJson);
    True(new ModuleInstanceAnimationDocument(
            result.ConfirmedAnimationJson)
        .TargetDurationFrames(
            "target-a") is null);
    True(result.Error is InvalidOperationException);
}

static void RapidAnimationCommandsUseLatestConfirmedDocument()
{
    const string confirmed =
        """{"schemaVersion":2,"tracks":[]}""";
    var firstStarted =
        new TaskCompletionSource(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
    var releaseFirst =
        new TaskCompletionSource(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
    var saved = new List<string>();
    var coordinator =
        new ModuleInstanceAnimationCommandCoordinator(
            confirmed,
            async (candidateJson) =>
            {
                saved.Add(candidateJson);
                if (saved.Count == 1)
                {
                    firstStarted.SetResult();
                    await releaseFirst.Task;
                }
                return new ModuleInstanceAnimationSnapshot(
                    "screen-a",
                    new ModuleInstanceAnimationSource(
                        "{}",
                        candidateJson,
                        "{}",
                        "{}",
                        "{}"),
                    0,
                    100);
            });

    var first = coordinator.ExecuteAsync(
        (candidate) =>
        {
            candidate.SetTargetDurationFrames(
                "target-a",
                12);
            return true;
        });
    True(firstStarted.Task.Wait(
        TimeSpan.FromSeconds(5)));
    var second = coordinator.ExecuteAsync(
        (candidate) =>
        {
            candidate.SetTargetDurationFrames(
                "target-b",
                24);
            return true;
        });
    Thread.Sleep(50);
    Equal(
        1,
        saved.Count);
    releaseFirst.SetResult();
    Task.WhenAll(first, second)
        .GetAwaiter()
        .GetResult();

    Equal(
        2,
        saved.Count);
    var firstDocument =
        new ModuleInstanceAnimationDocument(
            saved[0]);
    Equal(
        12,
        firstDocument.TargetDurationFrames(
            "target-a"));
    True(firstDocument.TargetDurationFrames(
        "target-b") is null);
    var secondDocument =
        new ModuleInstanceAnimationDocument(
            saved[1]);
    Equal(
        12,
        secondDocument.TargetDurationFrames(
            "target-a"));
    Equal(
        24,
        secondDocument.TargetDurationFrames(
            "target-b"));
}

static void ThemeRepositoryPreservesFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-theme-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
        Throws<InvalidOperationException>(() => database.GetThemeTokenOptions(project.Id, "missing_theme"));
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

static void ProductionFontRepositoryPreservesFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-font-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
        ProductionFontFilesContract.ParseRequired(
            record.FilesJson,
            $"Production Font '{record.Id}' files_json");
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
            Throws<InvalidOperationException>(() => repository.UpsertImported(
                connection,
                project.Id,
                "Incomplete Repository Font",
                "text",
                "fonts/incomplete-repository-font",
                "[{}]"));
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

static void ProductionFontFileDocumentsAreStrict()
{
    var valid = ProductionFontFilesContract.ParseRequired(
        """
        [{"fileName":"Family-Regular.ttf","relativePath":"fonts/family/Family-Regular.ttf","style":"normal","weight":400}]
        """,
        "test Production Font files");
    Equal(1, valid.Count);
    Equal("Family-Regular.ttf", valid[0].FileName);
    Equal("fonts/family/Family-Regular.ttf", valid[0].RelativePath);
    Equal("normal", valid[0].Style);
    Equal(400, valid[0].Weight);
    Equal(0, ProductionFontFilesContract.ParseRequired("[]", "empty Production Font files").Count);

    Throws<InvalidOperationException>(() => ProductionFontFilesContract.ParseRequired("[null]", "test files"));
    Throws<InvalidOperationException>(() => ProductionFontFilesContract.ParseRequired("[{}]", "test files"));
    Throws<InvalidOperationException>(() => ProductionFontFilesContract.ParseRequired(
        "[{\"fileName\":\"A.ttf\",\"relativePath\":\"fonts/A.ttf\",\"style\":\"normal\",\"weight\":\"400\"}]",
        "test files"));
    Throws<InvalidOperationException>(() => ProductionFontFilesContract.ParseRequired(
        "[{\"fileName\":\"A.ttf\",\"relativePath\":\"fonts/A.ttf\",\"style\":\"oblique\",\"weight\":400}]",
        "test files"));
    Throws<InvalidOperationException>(() => ProductionFontFilesContract.ParseRequired(
        "[{\"fileName\":\"A.ttf\",\"relativePath\":\"../A.ttf\",\"style\":\"normal\",\"weight\":400}]",
        "test files"));
    Throws<InvalidOperationException>(() => ProductionFontFilesContract.ParseRequired(
        "[{\"fileName\":\"B.ttf\",\"relativePath\":\"fonts/A.ttf\",\"style\":\"normal\",\"weight\":400}]",
        "test files"));
    Throws<InvalidOperationException>(() => ProductionFontFilesContract.ParseRequired(
        "[{\"fileName\":\"A.ttf\",\"relativePath\":\"fonts/A.ttf\",\"style\":\"normal\",\"weight\":400},{\"fileName\":\"A.ttf\",\"relativePath\":\"fonts/A.ttf\",\"style\":\"normal\",\"weight\":400}]",
        "test files"));

    AssertRejectedDatabaseIsReadOnly("production-font-non-object-file", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE production_fonts SET files_json = '[null]' WHERE id = (SELECT id FROM production_fonts ORDER BY id LIMIT 1)";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("production-font-string-weight", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE production_fonts SET files_json = json_set(files_json, '$[0].weight', '400') WHERE id = (SELECT id FROM production_fonts ORDER BY id LIMIT 1)";
        command.ExecuteNonQuery();
    });
    AssertRejectedDatabaseIsReadOnly("production-font-unknown-style", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE production_fonts SET files_json = json_set(files_json, '$[0].style', 'oblique') WHERE id = (SELECT id FROM production_fonts ORDER BY id LIMIT 1)";
        command.ExecuteNonQuery();
    });
}

static void IconThemeRepositoryPreservesFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-icon-theme-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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

static void AppModuleRepositoryPreservesFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-app-module-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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

static void ComponentClassRepositoryPreservesFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-component-class-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
        var variants = VariantEnvelopeContract.RequiredArray(metadata, "variants", $"Component class '{original.Id}'");
        var defaultVariant = variants.OfType<JsonObject>()
            .Single((variant) => JsonPath.String(variant, "id", "") == "default");
        defaultVariant["config"] = config.DeepClone();
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

static void ModuleInstanceRepositoryPreservesFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-instance-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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

static void ShotRepositoryPreservesFocusedContract()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-shot-repository-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var context = new SqliteProjectContext(temporary);
        IShotRepository repository = new ShotRepository(context);
        IProjectEpisodeRepository episodeRepository = new ProjectEpisodeRepository(context, repository);
        var tree = database.LoadProjectTree();
        var node = Descendants(tree).Single((candidate) => candidate.Id == "shot_001");
        var original = repository.Get(node.Id);
        var settings = database.GetShotSettings(node.Id);

        Equal(original.ProjectId, settings.ProjectId);
        Equal(original.EpisodeId, settings.EpisodeId);
        Equal(original.Slug, settings.Slug);
        Equal(original.ShotNumber, settings.ShotNumber);
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

        using (var connection = context.OpenConnection())
        {
            var duplicate = repository.Duplicate(
                connection,
                original.Id,
                $"shot_repository_{Guid.NewGuid():N}",
                $"{original.Name} repository copy",
                original.OwnerActorId,
                repository.SuggestShotNumber(
                    connection,
                    original.EpisodeId),
                "SH9999");
            Equal(original.OwnerActorId, duplicate.OwnerActorId);
            True(original.ShotNumber != duplicate.ShotNumber);
            Equal(original.CanvasJson, duplicate.CanvasJson);
            Equal(original.MetadataJson, duplicate.MetadataJson);
            repository.Delete(connection, duplicate.Id);

            var duplicatedEpisode = episodeRepository.DuplicateEpisode(
                connection,
                original.EpisodeId,
                "Repository Episode");
            var episodeShot = repository.QueryByEpisode(connection, duplicatedEpisode.Id).Single();
            Equal(original.OwnerActorId, episodeShot.OwnerActorId);
            Equal(original.CanvasJson, episodeShot.CanvasJson);
            Equal(original.MetadataJson, episodeShot.MetadataJson);
            episodeRepository.DeleteEpisode(connection, duplicatedEpisode.Id);
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

static void RenderOutputNamingReservesOneBatchVersion()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-render-output-plan-{Guid.NewGuid():N}");
    var output = Path.Combine(root, "shots", "output");
    Directory.CreateDirectory(output);
    try
    {
        File.WriteAllText(
            Path.Combine(output, "SHOT_010_LIGHT_v001.mov"),
            "occupied");
        var plan = RenderOutputPlanner.Suggest(
            root,
            "shots/output",
            "SHOT_010",
            [
                RenderQueueAppearance.Light,
                RenderQueueAppearance.Dark,
            ],
            RenderOutputModes.Require(
                RenderOutputModes.MovProRes422Hq),
            3);
        Equal(2, plan.Version);
        Equal(
            "SHOT_010_LIGHT_v002.mov",
            Path.GetFileName(
                plan.OutputPaths[RenderQueueAppearance.Light]));
        Equal(
            "SHOT_010_DARK_v002.mov",
            Path.GetFileName(
                plan.OutputPaths[RenderQueueAppearance.Dark]));
        Equal(
            "SHOT_010_GFX",
            RenderOutputPlanner.SuggestedBaseName(
                "Shot 010 · GFX"));
        Throws<InvalidOperationException>(() =>
            RenderOutputPlanner.RequireBaseName("../SHOT"));
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void MovH264ModesMatchCreditosProfiles()
{
    var light = RenderOutputModes.Require(
        RenderOutputModes.MovH264Light);
    var standard = RenderOutputModes.Require(
        RenderOutputModes.MovH264Standard);
    var high = RenderOutputModes.Require(
        RenderOutputModes.MovH264High);
    True(!light.PreservesAlpha);
    True(!standard.PreservesAlpha);
    True(!high.PreservesAlpha);
    Equal("mov", light.Kind);
    Equal("mov", standard.Kind);
    Equal("mov", high.Kind);
    Equal("mov", light.Extension);
    Equal("mov", standard.Extension);
    Equal("mov", high.Extension);

    SequenceEqual(
        new[]
        {
            "-c:v", "libx264",
            "-preset", "medium",
            "-b:v", "8M",
            "-maxrate", "10M",
            "-bufsize", "16M",
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
        },
        RenderMovEncodingProfiles.Arguments(
            light.EncodingProfile));
    SequenceEqual(
        new[]
        {
            "-c:v", "libx264",
            "-preset", "medium",
            "-b:v", "20M",
            "-maxrate", "25M",
            "-bufsize", "40M",
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
        },
        RenderMovEncodingProfiles.Arguments(
            standard.EncodingProfile));
    SequenceEqual(
        new[]
        {
            "-c:v", "libx264",
            "-preset", "slow",
            "-b:v", "40M",
            "-maxrate", "50M",
            "-bufsize", "80M",
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
        },
        RenderMovEncodingProfiles.Arguments(
            high.EncodingProfile));
}

static void ProductionRenderOverridesRespectScreenAppearance()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Path.GetTempPath(),
        $"mockups-render-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var tree = database.LoadProjectTree();
        var shot = Descendants(tree)
            .Single((node) => node.Kind == ProjectTreeNodeKind.Shot);
        var firstScreen = shot.Children
            .Where((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance)
            .OrderBy((node) =>
                database.GetModuleInstanceSettings(node.Id).SortOrder)
            .First();
        var instance = database.GetModuleInstanceSettings(firstScreen.Id);
        var writeContext = new SqliteProjectContext(temporary);
        using (var connection = writeContext.OpenConnection())
        {
            writeContext.Execute(
                connection,
                """
                UPDATE modules
                SET metadata_json = json_set(
                  metadata_json,
                  '$.variants[0].config.appearanceMode',
                  'light')
                WHERE id = $moduleId
                """,
                ("$moduleId", instance.ModuleId));
        }
        var shotSettings = database.GetShotSettings(shot.Id);
        var actor = database.GetActorSettings(
            shotSettings.OwnerActorId);
        var deviceId = database.GetDeviceOptions(
                shotSettings.ProjectId)
            .Select((option) => option.Value)
            .FirstOrDefault((id) =>
                !id.Equals(
                    actor.DefaultDeviceId,
                    StringComparison.Ordinal))
            ?? actor.DefaultDeviceId;
        var themeId = database.GetThemeOptions(
                shotSettings.ProjectId)
            .Select((option) => option.Value)
            .FirstOrDefault((id) =>
                !id.Equals(
                    actor.DefaultThemeId,
                    StringComparison.Ordinal))
            ?? actor.DefaultThemeId;
        var payload = DesignPreviewPayloadFactory.CreateProductionRender(
            new DesignPreviewPayloadDataSource(
                database.PreviewInputs,
                database.Production,
                database.Resources,
                database.Resources,
                database.ProjectPaths),
            shot,
            themeId,
            deviceId,
            RenderQueueAppearance.Dark,
            0);
        Equal(deviceId, payload.DeviceId);
        Equal(
            database.GetThemeSettings(themeId).TokensJson,
            payload.ThemeTokensJson);
        Equal(
            RenderQueueAppearance.Light,
            payload.ThemeMode);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void AnimatedConversationComposerRemainsVisible()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $"mockups-conversation-composer-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree()
            .SelectMany(DescendantsAndSelf)
            .ToList();
        var conversation = nodes.Single((node) =>
            node.Kind == ProjectTreeNodeKind.ModuleInstance
            && node.Id
                == "module_instance_900f1616432d4f63a97f2a74dd647e08");
        var theme = nodes.First((node) =>
            node.Kind == ProjectTreeNodeKind.Theme);
        var start = ModuleInstanceTimeline.ScreenStartFrame(
            new ModuleInstanceTimelineDataSource(
                database.Production,
                database.Resources),
            conversation.Id);
        var payload = Required(CreatePreviewPayload(
            database,
            conversation,
            theme.Id,
            timelineFrame: start + 1));
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(payload.DeviceId),
            false,
            payload).GetAwaiter().GetResult();
        True(!html.Contains(
            "preview-error",
            StringComparison.Ordinal));
        True(html.Contains(
            "data-renderable-id=\"component.keyboard\"",
            StringComparison.Ordinal));
        True(html.Contains(
            "data-renderable-id=\"component.textInputBar\"",
            StringComparison.Ordinal));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ConversationPlayMessagesAdvancesRootOwnerFrame()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $"mockups-conversation-playback-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree()
            .SelectMany(DescendantsAndSelf)
            .ToList();
        var conversation = nodes.Single((node) =>
            node.Kind == ProjectTreeNodeKind.ModuleVariant
            && node.Id == "module_core_chat::variant::default");
        var theme = nodes.First((node) =>
            node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) =>
            node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(CreatePreviewPayload(
            database,
            conversation,
            theme.Id,
            timelineFrame: 0));
        var preview = JsonPath.ParseRequiredObject(
            payload.DesignPreviewJson,
            "Conversation Design Preview");
        var action = ComponentPreviewActions.Read(preview)
            .Single((candidate) => candidate.Id == "playConversation");
        True(action.DefinesModuleDuration);
        var module = database.GetModuleSettings("module_core_chat");
        var inputSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
        inputSession.UpdateForPayload(payload, module.ProjectId);
        var interactivePayload = inputSession.ApplyInputs(
            payload,
            "light",
            module.ProjectId);

        var frames = EditorPreviewController.PlaybackFramePayloads(
                payload,
                payload.FrameRate,
                action)
            .ToList();
        True(frames.Count > 40);
        Equal(frames.Count - 1, interactivePayload.LocalFrame);
        True(inputSession.CanStepActionFrame(action.Id, -1));
        True(!inputSession.CanStepActionFrame(action.Id, 1));
        True(inputSession.StepActionFrame(action.Id, -1));
        Equal(frames.Count - 2, inputSession.CurrentPreviewFrame);
        var previousFramePayload = inputSession.ApplyInputs(
            payload,
            "light",
            module.ProjectId);
        Equal(frames.Count - 2, previousFramePayload.LocalFrame);
        Equal(true, JsonPath.RequiredBoolean(
            JsonPath.ParseRequiredObject(
                previousFramePayload.DesignPreviewJson,
                "Conversation previous frame"),
            "conversationPlayback",
            "Conversation previous frame"));
        True(inputSession.StepActionFrame(action.Id, 1));
        Equal(frames.Count - 1, inputSession.CurrentPreviewFrame);
        True(!inputSession.StepActionFrame(action.Id, 1));
        True(inputSession.RestoreAction(action.Id));
        var restoredPayload = inputSession.ApplyInputs(
            payload,
            "light",
            module.ProjectId);
        Equal(frames.Count - 1, restoredPayload.LocalFrame);
        Equal(false, JsonPath.RequiredBoolean(
            JsonPath.ParseRequiredObject(
                restoredPayload.DesignPreviewJson,
                "Conversation restored frame"),
            "conversationPlayback",
            "Conversation restored frame"));
        Equal(0, frames[0].LocalFrame);
        Equal(40, frames[40].LocalFrame);
        Equal(40, JsonPath.RequiredInteger(
            JsonPath.ParseRequiredObject(
                frames[40].DesignPreviewJson,
                "Conversation playback frame"),
            "conversationFrame",
            "Conversation playback frame"));

        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            false,
            frames[40]).GetAwaiter().GetResult();
        True(!html.Contains(
            "preview-error",
            StringComparison.Ordinal));
        True(html.Contains(
            "Tenias razon",
            StringComparison.Ordinal));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void RenderQueueChildrenAreIndependent()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-render-queue-{Guid.NewGuid():N}");
    var output = Path.Combine(root, "output");
    var queuePath = Path.Combine(root, "state", "queue.json");
    Directory.CreateDirectory(output);
    try
    {
        var mode = RenderOutputModes.Require(
            RenderOutputModes.MovProRes422Hq);
        var outputPlan = RenderOutputPlanner.Suggest(
            root,
            "output",
            "SHOT_020",
            [
                RenderQueueAppearance.Light,
                RenderQueueAppearance.Dark,
            ],
            mode,
            3);
        var context = new RenderShotContext(
            "project",
            "shot",
            "Shot 020",
            "actor",
            "Actor");
        var metrics = new DevicePreviewMetrics(
            "Device",
            100,
            200,
            0,
            0,
            100,
            200,
            0,
            0,
            0,
            0,
            0,
            1);
        RenderOutputTarget Output(string appearance) =>
            new(
                "production",
                "output",
                root,
                "output",
                "SHOT_020",
                appearance,
                outputPlan.Version,
                3,
                mode.Id,
                outputPlan.OutputPaths[appearance]);
        RenderJobSummary Summary(string appearance) =>
            new(
                context,
                "Device",
                "Theme",
                appearance,
                1,
                Output(appearance));
        RenderJobSnapshot Snapshot(
            string batchRoot,
            string appearance)
        {
            var frameStore = StoreRenderFrames(
                batchRoot,
                appearance,
                ["<html>frame</html>"]);
            return
            new(
                RenderJobSnapshot.CurrentSchema,
                RenderJobSnapshot.CurrentVersion,
                context,
                "device",
                "Device",
                "theme",
                "Theme",
                appearance,
                metrics,
                25,
                frameStore,
                Output(appearance));
        }

        using (var queue = new RenderQueueManager(
            queuePath,
            new AppearanceFailingRenderExecutor(
                RenderQueueAppearance.Dark)))
        {
            using var releasePreparation =
                new ManualResetEventSlim();
            using var preparationStarted =
                new ManualResetEventSlim();
            var children = queue.EnqueuePreparingBatch(
                [
                    Summary(RenderQueueAppearance.Light),
                    Summary(RenderQueueAppearance.Dark),
                ],
                async (batchRoot, _, cancellationToken) =>
                {
                    preparationStarted.Set();
                    await Task.Run(
                        () => releasePreparation.Wait(
                            cancellationToken),
                        cancellationToken);
                    return
                    [
                        Snapshot(
                            batchRoot,
                            RenderQueueAppearance.Light),
                        Snapshot(
                            batchRoot,
                            RenderQueueAppearance.Dark),
                    ];
                });
            Equal(2, children.Count);
            Equal(children[0].BatchId, children[1].BatchId);
            True(preparationStarted.Wait(
                TimeSpan.FromSeconds(2)));
            True(queue.Jobs().All((job) =>
                job.Status == RenderQueueStatus.Preparing
                && !job.SnapshotAvailable));
            releasePreparation.Set();
            True(SpinWait.SpinUntil(
                () => queue.Jobs().All((job) =>
                    RenderQueueStatus.IsTerminal(job.Status)),
                TimeSpan.FromSeconds(5)));
            Equal(
                RenderQueueStatus.Completed,
                queue.Jobs().Single((job) =>
                    job.Summary.Appearance
                        == RenderQueueAppearance.Light).Status);
            Equal(
                RenderQueueStatus.Failed,
                queue.Jobs().Single((job) =>
                    job.Summary.Appearance
                        == RenderQueueAppearance.Dark).Status);
        }

        using var reopened = new RenderQueueManager(
            queuePath,
            new AppearanceFailingRenderExecutor(""));
        Equal(2, reopened.Jobs().Count);
        True(reopened.Jobs().Single((job) =>
            job.Summary.Appearance == RenderQueueAppearance.Light)
            .SnapshotAvailable == false);
        True(reopened.Jobs().Single((job) =>
            job.Summary.Appearance == RenderQueueAppearance.Dark)
            .SnapshotAvailable);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void RenderSnapshotStoreInternsAssets()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-render-assets-{Guid.NewGuid():N}");
    try
    {
        var dataUri =
            "data:font/woff2;base64,AAECAwQFBgcICQ==";
        var compact = PreviewAssetRegistry.Compact(
            $"<style>@font-face{{src:url(\"{dataUri}\")}}</style>");
        var key = PreviewAssetRegistry.Keys(compact).Single();
        True(!compact.Contains(
            dataUri,
            StringComparison.Ordinal));
        True(PreviewAssetRegistry.TryResolve(
            key,
            out var resolved));
        Equal(dataUri, resolved);

        var store = new RenderSnapshotStore(
            root,
            create: true);
        store.WriteAsset(key, resolved);
        store.WriteAsset(key, resolved);
        Equal(
            1,
            Directory.GetFiles(
                Path.Combine(root, "assets"),
                "*.uri").Length);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

static void RenderQueueProgressControlIsStable()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-render-progress-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        using var session = HeadlessUnitTestSession.StartNew(
            typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var queuePath = Path.Combine(root, "queue.json");
            using var queue = new RenderQueueManager(
                queuePath,
                new AppearanceFailingRenderExecutor(""));
            var context = new RenderShotContext(
                "project",
                "shot",
                "Shot",
                "actor",
                "Actor");
            var mode = RenderOutputModes.Require(
                RenderOutputModes.PngSequence);
            var outputPlan = RenderOutputPlanner.Suggest(
                root,
                "output",
                "SHOT",
                [
                    RenderQueueAppearance.Light,
                    RenderQueueAppearance.Dark,
                ],
                mode,
                3);
            var summary = new RenderJobSummary(
                context,
                "Device",
                "Theme",
                RenderQueueAppearance.Light,
                10,
                new RenderOutputTarget(
                    "production",
                    "output",
                    root,
                    "output",
                    "SHOT",
                    RenderQueueAppearance.Light,
                    outputPlan.Version,
                    3,
                    mode.Id,
                    outputPlan.OutputPaths[
                        RenderQueueAppearance.Light]));
            var darkSummary = summary with
            {
                Appearance = RenderQueueAppearance.Dark,
                Output = summary.Output with
                {
                    Appearance = RenderQueueAppearance.Dark,
                    OutputPath = outputPlan.OutputPaths[
                        RenderQueueAppearance.Dark],
                },
            };
            var monitor = new RenderQueueMonitorControl(
                new Window(),
                queue);
            var window = new Window
            {
                Width = 900,
                Height = 500,
                Content = monitor,
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var children = queue.EnqueuePreparingBatch(
                [summary, darkSummary],
                async (_, _, cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);
                    return [];
                });
            var child = children.Single((candidate) =>
                candidate.Summary.Appearance
                    == RenderQueueAppearance.Light);
            var survivor = children.Single((candidate) =>
                candidate.Summary.Appearance
                    == RenderQueueAppearance.Dark);
            Dispatcher.UIThread.RunJobs();
            var first = monitor.GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{child.Id}");
            True(first.IsIndeterminate);

            var update = typeof(RenderQueueManager).GetMethod(
                "UpdatePreparationProgress",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "Missing Render Queue preparation progress boundary.");
            update.Invoke(
                queue,
                [
                    child.BatchId,
                    new RenderSnapshotFreezeProgress(
                        4,
                        10,
                        RenderQueueAppearance.Light),
                ]);
            Dispatcher.UIThread.RunJobs();
            var second = monitor.GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{child.Id}");
            True(ReferenceEquals(first, second));
            True(second.IsIndeterminate);
            var status = monitor.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single((text) =>
                    text.Name
                        == $"RenderQueueStatus_{child.Id}");
            True(!status.Text!.Contains(
                    "frames",
                    StringComparison.Ordinal));

            var updateExecution = typeof(RenderQueueManager).GetMethod(
                "UpdateProgress",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "Missing Render Queue execution progress boundary.");
            updateExecution.Invoke(
                queue,
                [
                    child.Id,
                    new RenderQueueExecutionProgress(
                        4,
                        10,
                        "Rendering 4 / 10",
                        RenderQueueStatus.Rendering),
                ]);
            updateExecution.Invoke(
                queue,
                [
                    child.Id,
                    new RenderQueueExecutionProgress(
                        3,
                        10,
                        "Rendering 3 / 10",
                        RenderQueueStatus.Rendering),
                ]);
            Dispatcher.UIThread.RunJobs();
            var rendering = monitor.GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{child.Id}");
            True(ReferenceEquals(first, rendering));
            True(!rendering.IsIndeterminate);
            Equal(4d, rendering.Value);

            var survivingProgress = monitor.GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{survivor.Id}");
            var finish = typeof(RenderQueueManager).GetMethod(
                "Finish",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "Missing Render Queue terminal-state boundary.");
            finish.Invoke(
                queue,
                [
                    child.Id,
                    RenderQueueStatus.Completed,
                    null,
                ]);
            Dispatcher.UIThread.RunJobs();
            monitor.GetVisualDescendants()
                .OfType<Button>()
                .Single((button) =>
                    string.Equals(
                        button.Content as string,
                        "Remove",
                        StringComparison.Ordinal))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Equal(1, queue.Jobs().Count);
            True(queue.Jobs().Single().Id.Equals(
                survivor.Id,
                StringComparison.Ordinal));
            True(!monitor.GetVisualDescendants()
                .OfType<ProgressBar>()
                .Any((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{child.Id}"));
            var survivingProgressAfterRemoval = monitor
                .GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{survivor.Id}");
            True(ReferenceEquals(
                survivingProgress,
                survivingProgressAfterRemoval));

            var later = queue.EnqueuePreparingBatch(
                [summary],
                async (_, _, cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);
                    return [];
                }).Single();
            Dispatcher.UIThread.RunJobs();
            var laterProgress = monitor.GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{later.Id}");
            finish.Invoke(
                queue,
                [
                    survivor.Id,
                    RenderQueueStatus.Completed,
                    null,
                ]);
            Dispatcher.UIThread.RunJobs();
            monitor.GetVisualDescendants()
                .OfType<Button>()
                .Single((button) =>
                    string.Equals(
                        button.Content as string,
                        "Clear finished",
                        StringComparison.Ordinal))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Equal(1, queue.Jobs().Count);
            True(queue.Jobs().Single().Id.Equals(
                later.Id,
                StringComparison.Ordinal));
            var laterProgressAfterClear = monitor
                .GetVisualDescendants()
                .OfType<ProgressBar>()
                .Single((progress) =>
                    progress.Name
                        == $"RenderQueueProgress_{later.Id}");
            True(ReferenceEquals(
                laterProgress,
                laterProgressAfterClear));

            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static RenderFrameStoreReference StoreRenderFrames(
    string batchRoot,
    string appearance,
    IReadOnlyList<string> frames)
{
    var store = new RenderSnapshotStore(
        batchRoot,
        create: true);
    using var manifest = store.CreateManifest(appearance);
    for (var index = 0; index < frames.Count; index++)
    {
        manifest.Write(
            index,
            store.WriteDocument(frames[index]));
    }
    manifest.Commit();
    return new RenderFrameStoreReference(
        Path.GetFullPath(batchRoot),
        $"{appearance}.frames",
        appearance,
        frames.Count);
}

static void RenderExecutorPublishesCleanPngSequence()
{
    var root = Path.Combine(
        Path.GetTempPath(),
        $"mockups-render-executor-{Guid.NewGuid():N}");
    var output = Path.Combine(root, "output");
    Directory.CreateDirectory(root);
    try
    {
        var mode = RenderOutputModes.Require(
            RenderOutputModes.PngSequence);
        var outputPlan = RenderOutputPlanner.Suggest(
            root,
            "output",
            "SHOT_030",
            [RenderQueueAppearance.Light],
            mode,
            3);
        var snapshot = new RenderJobSnapshot(
            RenderJobSnapshot.CurrentSchema,
            RenderJobSnapshot.CurrentVersion,
            new RenderShotContext(
                "project",
                "shot",
                "Shot 030",
                "actor",
                "Actor"),
            "device",
            "Device",
            "theme",
            "Theme",
            RenderQueueAppearance.Light,
            new DevicePreviewMetrics(
                "Device",
                64,
                64,
                0,
                0,
                64,
                64,
                0,
                0,
                0,
                0,
                0,
                1),
            25,
            StoreRenderFrames(
                Path.Combine(
                    root,
                    Guid.NewGuid().ToString()),
                RenderQueueAppearance.Light,
                [
                    """
                    <!doctype html>
                    <html>
                      <body style="margin:0">
                        <div
                          data-renderable-id="design_preview.surface"
                          style="width:64px;height:64px;background:#ff3366">
                        </div>
                      </body>
                    </html>
                    """,
                    """
                    <!doctype html>
                    <html>
                      <body style="margin:0">
                        <div
                          data-renderable-id="design_preview.surface"
                          style="width:64px;height:64px;background:#ff3366">
                        </div>
                      </body>
                    </html>
                    """,
                ]),
            new RenderOutputTarget(
                "production",
                "output",
                root,
                "output",
                "SHOT_030",
                RenderQueueAppearance.Light,
                outputPlan.Version,
                3,
                mode.Id,
                outputPlan.OutputPaths[RenderQueueAppearance.Light]));
        True(!Directory.Exists(output));
        using var executor = new RenderJobExecutor();
        executor.ExecuteAsync(
                snapshot,
                new Progress<RenderQueueExecutionProgress>(),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        True(Directory.Exists(output));
        True(Directory.Exists(snapshot.Output.OutputPath));
        var frames = Directory.GetFiles(
            snapshot.Output.OutputPath,
            "*.png");
        Equal(2, frames.Length);
        True(new FileInfo(frames[0]).Length > 0);
        Equal(
            Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(frames[0]))),
            Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(frames[1]))));
        Equal(
            1,
            Directory.GetFiles(
                Path.Combine(
                    snapshot.FrameStore.BatchRootPath,
                    "documents"),
                "*.html").Length);
        True(Directory.GetDirectories(
                output,
                "*.mockups-*",
                SearchOption.TopDirectoryOnly)
            .Length == 0);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void ShotActorContextIsExplicit()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-shot-actor-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var moduleInstances = ModuleInstances(database);
        var tree = database.LoadProjectTree();
        var episode = Descendants(tree)
            .First((node) => node.Kind == ProjectTreeNodeKind.Episode && node.Id == "episode_002");
        Throws<InvalidOperationException>(() => database.AddChild(episode));
        Throws<InvalidOperationException>(() =>
            database.AddShot(episode, "", 1));

        var shot = database.AddShot(
            episode,
            "actor_alex",
            database.SuggestShotNumber(episode.Id));
        Equal("actor_alex", database.GetShotSettings(shot.Id).OwnerActorId);
        var module = moduleInstances
            .GetAvailableShotModules(shot.Id)
            .First();
        var variant = moduleInstances
            .GetModuleVariantOptions(module.Id)
            .First();
        var screen = moduleInstances.AddModuleInstance(
            shot,
            new ShotModuleInstanceDraft(
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

        var duplicate = moduleInstances.Duplicate(screen);
        moduleInstances.MoveModuleInstance(duplicate.Id, -1);
        moduleInstances.Delete(duplicate);
        moduleInstances.Delete(screen);
        NodeCommands(database).Delete(shot);
        True(Descendants(database.LoadProjectTree()).All((node) =>
            node.Id != shot.Id
            && node.Id != screen.Id
            && node.Id != duplicate.Id));
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ProductionOutputGeneratesExactShotPlans()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(
        Path.GetTempPath(),
        $"mockups-production-output-{Guid.NewGuid():N}.sqlite");
    var rootsPath = Path.Combine(
        Path.GetTempPath(),
        $"mockups-production-output-roots-{Guid.NewGuid():N}.json");
    var outputRoot = Path.Combine(
        Path.GetTempPath(),
        $"mockups-production-output-files-{Guid.NewGuid():N}");
    File.Copy(source, temporary, overwrite: true);
    Directory.CreateDirectory(outputRoot);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var plan = database.GetProductionOutputShotPlan("shot_001");
        Equal("project_foqn_s2", plan.ProjectId);
        Equal("SH0001", plan.ShotCode);
        Equal("FOQN_S02_EP_01_SH0001", plan.TechnicalName);
        Equal("S02/EP_01/FOQN_S02_EP_01_SH0001/comp", plan.RelativeDirectory);
        Equal("FOQN_S02_EP_01_SH0001_LIGHT_v001",
            RenderOutputPlanner.FileStem(
                plan.TechnicalName,
                RenderQueueAppearance.Light,
                1,
                plan.VersionPadding));

        var roots = new ProductionOutputRootStore(rootsPath);
        roots.Set(plan.ProjectId, outputRoot);
        Equal(Path.GetFullPath(outputRoot), roots.Get(plan.ProjectId));

        var project = database.LoadProjectTree().Single();
        var episode = DescendantsAndSelf(project).Single((node) =>
            node.Id == "episode_001");
        var created = database.AddShot(
            episode,
            "actor_alex",
            12);
        Equal("SH0012", database.GetShotSettings(created.Id).Slug);
        Equal(
            "FOQN_S02_EP_01_SH0012",
            database.GetProductionOutputShotPlan(created.Id).TechnicalName);
        database.UpdateProjectField(
            plan.ProjectId,
            "project.shotPrefix",
            "PL");
        Equal("PL0001", database.GetShotSettings("shot_001").Slug);
        Equal("PL0012", database.GetShotSettings(created.Id).Slug);
        Equal(
            "FOQN_S02_EP_01_PL0012",
            database.GetProductionOutputShotPlan(created.Id).TechnicalName);
        database.UpdateProjectField(
            plan.ProjectId,
            "project.shotPrefix",
            "SH");

        var draft = new RenderJobSnapshotFactory(
                RenderSnapshots(database),
                database.ProjectPaths,
                roots)
            .LoadDraftAsync(
                DescendantsAndSelf(
                        database.LoadProjectTree().Single())
                    .Single((node) => node.Id == "shot_001"))
            .GetAwaiter()
            .GetResult();
        Equal(plan.TechnicalName, draft.SuggestedBaseName);
        Equal(outputRoot, draft.RootPath);
        Equal(1, draft.Routes.Count);
        Equal(plan.RelativeDirectory, draft.Routes[0].RelativeDirectory);
        Equal("", draft.RouteStatusMessage);
    }
    finally
    {
        File.Delete(temporary);
        File.Delete(rootsPath);
        Directory.Delete(outputRoot, recursive: true);
    }
}

static void ProductionShotContextBoundaryPreservesInheritedContext()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-context-data-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new ProductionShotContextDataSource(database.PreviewInputs, database.Resources);
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

static void PreviewPayloadRejectsIncompleteProductionContext()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-preview-production-context-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new DesignPreviewPayloadDataSource(
            database.PreviewInputs,
            database.Production,
            database.Resources,
            database.Resources,
            database.ProjectPaths);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var screen = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var component = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
        var shotId = database.GetModuleInstanceSettings(screen.Id).ShotId;
        var shot = database.GetShotSettings(shotId);
        var actorId = shot.OwnerActorId;
        var actor = database.GetActorSettings(actorId);

        Equal(actor.DefaultThemeId, dataSource.ResolveThemeId(component, actor.DefaultThemeId));
        Equal(actor.DefaultThemeId, dataSource.ResolveThemeId(screen, actor.DefaultThemeId));
        True(dataSource.LoadThemeContext(screen, actor.DefaultThemeId) is not null);

        UpdateProductionContext("UPDATE shots SET owner_actor_id = '' WHERE id = $id", shotId);
        Throws<InvalidOperationException>(() => dataSource.ResolveThemeId(screen, actor.DefaultThemeId));

        UpdateProductionContext("UPDATE shots SET owner_actor_id = $value WHERE id = $id", shotId, actorId);
        UpdateProductionContext("UPDATE actors SET default_theme_id = '' WHERE id = $id", actorId);
        Throws<InvalidOperationException>(() => dataSource.ResolveThemeId(screen, actor.DefaultThemeId));

        UpdateProductionContext("UPDATE actors SET default_theme_id = $value WHERE id = $id", actorId, "missing_theme");
        Throws<InvalidOperationException>(() => dataSource.ResolveThemeId(screen, actor.DefaultThemeId));

        UpdateProductionContext("UPDATE actors SET default_theme_id = $value WHERE id = $id", actorId, actor.DefaultThemeId);
        UpdateProductionContext("UPDATE actors SET default_device_id = '' WHERE id = $id", actorId);
        Throws<InvalidOperationException>(() => dataSource.LoadThemeContext(screen, actor.DefaultThemeId));

        UpdateProductionContext("UPDATE actors SET default_device_id = $value WHERE id = $id", actorId, "missing_device");
        Throws<InvalidOperationException>(() => dataSource.LoadThemeContext(screen, actor.DefaultThemeId));

        void UpdateProductionContext(string sql, string id, string? value = null)
        {
            using var connection = new SqliteConnection($"Data Source={temporary}");
            connection.Open();
            using (var foreignKeys = connection.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_keys = OFF";
                foreignKeys.ExecuteNonQuery();
            }
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id);
            if (value is not null) command.Parameters.AddWithValue("$value", value);
            Equal(1, command.ExecuteNonQuery());
        }
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ProductionPayloadPreservesActorAndAnimation()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-production-payload-owner-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var before = SHA256.HashData(File.ReadAllBytes(temporary));
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new DesignPreviewPayloadDataSource(
            database.PreviewInputs,
            database.Production,
            database.Resources,
            database.Resources,
            database.ProjectPaths);
        var preparer =
            new ProductionPreviewPayloadPreparer(
                dataSource,
                new ProductionPreviewRuntimeResolver(
                    database.Resources,
                    database.ProjectPaths));
        var screens = Descendants(database.LoadProjectTree())
            .Where((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance)
            .ToList();

        foreach (var screen in screens)
        {
            var instance = database.GetModuleInstanceSettings(screen.Id);
            var shot = database.GetShotSettings(instance.ShotId);
            var runtime = DesignPreviewTestValues.Parse(database.GetModuleInstanceRuntimePreviewJson(screen.Id));
            var runtimeActorId = runtime["actorId"]?.GetValue<string>();
            var expectedActorId = string.IsNullOrWhiteSpace(runtimeActorId)
                ? shot.OwnerActorId
                : runtimeActorId;
            var payload = Required(DesignPreviewPayloadFactory.Create(dataSource, screen, null));
            var prepared =
                preparer.PrepareRequired(
                    screen,
                    null,
                    "light",
                    0);
            Equal(payload.OwnerId, prepared.OwnerId);
            Equal(payload.LocalFrame, prepared.LocalFrame);
            Equal(payload.InstanceJson, prepared.InstanceJson);
            var resolvedRuntime = DesignPreviewTestValues.Parse(payload.DesignPreviewJson);
            var resolvedActor = resolvedRuntime["actor"] as JsonObject
                ?? throw new InvalidOperationException($"Screen '{screen.Id}' has no resolved Actor.");
            Equal(expectedActorId, resolvedActor["id"]?.GetValue<string>());
            True(resolvedActor["id"]?.GetValue<string>() != "sample_actor");

            var payloadInstance = DesignPreviewTestValues.Parse(payload.InstanceJson);
            True(JsonNode.DeepEquals(
                JsonPath.ParseRequiredObject(instance.AnimationJson, $"Screen '{screen.Id}' animation_json"),
                payloadInstance["animation"]));
            var payloadContext = JsonPath.RequiredObject(
                payloadInstance,
                "context",
                $"Screen '{screen.Id}' Preview instance");
            Equal(payload.LocalFrame, JsonPath.RequiredInteger(
                payloadContext,
                "screenFrame",
                $"Screen '{screen.Id}' Preview context"));
            True(!payloadContext.ContainsKey("localFrame"));
        }

        var playbackScreen = screens[0];
        using (var operations =
               new EditorOperationCoordinator())
        {
            var callingThread =
                Environment.CurrentManagedThreadId;
            var preparationThread = callingThread;
            var interactivePayload =
                operations.ExecuteAsync(
                    () =>
                    {
                        preparationThread =
                            Environment.CurrentManagedThreadId;
                        return preparer.Prepare(
                            playbackScreen,
                            null,
                            "light",
                            0,
                            CancellationToken.None);
                    }).GetAwaiter().GetResult();
            True(interactivePayload is not null);
            True(preparationThread != callingThread);
        }
        var playbackFrames =
            preparer.PrepareFrames(
                playbackScreen,
                null,
                "light",
                0,
                1,
                CancellationToken.None);
        Equal(2, playbackFrames.Count);
        SequenceEqual(
            new[] { 0, 1 },
            playbackFrames.Select(
                (payload) => payload.LocalFrame));
        EqualPreparedProductionPayload(
            preparer.PrepareRequired(
                playbackScreen,
                null,
                "light",
                1),
            playbackFrames[1]);

        var playbackShot =
            Descendants(
                    database.LoadProjectTree())
                .Single(
                    (node) =>
                        node.Kind
                            == ProjectTreeNodeKind.Shot
                        && node.Id
                            == "shot_001");
        var shotSlots =
            dataSource.LoadShotSlots(
                playbackShot.Id);
        True(shotSlots.Count >= 2);
        var boundaryFrame =
            shotSlots[0].DurationFrames;
        var boundaryFrames =
            preparer.PrepareFrames(
                playbackShot,
                null,
                "light",
                boundaryFrame - 1,
                boundaryFrame,
                CancellationToken.None);
        Equal(2, boundaryFrames.Count);
        EqualPreparedProductionPayload(
            preparer.PrepareRequired(
                playbackShot,
                null,
                "light",
                boundaryFrame - 1),
            boundaryFrames[0]);
        EqualPreparedProductionPayload(
            preparer.PrepareRequired(
                playbackShot,
                null,
                "light",
                boundaryFrame),
            boundaryFrames[1]);
        using (var cancellation =
               new CancellationTokenSource())
        {
            cancellation.Cancel();
            Throws<OperationCanceledException>(
                () => preparer.PrepareFrames(
                    playbackScreen,
                    null,
                    "light",
                    0,
                    1,
                    cancellation.Token));
        }
        True(typeof(EditorPreviewController)
            .GetField(
                "_productionPayloadPreparer",
                BindingFlags.Instance
                | BindingFlags.NonPublic) is not null);
        True(typeof(EditorPreviewController)
            .GetField(
                "_productionPayloadPreparation",
                BindingFlags.Instance
                | BindingFlags.NonPublic) is not null);
        var interactiveRefresh =
            typeof(EditorPreviewController)
                .GetMethod(
                    "RefreshProductionCoreAsync",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Missing asynchronous Production Preview refresh.");
        Equal(
            typeof(Task),
            interactiveRefresh.ReturnType);

        var after = SHA256.HashData(File.ReadAllBytes(temporary));
        SequenceEqual(before, after);
    }
    finally
    {
        File.Delete(temporary);
    }

    static void EqualPreparedProductionPayload(
        DesignPreviewPayload expected,
        DesignPreviewPayload actual)
    {
        Equal(expected.Kind, actual.Kind);
        Equal(expected.Name, actual.Name);
        Equal(expected.ConfigJson, actual.ConfigJson);
        Equal(expected.DesignPreviewJson, actual.DesignPreviewJson);
        Equal(expected.RuntimeContractJson, actual.RuntimeContractJson);
        Equal(expected.InstanceJson, actual.InstanceJson);
        Equal(expected.LocalFrame, actual.LocalFrame);
        Equal(expected.OwnerId, actual.OwnerId);
        Equal(expected.ThemeMode, actual.ThemeMode);
        Equal(expected.DeviceId, actual.DeviceId);
        Equal(expected.FrameRate, actual.FrameRate);
    }
}

static void ProductionPlaybackSelectsPreparedOwnerFrames()
{
    var screen = new ProjectTreeNode(
        ProjectTreeNodeKind.ModuleInstance,
        "screen-a",
        "Screen A",
        "",
        "module_instance");
    var frames = new[]
    {
        PlaybackPayloadAtFrame(10),
        PlaybackPayloadAtFrame(11),
    };
    var prepared =
        new PreparedProductionPlayback(
            "request-signature",
            screen.Kind,
            screen.Id,
            10,
            frames);

    True(prepared.TryGetFrame(
        screen,
        10,
        out var first));
    Equal(10, first?.LocalFrame);
    True(prepared.TryGetFrame(
        screen,
        11,
        out var second));
    Equal(11, second?.LocalFrame);
    True(prepared.Covers(
        screen,
        10,
        11));
    True(prepared.Covers(
        screen,
        11,
        11));
    True(!prepared.Covers(
        screen,
        9,
        11));
    True(!prepared.Covers(
        screen,
        10,
        12));
    True(!prepared.TryGetFrame(
        screen,
        12,
        out _));
    True(!prepared.TryGetFrame(
        new ProjectTreeNode(
            screen.Kind,
            "screen-b",
            "Screen B",
            "",
            screen.RecordClassId),
        10,
        out _));
    True(!prepared.Covers(
        new ProjectTreeNode(
            screen.Kind,
            "screen-b",
            "Screen B",
            "",
            screen.RecordClassId),
        10,
        11));

    static DesignPreviewPayload PlaybackPayloadAtFrame(
        int frame) =>
        new(
            "module",
            "Screen A",
            "{}",
            "{}",
            new Dictionary<string, string>(),
            new Dictionary<string, bool>(),
            "",
            "",
            "{}",
            Array.Empty<ProductionFontFace>(),
            "module.core.chat",
            "{}",
            "{}",
            "light",
            LocalFrame: frame,
            OwnerId: "screen-a");
}

static void PreviewThemeModeHasOneStrictPayloadOwner()
{
    Equal(
        "light",
        ModuleAppearanceModeContract.Resolve(
            Object("{\"appearanceMode\":\"light\"}"),
            "dark",
            "Test Module Variant"));
    Equal(
        "dark",
        ModuleAppearanceModeContract.Resolve(
            Object("{\"appearanceMode\":\"dark\"}"),
            "light",
            "Test Module Variant"));
    Equal(
        "dark",
        ModuleAppearanceModeContract.Resolve(
            Object("{\"appearanceMode\":\"inherit\"}"),
            "dark",
            "Test Module Variant"));
    Throws<InvalidOperationException>(() =>
        ModuleAppearanceModeContract.Read(Object("{}"), "Test Module Variant"));
    Throws<InvalidOperationException>(() =>
        ModuleAppearanceModeContract.Read(Object("{\"appearanceMode\":4}"), "Test Module Variant"));
    Throws<InvalidOperationException>(() =>
        ModuleAppearanceModeContract.Read(Object("{\"appearanceMode\":\"sepia\"}"), "Test Module Variant"));
    Throws<InvalidOperationException>(() =>
        ModuleAppearanceModeContract.RequireResolved("inherit", "Test resolved mode"));

    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-theme-mode-owner-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var dataSource = new DesignPreviewPayloadDataSource(
            database.PreviewInputs,
            database.Production,
            database.Resources,
            database.Resources,
            database.ProjectPaths);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var componentVariant = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
        var module = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Module);
        var defaultVariant = module.Children.Single((node) =>
            node.Kind == ProjectTreeNodeKind.ModuleVariant && node.IsProtected);

        Equal(
            "dark",
            Required(DesignPreviewPayloadFactory.Create(
                dataSource,
                componentVariant,
                theme.Id,
                themeMode: "dark")).ThemeMode);
        Equal(
            "dark",
            Required(DesignPreviewPayloadFactory.Create(
                dataSource,
                defaultVariant,
                theme.Id,
                themeMode: "dark")).ThemeMode);

        var lightVariant = NodeCommands(database).SaveModuleVariant(
            defaultVariant,
            "Forced Light");
        database.UpdateModuleVariantField(lightVariant, "module.appearanceMode", "light");
        Equal(
            "light",
            Required(DesignPreviewPayloadFactory.Create(
                dataSource,
                lightVariant,
                theme.Id,
                themeMode: "dark")).ThemeMode);

        var darkVariant = NodeCommands(database).SaveModuleVariant(
            defaultVariant,
            "Forced Dark");
        database.UpdateModuleVariantField(darkVariant, "module.appearanceMode", "dark");
        Equal(
            "dark",
            Required(DesignPreviewPayloadFactory.Create(
                dataSource,
                darkVariant,
                theme.Id,
                themeMode: "light")).ThemeMode);

        var beforeRejectedWrite = database.GetModuleVariantSettings(defaultVariant).ConfigJson;
        Throws<InvalidOperationException>(() =>
            database.UpdateModuleVariantField(defaultVariant, "module.appearanceMode", "sepia"));
        Equal(beforeRejectedWrite, database.GetModuleVariantSettings(defaultVariant).ConfigJson);
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void ConversationMessageActorsFollowDirectionContract()
{
    var sourcePath = ParityDatabasePath();
    var temporary = Path.Combine(
        Directory.GetCurrentDirectory(),
        "data",
        $".mockups-conversation-message-actors-{Guid.NewGuid():N}.sqlite");
    File.Copy(sourcePath, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var screen = Descendants(database.LoadProjectTree())
            .Single((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance
                && database.GetModuleSettings(database.GetModuleInstanceSettings(node.Id).ModuleId).RecordClassId
                    == ModuleRuntimeDocumentContracts.ConversationRecordClassId);
        var instance = database.GetModuleInstanceSettings(screen.Id);
        var shotOwnerActorId = database.GetShotSettings(instance.ShotId).OwnerActorId;
        var content = JsonPath.ParseRequiredObject(instance.ContentJson, $"Screen '{screen.Id}' content_json");
        var messages = content["messages"]?.AsArray()
            ?? throw new InvalidOperationException("Conversation instance has no messages.");
        var incoming = messages.OfType<JsonObject>().Single((message) => message["direction"]?.GetValue<string>() == "incoming");
        var incomingActorId = incoming["actorId"]?.GetValue<string>() ?? "";
        True(!string.IsNullOrWhiteSpace(incomingActorId));
        True(messages.OfType<JsonObject>()
            .Where((message) => message["direction"]?.GetValue<string>() == "outgoing")
            .All((message) => string.IsNullOrWhiteSpace(message["actorId"]?.GetValue<string>())));

        var system = incoming.DeepClone().AsObject();
        system["id"] = $"message_system_{Guid.NewGuid():N}";
        system["direction"] = "system";
        system["actorId"] = "";
        database.AddModuleInstanceRuntimeCollectionItem(screen.Id, "messages", system);

        var moduleVariant = database.GetModuleInstanceVariantSettings(screen.Id);
        var runtimePreview = DesignPreviewTestValues.Parse(database.GetModuleInstanceRuntimePreviewJson(screen.Id));
        var messageCollection = RuntimeInputDefinitionReader.ReadCollections(
            runtimePreview,
            JsonPath.ParseRequiredObject(moduleVariant.ConfigJson, "Conversation Variant config"))
            .Single((collection) => collection.Id == "messages");
        var actorField = messageCollection.Fields.Single((field) => field.Id == "actor");
        var optionsSource = new RuntimeInputOptionsDataSource(database.DictionaryContext, database.Resources);
        var incomingActorOptions = RuntimeInputFieldDefinitionFactory.Create(
            optionsSource,
            screen,
            actorField,
            CollectionFieldAvailability.AllowsEmpty(incoming, actorField)).Options ?? [];
        var systemActorOptions = RuntimeInputFieldDefinitionFactory.Create(
            optionsSource,
            screen,
            actorField,
            CollectionFieldAvailability.AllowsEmpty(system, actorField)).Options ?? [];
        True(incomingActorOptions.All((option) => !string.IsNullOrWhiteSpace(option.Value)));
        True(systemActorOptions.Any((option) => string.IsNullOrWhiteSpace(option.Value)));

        var payload = Required(DesignPreviewPayloadFactory.Create(
            new DesignPreviewPayloadDataSource(
                database.PreviewInputs,
                database.Production,
                database.Resources,
                database.Resources,
                database.ProjectPaths),
            screen,
            null));
        var preparedMessages = DesignPreviewTestValues.Parse(payload.DesignPreviewJson)["messages"]?.AsArray()
            ?? throw new InvalidOperationException("Prepared Conversation payload has no messages.");
        True(preparedMessages.OfType<JsonObject>()
            .Where((message) => message["direction"]?.GetValue<string>() == "outgoing")
            .All((message) => message["actorId"]?.GetValue<string>() == shotOwnerActorId));

        var resolved = new ProductionPreviewRuntimeResolver(
            database.Resources,
            database.ProjectPaths).Resolve(payload, "light");
        var resolvedMessages = DesignPreviewTestValues.Parse(resolved.DesignPreviewJson)["messages"]?.AsArray()
            ?? throw new InvalidOperationException("Resolved Conversation payload has no messages.");
        Equal(
            incomingActorId,
            resolvedMessages.OfType<JsonObject>()
                .Single((message) => message["direction"]?.GetValue<string>() == "incoming")["actor"]?["id"]?.GetValue<string>());
        True(resolvedMessages.OfType<JsonObject>()
            .Where((message) => message["direction"]?.GetValue<string>() == "outgoing")
            .All((message) => message["actor"]?["id"]?.GetValue<string>() == shotOwnerActorId));
        var resolvedSystemActor = resolvedMessages.OfType<JsonObject>()
            .Single((message) => message["direction"]?.GetValue<string>() == "system")["actor"] as JsonObject
            ?? throw new InvalidOperationException("System message Actor must resolve as an object.");
        Equal(0, resolvedSystemActor.Count);
        True(resolvedMessages.OfType<JsonObject>()
            .All((message) => message["actor"]?["id"]?.GetValue<string>() != "sample_actor"));

        var resolvedPreview = DesignPreviewTestValues.Parse(resolved.DesignPreviewJson);
        resolvedPreview["conversationFrame"] = 100000;
        var resolvedAtSystemMessage = resolved with
        {
            DesignPreviewJson = resolvedPreview.ToJsonString(),
        };
        var device = Descendants(database.LoadProjectTree())
            .First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            false,
            resolvedAtSystemMessage).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(html));
        True(!html.Contains("preview-error", StringComparison.Ordinal));

        var incomingId = incoming["id"]?.GetValue<string>() ?? "";
        var beforeRejectedWrite = SHA256.HashData(File.ReadAllBytes(temporary));
        Throws<InvalidOperationException>(() => database.UpdateModuleInstanceRuntimeCollectionValue(
            screen.Id,
            "messages",
            incomingId,
            "direction",
            JsonValue.Create("outgoing")));
        SequenceEqual(beforeRejectedWrite, SHA256.HashData(File.ReadAllBytes(temporary)));

        using var operations = new EditorOperationCoordinator();
        var store = new RuntimeInputInstanceDocumentStore(
            new SqliteRuntimeInputInstanceStore(
                database.Context,
                database.Design,
                database.Production,
                database.Resources),
            database.Production,
            database.Production,
            database.Resources,
            operations);
        store.UpdateCollectionValuesAsync(
            screen.Id,
            "messages",
            incomingId,
            new Dictionary<string, JsonNode?>
            {
                ["direction"] = JsonValue.Create("outgoing"),
                ["actorId"] = JsonValue.Create(""),
            }).GetAwaiter().GetResult();
        var updated = JsonPath.ParseRequiredObject(
            database.GetModuleInstanceSettings(screen.Id).ContentJson,
            $"Screen '{screen.Id}' updated content_json");
        var updatedMessage = updated["messages"]?.AsArray().OfType<JsonObject>()
            .Single((message) => message["id"]?.GetValue<string>() == incomingId)
            ?? throw new InvalidOperationException("Missing atomically updated message.");
        Equal("outgoing", updatedMessage["direction"]?.GetValue<string>());
        Equal("", updatedMessage["actorId"]?.GetValue<string>());
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void InvalidConversationMessageActorsFailReadOnly()
{
    AssertRejectedDatabaseIsReadOnly("conversation-outgoing-actor", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_set(content_json, '$.messages[0].actorId', 'actor_sam')
            WHERE module_id = 'module_core_chat'
            """;
        Equal(1, command.ExecuteNonQuery());
    });
    AssertRejectedDatabaseIsReadOnly("conversation-incoming-without-actor", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_set(content_json, '$.messages[2].actorId', '')
            WHERE module_id = 'module_core_chat'
            """;
        Equal(1, command.ExecuteNonQuery());
    });
    AssertRejectedDatabaseIsReadOnly("conversation-system-missing-actor", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE module_instances
            SET content_json = json_set(
                content_json,
                '$.messages[0].direction', 'system',
                '$.messages[0].actorId', 'missing_actor')
            WHERE module_id = 'module_core_chat'
            """;
        Equal(1, command.ExecuteNonQuery());
    });
}

static void ModuleVariantsAreExplicit()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-module-variants-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var moduleInstances = ModuleInstances(database);
        var roots = database.LoadProjectTree();
        var module = Descendants(roots).First((node) => node.Kind == ProjectTreeNodeKind.Module
            && node.RecordClassId == "module.core.lockScreen");
        var defaultVariant = module.Children.Single((node) => node.Id.EndsWith("::variant::default", StringComparison.Ordinal));
        True(defaultVariant.IsProtected);

        var android = NodeCommands(database).SaveModuleVariant(
            defaultVariant,
            "Android");
        database.UpdateModuleVariantField(android, "module.appearanceMode", "dark");
        Equal("dark", JsonNode.Parse(database.GetModuleVariantSettings(android).ConfigJson)?["appearanceMode"]?.GetValue<string>());
        Equal("inherit", JsonNode.Parse(database.GetModuleVariantSettings(defaultVariant).ConfigJson)?["appearanceMode"]?.GetValue<string>());

        var shot = Descendants(database.LoadProjectTree()).First((node) => node.Kind == ProjectTreeNodeKind.Shot);
        var appId = module.Parent?.Id ?? throw new InvalidOperationException("Lock Screen module has no App.");
        var screen = moduleInstances.AddModuleInstance(
            shot,
            new ShotModuleInstanceDraft(
                new ShotModuleChoice(
                    module.Id,
                    module.Name,
                    module.Parent!.Name,
                    appId,
                    module.RecordClassId),
                defaultVariant.Id,
                defaultVariant.Name,
                $"{module.Name} · {defaultVariant.Name}"));
        using (var connection = new SqliteConnection($"Data Source={temporary}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE module_instances SET content_json = json_set(content_json, '$.orphan', 'remove me') WHERE id = $id";
            command.Parameters.AddWithValue("$id", screen.Id);
            Equal(1, command.ExecuteNonQuery());
        }
        database.UpdateModuleInstanceAnimationJson(screen.Id,
            "{\"schemaVersion\":2,\"tracks\":[{\"id\":\"orphan-track\",\"fieldId\":\"orphan\",\"keyframes\":[{\"id\":\"orphan-kf\",\"frame\":0,\"value\":true,\"interpolation\":\"hold\",\"enabled\":true}]}]}");
        database.UpdateModuleInstanceVariant(screen.Id, android.Id);
        Equal(android.Id, database.GetModuleInstanceVariantReference(screen.Id));
        Equal("dark", JsonNode.Parse(database.GetModuleInstanceVariantSettings(screen.Id).ConfigJson)?["appearanceMode"]?.GetValue<string>());
        True(JsonNode.Parse(database.GetModuleInstanceSettings(screen.Id).ContentJson)?["orphan"] is null);
        Equal(0, JsonNode.Parse(database.GetModuleInstanceSettings(screen.Id).AnimationJson)?["tracks"]?.AsArray().Count);
        Throws<InvalidOperationException>(() =>
            NodeCommands(database).Delete(android));

        database.UpdateModuleInstanceVariant(screen.Id, defaultVariant.Id);
        NodeCommands(database).Delete(android);
        True(!moduleInstances.GetModuleVariantOptions(module.Id)
            .Any((option) => option.Value == android.Id));
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
    var source = ParityDatabasePath();
    var database = new SqliteProjectTestContext(source);
    var settings = database.GetComponentClassSettings("component_project_foqn_s2_label");
    var label = JsonNode.Parse(settings.ConfigJson)?["label"]?.AsObject()
        ?? throw new InvalidOperationException("Missing current Label values.");
    True(label["subtextPlacement"] is null);
    True(label["subtextVerticalPosition"] is JsonValue);
    True(label["subtextHorizontalAlign"] is JsonValue);
    var subtextFields = EditorLayouts(database).LoadEditorLayout("component.label").Cards
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

static void ForwardActionsUseSharedPresentation()
{
    Equal(10d, EditorForwardVisuals.IndicatorSize);
    Equal(30d, EditorForwardVisuals.ActionSize);
    Equal("M 1,1 L 9,5 L 1,9 Z", EditorForwardVisuals.IndicatorGeometry);
    Equal("Expose to parent runtime", EditorForwardVisuals.AccessibleName(isForwarded: false));
    Equal("Keep as Variant value", EditorForwardVisuals.AccessibleName(isForwarded: true));
}

var isolatedUiTests = new HashSet<string>(StringComparer.Ordinal)
{
    "collapsed editor cards defer their snapshot until expansion",
    "rapid visual selection commits only the latest prepared editor",
    "new Shot reload prepares Preview before selection",
    "obsolete Preview authoring preparation cannot replace the latest selection",
    "real Preview shell layout remains usable at 1040 and 1440",
    "List Item and List expose their runtime model in the real editor",
    "Conversation Module exposes its Test Values Runtime in the real editor",
    "pinned Module Variant Preview survives changing editor selection",
    "Chat List Module exposes its fixed List boundary and exact Runtime in the real editor",
};
var exhaustiveTests = new HashSet<string>(StringComparer.Ordinal)
{
    "manifest owners render their committed fixtures and Modules advance time",
};
var group = SingleArgumentValue(args, "--group") ?? "all";
if (group is not ("all" or "core" or "ui" or "exhaustive"))
{
    throw new InvalidOperationException(
        $"Unknown desktop test group '{group}'. Expected all, core, ui or exhaustive.");
}
var exactNames = ArgumentValues(args, "--exact");
var filters = ArgumentValues(args, "--filter");
var ownerSelectors = ArgumentValues(args, "--owner");
var knownArguments = new HashSet<string>(StringComparer.Ordinal)
{
    "--group",
    "--exact",
    "--filter",
    "--owner",
    "--list",
};
for (var index = 0; index < args.Length; index++)
{
    var argument = args[index];
    if (!knownArguments.Contains(argument))
    {
        throw new InvalidOperationException($"Unknown desktop test argument '{argument}'.");
    }
    if (argument != "--list") index++;
}
foreach (var exactName in exactNames)
{
    if (!tests.Any((test) => test.Name.Equals(exactName, StringComparison.Ordinal)))
    {
        throw new InvalidOperationException($"Unknown exact desktop test '{exactName}'.");
    }
}
var knownOwnerSelectors = DesktopPreviewManifest.Components.Keys
    .Select((owner) => $"component:{owner}")
    .Concat(DesktopPreviewManifest.Modules.Keys.Select((owner) => $"module:{owner}"))
    .ToHashSet(StringComparer.Ordinal);
foreach (var ownerSelector in ownerSelectors)
{
    if (!knownOwnerSelectors.Contains(ownerSelector))
    {
        throw new InvalidOperationException(
            $"Unknown Preview owner selector '{ownerSelector}'.");
    }
}
if (ownerSelectors.Count > 0)
{
    if (group != "exhaustive")
    {
        throw new InvalidOperationException(
            "Preview owner selection requires '--group exhaustive'.");
    }
    if (exactNames.Count > 0 || filters.Count > 0)
    {
        throw new InvalidOperationException(
            "Preview owner selection cannot be combined with exact-name or text filters.");
    }
    selectedManifestOwners.UnionWith(ownerSelectors);
}
var selectedTests = tests
    .Where((test) => group switch
    {
        "core" => !isolatedUiTests.Contains(test.Name) && !exhaustiveTests.Contains(test.Name),
        "ui" => isolatedUiTests.Contains(test.Name),
        "exhaustive" => exhaustiveTests.Contains(test.Name),
        _ => true,
    })
    .Where((test) =>
        (exactNames.Count == 0 && filters.Count == 0)
        || exactNames.Contains(test.Name, StringComparer.Ordinal)
        || filters.Any((filter) => test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
    .ToArray();
if (selectedTests.Length == 0)
{
    throw new InvalidOperationException("Desktop test selection matched no tests.");
}
if (args.Contains("--list", StringComparer.Ordinal))
{
    foreach (var (name, _) in selectedTests) Console.WriteLine(name);
    return;
}

var failures = new List<string>();
foreach (var (name, run) in selectedTests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add(name);
        Console.Error.WriteLine(
            $"FAIL {name}: {exception.GetBaseException().Message}");
    }
}

Console.WriteLine(
    $"Animation desktop tests: {selectedTests.Length - failures.Count}/{selectedTests.Length} passed.");
if (failures.Count > 0) Environment.Exit(1);

static void ForwardedChildInputsBecomeParentRuntimeInputs()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-forwarding-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var settings = database.GetComponentClassSettings("component_project_foqn_s2_textInputBar");
        var config = DesignPreviewTestValues.Parse(settings.ConfigJson);
        var preview = DesignPreviewTestValues.Parse(settings.DesignPreviewJson);
        var effective = RuntimeInputForwardingContract.EffectivePreview(preview, config);
        var inputs = RuntimeInputDefinitionReader.ReadInputs(effective, config);
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

static void RuntimeInputForwardingEnvelopesAreStrict()
{
    Throws<InvalidOperationException>(() => RuntimeInputForwardingContract.EffectivePreview(
        new JsonObject { ["inputs"] = new JsonObject() },
        new JsonObject()));
    Throws<InvalidOperationException>(() => RuntimeInputForwardingContract.EffectivePreview(
        new JsonObject { ["collections"] = JsonValue.Create(false) },
        new JsonObject()));
    Throws<InvalidOperationException>(() => RuntimeInputForwardingContract.EffectivePreview(
        new JsonObject(),
        new JsonObject
        {
            ["owner"] = new JsonObject
            {
                [RuntimeInputForwardingContract.StorageKey] = new JsonArray(),
            },
        }));
    Throws<InvalidOperationException>(() => RuntimeInputForwardingContract.EffectivePreview(
        new JsonObject(),
        new JsonObject
        {
            ["owner"] = new JsonObject
            {
                [RuntimeInputForwardingContract.StorageKey] = new JsonObject
                {
                    ["title"] = JsonValue.Create("invalid"),
                },
            },
        }));

    var owner = new FieldDefinition(
        "component.test.inputs",
        "Component inputs",
        ValueKind.ComponentInputBindings);
    var input = new ComponentInputBindingDefinition(
        "enabled",
        "Enabled",
        "enabled",
        ValueKind.Boolean,
        ComponentInputBindingSource.Runtime,
        "true");
    var forwardingDefinition = RuntimeInputForwardingContract.Definition(
        owner,
        input,
        "Enabled",
        "true");
    var forwardedJsonKey = forwardingDefinition["jsonKey"]?.GetValue<string>()
        ?? throw new InvalidOperationException("Missing forwarded test jsonKey.");
    var validConfig = new JsonObject
    {
        ["owner"] = new JsonObject
        {
            ["enabled"] = true,
            [RuntimeInputForwardingContract.StorageKey] = new JsonObject
            {
                ["enabled"] = forwardingDefinition,
            },
        },
    };
    var effective = RuntimeInputForwardingContract.EffectivePreview(new JsonObject(), validConfig);
    True(effective[forwardedJsonKey]?.GetValue<bool>() == true);
    True(effective["inputs"] is JsonArray { Count: 1 });

    AssertRejectedDatabaseIsReadOnly("forwarding-envelope-wrong-root", (connection) =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE component_classes
            SET config_json = json_set(config_json, '$."$forwardedInputs"', json('[]'))
            WHERE id = 'component_project_foqn_s2_label'
            """;
        command.ExecuteNonQuery();
    });
}

static void ForwardedRuntimeCollectionsExposeSlotStateActions()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-forwarded-slots-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var moduleVariant = database.LoadProjectTree()
            .SelectMany(DescendantsAndSelf)
            .Single((node) => node.Kind == ProjectTreeNodeKind.ModuleVariant
                && node.Parent?.RecordClassId == "module.core.lockScreen"
                && node.Name == "Default");
        moduleVariant = NodeCommands(database)
            .ToggleModuleVariantLock(moduleVariant);
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
        var forwardedInputs = RuntimeInputDefinitionReader.ReadInputs(effective, config);
        Equal(0, forwardedInputs.Count((input) => input.Label is "Hora" or "Subtext" or "Password" or "Attempt"));
        var collections = RuntimeInputDefinitionReader.ReadCollections(effective, config);
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
            item["variantReference"]?.GetValue<string>()?.Contains("_password::variant::", StringComparison.Ordinal) == true);
        var passwordContract = passwordState["inputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing projected Password State runtime contract.");
        var passwordInputs = RuntimeInputDefinitionReader.ReadInputs(passwordContract, new JsonObject());
        var passwordTrigger = passwordInputs.Single((input) => input.Label == "Enter password" && input.ActionOnly);
        var passwordFrame = passwordInputs.Single((input) => input.Label == "Entry frame" && input.ActionOnly);
        var passwordTiming = passwordInputs.Single((input) => input.Label == "Entry timing");
        var passwordAttempt = passwordInputs.Single((input) => input.Label == "Attempt");
        Equal(true, passwordTrigger.Animation is not null);
        Equal(true, passwordFrame.Animation is null);
        Equal(passwordAttempt.Id, passwordTiming.BehaviorTiming?.SourceFieldId ?? "");
        var forwardedPasswordAction = ComponentPreviewActions.ReadWithEmbedded(
                effective,
                new ComponentPreviewInputDataSource(database.Design, database.Resources).ComponentVariantRuntimeContract)
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
        var session = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
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

        var missingReflowTheme = themeTokens.DeepClone().AsObject();
        (missingReflowTheme["motion"] as JsonObject)?.Remove("reflowDurationMs");
        Throws<InvalidOperationException>(() =>
            ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(
                selectedPreview,
                stateAction,
                missingReflowTheme.ToJsonString()));
        var invalidSlideTheme = themeTokens.DeepClone().AsObject();
        invalidSlideTheme["motion"]!["transitions"]!["slide"]!["durationMs"] = "260";
        Throws<InvalidOperationException>(() =>
            ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(
                selectedPreview,
                stateAction,
                invalidSlideTheme.ToJsonString()));
        var missingStatePreview = selectedPreview.DeepClone().AsObject();
        missingStatePreview[slots.JsonKey]![0]!["runtimeStateId"] = "state_missing";
        Throws<InvalidOperationException>(() =>
            ComponentPreviewActions.MotionStateTransitionDurationMilliseconds(
                missingStatePreview,
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
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("{\"schemaVersion\":2,\"tracks\":[4]}"));
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("{\"schemaVersion\":2,\"tracks\":[{\"id\":\"t\",\"fieldId\":\"f\",\"keyframes\":[4]}]}"));
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("{\"schemaVersion\":2,\"tracks\":[{\"id\":\"t\",\"fieldId\":\"f\",\"keyframes\":[{\"id\":\"k\",\"frame\":0,\"value\":true,\"enabled\":true}]}]}"));
    Throws<InvalidOperationException>(() => new ModuleInstanceAnimationDocument("{\"schemaVersion\":2,\"tracks\":[{\"id\":\"t\",\"fieldId\":\"f\",\"keyframes\":[{\"id\":\"k\",\"frame\":0,\"value\":true,\"interpolation\":\"hold\"}]}]}"));
    _ = new ModuleInstanceAnimationDocument("{\"schemaVersion\":2,\"tracks\":[]}");
}

static void ExplicitReferenceUsageIsExactTypedAndShared()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-explicit-usage-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
                         or ProjectTreeNodeKind.ComponentVariant
                         or ProjectTreeNodeKind.ModuleVariant))
            {
                Equal(node.IsUsed, index.ContainsKey(new ReferenceTarget(node.Kind, node.Id)));
            }
        }

        var usedDevice = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device && node.IsUsed);
        var deviceUsages =
            database.ReferenceUsages.GetReferenceUsageDetails(usedDevice);
        True(deviceUsages.Any((usage) => usage.SourceKind == ProjectTreeNodeKind.Actor && usage.IsProduction));
        Throws<InvalidOperationException>(() => database.Delete(usedDevice));

        var actor = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Actor && node.IsUsed);
        var actorUsages =
            database.ReferenceUsages.GetReferenceUsageDetails(actor);
        True(actorUsages.Any((usage) => usage.SourceKind == ProjectTreeNodeKind.Shot && usage.IsProduction));
        True(actorUsages.Any((usage) =>
            (usage.SourceKind is ProjectTreeNodeKind.ComponentClass
                or ProjectTreeNodeKind.Module
                or ProjectTreeNodeKind.ComponentVariant)
            && !usage.IsProduction));

        var usedComponentVariant = nodes
            .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant)
            .Select((node) => (
                Node: node,
                Usages: database.ReferenceUsages
                    .GetReferenceUsageDetails(node)))
            .First((candidate) => candidate.Usages.Any((usage) => usage.SourceKind == ProjectTreeNodeKind.ComponentVariant));
        True(usedComponentVariant.Usages.Any((usage) =>
            usage.SourceKind == ProjectTreeNodeKind.ComponentVariant
            && usage.SourceNodeId.Contains("::variant::", StringComparison.Ordinal)));

        var usedModuleVariant = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleVariant && node.IsUsed);
        True(database.ReferenceUsages
            .GetReferenceUsageDetails(usedModuleVariant).Any((usage) =>
            usage.SourceKind == ProjectTreeNodeKind.ModuleInstance && usage.IsProduction));

        const string designActorId = "actor_usage_design_only";
        const string productionActorId = "actor_usage_production_only";
        var projectId = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.Project).Id;
        using (var connection = context.OpenConnection())
        {
            context.Execute(
                connection,
                "INSERT INTO actors (id, project_id, display_name, short_name, metadata_json) VALUES ($id, $projectId, $name, $name, '{}')",
                ("$id", designActorId),
                ("$projectId", projectId),
                ("$name", "Design-only Usage Actor"));
            context.Execute(
                connection,
                "INSERT INTO actors (id, project_id, display_name, short_name, metadata_json) VALUES ($id, $projectId, $name, $name, '{}')",
                ("$id", productionActorId),
                ("$projectId", projectId),
                ("$name", "Production-only Usage Actor"));
            context.Execute(
                connection,
                "UPDATE modules SET design_preview_json = json_set(design_preview_json, '$.testValues.actorId', $actorId) WHERE id = 'module_project_foqn_s2_lock_screen'",
                ("$actorId", designActorId));
            context.Execute(
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
            context.Execute(
                connection,
                "UPDATE projects SET notes = $notes, metadata_json = $metadataJson",
                ("$notes", $"Unrelated prose blue plus substring prefix-{blue.Id}-suffix"),
                ("$metadataJson", "{\"comment\":\"blue\"}"));
        }
        var blueUsages =
            database.ReferenceUsages.GetReferenceUsageDetails(blue);
        True(blueUsages.Count > 0);
        True(blueUsages.All((usage) => usage.SourceKind != ProjectTreeNodeKind.Project));

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
            return Task.FromResult(true);
        },
        (embedded, nodeId) =>
        {
            events.Add($"embedded:{nodeId}:{embedded.SlotFieldId}");
            return Task.CompletedTask;
        },
        messages);
    var embeddedUsage = new EmbeddedComponentUsage(
        "component_parent",
        "Parent",
        "parent",
        "parent.slot",
        "Slot",
        true,
        "component_parent::variant::default");

    navigator.Navigate(new ReferenceUsageDetail(
        "component_parent::variant::default",
        ProjectTreeNodeKind.ComponentVariant,
        "Component Variant",
        "Parent · Default",
        "Slot · overrides",
        ReferenceUsageScope.Design,
        embeddedUsage)).GetAwaiter().GetResult();
    navigator.Navigate(new ReferenceUsageDetail(
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
            "select:Design:component_parent::variant::default",
            "embedded:component_parent::variant::default:parent.slot",
            "select:Production:screen_1",
        },
        events);
    Equal(0, messages.Warnings.Count);
}

static void ProductionDataOwnsConcreteResources()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var project = database.LoadProjectTree().Single();
    var productionSections = EditorWorkspaceNavigation.SectionRoots(project, EditorWorkspace.Production);
    SequenceEqual(
        new[]
        {
            ProjectTreeNodeKind.EpisodesRoot,
            ProjectTreeNodeKind.RenderQueueRoot,
            ProjectTreeNodeKind.ProductionDataRoot,
        },
        productionSections.Select((node) => node.Kind));

    var productionData = productionSections.Single((node) => node.Kind == ProjectTreeNodeKind.ProductionDataRoot);
    SequenceEqual(
        new[]
        {
            ProjectTreeNodeKind.ActorsRoot,
            ProjectTreeNodeKind.DevicesRoot,
            ProjectTreeNodeKind.ProductionFontsRoot,
        },
        productionData.Children.Select((node) => node.Kind));
    True(productionData.Children.All((node) =>
        EditorNavigationMetadata.WorkspaceScope(node.Kind) == EditorWorkspaceScope.Production));
    True(productionData.Children.All((node) => EditorNavigationRenderer.ShowsActions(node, null)));

    var designSections = EditorWorkspaceNavigation.SectionRoots(project, EditorWorkspace.Design);
    True(designSections.Any((node) => node.Kind == ProjectTreeNodeKind.ThemesRoot));
    True(designSections.All((node) => node.Kind is not ProjectTreeNodeKind.DevicesRoot
        and not ProjectTreeNodeKind.ProductionFontsRoot
        and not ProjectTreeNodeKind.ActorsRoot));
    var themeRoot = DescendantsAndSelf(project).Single((node) => node.Kind == ProjectTreeNodeKind.ThemesRoot);
    Equal(ProjectTreeNodeKind.SystemDataRoot, Required(themeRoot.Parent).Kind);
}

static void RenderQueueNavigationAndSurfaceAreAlwaysAvailable()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var project = database.LoadProjectTree().Single();
    var queueNode = EditorWorkspaceNavigation
        .SectionRoots(project, EditorWorkspace.Production)
        .Single((node) =>
            node.Kind == ProjectTreeNodeKind.RenderQueueRoot);
    Equal(project, Required(queueNode.Parent));
    True(queueNode.CanOpenEditor);
    Equal(
        "navigation.render_queue",
        queueNode.RecordClassId);

    True(RenderQueueEditorSurface.Owns(queueNode));
    var shot = DescendantsAndSelf(project)
        .First((node) => node.Kind == ProjectTreeNodeKind.Shot);
    True(RenderQueueController.OwnsNavigationAction(shot));
    True(!RenderQueueController.OwnsNavigationAction(queueNode));
}

static void ProductionOutputActionOwnsConfiguration()
{
    var database = new SqliteProjectTestContext(ParityDatabasePath());
    var layout = EditorLayouts(database).LoadEditorLayout("project");
    var card = layout.Cards.Single((candidate) =>
        candidate.Id == "production-output");
    Equal(
        "layout:production-output",
        ProductionOutputNavigationAction.CardSessionStateId);
    True(card.Groups.SelectMany((group) => group.Fields).Any((field) =>
        field.Id == "project.productionRoot"));
    True(card.Groups.SelectMany((group) => group.Fields).Any((field) =>
        field.Id == "project.outputRelativeDirectoryTemplate"));

    var rootsPath = Path.Combine(
        Path.GetTempPath(),
        $"mockups-production-output-action-{Guid.NewGuid():N}.json");
    var outputRoot = Path.Combine(
        Path.GetTempPath(),
        $"mockups-production-output-action-files-{Guid.NewGuid():N}");
    Directory.CreateDirectory(outputRoot);
    try
    {
        using var session =
            HeadlessUnitTestSession.StartNew(
                typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var button = new Button();
            var roots = new ProductionOutputRootStore(rootsPath);
            var opened = false;
            var action = new ProductionOutputNavigationAction(
                button,
                roots,
                () => true,
                () => opened = true);
            action.Refresh("project_foqn_s2");
            True(!action.HasLocalRoot);

            roots.Set("project_foqn_s2", outputRoot);
            action.Refresh("project_foqn_s2");
            True(action.HasLocalRoot);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            True(opened);
            True((ToolTip.GetTip(button)?.ToString() ?? "")
                .Contains("Production Output configured", StringComparison.Ordinal));
        }, CancellationToken.None).GetAwaiter().GetResult();
    }
    finally
    {
        File.Delete(rootsPath);
        Directory.Delete(outputRoot, recursive: true);
    }
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

static void RuntimeOwnerTimelineRejectsFilteredEnvelopes()
{
    var emptyAnimation = new JsonObject();
    Equal(1, RuntimeAnimationFrameOrigin.DurationFrames(
        new JsonObject(),
        new JsonObject(),
        Object("""{"tracks":[{"fieldId":"screenField","targetId":"","keyframes":[]}]}"""),
        0));
    foreach (var invalidAnimation in new[]
    {
        Object("""{"tracks":null}"""),
        Object("""{"tracks":[4]}"""),
        Object("""{"tracks":[{"fieldId":""}]}"""),
        Object("""{"tracks":[{"fieldId":"field","targetId":4}]}"""),
        Object("""{"tracks":[{"fieldId":"field","keyframes":{}}]}"""),
        Object("""{"tracks":[{"fieldId":"field","keyframes":[null]}]}"""),
        Object("""{"tracks":[{"fieldId":"field","keyframes":[{"frame":"0"}]}]}"""),
        Object("""{"tracks":[{"fieldId":"field","targetId":"item","keyframes":[]},{"fieldId":"field","targetId":"item","keyframes":[]}]}"""),
        Object("""{"tracks":[{"fieldId":"field","keyframes":[]},{"fieldId":"field","targetId":"","keyframes":[]}]}"""),
        Object("""{"tracks":[{"fieldId":"field","keyframes":[{"frame":0},{"frame":0}]}]}"""),
        Object("""{"tracks":[{"fieldId":"field","keyframes":[{"frame":2},{"frame":1}]}]}"""),
        Object("""{"tracks":[{"fieldId":"field","keyframes":[{"frame":0,"enabled":"true"}]}]}"""),
        Object("""{"retime":[]}"""),
        Object("""{"retime":{"targetDurationFrames":0}}"""),
        Object("""{"retime":{"targetDurationFrames":"4"}}"""),
        Object("""{"retime":{"targets":[]}}"""),
        Object("""{"retime":{"targets":{"item":null}}}"""),
        Object("""{"retime":{"targets":{"item":{"targetDurationFrames":0}}}}"""),
    })
    {
        Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
            new JsonObject(),
            new JsonObject(),
            invalidAnimation,
            0));
    }

    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"collections":null}"""),
        new JsonObject(),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"collections":[4]}"""),
        new JsonObject(),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"inputs":{}}"""),
        new JsonObject(),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"actions":[null]}"""),
        new JsonObject(),
        emptyAnimation,
        0));
    foreach (var invalidKeyContract in new[]
    {
        """{"collections":[{}]}""",
        """{"collections":[{"jsonKey":4}]}""",
        """{"collections":[{"storageCollectionJsonKey":"","jsonKey":"items"}]}""",
        """{"collections":[{"sourceCollectionJsonKey":4,"jsonKey":"items"}]}""",
    })
    {
        Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
            Object(invalidKeyContract),
            new JsonObject(),
            emptyAnimation,
            0));
    }
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"collections":[{"jsonKey":"items"},{"storageCollectionJsonKey":"items"}]}"""),
        new JsonObject(),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"collections":[{"jsonKey":"first"},{"jsonKey":"second"}]}"""),
        Object("""{"first":[{"id":"item"}],"second":[{"id":"item"}]}"""),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"inputs":[{"id":"value"},{"id":"value"}]}"""),
        new JsonObject(),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        Object("""{"collections":[{"jsonKey":"items","fields":[{"id":"value"},{"id":"value"}]}]}"""),
        new JsonObject(),
        emptyAnimation,
        0));

    var collection = Object("""
        {"collections":[{
          "jsonKey":"items",
          "animationTimeline":{"preDurationFieldIds":["delay"]},
          "fields":[{"id":"delay","jsonKey":"delay"}]
        }]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        collection,
        Object("""{"items":[null]}"""),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        collection,
        Object("""{"items":[{"id":"","delay":0}]}"""),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        collection,
        Object("""{"items":{}}"""),
        emptyAnimation,
        0));

    var wrongFields = Object("""
        {"collections":[{"jsonKey":"items","fields":{}}]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        wrongFields,
        Object("""{"items":[{"id":"item"}]}"""),
        emptyAnimation,
        0));

    var wrongItemActions = Object("""
        {"collections":[{"jsonKey":"items","itemActions":[null]}]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        wrongItemActions,
        Object("""{"items":[{"id":"item"}]}"""),
        emptyAnimation,
        0));

    var wrongTimeline = Object("""
        {"collections":[{"jsonKey":"items","animationTimeline":null}]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        wrongTimeline,
        Object("""{"items":[{"id":"item"}]}"""),
        emptyAnimation,
        0));

    var projected = Object("""
        {"collections":[{
          "jsonKey":"items",
          "itemRuntimeContractJsonKey":"runtimeContract"
        }]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        projected,
        Object("""{"items":[{"id":"item"}]}"""),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        projected,
        Object("""{"items":[{"id":"item","runtimeContract":{"inputs":null}}]}"""),
        emptyAnimation,
        0));

    var wrongTimelineLists = Object("""
        {"collections":[{
          "jsonKey":"items",
          "animationTimeline":{"postDurationFieldIds":["hold",4]},
          "fields":[{"id":"hold","jsonKey":"hold"}]
        }]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        wrongTimelineLists,
        Object("""{"items":[{"id":"item","hold":0}]}"""),
        emptyAnimation,
        0));

    foreach (var invalidTimelineContract in new[]
    {
        """{"collections":[{"jsonKey":"items","animationTimeline":{"sequence":"parallel"}}]}""",
        """{"collections":[{"jsonKey":"items","animationTimeline":{"sequenceItems":"false"}}]}""",
        """{"collections":[{"jsonKey":"items","animationTimeline":{"ownerOrigin":null}}]}""",
        """{"collections":[{"jsonKey":"items","animationTimeline":{"ownerOrigin":{"kind":"ownerStart"}}}]}""",
        """{"collections":[{"jsonKey":"items","animationTimeline":{"ownerOrigin":{"kind":"firstMatchingValue"}}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"extendsOwnerDuration":"false"}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"origin":null}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"origin":{"kind":"unknown"}}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"source"}}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"source","offsetFrames":-1}}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"completion":null}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"completion":{}}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"completion":{"baseDurationFieldId":"duration","trackOverride":"first"}}}]}""",
        """{"inputs":[{"id":"field","animationTimeline":{"completion":{"baseDurationFieldId":"duration","minimumEnabledKeyframes":1}}}]}""",
    })
    {
        Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
            Object(invalidTimelineContract),
            new JsonObject(),
            emptyAnimation,
            0));
    }

    foreach (var invalidActionContract in new[]
    {
        """{"actions":[{"definesModuleDuration":"true"}]}""",
        """{"actions":[{"definesModuleDuration":true,"durationBaseFrames":1}]}""",
        """{"actions":[{"id":"duration","definesModuleDuration":true,"durationBaseFrames":"1"}]}""",
        """{"collections":[{"jsonKey":"items","itemActions":[{"extendsModuleDuration":"true"}]}]}""",
        """{"collections":[{"jsonKey":"items","fields":[{"id":"play"}],"itemActions":[{"id":"play","extendsModuleDuration":true,"playInputId":"play","durationInputId":"duration"}]}]}""",
        """{"collections":[{"jsonKey":"items","fields":[{"id":"play"}],"itemActions":[{"id":"play","extendsModuleDuration":true,"playInputId":"play","playFieldId":"","durationInputId":"duration","durationEnabledInputId":"enabled"}]}]}""",
        """{"collections":[{"jsonKey":"items","fields":[{"id":"other"}],"itemActions":[{"id":"play","extendsModuleDuration":true,"playInputId":"play","durationInputId":"duration","durationEnabledInputId":"enabled"}]}]}""",
    })
    {
        Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
            Object(invalidActionContract),
            Object("""{"items":[{"id":"item","enabled":false}]}"""),
            emptyAnimation,
            0));
    }

    var finiteActionContract = Object("""
        {"collections":[{
          "jsonKey":"items",
          "fields":[
            {"id":"play"},
            {"id":"duration","jsonKey":"durationFrames"}
          ],
          "itemActions":[{
            "id":"play","extendsModuleDuration":true,"playInputId":"play",
            "durationInputId":"duration","durationEnabledInputId":"enabled"
          }]
        }]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        finiteActionContract,
        Object("""{"items":[{"id":"item","durationFrames":4}]}"""),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        finiteActionContract,
        Object("""{"items":[{"id":"item","enabled":"false","durationFrames":4}]}"""),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        finiteActionContract,
        Object("""{"items":[{"id":"item","enabled":false,"durationFrames":4}]}"""),
        Object("""{"tracks":[{"fieldId":"play","targetId":"item","keyframes":[{"frame":0,"value":"true"}]}]}"""),
        0));
    Equal(1, RuntimeAnimationFrameOrigin.DurationFrames(
        finiteActionContract,
        Object("""{"items":[{"id":"item","enabled":false}]}"""),
        emptyAnimation,
        0));

    var missingDurationField = Object("""
        {"collections":[{
          "jsonKey":"items",
          "fields":[{"id":"text","jsonKey":"text","animationTimeline":{
            "completion":{"baseDurationFieldId":"missing","minimumEnabledKeyframes":2}
          }}]
        }]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        missingDurationField,
        Object("""{"items":[{"id":"item","text":"value"}]}"""),
        emptyAnimation,
        0));

    var missingPreDurationValue = Object("""
        {"collections":[{
          "jsonKey":"items",
          "animationTimeline":{"preDurationFieldIds":["delay"]},
          "fields":[{"id":"delay","jsonKey":"delay"}]
        }]}
        """);
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        missingPreDurationValue,
        Object("""{"items":[{"id":"item"}]}"""),
        emptyAnimation,
        0));
    Throws<InvalidOperationException>(() => RuntimeAnimationFrameOrigin.DurationFrames(
        missingPreDurationValue,
        Object("""{"items":[{"id":"item","delay":"2"}]}"""),
        emptyAnimation,
        0));
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
    Throws<InvalidOperationException>(() => RuntimeDurationContract.Policy(
        "{\"animationTimeline\":null}"));
    Throws<InvalidOperationException>(() => RuntimeDurationContract.Policy(
        "{\"animationTimeline\":[]}"));
    Throws<InvalidOperationException>(() => RuntimeDurationContract.Policy(
        "{\"animationTimeline\":{\"durationPolicy\":4}}"));
    Throws<InvalidOperationException>(() => RuntimeDurationContract.InitialDurationFrames(
        "{\"animationTimeline\":{\"durationPolicy\":\"explicit\",\"defaultDurationFrames\":\"240\"}}"));
    Throws<InvalidOperationException>(() => RuntimeDurationContract.InitialDurationFrames(
        "{\"animationTimeline\":{\"durationPolicy\":\"explicit\",\"defaultDurationFrames\":1.5}}"));
    Throws<InvalidOperationException>(() => RuntimeDurationContract.InitialDurationFrames(
        "{\"animationTimeline\":{\"durationPolicy\":\"explicit\",\"defaultDurationFrames\":0}}"));
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
    var runtime = Object("""{"messages":[{"id":"m1","delay":0,"write":2,"hold":1,"isPlaying":false,"playDuration":5},{"id":"m2","delay":3,"write":1,"hold":0,"isPlaying":false,"playDuration":1}]}""");
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
          "fields":[
            {"id":"isPlaying","jsonKey":"isPlaying","animationTimeline":{"origin":{"kind":"ownerStart"}}},
            {"id":"playDuration","jsonKey":"playDurationFrames"}
          ],
          "itemActions":[{"id":"play","extendsModuleDuration":true,"playInputId":"isPlaying","durationInputId":"playDuration","durationEnabledInputId":"isPlaying"}]
        }]}
        """;
    var runtime = """{"messages":[{"id":"m1","isPlaying":false,"playDurationFrames":3}]}""";
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
          {"id":"a","jsonKey":"a","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"b","offsetFrames":0}}},
          {"id":"b","jsonKey":"b","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"a","offsetFrames":0}}}
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
            {"id":"status","jsonKey":"status","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"text","offsetFrames":0},"extendsOwnerDuration":false}}
          ]
        }]}
        """);
    var runtime = Object("""{"messages":[{"id":"m1","delay":0,"write":2},{"id":"m2","delay":3,"write":1}]}""");
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
          {"id":"a","fieldId":"text","targetId":"m1","keyframes":[{"id":"a0","frame":0,"value":"a","interpolation":"hold","enabled":true}]},
          {"id":"b","fieldId":"text","targetId":"m1","keyframes":[{"id":"b0","frame":0,"value":"b","interpolation":"hold","enabled":true}]}
        ]}
        """);
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(animation, "Test animation_json"));
}

static void StrictValidationRejectsInvalidFrames()
{
    var duplicate = Object("""
        {"schemaVersion":2,"tracks":[{"id":"a","fieldId":"text","keyframes":[
          {"id":"k0","frame":0,"value":"a","interpolation":"hold","enabled":true},{"id":"k1","frame":0,"value":"b","interpolation":"hold","enabled":true}
        ]}]}
        """);
    var negative = Object("""
        {"schemaVersion":2,"tracks":[{"id":"a","fieldId":"text","keyframes":[
          {"id":"k0","frame":-1,"value":"a","interpolation":"hold","enabled":true}
        ]}]}
        """);
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(duplicate, "Test animation_json"));
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(negative, "Test animation_json"));
}

static void StrictValidationRejectsMalformedEntriesAndOrder()
{
    var malformedTrack = Object("""{"schemaVersion":2,"tracks":[4]}""");
    var malformedKeyframe = Object("""
        {"schemaVersion":2,"tracks":[{"id":"t","fieldId":"text","keyframes":[4]}]}
        """);
    var unsorted = Object("""
        {"schemaVersion":2,"tracks":[{"id":"t","fieldId":"text","keyframes":[
          {"id":"k2","frame":2,"value":"b","interpolation":"hold","enabled":true},
          {"id":"k0","frame":0,"value":"a","interpolation":"hold","enabled":true}
        ]}]}
        """);
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(malformedTrack, "Test animation_json"));
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(malformedKeyframe, "Test animation_json"));
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(unsorted, "Test animation_json"));
}

static void StrictValidationRejectsInvalidTargetDurations()
{
    var animation = Object("""
        {"schemaVersion":2,"retime":{"targetDurationFrames":0},"tracks":[]}
        """);
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(animation, "Test animation_json"));
}

static void StrictValidationRejectsMissingOrigin()
{
    var animation = Object("""{"schemaVersion":2,"tracks":[{"id":"track","fieldId":"text","targetId":"m1","keyframes":[]}]}""");
    Throws<InvalidOperationException>(() =>
        ModuleInstanceAnimationDocumentContract.Validate(animation, "Test animation_json"));
}

static void LegacyAnimationRequiresExplicitMigration()
{
    var source = ParityDatabasePath();
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
        Throws<InvalidOperationException>(() => _ = new SqliteProjectTestContext(temporary));
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
    var source = ParityDatabasePath();
    var database = new SqliteProjectTestContext(source);
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

static void RuntimeActionControlsReactivateAfterPlaybackAndReattachment()
{
    using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApplication));
    session.Dispatch(() =>
    {
        var playbackState = new PreviewPlaybackState();
        var canRestore = false;
        var currentFrame = 0;
        var control = new RuntimeTestActionControl(
            "Test Action",
            (_) => { },
            () => { },
            () => canRestore,
            (_, delta) => currentFrame = Math.Clamp(currentFrame + delta, 0, 1),
            (delta) => delta < 0 ? currentFrame > 0 : currentFrame < 1,
            playbackState);
        var window = new Window
        {
            Width = 320,
            Height = 120,
            Content = control,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button ActionButton(string accessibleName) => control.GetVisualDescendants()
            .OfType<Button>()
            .Single((button) => ToolTip.GetTip(button) as string == accessibleName);

        var play = ActionButton("Play Test Action");
        var restore = ActionButton("Restore Test Action");
        var previous = ActionButton("Previous frame · Test Action");
        var next = ActionButton("Next frame · Test Action");
        True(play.IsEnabled);
        True(!restore.IsEnabled);
        True(!previous.IsEnabled);
        True(next.IsEnabled);

        playbackState.SetBusy(true);
        Dispatcher.UIThread.RunJobs();
        True(!play.IsEnabled);
        True(!restore.IsEnabled);
        True(!previous.IsEnabled);
        True(!next.IsEnabled);

        canRestore = true;
        playbackState.SetBusy(false);
        Dispatcher.UIThread.RunJobs();
        True(play.IsEnabled);
        True(restore.IsEnabled);
        True(!previous.IsEnabled);
        True(next.IsEnabled);

        next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        True(previous.IsEnabled);
        True(!next.IsEnabled);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        playbackState.SetBusy(true);
        window.Content = control;
        Dispatcher.UIThread.RunJobs();
        True(!play.IsEnabled);

        playbackState.SetBusy(false);
        Dispatcher.UIThread.RunJobs();
        True(play.IsEnabled);
        True(restore.IsEnabled);
        True(previous.IsEnabled);
        True(!next.IsEnabled);
        window.Close();
    }, CancellationToken.None).GetAwaiter().GetResult();
}

static void PreviewPreparationCancellationRetainsLatestOperation()
{
    using var cancellation = new PreviewPreparationCancellation();
    var first = cancellation.Begin();
    var firstToken = first.Token;
    True(cancellation.IsCurrent(first));
    True(!firstToken.IsCancellationRequested);

    var second = cancellation.Begin();
    var secondToken = second.Token;
    True(firstToken.IsCancellationRequested);
    True(!cancellation.IsCurrent(first));
    True(cancellation.IsCurrent(second));
    True(!secondToken.IsCancellationRequested);
    True(!cancellation.Complete(first));

    cancellation.Cancel();
    True(secondToken.IsCancellationRequested);
    True(cancellation.Complete(second));
}

static void PreparedPlaybackReuseRequiresExactSignature()
{
    Equal(
        PreparedPlaybackReuse.Complete,
        PreparedPlaybackReusePolicy.Decide("exact", "exact", hasFrameCacheReservation: true));
    Equal(
        PreparedPlaybackReuse.Frames,
        PreparedPlaybackReusePolicy.Decide("exact", "exact", hasFrameCacheReservation: false));
    Equal(
        PreparedPlaybackReuse.None,
        PreparedPlaybackReusePolicy.Decide("stale", "exact", hasFrameCacheReservation: true));
    Equal(
        PreparedPlaybackReuse.None,
        PreparedPlaybackReusePolicy.Decide(null, "exact", hasFrameCacheReservation: true));
}

static void PreparedPlaybackOwnersRetainCombinedFrameCache()
{
    var capacity =
        typeof(WebDesignPreviewRenderer)
            .GetField(
                "_frameCacheCapacity",
                BindingFlags.Static
                | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Missing Preview frame-cache capacity owner.");

    Equal(
        180,
        (int)Required(
            capacity.GetValue(null)));
    using (var first =
           WebDesignPreviewRenderer
               .ReserveFrameCacheCapacity(
                   181))
    {
        Equal(
            181,
            (int)Required(
                capacity.GetValue(null)));
        using (var second =
               WebDesignPreviewRenderer
                   .ReserveFrameCacheCapacity(
                       182))
        {
            Equal(
                363,
                (int)Required(
                    capacity.GetValue(null)));
        }
        Equal(
            181,
            (int)Required(
                capacity.GetValue(null)));
    }
    Equal(
        180,
        (int)Required(
            capacity.GetValue(null)));
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
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-animation-reorder-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var before = CollectionOrder(temporary);
        True(before.ItemIds.Count >= 2);
        var database = new SqliteProjectTestContext(temporary);
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
        ProjectTreeNodeKind.ComponentVariant, "variant", "Default", "", "component.audio", componentClass);
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

    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-definition-lifecycle-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
    selection.RememberComponentVariantSelection(androidVariant);
    Equal(androidVariant.Id, selection.ResolveSelectionNode(module).Id);
    True(module.CanRenameDirectly);
    True(EditorNavigationRenderer.ShowsActions(module, androidVariant));
}

static void OnlyDefaultSystemBarVariantsAreProtected()
{
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-system-variants-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        foreach (var componentType in new[] { "status_bar", "navigation_bar" })
        {
            var componentClass = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
                && database.GetComponentClassSettings(node.Id).ComponentType == componentType);
            var variants = componentClass.Children
                .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant)
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
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-component-stack-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var stack = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "componentStack");
        Equal("Atoms", stack.Parent?.Name ?? "");
        var defaultVariant = stack.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant && node.IsLocked);
        var settings = database.GetComponentClassSettings(stack.Id);
        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject ?? throw new InvalidOperationException("Missing Component Stack config.");
        var stackConfig = config["componentStack"] as JsonObject ?? throw new InvalidOperationException("Missing Component Stack contract.");
        True(!stackConfig.ContainsKey("order"));
        True(!stackConfig.ContainsKey("slots"));
        Equal(0, stackConfig.Count);
        var designPreview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject ?? throw new InvalidOperationException("Missing Component Stack Runtime Inputs.");
        True(designPreview["items"] is JsonArray);
        var runtimeInputs = RuntimeInputDefinitionReader.ReadInputs(designPreview, config);
        SequenceEqual(["sizingMode", "startGapToken", "endGapToken"], runtimeInputs.Select((input) => input.Id).ToList());
        Equal("fill", runtimeInputs[0].DefaultValue);
        Equal("theme.spacing.none", runtimeInputs[1].DefaultValue);
        Equal("theme.spacing.none", runtimeInputs[2].DefaultValue);
        var collections = designPreview["collections"] as JsonArray ?? throw new InvalidOperationException("Missing Component Stack collection contract.");
        var slotCollection = collections.OfType<JsonObject>().Single();
        Equal("items", slotCollection["jsonKey"]?.GetValue<string>() ?? "");
        Equal(false, slotCollection["animationTimeline"]?["sequenceItems"]?.GetValue<bool>() ?? true);
        var runtimeCollection = RuntimeInputDefinitionReader.ReadCollections(designPreview, config).Single();
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
        var componentOptions = database.GetComponentVariantReferenceOptions(settings.ProjectId, "*,-componentStack");
        True(componentOptions.All((option) => !option.Value.StartsWith(stack.Id + "::variant::", StringComparison.Ordinal)));
        True(componentOptions.All((option) => !string.IsNullOrWhiteSpace(option.GroupValue)));
        True(componentOptions.GroupBy((option) => option.GroupValue)
            .All((group) => group.Any((option) => option.Value == $"{group.Key}::variant::default")));
        _ = database.ReferenceUsages.GetReferenceUsageDetails(stack);
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(CreatePreviewPayload(database, defaultVariant, theme.Id));
        var refreshCount = 0;
        var inputSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => refreshCount++);
        inputSession.UpdateForPayload(payload, settings.ProjectId);
        var resolvedPayload = inputSession.ApplyInputs(payload, "light", settings.ProjectId);
        var resolvedPreview = DesignPreviewTestValues.Parse(resolvedPayload.DesignPreviewJson);
        True(resolvedPreview["items"]?[1]?["alternatives"]?[0]?["inputs"]?["showBadge"]?.GetValue<bool>() == true);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            false,
            resolvedPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(html));
        True(!html.Contains("preview-error", StringComparison.Ordinal));

        var childVariant = database.GetComponentVariantReferenceOptionsByType(settings.ProjectId, "audio").First().Value;
        var audioInputs = database.GetComponentVariantRuntimeInputs(childVariant);
        True(audioInputs["showBadge"] is JsonValue);
        Equal("icon", audioInputs["badgeContentMode"]?.GetValue<string>() ?? "");
        True(RuntimeInputFieldDefinitionFactory.Create(
            new RuntimeInputOptionsDataSource(database.DictionaryContext, database.Resources),
            defaultVariant,
            alternatives.Fields.Single((field) => field.Id == "variantReference")).SelectComponentClass);
        var runtimeItem = new JsonObject
        {
            ["id"] = "test_button",
            ["alternatives"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "test_button_default",
                    ["variantReference"] = childVariant,
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
        inputSession.SetExternalCollectionItems(payload, "items", [runtimeItem]);
        Equal(1, refreshCount);
        var childVariantNode = nodes.Single((node) => node.Id == childVariant);
        True(!database.CreateComponentVariantFieldValue(
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
            false,
            transientPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(transientHtml));
        True(!transientHtml.Contains("preview-error", StringComparison.Ordinal));

        var selectedComponent = database.GetComponentVariantSelectionSettings(childVariant);
        var overrides = new JsonObject();
        var runtimeOverrideChanges = 0;
        var embeddedDocuments = new EmbeddedComponentDocumentStore(
            ComponentDocuments(database));
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
                (_) =>
                {
                    runtimeOverrideChanges++;
                    return Task.CompletedTask;
                }));
        Equal(selectedComponent.RecordClassId, runtimeContext.RecordClassId);
        Equal(selectedComponent.ComponentType, runtimeContext.ComponentType);
        True(embeddedDocuments.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);
        embeddedDocuments.CommitFieldValueAsync(
                runtimeContext,
                "component.audio.padding",
                "theme.spacing.xl|theme.spacing.l")
            .GetAwaiter()
            .GetResult();
        Equal(1, runtimeOverrideChanges);
        True(!embeddedDocuments.CreateFieldValue(runtimeContext, "component.audio.padding").IsInherited);
        embeddedDocuments.CommitFieldValueAsync(
                runtimeContext,
                "component.audio.padding",
                "inherited")
            .GetAwaiter()
            .GetResult();
        Equal(2, runtimeOverrideChanges);
        var surfaceSlot = EmbeddedComponentSlotCatalog.Get("component.audio.surface.editor");
        var badgeSlot = EmbeddedComponentSlotCatalog.Get("component.audio.badge.editor");
        Equal("badge", badgeSlot.EmbeddedComponentType);
        Equal("component.badge", badgeSlot.RecordClassId);
        var nestedRuntimeContext = runtimeContext.Nested(surfaceSlot);
        Equal(surfaceSlot.RecordClassId, nestedRuntimeContext.RecordClassId);
        Equal(surfaceSlot.EmbeddedComponentType, nestedRuntimeContext.ComponentType);
        var nestedFieldId = EditorLayouts(database).LoadEditorLayout(surfaceSlot.RecordClassId).Cards
            .Where((card) => card.Visible)
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .Select((field) => field.Id)
            .First(ComponentClassFieldCatalog.IsRuntimeOverrideField);
        _ = embeddedDocuments.CreateFieldValue(nestedRuntimeContext, nestedFieldId);
        var avatarVariant = database.GetComponentVariantReferenceOptionsByType(settings.ProjectId, "avatar").First().Value;
        var avatarSelection = database.GetComponentVariantSelectionSettings(avatarVariant);
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
                (_) => Task.CompletedTask));
        foreach (var avatarFieldId in EditorLayouts(database).LoadEditorLayout(avatarSelection.RecordClassId).Cards
                     .Where((card) => card.Visible)
                     .SelectMany((card) => card.VisibleGroups)
                     .SelectMany((group) => group.VisibleFields)
                     .Select((field) => field.Id)
                     .Where(ComponentClassFieldCatalog.IsRuntimeOverrideField)
                     .Distinct(StringComparer.Ordinal))
        {
            _ = embeddedDocuments.CreateFieldValue(avatarContext, avatarFieldId);
        }
        var selectedLayout = EditorLayouts(database).LoadEditorLayout(selectedComponent.RecordClassId);
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
        var populatedInputSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
        populatedInputSession.UpdateForPayload(populatedPayloadSource, settings.ProjectId);
        var populatedPayload = populatedInputSession.ApplyInputs(populatedPayloadSource, "light", settings.ProjectId);
        var populatedPreview = DesignPreviewTestValues.Parse(populatedPayload.DesignPreviewJson);
        True(populatedPreview["items"]?[0]?["alternatives"]?[0]?["inputs"]?["showBadge"] is JsonValue);
        var populatedHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
            false,
            populatedPayload).GetAwaiter().GetResult();
        True(!string.IsNullOrWhiteSpace(populatedHtml));
        True(!populatedHtml.Contains("preview-error", StringComparison.Ordinal));
        var reopened = new SqliteProjectTestContext(temporary);
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
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-collection-stack-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var stack = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "collectionStack");
        Equal("Atoms", stack.Parent?.Name ?? "");
        var variants = stack.Children.Where((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant).ToList();
        Equal(1, variants.Count);
        Equal("Default", variants[0].Name);
        True(variants[0].IsLocked);

        var settings = database.GetComponentClassSettings(stack.Id);
        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Collection Stack config.");
        Equal(0, (config["collectionStack"] as JsonObject)?.Count ?? -1);
        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Collection Stack preview.");
        var runtimeInputs = RuntimeInputDefinitionReader.ReadInputs(preview, config);
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
        var collection = RuntimeInputDefinitionReader.ReadCollections(preview, config).Single();
        Equal("items", collection.JsonKey);
        Equal("*,-collectionStack", collection.Fields.Single((field) => field.Id == "variantReference").ComponentType);

        var componentOptions = database.GetComponentVariantReferenceOptions(settings.ProjectId, "*,-collectionStack");
        True(componentOptions.All((option) => !option.Value.StartsWith(stack.Id + "::variant::", StringComparison.Ordinal)));
        True(componentOptions.Any((option) => option.GroupValue.EndsWith("componentStack", StringComparison.Ordinal)));

        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(CreatePreviewPayload(database, variants[0], theme.Id));
        var inputSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
        inputSession.UpdateForPayload(payload, settings.ProjectId);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
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
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-notifications-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
        var avatarLayout = EditorLayouts(database).LoadEditorLayout("component.avatar");
        SequenceEqual(
            ["component.avatar.badge.editor", "component.avatar.badge.placement"],
            avatarLayout.Cards.Single((card) => card.Id == "avatar").VisibleGroups
                .Single((group) => group.Id == "avatarBadge").VisibleFields
                .OrderBy((field) => field.Order)
                .Select((field) => field.Id)
                .ToList());
        var notificationVariant = notification.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
        var notificationsVariant = notifications.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant);
        var notificationLayout = EditorLayouts(database).LoadEditorLayout("component.notification");
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
        True(!notificationsCollectionFields.Overlaps(["variantReference", "presenceMotion", "alignment", "gapBeforeMode", "gapBeforeToken", "gapBeforeWeight"]));
        var notificationsLayout = EditorLayouts(database).LoadEditorLayout("component.notifications");
        SequenceEqual(["general", "layout"], notificationsLayout.Cards.OrderBy((card) => card.Order).Select((card) => card.Id).ToList());
        SequenceEqual(
            ["stack", "notification", "badge", "motion"],
            notificationsLayout.Cards.Single((card) => card.Id == "layout").VisibleGroups.OrderBy((group) => group.Order).Select((group) => group.Id).ToList());

        foreach (var variant in new[] { notificationVariant, notificationsVariant })
        {
            var payload = Required(CreatePreviewPayload(database, variant, theme.Id));
            var inputSession = new ComponentPreviewInputSession(
                database.Design,
                database.DictionaryContext,
                database.Resources,
                database.ProjectPaths,
                () => { });
            inputSession.UpdateForPayload(payload, database.GetComponentClassSettings(variant.Parent!.Id).ProjectId);
            var html = WebDesignPreviewRenderer.RenderBodyAsync(
                database.GetDevicePreviewMetrics(device.Id), false,
                inputSession.ApplyInputs(payload, "light", database.GetComponentClassSettings(variant.Parent.Id).ProjectId)).GetAwaiter().GetResult();
            True(!html.Contains("preview-error", StringComparison.Ordinal));
        }

        var transitionPayload = Required(CreatePreviewPayload(database, notificationVariant, theme.Id));
        var transitionPreview = JsonNode.Parse(transitionPayload.DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing Notification transition preview.");
        var transitionAction = ComponentPreviewActions.ReadWithEmbedded(
                transitionPreview,
                new ComponentPreviewInputDataSource(database.Design, database.Resources).ComponentVariantRuntimeContract)
            .Single((action) => action.Id == "changeDisplayMode");
        var transitionSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
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
        var wrappingSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
        wrappingSession.UpdateForPayload(wrappingPayload, wrappingSettings.ProjectId);
        var wrappingHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id), false,
            wrappingSession.ApplyInputs(wrappingPayload, "light", wrappingSettings.ProjectId)).GetAwaiter().GetResult();
        True(wrappingHtml.Contains("component.notification.label.text.1", StringComparison.Ordinal));
        database.UpdateComponentClassDesignPreviewJson(notification.Id, wrappingSettings.DesignPreviewJson);

        var settings = database.GetComponentClassSettings(notifications.Id);
        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Notifications preview.");
        var reference = notificationVariant.Id;
        var notificationInputs = database.GetComponentVariantRuntimeInputs(reference);
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
        var populatedSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
        populatedSession.UpdateForPayload(populated, settings.ProjectId);
        var populatedContract = JsonNode.Parse(populated.DesignPreviewJson)?.AsObject()
            ?? throw new InvalidOperationException("Missing populated Notifications contract.");
        var embeddedActions = ComponentPreviewActions.ReadWithEmbedded(
                populatedContract,
                new ComponentPreviewInputDataSource(database.Design, database.Resources).ComponentVariantRuntimeContract)
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
            database.GetDevicePreviewMetrics(device.Id), false,
            populatedSession.ApplyInputs(populated, "light", settings.ProjectId)).GetAwaiter().GetResult();
        if (populatedHtml.Contains("preview-error", StringComparison.Ordinal))
            throw new InvalidOperationException("Stacked Notifications preview failed.");
        if (!populatedHtml.Contains("component.notifications.badge", StringComparison.Ordinal))
            throw new InvalidOperationException("Stacked Notifications preview omitted its Badge.");
        populatedSession.SetExternalInputValue("distributionMode", "flow");
        var flowHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id), false,
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
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-keypad-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var keypad = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "keypad");
        Equal("System", keypad.Parent?.Name ?? "");
        var defaultVariant = keypad.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant && node.IsProtected);
        var settings = database.GetComponentClassSettings(keypad.Id);
        var layout = EditorLayouts(database).LoadEditorLayout("component.keypad");
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
        var keysField = database.CreateComponentVariantFieldValue(defaultVariant, "component.keypad.keys");
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
            RuntimeInputDefinitionReader.ReadInputs(preview, config).Select((input) => input.Id).ToList());
        var theme = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Theme);
        var device = nodes.First((node) => node.Kind == ProjectTreeNodeKind.Device);
        var payload = Required(CreatePreviewPayload(database, defaultVariant, theme.Id));
        var inputSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
        inputSession.UpdateForPayload(payload, settings.ProjectId);
        inputSession.SetExternalInputValue("activeKey", "5");
        inputSession.SetExternalInputValue("pushedKey", "5");
        var resolvedPayload = inputSession.ApplyInputs(payload, "light", settings.ProjectId);
        var html = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
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
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-password-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = database.LoadProjectTree().SelectMany(DescendantsAndSelf).ToList();
        var indicator = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "codeIndicator");
        Equal("Atoms", indicator.Parent?.Name ?? "");
        var password = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
            && database.GetComponentClassSettings(node.Id).ComponentType == "password");
        Equal("System", password.Parent?.Name ?? "");
        var defaultVariant = password.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant && node.IsProtected);
        var settings = database.GetComponentClassSettings(password.Id);
        var layout = EditorLayouts(database).LoadEditorLayout("component.password");
        SequenceEqual(["general", "layout", "labels", "indicator", "modes", "iconBar"],
            layout.Cards.OrderBy((card) => card.Order).Select((card) => card.Id).ToList());
        Equal("verticalCards", layout.Cards.Single((card) => card.Id == "labels").GroupLayout);
        Equal("verticalCards", layout.Cards.Single((card) => card.Id == "modes").GroupLayout);

        var config = JsonNode.Parse(settings.ConfigJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Password config.");
        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Password preview.");
        var runtimeInputs = RuntimeInputDefinitionReader.ReadInputs(preview, config);
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
        var inputSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
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
        True(inputSession.StopActivePlayback());
        True(!inputSession.IsPlaybackActive);
        True(!playbackBusy);
        True(!inputSession.StopActivePlayback());
        True(inputSession.ResetCurrentTestValues());

        foreach (var componentType in new[] { "fingerprint", "faceRecognition", "drawPassword" })
        {
            var component = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentClass
                && database.GetComponentClassSettings(node.Id).ComponentType == componentType);
            Equal("System", component.Parent?.Name ?? "");
            var componentVariant = component.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant && node.IsProtected);
            var componentPayload = Required(CreatePreviewPayload(database, componentVariant, theme.Id));
            var componentHtml = WebDesignPreviewRenderer.RenderBodyAsync(
                database.GetDevicePreviewMetrics(device.Id),
                false,
                componentPayload).GetAwaiter().GetResult();
            True(!componentHtml.Contains("preview-error", StringComparison.Ordinal));
        }

        foreach (var mode in new[] { "fingerprint", "faceRecognition", "drawPassword" })
        {
            var variant = password.Children.Single((node) => node.Kind == ProjectTreeNodeKind.ComponentVariant && node.Id.EndsWith($"::variant::{mode}", StringComparison.Ordinal));
            var modePayload = Required(CreatePreviewPayload(database, variant, theme.Id));
            var modePreview = JsonNode.Parse(modePayload.DesignPreviewJson)?.AsObject()
                ?? throw new InvalidOperationException($"Missing {mode} Password preview.");
            modePreview["entryTrigger"] = true;
            modePreview["entryFrame"] = 16;
            modePayload = modePayload with { DesignPreviewJson = modePreview.ToJsonString() };
            var modeHtml = WebDesignPreviewRenderer.RenderBodyAsync(
                database.GetDevicePreviewMetrics(device.Id),
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
    var source = ParityDatabasePath();
    var temporary = Path.Combine(Directory.GetCurrentDirectory(), "data", $".mockups-lock-screen-stack-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
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
        True(EditorLayouts(database).LoadEditorLayout("app.system").Cards
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
        True((stackSlot["variantReference"]?.GetValue<string>() ?? "").Contains("::variant::default", StringComparison.Ordinal));
        True(stackSlot["overrides"] is JsonObject);
        True(lockScreen["stackVariant"] is null);
        True(lockScreen["statusBarVariant"] is null);
        True(lockScreen["navigationBarVariant"] is null);

        var preview = JsonNode.Parse(settings.DesignPreviewJson) as JsonObject
            ?? throw new InvalidOperationException("Missing Lock Screen Runtime Inputs.");
        Equal("explicit", preview["animationTimeline"]?["durationPolicy"]?.GetValue<string>() ?? "");
        Equal(240, preview["animationTimeline"]?["defaultDurationFrames"]?.GetValue<int>() ?? 0);
        var inputs = RuntimeInputDefinitionReader.ReadInputs(preview, config);
        SequenceEqual(
            ["actor", "showStatusBar", "showNavigationBar"],
            inputs.Take(3).Select((input) => input.Id).ToList());
        Equal("true", DesignPreviewTestValues.Value(preview, inputs.Single((input) => input.Id == "showStatusBar")));
        Equal("true", DesignPreviewTestValues.Value(preview, inputs.Single((input) => input.Id == "showNavigationBar")));
        Equal(0, RuntimeInputDefinitionReader.ReadCollections(preview, config).Count);
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
        var lockScreenFields = EditorLayouts(database).LoadEditorLayout("module.core.lockScreen").Cards
            .SelectMany((card) => card.VisibleGroups)
            .SelectMany((group) => group.VisibleFields)
            .Select((field) => field.Id)
            .ToHashSet(StringComparer.Ordinal);
        True(lockScreenFields.Contains("module.lockScreen.stackInputs"));
        True(lockScreenFields.Contains("module.lockScreen.stackItems"));

        var lockScreenInstance = nodes.Single((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance
            && database.GetModuleInstanceSettings(node.Id).ModuleId == module.Id);
        var values = new RecordClassFieldValueService(
            ProductionRecordFields(database),
            DesignRecordFields(database),
            ResourceRecordFields(database),
            database.Production,
            database.Resources);
        True(values.CreateFieldValue(lockScreenInstance, "moduleInstance.durationFrames").Definition.IsEditable);
        Equal(
            240,
            ModuleInstanceTimeline.DurationFrames(
                new ModuleInstanceTimelineDataSource(
                    database.Production,
                    database.Resources),
                lockScreenInstance.Id));
        database.UpdateModuleInstanceField(lockScreenInstance.Id, "moduleInstance.durationFrames", "180");
        Equal(
            180,
            ModuleInstanceTimeline.DurationFrames(
                new ModuleInstanceTimelineDataSource(
                    database.Production,
                    database.Resources),
                lockScreenInstance.Id));

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
            .Single((candidate) => (candidate.State["variantReference"]?.GetValue<string>() ?? "")
                .Contains("_password::variant::", StringComparison.Ordinal));
        var passwordSlotId = passwordState.Slot["id"]?.GetValue<string>() ?? "";
        var passwordStateId = passwordState.State["id"]?.GetValue<string>() ?? "";
        var instancePreview = DesignPreviewTestValues.Parse(
            database.GetModuleInstanceRuntimePreviewJson(lockScreenInstance.Id));
        var stateInputsCollection = RuntimeInputDefinitionReader
            .ReadCollections(instancePreview, instanceVariantConfig)
            .Single((collection) => collection.Id == "stackStateInputs");
        var projectedPasswordState = DesignPreviewTestValues
            .CollectionItems(instancePreview, stateInputsCollection)
            .Single((state) => state["id"]?.GetValue<string>() == passwordStateId);
        var projectedPasswordInputs = projectedPasswordState["inputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing projected Password runtime inputs.");
        var passwordEntryTrigger = RuntimeInputDefinitionReader
            .ReadInputs(projectedPasswordInputs, new JsonObject())
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
            timelineFrame: ModuleInstanceTimeline.ScreenStartFrame(
                new ModuleInstanceTimelineDataSource(
                    database.Production,
                    database.Resources),
                lockScreenInstance.Id) + 30));
        var passwordFrameHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
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
            timelineFrame: ModuleInstanceTimeline.ScreenStartFrame(
                new ModuleInstanceTimelineDataSource(
                    database.Production,
                    database.Resources),
                lockScreenInstance.Id) + 40));
        var completedPasswordHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
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
        var session = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
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
            false,
            resolved).GetAwaiter().GetResult();
        True(!html.Contains("preview-error", StringComparison.Ordinal));

        var childVariant = database.GetComponentVariantReferenceOptionsByType(settings.ProjectId, "label").First().Value;
        var childInputs = database.GetComponentVariantRuntimeInputs(childVariant);
        var subtitleBinding = database.GetComponentVariantRuntimeInputBindings(childVariant)
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
                        ["variantReference"] = childVariant,
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
        var populatedSession = new ComponentPreviewInputSession(
            database.Design,
            database.DictionaryContext,
            database.Resources,
            database.ProjectPaths,
            () => { });
        populatedSession.UpdateForPayload(populatedPayload, settings.ProjectId);
        var populatedPreview = JsonNode.Parse(populatedPayload.DesignPreviewJson) as JsonObject ?? new JsonObject();
        var populatedConfig = JsonNode.Parse(populatedPayload.ConfigJson) as JsonObject ?? new JsonObject();
        var populatedCollections = RuntimeInputDefinitionReader.ReadCollections(populatedPreview, populatedConfig);
        var populatedStateInputs = populatedCollections.Single((collection) => collection.Id == "stackStateInputs");
        var populatedStateItems = DesignPreviewTestValues.CollectionItems(populatedPreview, populatedStateInputs)
            .Select((item) => item.DeepClone() as JsonObject ?? new JsonObject())
            .ToList();
        var populatedState = populatedStateItems.Single((item) => item["id"]?.GetValue<string>() == "lock_screen_label_default");
        var populatedStateContract = populatedState["inputs"] as JsonObject
            ?? throw new InvalidOperationException("Missing populated State runtime contract.");
        var populatedInputs = RuntimeInputDefinitionReader.ReadInputs(populatedStateContract, new JsonObject());
        var forwardedSubtitle = populatedInputs.Single((input) => input.Label == "Lock subtitle");
        populatedStateContract[forwardedSubtitle.JsonKey] = "Forwarded subtitle";
        populatedSession.SetExternalCollectionItems(
            populatedPayload,
            populatedStateInputs.JsonKey,
            populatedStateItems);
        populatedSession.SetExternalInputValue("showStatusBar", "false");
        populatedSession.SetExternalInputValue("showNavigationBar", "false");
        var populated = populatedSession.ApplyInputs(populatedPayload, "light", settings.ProjectId);
        var populatedHtml = WebDesignPreviewRenderer.RenderBodyAsync(
            database.GetDevicePreviewMetrics(device.Id),
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
    var collection = RuntimeInputDefinitionReader.ReadCollections(preview, new JsonObject()).Single();
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

static void LifecycleActionsStayConsistentAcrossNavigationAndEditors()
{
    var episode = new ProjectTreeNode(ProjectTreeNodeKind.Episode, "episode", "Episode", "", "episode");
    var shot = new ProjectTreeNode(ProjectTreeNodeKind.Shot, "shot", "Shot", "", "shot", episode);
    var screen = new ProjectTreeNode(
        ProjectTreeNodeKind.ModuleInstance,
        "screen",
        "Screen",
        "",
        "module_instance",
        shot);

    True(shot.CanAddChild);
    True(episode.CanRenameDirectly);
    True(shot.CanRenameDirectly);
    True(screen.CanRenameDirectly);
    True(screen.CanDuplicate);
    True(screen.CanDelete);

    var source = ParityDatabasePath();
    var temporary = Path.Combine(Path.GetTempPath(), $"mockups-lifecycle-consistency-{Guid.NewGuid():N}.sqlite");
    File.Copy(source, temporary, overwrite: true);
    try
    {
        var database = new SqliteProjectTestContext(temporary);
        var nodes = Descendants(database.LoadProjectTree()).ToList();
        var currentScreen = nodes.First((node) => node.Kind == ProjectTreeNodeKind.ModuleInstance);
        var originalName = currentScreen.Name;
        var editorName = $"{originalName} editor rename";
        var coreFields = new CoreFieldValueService(
            CoreFields(database));

        True(coreFields.CreateFieldValue(currentScreen, "core.name").Definition.IsEditable);
        coreFields.CommitFieldValue(currentScreen, "core.name", editorName);
        Equal(editorName, Descendants(database.LoadProjectTree()).Single((node) => node.Id == currentScreen.Id).Name);
        database.RenameDirectNode(
            Descendants(database.LoadProjectTree()).Single((node) => node.Id == currentScreen.Id),
            originalName);

        var componentRecordClasses = nodes
            .Where((node) => node.Kind == ProjectTreeNodeKind.ComponentClass)
            .Select((node) => node.RecordClassId)
            .Distinct(StringComparer.Ordinal);
        foreach (var recordClassId in componentRecordClasses)
        {
            var layout = EditorLayouts(database).LoadEditorLayout(recordClassId);
            True(layout.Cards
                .SelectMany((card) => card.Groups)
                .SelectMany((group) => group.Fields)
                .Any((field) => field.Id == "core.name"));
        }
    }
    finally
    {
        File.Delete(temporary);
    }
}

static void NaturalBehaviorTimingUsesGraphemesAndThemePace()
{
    var dictionaryDefinition = new FieldDefinition(
        "test.timing",
        "Timing",
        ValueKind.BehaviorTiming,
        BehaviorTiming: new BehaviorTimingDefinition("text", "grapheme", 7));
    Throws<InvalidOperationException>(() => new DictionaryBehaviorTimingControl(
        dictionaryDefinition,
        "{\"mode\":\"natural\",\"fixedFrames\":12,\"paceToken\":\"theme.motion.naturalPace.normal\"}",
        showThemeTokenPicker: null,
        resolveFrames: null));

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
    Throws<InvalidOperationException>(() => BehaviorTimingResolver.ResolveNaturalFrames(
        "text",
        "grapheme",
        0,
        "theme.motion.naturalPace.slow",
        theme));
    Throws<InvalidOperationException>(() => BehaviorTimingResolver.ResolveNaturalFrames(
        "text",
        "grapheme",
        7,
        "theme.motion.naturalPace.slow",
        Object("""{"motion":{"naturalPace":{"slow":"1.5"}}}""")));

    var numericTheme = Object("""
        {"motion":{"naturalPace":{"slow":1.5},"transitions":{"fade":{"delayMs":0,"durationMs":180}},"buttonPushedDurationMs":120}}
        """);
    Equal(1.5, ThemeNumericTokenValue.RequirePositive(
        numericTheme,
        "theme.motion.naturalPace.slow",
        "Natural pace"));
    Equal(0d, ThemeNumericTokenValue.RequireNonNegative(
        numericTheme,
        "theme.motion.fade.delayMs",
        "Fade delay"));
    Throws<InvalidOperationException>(() => ThemeNumericTokenValue.Require(
        numericTheme,
        "theme.motion.missing",
        "Unknown token"));
    Throws<InvalidOperationException>(() => ThemeNumericTokenValue.RequirePositive(
        numericTheme,
        "theme.motion.reflowDurationMs",
        "Missing token value"));
    Throws<InvalidOperationException>(() => ThemeNumericTokenValue.RequirePositive(
        Object("""{"motion":{"buttonPushedDurationMs":"120"}}"""),
        "theme.motion.buttonPushedDurationMs",
        "Wrong token type"));
    Throws<InvalidOperationException>(() => ThemeNumericTokenValue.RequirePositive(
        Object("""{"motion":{"buttonPushedDurationMs":0}}"""),
        "theme.motion.buttonPushedDurationMs",
        "Zero duration"));
    Throws<InvalidOperationException>(() => ThemeNumericTokenValue.RequireNonNegative(
        Object("""{"motion":{"transitions":{"fade":{"delayMs":-1}}}}"""),
        "theme.motion.fade.delayMs",
        "Negative delay"));
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
          {"id":"isPlaying","jsonKey":"isPlaying","animationTimeline":{"origin":{"kind":"fieldCompletion","fieldId":"text","offsetFrames":0}}},
          {"id":"playDuration","jsonKey":"playDuration"}
        ]{{{{(withMediaAction ? ",\n        \"itemActions\": [{\"id\":\"play\",\"extendsModuleDuration\":true,\"playInputId\":\"isPlaying\",\"durationInputId\":\"playDuration\",\"durationEnabledInputId\":\"isPlaying\"}]" : "")}}}}
      }]
    }
    """);

static JsonObject Object(string json) => JsonNode.Parse(json)!.AsObject();

static string ParityDatabasePath()
{
    var configured = Environment.GetEnvironmentVariable("MOCKUPS_VALIDATION_DATABASE");
    return string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(Directory.GetCurrentDirectory(), "data", "mockups.sqlite")
        : Path.GetFullPath(configured);
}

static IEditorLayoutStore EditorLayouts(
    SqliteProjectTestContext database) =>
    new SqliteEditorLayoutStore(database.Context);

static IReadOnlyList<string> ArgumentValues(string[] arguments, string key)
{
    var values = new List<string>();
    for (var index = 0; index < arguments.Length; index++)
    {
        if (!arguments[index].Equals(key, StringComparison.Ordinal)) continue;
        if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Desktop test argument '{key}' requires a value.");
        }
        values.Add(arguments[index + 1]);
        index++;
    }
    return values;
}

static string? SingleArgumentValue(string[] arguments, string key)
{
    var values = ArgumentValues(arguments, key);
    return values.Count switch
    {
        0 => null,
        1 => values[0],
        _ => throw new InvalidOperationException(
            $"Desktop test argument '{key}' may be specified only once."),
    };
}

static T Required<T>(T? value) where T : class => value ?? throw new Exception("Expected a value.");
static EditorSessionState WindowSession(MainWindow window) =>
    (typeof(MainWindow)
        .GetField(
            "_workspaceCoordinator",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?.GetValue(window) as EditorWorkspaceCoordinator
        ?? throw new InvalidOperationException(
            "Missing MainWindow workspace coordinator."))
    .State;
static DesignPreviewPayload? CreatePreviewPayload(
    SqliteProjectTestContext database,
    ProjectTreeNode? node,
    string? themeId,
    string themeMode = "light",
    int timelineFrame = 0) =>
    DesignPreviewPayloadFactory.Create(
        new DesignPreviewPayloadDataSource(
            database.PreviewInputs,
            database.Production,
            database.Resources,
            database.Resources,
            database.ProjectPaths),
        node,
        themeId,
        themeMode,
        timelineFrame);
static void True(
    bool condition,
    [CallerArgumentExpression(nameof(condition))]
    string expression = "")
{
    if (!condition)
    {
        throw new Exception(
            $"Expected true: {expression}");
    }
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

internal sealed class RecordingPresentationContextRepository :
    IEditorPresentationContextRepository
{
    public List<int> ReadThreadIds { get; } = [];

    public ProjectSettings GetProjectSettings(string projectId) =>
        throw new InvalidOperationException(
            "Project settings are not part of this test.");

    public ThemeSettings GetThemeSettings(string themeId)
    {
        ReadThreadIds.Add(
            Environment.CurrentManagedThreadId);
        return new ThemeSettings(
            "project",
            "Theme",
            "Editorial",
            "icons",
            "status",
            "",
            "{}",
            "{}");
    }

    public string GetProductionFontFieldValue(
        string fontId,
        string fieldId)
    {
        ReadThreadIds.Add(
            Environment.CurrentManagedThreadId);
        return "regular.ttf\nbold.ttf";
    }
}

internal sealed class RecordingVariantHistoryStore : IVariantHistoryStore
{
    public Dictionary<string, string> ConfigByVariant { get; } =
        new(StringComparer.Ordinal);
    public List<int> ReadThreadIds { get; } = [];

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode)
    {
        ReadThreadIds.Add(Environment.CurrentManagedThreadId);
        return new ComponentClassSettings(
            "project",
            "history",
            "component.history",
            variantNode.Name,
            "",
            ConfigByVariant[variantNode.Id],
            "{}",
            "{}");
    }

    public ModuleSettings GetModuleVariantSettings(
        ProjectTreeNode variantNode)
    {
        ReadThreadIds.Add(Environment.CurrentManagedThreadId);
        return new ModuleSettings(
            "project",
            "module.history",
            0,
            ConfigByVariant[variantNode.Id],
            "{}",
            "{}");
    }
}

internal sealed class AppearanceFailingRenderExecutor(
    string failingAppearance) : IRenderJobExecutor
{
    public Task ExecuteAsync(
        RenderJobSnapshot snapshot,
        IProgress<RenderQueueExecutionProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress.Report(new RenderQueueExecutionProgress(
            snapshot.FrameStore.TotalFrames,
            snapshot.FrameStore.TotalFrames,
            "Test execution",
            RenderQueueStatus.Rendering));
        if (snapshot.RequestedAppearance.Equals(
            failingAppearance,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Deliberate child failure.");
        }
        return Task.CompletedTask;
    }

    public void Dispose() { }
}

internal static class HeadlessTestApplication
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true,
            })
            .WithInterFont();
}
