using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System.Diagnostics;
using System.Reflection;

internal static class TreeActionFeedbackRegressionTests
{
    public static void DeletePreparationAndCompletion()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApplication));
        session.Dispatch(() =>
        {
            var preview = new Border { Child = new TextBlock { Text = "Resident Preview" } };
            var owner = new Window { Width = 1000, Height = 700, Content = preview };
            owner.Show();
            EditorModalWindowScope.RegisterHost(owner);
            using var operations = new EditorOperationCoordinator();
            var scrim = new EditorLoadingScrim();
            using var presenter = new EditorOperationActivityPresenter(operations, scrim);
            var active = new HashSet<long>();
            operations.ActivityChanged += activity =>
            {
                if (activity.IsActive) active.Add(activity.Id);
                else active.Remove(activity.Id);
            };
            try
            {
                foreach (var kind in new[] { ProjectTreeNodeKind.ModuleInstance, ProjectTreeNodeKind.Actor })
                {
                    var parent = new ProjectTreeNode(ProjectTreeNodeKind.Project, "project", "Project", "", "projects");
                    var node = new ProjectTreeNode(kind, "target", "Target", "", ProjectTreeNode.DefaultRecordClassId(kind), parent);
                    parent.AddChild(node);
                    var reads = 0;
                    var writes = 0;
                    var reloads = 0;
                    using var release = new ManualResetEventSlim();
                    var reload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var query = TreeActionPortProxy.Create<IReferenceUsageQuery>((method, _) =>
                    {
                        if (method.Name != "GetReferenceUsageDetails") throw new InvalidOperationException(method.Name);
                        Interlocked.Increment(ref reads);
                        if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                        return Array.Empty<ReferenceUsageDetail>();
                    });
                    var commands = TreeActionPortProxy.Create<IEditorNodeCommandStore>((method, _) =>
                    {
                        if (method.Name != "Delete") throw new InvalidOperationException(method.Name);
                        Interlocked.Increment(ref writes);
                        return null;
                    });
                    var controller = new EditorNodeCommandController(owner, commands, query,
                        null!, null!, null!, operations, () => true, () => [parent],
                        () => throw new InvalidOperationException("Deletion must not reload before confirmation"),
                        _ => { reloads++; return reload.Task; }, () => { }, _ => Task.CompletedTask,
                        new EditorShellMessageSink(new TextBox()));
                    var deletion = controller.DeleteNode(node);
                    var dialog = owner.OwnedWindows.Single();
                    Require(dialog.IsVisible && ReferenceEquals(owner, dialog.Owner), "Dialog must be visible immediately with exact owner");
                    Require(preview.IsVisible && ReferenceEquals(owner.Content, preview), "Preview must remain mounted and visible");
                    Require(writes == 0 && reloads == 0, "Preparation cannot write or refresh the tree");
                    Require(!Buttons(dialog).Any(button => Equals(button.Content, "Delete")), "Delete cannot be available while preparing");
                    release.Set();
                    Pump(() => Buttons(dialog).Any(button => Equals(button.Content, "Delete")));
                    Require(!scrim.IsVisible, "Confirmation must not duplicate progress behind the modal");
                    Require(active.Count == 0, "Waiting for confirmation must not announce deletion");
                    Require(reads == 1, "Usage must be read exactly once");
                    Buttons(dialog).Single(button => Equals(button.Content, "Delete")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Pump(() => reloads == 1);
                    Require(!scrim.IsVisible, "Execution progress belongs only to the modal");
                    dialog.Close();
                    Require(dialog.IsVisible, "Execution cannot be dismissed before refresh finishes");
                    Require(dialog.IsVisible && !Buttons(dialog).Any(button => Equals(button.Content, "Delete")), "Execution must retain the same dialog without an enabled confirmation");
                    Require(writes == 1 && !deletion.IsCompleted && active.Count == 1, "Activity must include post-write refresh");
                    reload.SetResult();
                    Pump(() => deletion.IsCompleted);
                    deletion.GetAwaiter().GetResult();
                    Require(active.Count == 0 && reloads == 1, "Completion must clear activity after one refresh");
                }

                // An obsolete completion must not republish a closed loading dialog.
                var late = new TaskCompletionSource<IReadOnlyList<ReferenceUsageDetail>>(TaskCreationOptions.RunContinuationsAsynchronously);
                var item = new ProjectTreeNode(ProjectTreeNodeKind.Actor, "cancel", "Cancel", "", "actors");
                var pending = new EditorDialogService(owner, true).PrepareDelete(item, _ => late.Task, () => Task.CompletedTask);
                var closed = owner.OwnedWindows.Single();
                Dispatcher.UIThread.RunJobs();
                closed.Close();
                late.SetResult([]);
                Pump(() => pending.IsCompleted);
                Dispatcher.UIThread.RunJobs();
                Require(owner.OwnedWindows.Count == 0 && preview.IsVisible, "Canceled preparation cannot reopen or alter Preview");
            }
            finally
            {
                foreach (var dialog in owner.OwnedWindows.ToArray()) dialog.Close();
                owner.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static IEnumerable<Button> Buttons(Window window) => window.GetVisualDescendants().OfType<Button>();
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Pump(Func<bool> completed)
    {
        var timer = Stopwatch.StartNew();
        while (!completed())
        {
            Dispatcher.UIThread.RunJobs();
            if (timer.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Dialog did not complete its declared phase");
            Thread.Sleep(1);
        }
    }
}

public class TreeActionPortProxy : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?> _invoke = null!;
    internal static T Create<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var result = Create<T, TreeActionPortProxy>();
        ((TreeActionPortProxy)(object)result)._invoke = invoke;
        return result;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _invoke(targetMethod!, args);
}
