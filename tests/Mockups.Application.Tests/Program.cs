using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

var tests = new (string Name, Action Run)[]
{
    ("initial tree load resolves a selectable Design context", InitialTreeLoadSelectsDesignContext),
    ("workspace changes restore each workspace selection", WorkspaceChangesRestoreSelection),
    ("tree refresh replaces a deleted selection with a valid fallback", DeletedSelectionFallsBack),
    ("tree refresh replaces a deleted Production", DeletedProductionFallsBack),
    ("Production selection commits its first exact Production node", ProductionSelectionIsExact),
    ("synthetic Production navigation selects and restores Render Queue", SyntheticProductionNavigationIsSelectable),
    ("typed reference navigation changes workspace and exact Production", ReferenceNavigationChangesWorkspace),
    ("invalid node selection leaves the session unchanged", InvalidSelectionIsRejected),
    ("active editor refresh rebases embedded context to the new tree", ActiveEditorRefreshRebasesEmbeddedContext),
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
    ("Runtime documents reject missing and parent-owned values", RuntimeDocumentsRejectInvalidOwnership),
    ("Runtime contract transitions retain only current values and animation owners", RuntimeContractTransitionsRetainCurrentOwners),
    ("editor operations execute away from the caller thread", EditorOperationsRunOnWorker),
    ("editor operations preserve their submission order", EditorOperationsAreSerialized),
    ("disposing editor operations cancels queued work", DisposeCancelsQueuedEditorOperations),
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
        Console.Error.WriteLine(
            $"FAIL {name}: {exception.GetBaseException().Message}");
    }
}

Console.WriteLine(
    $"Application workspace tests: {tests.Length - failures.Count}/{tests.Length} passed.");
if (failures.Count > 0) Environment.Exit(1);

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
