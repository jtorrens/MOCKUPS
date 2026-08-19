using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

var tests = new (string Name, Action Run)[]
{
    ("SVG fill transforms keep direct reusable geometry", SvgFillTransformsKeepDirectReusableGeometry),
    ("redirected child-process text round-trips exact UTF-8", RedirectedChildProcessTextRoundTripsExactUtf8),
    ("initial tree load resolves a selectable Design context", InitialTreeLoadSelectsDesignContext),
    ("workspace changes restore each workspace selection", WorkspaceChangesRestoreSelection),
    ("tree refresh replaces a deleted selection with a valid fallback", DeletedSelectionFallsBack),
    ("tree refresh replaces a deleted Production", DeletedProductionFallsBack),
    ("Production selection commits its first exact Production node", ProductionSelectionIsExact),
    ("synthetic Production navigation selects and restores Render Queue", SyntheticProductionNavigationIsSelectable),
    ("typed reference navigation changes workspace and exact Production", ReferenceNavigationChangesWorkspace),
    ("invalid node selection leaves the session unchanged", InvalidSelectionIsRejected),
    ("active editor refresh rebases embedded context to the new tree", ActiveEditorRefreshRebasesEmbeddedContext),
    ("Design navigation history restores exact visited owners", DesignNavigationHistoryRestoresVisitedOwners),
    ("Design navigation history unwinds embedded breadcrumbs", DesignNavigationHistoryUnwindsEmbeddedBreadcrumbs),
    ("new Design navigation truncates forward history", NewDesignNavigationTruncatesForwardHistory),
    ("Design navigation history skips deleted owners", DesignNavigationHistorySkipsDeletedOwners),
    ("a newer tree load cancels and rejects the older result", NewerTreeLoadRejectsOlderResult),
    ("prepared tree data remains invisible until its dependent state can commit", PreparedTreeRemainsInvisibleUntilCommit),
    ("async tree reads run on a worker before committing immutable state", AsyncTreeReadRunsOnWorker),
    ("desktop consumers can compile only asynchronous tree loading", OnlyAsyncTreeLoadingIsPublic),
    ("rapid async workspace changes discard the older result", RapidAsyncWorkspaceChangeDiscardsOlderResult),
    ("returning to the current workspace cancels an in-flight change", ReturnToCurrentWorkspaceCancelsInFlightChange),
    ("disposing the session discards a pending async tree result", DisposeDiscardsPendingAsyncTreeResult),
    ("a selection transition invalidates an in-flight tree result", SelectionInvalidatesInFlightLoad),
    ("a failed tree read leaves the prior session state current", FailedTreeReadLeavesStateCurrent),
    ("session revisions identify only the current owner transition", SessionRevisionGuardsOwner),
    ("Runtime definitions preserve their explicit owner", RuntimeDefinitionsPreserveOwner),
    ("projected Runtime collections reconcile by stable id", ProjectedRuntimeCollectionsReconcileById),
    ("Component Input bindings project exact structure-owned Runtime values", ComponentInputBindingsProjectExactStructuredRuntimeValues),
    ("Component Input projection ownership covers Module and Component parents", ComponentInputProjectionOwnershipCoversParents),
    ("Runtime documents reject missing and parent-owned values", RuntimeDocumentsRejectInvalidOwnership),
    ("Runtime scalar patterns validate defaults and authored values", RuntimeScalarPatternsValidateValues),
    ("Runtime contract transitions retain only current values and animation owners", RuntimeContractTransitionsRetainCurrentOwners),
    ("structured collection mutations update nested content and animation together", StructuredCollectionMutationsAreAtomicDocuments),
    ("editor operations execute away from the caller thread", EditorOperationsRunOnWorker),
    ("editor operations preserve their submission order", EditorOperationsAreSerialized),
    ("disposing editor operations cancels queued work", DisposeCancelsQueuedEditorOperations),
};

static void StructuredCollectionMutationsAreAtomicDocuments()
{
    var leaf = new RuntimeInputCollectionDefinition(
        "leaf",
        "Leaf",
        "children",
        "Child",
        []);
    var nested = new RuntimeInputCollectionDefinition(
        "state",
        "States",
        "states",
        "State",
        [
            new ComponentInputDefinition(
                "note",
                "Note",
                "note",
                ComponentInputKind.Text,
                ValueKind.StringSingleLine,
                ""),
            new ComponentInputDefinition(
                "children",
                "Children",
                "children",
                ComponentInputKind.Text,
                ValueKind.StructuredCollection,
                "[]",
                StructuredCollection: leaf),
        ]);
    var root = new RuntimeInputCollectionDefinition(
        "message",
        "Messages",
        "messages",
        "Message",
        [
            new ComponentInputDefinition(
                "states",
                "States",
                "states",
                ComponentInputKind.Text,
                ValueKind.StructuredCollection,
                "[]",
                StructuredCollection: nested),
        ]);
    var content = new JsonObject
    {
        ["messages"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "message-a",
                ["states"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "state-a",
                        ["note"] = "child-a",
                        ["children"] = new JsonArray
                        {
                            new JsonObject { ["id"] = "child-a" },
                        },
                    },
                },
            },
        },
    };
    var animation = new JsonObject
    {
        ["schemaVersion"] = 2,
        ["tracks"] = new JsonArray
        {
            TestTrack("track-message", "message-a"),
            TestTrack("track-state", "state-a"),
            TestTrack("track-child", "child-a"),
            TestTrack("track-untyped", "untyped-a"),
        },
    };

    var added = StructuredCollectionMutationEngine.Apply(
        content,
        animation,
        root,
        new AddStructuredCollectionItem(
            new StructuredCollectionAddress(
                "messages",
                [new StructuredCollectionOwnerSegment("messages", "message-a")],
                "states"),
            new JsonObject
            {
                ["note"] = "prototype-child",
                ["children"] = new JsonArray
                {
                    new JsonObject { ["id"] = "prototype-child" },
                },
            },
            "state-a"));
    var addedId = added.SelectedItemId
        ?? throw new InvalidOperationException("Missing added item identity.");
    Equal(1, content["messages"]![0]!["states"]!.AsArray().Count);
    Equal(2, added.Content["messages"]![0]!["states"]!.AsArray().Count);
    Equal(addedId, added.Content["messages"]![0]!["states"]![0]!["id"]!.GetValue<string>());
    True(added.Item!["children"]![0]!["id"]!.GetValue<string>() != "prototype-child");
    var moved = StructuredCollectionMutationEngine.Apply(
        added.Content,
        added.Animation,
        root,
        new MoveStructuredCollectionItem(
            new StructuredCollectionAddress(
                "messages",
                [new StructuredCollectionOwnerSegment("messages", "message-a")],
                "states"),
            addedId));
    Equal(addedId, moved.Content["messages"]![0]!["states"]![1]!["id"]!.GetValue<string>());
    Equal(addedId, moved.SelectedItemId ?? "");

    var duplicateNestedIds = content.DeepClone().AsObject();
    duplicateNestedIds["messages"]![0]!["states"]![0]!["children"]!.AsArray().Add(
        new JsonObject { ["id"] = "child-a" });
    Throws<InvalidOperationException>(() => StructuredCollectionMutationEngine.Apply(
        duplicateNestedIds,
        animation,
        root,
        new DuplicateStructuredCollectionItem(
            new StructuredCollectionAddress(
                "messages",
                [new StructuredCollectionOwnerSegment("messages", "message-a")],
                "states"),
                "state-a")));

    var duplicateSiblingBranchIds = content.DeepClone().AsObject();
    duplicateSiblingBranchIds["messages"]!.AsArray().Add(
        new JsonObject
        {
            ["id"] = "message-b",
            ["states"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "state-b",
                    ["note"] = "child-b",
                    ["children"] = new JsonArray
                    {
                        new JsonObject { ["id"] = "child-a" },
                    },
                },
            },
        });
    Throws<InvalidOperationException>(() => StructuredCollectionMutationEngine.Apply(
        duplicateSiblingBranchIds,
        animation,
        root,
        new MoveStructuredCollectionItem(
            new StructuredCollectionAddress("messages", [], "messages"),
            "message-a")));

    var duplicate = StructuredCollectionMutationEngine.Apply(
        content,
        animation,
        root,
        new DuplicateStructuredCollectionItem(
            new StructuredCollectionAddress(
                "messages",
                [new StructuredCollectionOwnerSegment("messages", "message-a")],
                "states"),
            "state-a"));
    var duplicatedState = duplicate.Item
        ?? throw new InvalidOperationException("Missing duplicated nested item.");
    var duplicatedStateId = duplicatedState["id"]!.GetValue<string>();
    var duplicatedChildId = duplicatedState["children"]![0]!["id"]!.GetValue<string>();
    Equal("child-a", duplicatedState["note"]?.GetValue<string>() ?? "");
    Equal(1, content["messages"]![0]!["states"]!.AsArray().Count);
    Equal(4, animation["tracks"]!.AsArray().Count);
    content = duplicate.Content;
    animation = duplicate.Animation;
    Equal(2, content["messages"]![0]!["states"]!.AsArray().Count);
    Equal(6, animation["tracks"]!.AsArray().Count);
    Equal(true, animation["tracks"]!.AsArray().OfType<JsonObject>().Any((track) =>
        track["targetId"]?.GetValue<string>() == duplicatedStateId));
    Equal(true, animation["tracks"]!.AsArray().OfType<JsonObject>().Any((track) =>
        track["targetId"]?.GetValue<string>() == duplicatedChildId));
    Equal(1, animation["tracks"]!.AsArray().OfType<JsonObject>().Count((track) =>
        track["targetId"]?.GetValue<string>() == "message-a"));

    var deletedDuplicate = StructuredCollectionMutationEngine.Apply(
        content,
        animation,
        root,
        new DeleteStructuredCollectionItem(
            new StructuredCollectionAddress(
                "messages",
                [new StructuredCollectionOwnerSegment("messages", "message-a")],
                "states"),
            duplicatedStateId));
    content = deletedDuplicate.Content;
    animation = deletedDuplicate.Animation;
    Equal(1, content["messages"]![0]!["states"]!.AsArray().Count);
    Equal(4, animation["tracks"]!.AsArray().Count);
    Equal(false, animation["tracks"]!.AsArray().OfType<JsonObject>().Any((track) =>
        track["targetId"]?.GetValue<string>() == duplicatedStateId
        || track["targetId"]?.GetValue<string>() == duplicatedChildId));

    var deletedOriginal = StructuredCollectionMutationEngine.Apply(
        content,
        animation,
        root,
        new DeleteStructuredCollectionItem(
            new StructuredCollectionAddress(
                "messages",
                [new StructuredCollectionOwnerSegment("messages", "message-a")],
                "states"),
            "state-a"));
    content = deletedOriginal.Content;
    animation = deletedOriginal.Animation;
    Equal(0, content["messages"]![0]!["states"]!.AsArray().Count);
    Equal(2, animation["tracks"]!.AsArray().Count);
    Equal("message-a", animation["tracks"]![0]!["targetId"]!.GetValue<string>());
    Equal("untyped-a", animation["tracks"]![1]!["targetId"]!.GetValue<string>());

    var fixedRuntimeCollection = RuntimeCollectionDefinition(
        "fixedItems",
        canEditStructure: false);
    var editableRuntimeCollection = RuntimeCollectionDefinition(
        "editableItems",
        canEditStructure: true);
    var editableChildRuntimeCollection = RuntimeCollectionDefinition(
        "editableChildren",
        canEditStructure: true,
        uiParentCollectionJsonKey: "editableItems",
        uiParentItemIdJsonKey: "parentId");
    var runtimeOwner = new RuntimeInputCollectionDefinition(
        "runtimeOwner",
        "Runtime owners",
        "runtimeOwners",
        "Runtime owner",
        [],
        ItemRuntimeContractJsonKey: "runtime");
    var runtimeContent = new JsonObject
    {
        ["runtimeOwners"] = new JsonArray
        {
            RuntimeOwner("owner-a", "editable-a"),
            RuntimeOwner("owner-b", "editable-b"),
        },
    };
    StructuredCollectionItemIdentity.ValidateUniqueTargetIds(
        runtimeContent["runtimeOwners"]!.AsArray(),
        runtimeOwner,
        "Runtime boundary fixture");
    var runtimeDuplicate = StructuredCollectionMutationEngine.Apply(
        runtimeContent,
        new JsonObject
        {
            ["schemaVersion"] = 2,
            ["tracks"] = new JsonArray(),
        },
        runtimeOwner,
        new DuplicateStructuredCollectionItem(
            new StructuredCollectionAddress("runtimeOwners", [], "runtimeOwners"),
            "owner-a"));
    var duplicatedRuntime = runtimeDuplicate.Item!["runtime"]!.AsObject();
    Equal(
        "primary",
        duplicatedRuntime["fixedItems"]![0]!["id"]!.GetValue<string>());
    var duplicatedEditableId = duplicatedRuntime["editableItems"]![0]!["id"]!.GetValue<string>();
    True(duplicatedEditableId != "editable-a");
    True(!runtimeDuplicate.IdentityChange.RebasedItemIds.ContainsKey("primary"));
    Equal(
        duplicatedEditableId,
        runtimeDuplicate.IdentityChange.RebasedItemIds["editable-a"]);
    Equal(
        duplicatedEditableId,
        duplicatedRuntime["editableChildren"]![0]!["parentId"]!.GetValue<string>());
    True(
        duplicatedRuntime["editableChildren"]![0]!["id"]!.GetValue<string>()
        != "editable-a-child");

    JsonObject RuntimeOwner(string id, string editableId) => new()
    {
        ["id"] = id,
        ["runtime"] = new JsonObject
        {
            ["inputs"] = new JsonArray(),
            ["collections"] = new JsonArray
            {
                RuntimeCollectionContract(fixedRuntimeCollection),
                RuntimeCollectionContract(editableRuntimeCollection),
                RuntimeCollectionContract(editableChildRuntimeCollection),
            },
            ["fixedItems"] = new JsonArray
            {
                new JsonObject { ["id"] = "primary" },
            },
            ["editableItems"] = new JsonArray
            {
                new JsonObject { ["id"] = editableId },
            },
            ["editableChildren"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = $"{editableId}-child",
                    ["parentId"] = editableId,
                },
            },
        },
    };

    static RuntimeInputCollectionDefinition RuntimeCollectionDefinition(
        string id,
        bool canEditStructure,
        string uiParentCollectionJsonKey = "",
        string uiParentItemIdJsonKey = "") => new(
            id,
            id,
            id,
            "Item",
            [],
            UiParentCollectionJsonKey: uiParentCollectionJsonKey,
            UiParentItemIdJsonKey: uiParentItemIdJsonKey,
            CanEditStructure: canEditStructure);

    static JsonObject RuntimeCollectionContract(RuntimeInputCollectionDefinition collection) => new()
    {
        ["id"] = collection.Id,
        ["label"] = collection.Label,
        ["jsonKey"] = collection.JsonKey,
        ["itemLabel"] = collection.ItemLabel,
        ["fields"] = new JsonArray(),
        ["canEditStructure"] = collection.CanEditStructure,
        ["uiParentCollectionJsonKey"] = collection.UiParentCollectionJsonKey,
        ["uiParentItemIdJsonKey"] = collection.UiParentItemIdJsonKey,
    };

    static JsonObject TestTrack(string id, string targetId) => new()
    {
        ["id"] = id,
        ["fieldId"] = "value",
        ["targetId"] = targetId,
        ["keyframes"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = $"keyframe-{id}",
                ["frame"] = 0,
                ["enabled"] = true,
                ["value"] = false,
                ["interpolation"] = "hold",
            },
        },
    };
}

static void RedirectedChildProcessTextRoundTripsExactUtf8()
{
    const string expected =
        "áéíóúüñ¿¡ 😀 👨‍👩‍👧‍👦";
    var startInfo = DesktopChildProcess.CreateHiddenStartInfo(
        DesktopChildProcess.ResolveNodeExecutable(),
        Directory.GetCurrentDirectory(),
        redirectStandardInput: true);
    startInfo.ArgumentList.Add("-e");
    startInfo.ArgumentList.Add(
        "let input='';"
        + "process.stdin.setEncoding('utf8');"
        + "process.stdin.on('data',chunk=>input+=chunk);"
        + "process.stdin.on('end',()=>process.stdout.write(input));");

    Equal(true, startInfo.RedirectStandardInput);
    Equal("utf-8", startInfo.StandardInputEncoding?.WebName);
    Equal("utf-8", startInfo.StandardOutputEncoding?.WebName);
    Equal("utf-8", startInfo.StandardErrorEncoding?.WebName);
    Equal(
        0,
        startInfo.StandardInputEncoding?.GetPreamble().Length);
    var outputOnlyStartInfo =
        DesktopChildProcess.CreateHiddenStartInfo(
            DesktopChildProcess.ResolveNodeExecutable(),
            Directory.GetCurrentDirectory());
    Equal(false, outputOnlyStartInfo.RedirectStandardInput);
    Equal(null, outputOnlyStartInfo.StandardInputEncoding);
    Equal("utf-8", outputOnlyStartInfo.StandardOutputEncoding?.WebName);
    Equal("utf-8", outputOnlyStartInfo.StandardErrorEncoding?.WebName);

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException(
            "Could not start the UTF-8 child-process fixture.");
    process.StandardInput.Write(expected);
    process.StandardInput.Close();
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"Node UTF-8 fixture failed: {error}");
    }
    Equal(expected, output);
}

static void SvgFillTransformsKeepDirectReusableGeometry()
{
    const string source =
        """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M5 5 L19 12 L5 19 Z"/>
        </svg>
        """;
    var options = new SvgReplacementService.TransformOptions(
        "fill",
        1,
        0,
        0,
        1,
        0,
        0,
        0,
        source);
    var filled = SvgReplacementService.Transform(source, options);

    True(!filled.Contains("<mask", StringComparison.OrdinalIgnoreCase));
    True(filled.Contains("fill=\"#000\"", StringComparison.Ordinal));
    True(filled.Contains("stroke=\"none\"", StringComparison.Ordinal));
    True(filled.Contains("M5 5 L19 12 L5 19 Z", StringComparison.Ordinal));

    const string legacyGeneratedFill =
        """
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor">
          <defs>
            <mask id="mockups-fill-silhouette">
              <rect width="24" height="24" fill="#000"/>
              <g data-mockups-transform="fit-center-scale-rotate">
                <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="#fff" stroke="none">
                  <path d="M5 5 L19 12 L5 19 Z"/>
                </svg>
              </g>
            </mask>
          </defs>
          <rect width="24" height="24" fill="#000" mask="url(#mockups-fill-silhouette)"/>
        </svg>
        """;
    var retransformed = SvgReplacementService.Transform(
        legacyGeneratedFill,
        options with { Mode = "positive" });

    True(!retransformed.Contains("<mask", StringComparison.OrdinalIgnoreCase));
    Equal(
        1,
        Regex.Matches(
            retransformed,
            "data-mockups-transform=",
            RegexOptions.IgnoreCase).Count);
    True(retransformed.Contains("fill=\"#000\"", StringComparison.Ordinal));
    True(retransformed.Contains("M5 5 L19 12 L5 19 Z", StringComparison.Ordinal));
}

var exactNames = ArgumentValues(args, "--exact");
var filters = ArgumentValues(args, "--filter");
var knownArguments = new HashSet<string>(StringComparer.Ordinal)
{
    "--exact",
    "--filter",
    "--list",
};
for (var index = 0; index < args.Length; index++)
{
    var argument = args[index];
    if (!knownArguments.Contains(argument))
    {
        throw new InvalidOperationException(
            $"Unknown Application test argument '{argument}'.");
    }
    if (argument != "--list") index++;
}
foreach (var exactName in exactNames)
{
    if (!tests.Any((test) =>
        test.Name.Equals(exactName, StringComparison.Ordinal)))
    {
        throw new InvalidOperationException(
            $"Unknown exact Application test '{exactName}'.");
    }
}
var selectedTests = tests
    .Where((test) =>
        (exactNames.Count == 0 && filters.Count == 0)
        || exactNames.Contains(test.Name, StringComparer.Ordinal)
        || filters.Any((filter) =>
            test.Name.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase)))
    .ToArray();
if (selectedTests.Length == 0)
{
    throw new InvalidOperationException(
        "Application test selection matched no tests.");
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
    $"Application workspace tests: {selectedTests.Length - failures.Count}/{selectedTests.Length} passed.");
if (failures.Count > 0) Environment.Exit(1);

static IReadOnlyList<string> ArgumentValues(
    string[] arguments,
    string key)
{
    var values = new List<string>();
    for (var index = 0; index < arguments.Length; index++)
    {
        if (!arguments[index].Equals(
            key,
            StringComparison.Ordinal))
        {
            continue;
        }
        if (index + 1 >= arguments.Length
            || arguments[index + 1].StartsWith(
                "--",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Application test argument '{key}' requires a value.");
        }
        values.Add(arguments[index + 1]);
        index++;
    }
    return values;
}

static void EditorOperationsRunOnWorker()
{
    using var coordinator = new EditorOperationCoordinator();
    var callerThread = Environment.CurrentManagedThreadId;
    var operationThread = coordinator.ExecuteAsync(
            () => Environment.CurrentManagedThreadId)
        .GetAwaiter()
        .GetResult();

    True(operationThread != callerThread);
}

static void EditorOperationsAreSerialized()
{
    using var coordinator = new EditorOperationCoordinator();
    using var firstStarted = new ManualResetEventSlim();
    using var releaseFirst = new ManualResetEventSlim();
    var order = new List<int>();
    var first = coordinator.ExecuteAsync(
        () =>
        {
            firstStarted.Set();
            if (!releaseFirst.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException(
                    "Timed out waiting to release the first editor operation.");
            }
            order.Add(1);
        });
    True(firstStarted.Wait(TimeSpan.FromSeconds(10)));
    var second = coordinator.ExecuteAsync(() => order.Add(2));
    Thread.Sleep(50);
    Equal(0, order.Count);

    releaseFirst.Set();
    Task.WhenAll(first, second).GetAwaiter().GetResult();
    Equal(2, order.Count);
    Equal(1, order[0]);
    Equal(2, order[1]);
}

static void DisposeCancelsQueuedEditorOperations()
{
    var coordinator = new EditorOperationCoordinator();
    using var firstStarted = new ManualResetEventSlim();
    using var releaseFirst = new ManualResetEventSlim();
    var secondRan = false;
    var first = coordinator.ExecuteAsync(
        () =>
        {
            firstStarted.Set();
            if (!releaseFirst.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException(
                    "Timed out waiting to release the first editor operation.");
            }
        });
    True(firstStarted.Wait(TimeSpan.FromSeconds(10)));
    var second = coordinator.ExecuteAsync(() => secondRan = true);
    coordinator.Dispose();
    releaseFirst.Set();
    first.GetAwaiter().GetResult();
    Throws<OperationCanceledException>(
        () => second.GetAwaiter().GetResult());
    True(!secondRan);
}

static void RuntimeDefinitionsPreserveOwner()
{
    True(RuntimeInputDocumentContract.IsRuntimeDefinition(
        new JsonObject()));
    True(RuntimeInputDocumentContract.IsRuntimeDefinition(
        new JsonObject { ["source"] = "runtime" }));
    True(!RuntimeInputDocumentContract.IsRuntimeDefinition(
        new JsonObject { ["source"] = "variant" }));
    True(!RuntimeInputDocumentContract.IsRuntimeDefinition(
        new JsonObject { ["source"] = "calculated" }));
    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.IsRuntimeDefinition(
            new JsonObject { ["source"] = "unknown" }));
}

static void ProjectedRuntimeCollectionsReconcileById()
{
    var defaults = new JsonArray
    {
        new JsonObject
        {
            ["id"] = "first",
            ["label"] = "First",
            ["value"] = "default",
        },
        new JsonObject
        {
            ["id"] = "second",
            ["label"] = "Second",
            ["value"] = "second-default",
        },
    };
    var current = new JsonArray
    {
        new JsonObject
        {
            ["id"] = "second",
            ["label"] = "Changed",
            ["value"] = "authored",
        },
        new JsonObject
        {
            ["id"] = "retired",
            ["label"] = "Retired",
            ["value"] = "discarded",
        },
    };

    var result =
        RuntimeInputDocumentContract.ReconcileProjectedCollection(
            current,
            defaults);

    Equal(2, result.Count);
    Equal("first", result[0]?["id"]?.GetValue<string>());
    Equal("default", result[0]?["value"]?.GetValue<string>());
    Equal("second", result[1]?["id"]?.GetValue<string>());
    Equal("authored", result[1]?["value"]?.GetValue<string>());
    Equal("Changed", result[1]?["label"]?.GetValue<string>());
}

static void ComponentInputBindingsProjectExactStructuredRuntimeValues()
{
    var definition = new JsonObject
    {
        ["id"] = "rows",
        ["label"] = "Rows",
        ["jsonKey"] = "rows",
        ["kind"] = "collection",
        ["valueKind"] = nameof(ValueKind.StructuredCollection),
        ["source"] = "runtime",
        ["defaultValue"] = "[]",
        ["structuredCollection"] = new JsonObject
        {
            ["id"] = "rows",
            ["label"] = "Rows",
            ["jsonKey"] = "rows",
            ["itemLabel"] = "Row",
            ["canEditStructure"] = false,
            ["fields"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "value",
                    ["label"] = "Value",
                    ["jsonKey"] = "value",
                    ["kind"] = "text",
                    ["valueKind"] = nameof(ValueKind.StringSingleLine),
                    ["defaultValue"] = "",
                },
                new JsonObject
                {
                    ["id"] = "calculated",
                    ["label"] = "Calculated",
                    ["jsonKey"] = "calculated",
                    ["kind"] = "text",
                    ["valueKind"] = nameof(ValueKind.StringSingleLine),
                    ["source"] = "calculated",
                    ["defaultValue"] = "",
                },
            },
            ["structureProjection"] = new JsonObject
            {
                ["sourceConfigPath"] = "structure.rows",
                ["sourceIdJsonKey"] = "id",
                ["runtimeIdJsonKey"] = "id",
                ["fieldBindings"] = new JsonObject
                {
                    ["value"] = "value",
                },
            },
        },
    };
    var contract = new JsonObject
    {
        ["inputs"] = new JsonArray(definition),
        ["rows"] = new JsonArray
        {
            new JsonObject { ["id"] = "added", ["value"] = "default" },
            new JsonObject { ["id"] = "kept", ["value"] = "variant" },
        },
    };
    var current = new JsonObject
    {
        ["rows"] = new JsonArray
        {
            new JsonObject { ["id"] = "kept", ["value"] = "authored" },
            new JsonObject { ["id"] = "removed", ["value"] = "discarded" },
        },
    };
    var parsedCollection = RuntimeInputDefinitionReader.ReadInputs(
            new JsonObject { ["inputs"] = new JsonArray(definition.DeepClone()) },
            new JsonObject())
        .Single()
        .StructuredCollection
        ?? throw new InvalidOperationException("Missing parsed structured collection.");
    Equal(
        ComponentInputSource.Calculated,
        parsedCollection.Fields.Single((field) => field.Id == "calculated").Source);

    var projected = RuntimeInputDocumentContract
        .ProjectInputValuesForContract(current, contract);
    var rows = projected["rows"]!.AsArray();
    Equal(2, rows.Count);
    Equal("added", rows[0]?["id"]?.GetValue<string>());
    Equal("default", rows[0]?["value"]?.GetValue<string>());
    Equal("kept", rows[1]?["id"]?.GetValue<string>());
    Equal("variant", rows[1]?["value"]?.GetValue<string>());

    var prepared = RuntimePreviewDocumentContract.PrepareRuntime(
        new JsonObject
        {
            ["inputs"] = new JsonArray(definition.DeepClone()),
            ["rows"] = new JsonArray
            {
                new JsonObject { ["id"] = "kept", ["value"] = "fixture" },
            },
        },
        new JsonObject
        {
            ["structure"] = new JsonObject
            {
                ["rows"] = new JsonArray
                {
                    new JsonObject { ["id"] = "kept", ["value"] = "variant" },
                },
            },
        },
        current);
    Equal(
        "variant",
        prepared["rows"]?[0]?["value"]?.GetValue<string>());

    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.ProjectInputValuesForContract(
            new JsonObject { ["items"] = new JsonArray() },
            contract));
    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.ProjectInputValuesForContract(
            new JsonObject
            {
                ["rows"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "kept",
                        ["value"] = "authored",
                        ["calculated"] = "must-not-persist",
                    },
                },
            },
            contract));
    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.ProjectInputValuesForContract(
            new JsonObject
            {
                ["rows"] = new JsonArray
                {
                    new JsonObject { ["id"] = "kept" },
                },
            },
            contract));

    var read = RuntimeInputDefinitionReader.ReadInputs(
        new JsonObject
        {
            ["inputs"] = new JsonArray(definition.DeepClone()),
        },
        new JsonObject());
    Equal(1, read.Count);
    Equal(false, read[0].StructuredCollection?.CanEditStructure);

    var collectionContract = new JsonObject
    {
        ["inputs"] = new JsonArray(),
        ["collections"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "items",
                ["jsonKey"] = "items",
            },
        },
    };
    var collectionCurrent = new JsonObject
    {
        ["items"] = new JsonArray
        {
            new JsonObject { ["id"] = "stable", ["value"] = "authored" },
        },
    };
    var collectionProjected = RuntimeInputDocumentContract
        .ProjectInputValuesForContract(collectionCurrent, collectionContract);
    Equal(
        "authored",
        collectionProjected["items"]?[0]?["value"]?.GetValue<string>());
    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.ProjectInputValuesForContract(
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["orphan"] = new JsonArray(),
            },
            collectionContract));
}

static void ComponentInputProjectionOwnershipCoversParents()
{
    var moduleOwners = ComponentInputBindingsProjectionCatalog.RecordOwners();
    var componentOwners = ComponentInputBindingsProjectionCatalog.ComponentOwners();

    Equal(3, moduleOwners.Count);
    Equal(7, componentOwners.Count);
    Equal(true, moduleOwners.Any((owner) => owner.Id.Equals(
        "module.core.chat.headerRightIconRow.inputs",
        StringComparison.Ordinal)));
    Equal(true, moduleOwners.Any((owner) => owner.Id.Equals(
        "module.core.lockScreen.stackInputs",
        StringComparison.Ordinal)));
    Equal(true, componentOwners.Any((owner) => owner.Id.Equals(
        "component.iconBar.activeRightIconRow.inputs",
        StringComparison.Ordinal)));
    Equal(true, componentOwners.Any((owner) => owner.Id.Equals(
        "component.textInputBar.textBox.inputs",
        StringComparison.Ordinal)));
    var textBox = componentOwners.Single((owner) => owner.Id.Equals(
        "component.textInputBar.textBox.inputs",
        StringComparison.Ordinal));
    Equal(3, textBox.CalculatedInputIds.Count);
    Equal(true, textBox.CalculatedInputIds.Contains("fixedSize"));
    Equal(true, textBox.CalculatedInputIds.Contains("contentMaxWidth"));
    Equal(true, textBox.CalculatedInputIds.Contains("growSize"));
}

static void RuntimeDocumentsRejectInvalidOwnership()
{
    var contract = new JsonObject
    {
        ["inputs"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "runtimeText",
                ["jsonKey"] = "runtimeText",
                ["source"] = "runtime",
                ["kind"] = "text",
                ["valueKind"] = "StringSingleLine",
                ["defaultValue"] = "",
            },
            new JsonObject
            {
                ["id"] = "variantText",
                ["jsonKey"] = "variantText",
                ["source"] = "variant",
                ["kind"] = "text",
                ["valueKind"] = "StringSingleLine",
                ["defaultValue"] = "",
            },
        },
    };
    RuntimeInputDocumentContract.ValidateCurrentValues(
        contract,
        new JsonObject { ["runtimeText"] = "valid" },
        "Test owner");
    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.ValidateCurrentValues(
            contract,
            new JsonObject(),
            "Test owner"));
    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.ValidateCurrentValues(
            contract,
            new JsonObject
            {
                ["runtimeText"] = "valid",
                ["variantText"] = "forbidden",
            },
            "Test owner"));
}

static void RuntimeScalarPatternsValidateValues()
{
    var definition = new JsonObject
    {
        ["id"] = "format",
        ["label"] = "Format",
        ["jsonKey"] = "format",
        ["source"] = "runtime",
        ["kind"] = "text",
        ["valueKind"] = "StringSingleLine",
        ["defaultValue"] = "MM:SS",
        ["helpText"] = "Clock or numeric mask.",
        ["valuePattern"] = "^(?:MM:SS|#*0+)$",
        ["valuePatternMessage"] = "must be a supported mask.",
    };
    var contract = new JsonObject
    {
        ["inputs"] = new JsonArray(definition.DeepClone()),
    };
    var input = RuntimeInputDefinitionReader.ReadInputs(
        contract,
        new JsonObject()).Single();

    Equal("Clock or numeric mask.", input.HelpText);
    Equal("^(?:MM:SS|#*0+)$", input.ValuePattern);
    RuntimeInputDocumentContract.ValidateCurrentValues(
        contract,
        new JsonObject { ["format"] = "###0" },
        "Pattern test");
    Throws<InvalidOperationException>(() =>
        RuntimeInputDocumentContract.ValidateCurrentValues(
            contract,
            new JsonObject { ["format"] = "minutes" },
            "Pattern test"));

    definition["defaultValue"] = "minutes";
    Throws<InvalidOperationException>(() =>
        RuntimeInputDefinitionReader.ReadInputs(
            new JsonObject
            {
                ["inputs"] = new JsonArray(definition.DeepClone()),
            },
            new JsonObject()));
}

static void RuntimeContractTransitionsRetainCurrentOwners()
{
    var contract = new JsonObject
    {
        ["inputs"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "runtimeText",
                ["jsonKey"] = "runtimeText",
                ["source"] = "runtime",
                ["kind"] = "text",
                ["valueKind"] = "StringSingleLine",
                ["defaultValue"] = "",
            },
            new JsonObject
            {
                ["id"] = "variantText",
                ["jsonKey"] = "variantText",
                ["source"] = "variant",
                ["kind"] = "text",
                ["valueKind"] = "StringSingleLine",
                ["defaultValue"] = "",
            },
        },
    };
    var content =
        RuntimeInputDocumentContract.CreateContentForContract(
            new JsonObject
            {
                ["schemaVersion"] = 2,
                ["runtimeText"] = "authored",
                ["variantText"] = "parent-owned",
                ["retired"] = true,
            },
            contract);

    Equal("authored", content["runtimeText"]?.GetValue<string>());
    True(content["variantText"] is null);
    True(content["retired"] is null);

    content["items"] = new JsonArray
    {
        new JsonObject { ["id"] = "kept-target" },
    };
    var animation = new JsonObject
    {
        ["tracks"] = new JsonArray
        {
            new JsonObject
            {
                ["fieldId"] = "runtimeText",
                ["targetId"] = "",
            },
            new JsonObject
            {
                ["fieldId"] = "retired",
                ["targetId"] = "",
            },
            new JsonObject
            {
                ["fieldId"] = "value",
                ["targetId"] = "kept-target",
            },
            new JsonObject
            {
                ["fieldId"] = "value",
                ["targetId"] = "retired-target",
            },
        },
    };

    var reconciled =
        RuntimeInputDocumentContract.RemoveOrphanedAnimationTracks(
            animation,
            contract,
            content);
    var tracks = reconciled["tracks"]?.AsArray()
        ?? throw new InvalidOperationException(
            "Expected reconciled animation tracks.");
    Equal(2, tracks.Count);
    Equal(
        "runtimeText",
        tracks[0]?["fieldId"]?.GetValue<string>());
    Equal(
        "kept-target",
        tracks[1]?["targetId"]?.GetValue<string>());
}

static void InitialTreeLoadSelectsDesignContext()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);

    coordinator.ReloadTree();

    Equal(EditorWorkspace.Design, coordinator.State.Workspace);
    Equal("component-a::variant::default", coordinator.State.SelectedNode?.Id);
    Equal(
        coordinator.State.Revision,
        coordinator.State.Preview.Revision);
    Equal(
        coordinator.State.SelectedNode?.Id,
        coordinator.State.Preview.SelectedNodeId);
}

static void WorkspaceChangesRestoreSelection()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    True(coordinator.TrySelectNodeById(
        "component-a::variant::alternate",
        "alternate",
        out _));

    coordinator.SwitchWorkspace(EditorWorkspace.Production);
    Equal("episode-a", coordinator.State.SelectedNode?.Id);
    True(coordinator.TrySelectNodeById("shot-a", "shot", out _));

    coordinator.SwitchWorkspace(EditorWorkspace.Design);
    Equal(
        "component-a::variant::alternate",
        coordinator.State.SelectedNode?.Id);

    coordinator.SwitchWorkspace(EditorWorkspace.Production);
    Equal("shot-a", coordinator.State.SelectedNode?.Id);
}

static void DeletedSelectionFallsBack()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    True(coordinator.TrySelectNodeById(
        "component-a::variant::alternate",
        "alternate",
        out _));

    source.Tree = CreateTree(includeAlternateVariant: false);
    coordinator.ReloadTree();

    Equal(
        "component-a::variant::default",
        coordinator.State.SelectedNode?.Id);
    True(coordinator.State.EmbeddedEditor is null);
}

static void DeletedProductionFallsBack()
{
    var source = new MutableNavigationDataSource(
        [CreateProject("project-a"), CreateProject("project-b")]);
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.Restore(new EditorSessionRestoreState(
        EditorWorkspace.Production,
        "project-b"));
    coordinator.ReloadTree();
    Equal("project-b", coordinator.State.ProductionId);

    source.Tree = [CreateProject("project-a")];
    coordinator.ReloadTree();

    Equal("project-a", coordinator.State.ProductionId);
}

static void ProductionSelectionIsExact()
{
    var source = new MutableNavigationDataSource(
        [CreateProject("project-a"), CreateProject("project-b")]);
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();

    True(coordinator.TrySelectProduction(
        "project-b",
        "production-picker",
        out var transition));

    Equal(EditorWorkspace.Production, coordinator.State.Workspace);
    Equal("project-b", coordinator.State.ProductionId);
    Equal("episode-project-b", coordinator.State.SelectedNode?.Id);
    True(transition.Effects.HasFlag(EditorSessionEffects.Production));
    True(!coordinator.TrySelectProduction(
        "missing",
        "production-picker",
        out _));
}

static void SyntheticProductionNavigationIsSelectable()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    coordinator.SwitchWorkspace(EditorWorkspace.Production);
    var project = coordinator.State.TreeRoots.Single();
    var queue = EditorWorkspaceNavigation
        .SectionRoots(project, EditorWorkspace.Production)
        .Single((node) =>
            node.Kind == ProjectTreeNodeKind.RenderQueueRoot);

    True(coordinator.TrySelectNode(
        queue,
        "render-queue",
        out var transition));

    Equal(queue.Id, coordinator.State.SelectedNode?.Id);
    Equal(
        ProjectTreeNodeKind.RenderQueueRoot,
        coordinator.State.SelectedNode?.Kind);
    Equal(
        coordinator.State.Revision,
        coordinator.State.Preview.Revision);
    True(transition.Effects.HasFlag(EditorSessionEffects.Editor));

    coordinator.SwitchWorkspace(EditorWorkspace.Design);
    coordinator.SwitchWorkspace(EditorWorkspace.Production);

    Equal(queue.Id, coordinator.State.SelectedNode?.Id);
    Equal(
        ProjectTreeNodeKind.RenderQueueRoot,
        coordinator.State.SelectedNode?.Kind);
}

static void ReferenceNavigationChangesWorkspace()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();

    True(coordinator.TrySelectNodeInWorkspace(
        EditorWorkspace.Production,
        "shot-a",
        "reference-usage",
        out var transition));

    Equal(EditorWorkspace.Production, coordinator.State.Workspace);
    Equal("project-a", coordinator.State.ProductionId);
    Equal("shot-a", coordinator.State.SelectedNode?.Id);
    True(transition.Effects.HasFlag(EditorSessionEffects.Workspace));
}

static void InvalidSelectionIsRejected()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    var previous = coordinator.State;

    True(!coordinator.TrySelectNodeById(
        "missing",
        "invalid-selection",
        out var transition));

    Equal(EditorSessionEffects.None, transition.Effects);
    True(ReferenceEquals(previous, coordinator.State));
}

static void ActiveEditorRefreshRebasesEmbeddedContext()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    var owner = Required(coordinator.State.SelectedNode);
    var slot = new EmbeddedComponentSlotDefinition(
        "slot-a",
        "label",
        "Label",
        "component.label",
        ["component", "slot"]);
    coordinator.ShowEmbeddedEditor(
        new EditorEmbeddedContext(owner, [slot]));
    var oldOwner = Required(coordinator.State.EmbeddedEditor).OwnerNode;

    source.Tree = CreateTree();
    coordinator.ReloadTree(
        "active-editor",
        EditorTreeLoadIntent.ActiveEditor);

    var refreshed = Required(coordinator.State.EmbeddedEditor);
    True(!ReferenceEquals(oldOwner, refreshed.OwnerNode));
    Equal(coordinator.State.SelectedNode, refreshed.OwnerNode);
}

static void DesignNavigationHistoryRestoresVisitedOwners()
{
    var source =
        new MutableNavigationDataSource(
            CreateDesignHistoryTree());
    using var coordinator =
        new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();

    Equal(
        "component-a::variant::default",
        coordinator.State.SelectedNode?.Id);
    True(coordinator.TrySelectNodeById(
        "component-icon-row::variant::default",
        "icon-row",
        out _));
    True(coordinator.TrySelectNodeById(
        "component-button::variant::default",
        "button",
        out _));
    Equal(
        new EditorDesignNavigationAvailability(
            CanGoBack: true,
            CanGoForward: false),
        coordinator.DesignNavigationAvailability);

    True(coordinator.TryNavigateDesignHistory(
        -1,
        out var iconRow));
    Equal(
        "component-icon-row::variant::default",
        iconRow.Current.SelectedNode?.Id);
    True(coordinator.TryNavigateDesignHistory(
        -1,
        out var textBox));
    Equal(
        "component-a::variant::default",
        textBox.Current.SelectedNode?.Id);
    True(coordinator.TryNavigateDesignHistory(
        1,
        out var forward));
    Equal(
        "component-icon-row::variant::default",
        forward.Current.SelectedNode?.Id);
}

static void DesignNavigationHistoryUnwindsEmbeddedBreadcrumbs()
{
    var source =
        new MutableNavigationDataSource(
            CreateDesignHistoryTree());
    using var coordinator =
        new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    var owner = Required(
        coordinator.State.SelectedNode);
    var leftIconRow =
        new EmbeddedComponentSlotDefinition(
            "component.textBox.leftIconRow.editor",
            "iconRow",
            "Left icon row",
            "component.iconRow",
            ["textBox", "leftIconRowSlot"]);
    var button =
        new EmbeddedComponentSlotDefinition(
            "component.iconRow.button.editor",
            "button",
            "Button",
            "component.button",
            ["iconRow", "buttonSlot"]);
    var iconRowContext =
        new EditorEmbeddedContext(
            owner,
            [leftIconRow]);
    coordinator.ShowEmbeddedEditor(
        iconRowContext);
    coordinator.ShowEmbeddedEditor(
        iconRowContext.Nested(button));
    True(coordinator.TrySelectNodeById(
        "component-button::variant::default",
        "button-class",
        out _));

    True(coordinator.TryNavigateDesignHistory(
        -1,
        out var nested));
    SequenceEqual(
        [
            leftIconRow.FieldId,
            button.FieldId,
        ],
        Required(
                nested.Current.EmbeddedEditor)
            .Slots.Select(
                (slot) => slot.FieldId));
    True(coordinator.TryNavigateDesignHistory(
        -1,
        out var parent));
    SequenceEqual(
        [leftIconRow.FieldId],
        Required(
                parent.Current.EmbeddedEditor)
            .Slots.Select(
                (slot) => slot.FieldId));
    True(coordinator.TryNavigateDesignHistory(
        -1,
        out var root));
    True(root.Current.EmbeddedEditor is null);
    Equal(
        owner.Id,
        root.Current.SelectedNode?.Id);
}

static void NewDesignNavigationTruncatesForwardHistory()
{
    var source =
        new MutableNavigationDataSource(
            CreateDesignHistoryTree());
    using var coordinator =
        new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    True(coordinator.TrySelectNodeById(
        "component-icon-row::variant::default",
        "icon-row",
        out _));
    True(coordinator.TrySelectNodeById(
        "component-button::variant::default",
        "button",
        out _));
    True(coordinator.TryNavigateDesignHistory(
        -1,
        out _));
    True(coordinator.TrySelectNodeById(
        "component-a::variant::alternate",
        "alternate",
        out _));

    Equal(
        new EditorDesignNavigationAvailability(
            CanGoBack: true,
            CanGoForward: false),
        coordinator.DesignNavigationAvailability);
    True(!coordinator.TryNavigateDesignHistory(
        1,
        out _));
}

static void DesignNavigationHistorySkipsDeletedOwners()
{
    var source =
        new MutableNavigationDataSource(
            CreateDesignHistoryTree());
    using var coordinator =
        new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    True(coordinator.TrySelectNodeById(
        "component-icon-row::variant::default",
        "icon-row",
        out _));
    True(coordinator.TrySelectNodeById(
        "component-button::variant::default",
        "button",
        out _));

    source.Tree =
        CreateDesignHistoryTree(
            includeIconRow: false);
    coordinator.ReloadTree();
    True(coordinator.TryNavigateDesignHistory(
        -1,
        out var transition));
    Equal(
        "component-a::variant::default",
        transition.Current.SelectedNode?.Id);
}

static void NewerTreeLoadRejectsOlderResult()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    var first = coordinator.BeginTreeLoad(EditorWorkspace.Design);
    var second = coordinator.BeginTreeLoad(EditorWorkspace.Production);

    True(first.Token.IsCancellationRequested);
    True(!second.Token.IsCancellationRequested);
    True(!coordinator.TryCommitTreeLoad(
        first,
        CreateTree(),
        "stale",
        out _));
    True(coordinator.TryCommitTreeLoad(
        second,
        CreateTree(),
        "current",
        out _));
    Equal(EditorWorkspace.Production, coordinator.State.Workspace);
}

static void PreparedTreeRemainsInvisibleUntilCommit()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator =
        new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    var previous = coordinator.State;
    source.Tree = CreateTree(
        includeAlternateVariant: false);

    var preparation = coordinator
        .PrepareTreeReloadAsync()
        .GetAwaiter()
        .GetResult()
        ?? throw new InvalidOperationException(
            "Expected a prepared tree candidate.");

    Equal(previous, coordinator.State);
    True(coordinator.IsCurrentTreeLoad(
        preparation));
    coordinator.DiscardTreeLoad(preparation);
    Equal(previous, coordinator.State);
    True(!coordinator.HasPendingTreeLoad);

    preparation = coordinator
        .PrepareTreeReloadAsync()
        .GetAwaiter()
        .GetResult()
        ?? throw new InvalidOperationException(
            "Expected a second prepared tree candidate.");
    True(coordinator.TryCommitTreeLoad(
        preparation,
        "prepared-commit",
        out _));
    True(coordinator.State.Revision
        > previous.Revision);
}

static void AsyncTreeReadRunsOnWorker()
{
    var source = new BlockingNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    var ownerThreadId = Environment.CurrentManagedThreadId;

    var load = coordinator.ReloadTreeAsync();
    True(source.Started.Wait(TimeSpan.FromSeconds(3)));
    True(source.LoadThreadId != ownerThreadId);
    source.Release.Set();

    var transition = load.GetAwaiter().GetResult();
    True(transition is not null);
    Equal(
        "component-a::variant::default",
        coordinator.State.SelectedNode?.Id);
}

static void OnlyAsyncTreeLoadingIsPublic()
{
    var publicMethods = typeof(EditorWorkspaceCoordinator)
        .GetMethods(
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public)
        .Select((method) => method.Name)
        .ToHashSet(StringComparer.Ordinal);
    True(publicMethods.Contains(
        nameof(EditorWorkspaceCoordinator.ReloadTreeAsync)));
    True(publicMethods.Contains(
        nameof(EditorWorkspaceCoordinator.SwitchWorkspaceAsync)));
    True(!publicMethods.Contains("ReloadTree"));
    True(!publicMethods.Contains("SwitchWorkspace"));
}

static void RapidAsyncWorkspaceChangeDiscardsOlderResult()
{
    var source = new SequencedNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);

    var older = coordinator.ReloadTreeAsync("older");
    True(source.FirstStarted.Wait(TimeSpan.FromSeconds(3)));
    var current = coordinator.SwitchWorkspaceAsync(
            EditorWorkspace.Production,
            "current")
        .GetAwaiter()
        .GetResult();
    True(current is not null);
    source.ReleaseFirst.Set();

    True(older.GetAwaiter().GetResult() is null);
    Equal(EditorWorkspace.Production, coordinator.State.Workspace);
    Equal("episode-a", coordinator.State.SelectedNode?.Id);
}

static void ReturnToCurrentWorkspaceCancelsInFlightChange()
{
    var source = new SecondReadBlockingNavigationDataSource(
        CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();

    var production = coordinator.SwitchWorkspaceAsync(
        EditorWorkspace.Production,
        "production");
    True(source.SecondStarted.Wait(TimeSpan.FromSeconds(3)));
    True(coordinator.HasPendingTreeLoad);

    var design = coordinator.SwitchWorkspaceAsync(
            EditorWorkspace.Design,
            "design")
        .GetAwaiter()
        .GetResult();
    True(design is not null);
    source.ReleaseSecond.Set();

    True(production.GetAwaiter().GetResult() is null);
    Equal(EditorWorkspace.Design, coordinator.State.Workspace);
    True(!coordinator.HasPendingTreeLoad);
}

static void DisposeDiscardsPendingAsyncTreeResult()
{
    var source = new BlockingNavigationDataSource(CreateTree())
    {
        FailureAfterRelease =
            new InvalidOperationException(
                "The closed session must discard this read failure."),
    };
    var coordinator = new EditorWorkspaceCoordinator(source);
    var pending = coordinator.ReloadTreeAsync();
    True(source.Started.Wait(TimeSpan.FromSeconds(3)));

    coordinator.Dispose();
    source.Release.Set();

    True(pending.GetAwaiter().GetResult() is null);
}

static void SelectionInvalidatesInFlightLoad()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    var operation = coordinator.BeginTreeLoad(EditorWorkspace.Design);

    True(coordinator.TrySelectNodeById(
        "component-a::variant::alternate",
        "rapid-selection",
        out _));

    True(operation.Token.IsCancellationRequested);
    True(!coordinator.TryCommitTreeLoad(
        operation,
        CreateTree(),
        "obsolete-refresh",
        out _));
    Equal(
        "component-a::variant::alternate",
        coordinator.State.SelectedNode?.Id);
}

static void FailedTreeReadLeavesStateCurrent()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    var previous = coordinator.State;
    source.Failure = new InvalidOperationException("read failed");

    Throws<InvalidOperationException>(() => coordinator.ReloadTree());

    Equal(previous, coordinator.State);
    source.Failure = null;
    coordinator.ReloadTree();
    True(coordinator.State.Revision > previous.Revision);
}

static void SessionRevisionGuardsOwner()
{
    var source = new MutableNavigationDataSource(CreateTree());
    using var coordinator = new EditorWorkspaceCoordinator(source);
    coordinator.ReloadTree();
    var revision = coordinator.State.Revision;
    var ownerId = Required(coordinator.State.SelectedNode).Id;
    True(coordinator.IsCurrent(revision, ownerId));

    True(coordinator.TrySelectNodeById(
        "component-a::variant::alternate",
        "rapid-selection",
        out _));

    True(!coordinator.IsCurrent(revision, ownerId));
    True(coordinator.IsCurrent(
        coordinator.State.Revision,
        "component-a::variant::alternate"));
}

static IReadOnlyList<ProjectTreeNode> CreateTree(
    bool includeAlternateVariant = true) =>
    [CreateProject("project-a", includeAlternateVariant)];

static IReadOnlyList<ProjectTreeNode>
    CreateDesignHistoryTree(
        bool includeIconRow = true)
{
    var project =
        CreateProject("project-a");
    var apps = project.Children.Single(
        (node) =>
            node.Kind
            == ProjectTreeNodeKind.AppsRoot);
    if (includeIconRow)
    {
        AddHistoryComponent(
            apps,
            "component-icon-row",
            "Icon Row",
            "component.iconRow");
    }
    AddHistoryComponent(
        apps,
        "component-button",
        "Button",
        "component.button");
    return [project];
}

static void AddHistoryComponent(
    ProjectTreeNode parent,
    string id,
    string name,
    string recordClassId)
{
    var component = Node(
        ProjectTreeNodeKind.ComponentClass,
        id,
        name,
        recordClassId);
    parent.AddChild(component);
    component.AddChild(Node(
        ProjectTreeNodeKind.ComponentVariant,
        $"{id}::variant::default",
        "Default",
        "component.variant",
        isProtected: true));
}

static ProjectTreeNode CreateProject(
    string id,
    bool includeAlternateVariant = true)
{
    var project = Node(ProjectTreeNodeKind.Project, id, id, "project");
    var apps = Node(
        ProjectTreeNodeKind.AppsRoot,
        $"apps-{id}",
        "Apps",
        "navigation.apps");
    project.AddChild(apps);
    var component = Node(
        ProjectTreeNodeKind.ComponentClass,
        id == "project-a" ? "component-a" : $"component-{id}",
        "Component",
        "component.label");
    apps.AddChild(component);
    component.AddChild(Node(
        ProjectTreeNodeKind.ComponentVariant,
        id == "project-a"
            ? "component-a::variant::default"
            : $"component-{id}::variant::default",
        "Default",
        "component.variant",
        isProtected: true));
    if (includeAlternateVariant && id == "project-a")
    {
        component.AddChild(Node(
            ProjectTreeNodeKind.ComponentVariant,
            "component-a::variant::alternate",
            "Alternate",
            "component.variant"));
    }

    var episodes = Node(
        ProjectTreeNodeKind.EpisodesRoot,
        $"episodes-{id}",
        "Episodes",
        "navigation.episodes");
    project.AddChild(episodes);
    var episodeId = id == "project-a" ? "episode-a" : $"episode-{id}";
    var episode = Node(
        ProjectTreeNodeKind.Episode,
        episodeId,
        "Episode",
        "episode");
    episodes.AddChild(episode);
    episode.AddChild(Node(
        ProjectTreeNodeKind.Shot,
        id == "project-a" ? "shot-a" : $"shot-{id}",
        "Shot",
        "shot"));
    return project;
}

static ProjectTreeNode Node(
    ProjectTreeNodeKind kind,
    string id,
    string name,
    string recordClassId,
    bool isProtected = false) =>
    new(
        kind,
        id,
        name,
        "",
        recordClassId,
        isProtected: isProtected);

static T Required<T>(T? value) where T : class =>
    value ?? throw new InvalidOperationException(
        $"Expected {typeof(T).Name}.");

static void True(bool condition)
{
    if (!condition) throw new InvalidOperationException(
        "Expected condition to be true.");
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"Expected '{expected}', got '{actual}'.");
    }
}

static void SequenceEqual<T>(
    IEnumerable<T> expected,
    IEnumerable<T> actual)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            "Expected sequences to be equal.");
    }
}

static void Throws<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException(
        $"Expected {typeof(TException).Name}.");
}

internal sealed class MutableNavigationDataSource(
    IReadOnlyList<ProjectTreeNode> tree) : IEditorNavigationDataSource
{
    public IReadOnlyList<ProjectTreeNode> Tree { get; set; } = tree;
    public Exception? Failure { get; set; }

    public IReadOnlyList<ProjectTreeNode> LoadProjectTree() =>
        Failure is null ? Tree : throw Failure;
}

internal sealed class BlockingNavigationDataSource(
    IReadOnlyList<ProjectTreeNode> tree) : IEditorNavigationDataSource
{
    public ManualResetEventSlim Started { get; } = new();
    public ManualResetEventSlim Release { get; } = new();
    public int LoadThreadId { get; private set; }
    public Exception? FailureAfterRelease { get; init; }

    public IReadOnlyList<ProjectTreeNode> LoadProjectTree()
    {
        LoadThreadId = Environment.CurrentManagedThreadId;
        Started.Set();
        if (!Release.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException(
                "Timed out waiting to release the test tree read.");
        }
        if (FailureAfterRelease is not null)
        {
            throw FailureAfterRelease;
        }
        return tree;
    }
}

internal sealed class SequencedNavigationDataSource(
    IReadOnlyList<ProjectTreeNode> tree) : IEditorNavigationDataSource
{
    private int _calls;

    public ManualResetEventSlim FirstStarted { get; } = new();
    public ManualResetEventSlim ReleaseFirst { get; } = new();

    public IReadOnlyList<ProjectTreeNode> LoadProjectTree()
    {
        if (Interlocked.Increment(ref _calls) == 1)
        {
            FirstStarted.Set();
            if (!ReleaseFirst.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException(
                    "Timed out waiting to release the first test tree read.");
            }
        }
        return tree;
    }
}

internal sealed class SecondReadBlockingNavigationDataSource(
    IReadOnlyList<ProjectTreeNode> tree) : IEditorNavigationDataSource
{
    private int _calls;

    public ManualResetEventSlim SecondStarted { get; } = new();
    public ManualResetEventSlim ReleaseSecond { get; } = new();

    public IReadOnlyList<ProjectTreeNode> LoadProjectTree()
    {
        if (Interlocked.Increment(ref _calls) == 2)
        {
            SecondStarted.Set();
            if (!ReleaseSecond.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException(
                    "Timed out waiting to release the second test tree read.");
            }
        }
        return tree;
    }
}
