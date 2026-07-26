using Mockups.DesktopEditorShell.EditorShell;

var tests = new (string Name, Action Run)[]
{
    ("initial tree load resolves a selectable Design context", InitialTreeLoadSelectsDesignContext),
    ("workspace changes restore each workspace selection", WorkspaceChangesRestoreSelection),
    ("tree refresh replaces a deleted selection with a valid fallback", DeletedSelectionFallsBack),
    ("tree refresh replaces a deleted Production", DeletedProductionFallsBack),
    ("Production selection commits its first exact Production node", ProductionSelectionIsExact),
    ("typed reference navigation changes workspace and exact Production", ReferenceNavigationChangesWorkspace),
    ("invalid node selection leaves the session unchanged", InvalidSelectionIsRejected),
    ("active editor refresh rebases embedded context to the new tree", ActiveEditorRefreshRebasesEmbeddedContext),
    ("a newer tree load cancels and rejects the older result", NewerTreeLoadRejectsOlderResult),
    ("a selection transition invalidates an in-flight tree result", SelectionInvalidatesInFlightLoad),
    ("a failed tree read leaves the prior session state current", FailedTreeReadLeavesStateCurrent),
    ("session revisions identify only the current owner transition", SessionRevisionGuardsOwner),
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
