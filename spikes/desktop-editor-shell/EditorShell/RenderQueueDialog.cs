using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RenderQueueDialog
{
    private readonly Window _owner;
    private readonly RenderQueueManager _queue;
    private readonly RenderJobSnapshotFactory _snapshots;

    public RenderQueueDialog(
        Window owner,
        RenderQueueManager queue,
        RenderJobSnapshotFactory snapshots)
    {
        _owner = owner;
        _queue = queue;
        _snapshots = snapshots;
    }

    public async Task Show(
        ProjectTreeNode shot,
        Func<CancellationToken, Task<RenderQueueShotDraft>>
            loadDraft)
    {
        var dialog = new SukiWindow
        {
            Title = "Add to Render Queue",
            Width = 680,
            Height = 720,
            MinWidth = 560,
            MinHeight = 580,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var device = Combo([], null);
        var theme = Combo([], null);
        var appearance = Combo(
        [
            new FieldOption(RenderQueueAppearance.Light, "Light"),
            new FieldOption(RenderQueueAppearance.Dark, "Dark"),
            new FieldOption(RenderQueueAppearance.Both, "Light + Dark"),
        ],
            RenderQueueAppearance.Both);
        var outputMode = Combo(
            RenderOutputModes.All.Select((mode) =>
                new FieldOption(mode.Id, mode.Label)).ToList(),
            RenderOutputModes.MovProRes422Hq);
        var route = Combo([], null);
        var baseName = EditorTextBoxBehavior.Configure(new TextBox
        {
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        });
        var actorValue = new TextBlock
        {
            Text = "Loading…",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var shotDetails = new TextBlock
        {
            Text = "Resolving Shot Manager routes…",
            Opacity = 0.68,
        };
        var proposal = new TextBlock
        {
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };
        var validation = new TextBlock
        {
            Text = "Loading the render options for this Shot…",
            TextWrapping = TextWrapping.Wrap,
        };
        var add = new Button
        {
            Content = "Add to queue",
            MinWidth = 132,
            IsEnabled = false,
        };

        var draftControls = new Control[]
        {
            device,
            theme,
            appearance,
            outputMode,
            route,
            baseName,
        };
        foreach (var control in draftControls)
        {
            control.IsEnabled = false;
        }

        RenderQueueShotDraft? currentDraft = null;
        RenderOutputPlan? currentPlan = null;
        void RefreshProposal()
        {
            if (currentDraft is null)
            {
                currentPlan = null;
                proposal.Text = "";
                add.IsEnabled = false;
                return;
            }
            try
            {
                validation.Foreground = null;
                currentPlan = null;
                if (device.SelectedItem is null
                    || theme.SelectedItem is null
                    || appearance.SelectedItem is null
                    || outputMode.SelectedItem is null
                    || route.SelectedItem is null)
                {
                    validation.Text = route.SelectedItem is null
                        ? string.IsNullOrWhiteSpace(
                            currentDraft.RouteStatusMessage)
                            ? "Choose one of the predefined Shot Manager output routes."
                            : currentDraft.RouteStatusMessage
                        : "Complete every render option.";
                    proposal.Text = "";
                    add.IsEnabled = false;
                    return;
                }
                var routeContract = currentDraft.Routes.Single(
                    (candidate) => candidate.EntryId.Equals(
                        route.SelectedItem.Value,
                        StringComparison.Ordinal));
                var mode = RenderOutputModes.Require(
                    outputMode.SelectedItem.Value);
                currentPlan = RenderOutputPlanner.Suggest(
                    currentDraft.RootPath,
                    routeContract.RelativeDirectory,
                    baseName.Text ?? "",
                    RenderQueueAppearance.Expand(
                        appearance.SelectedItem.Value),
                    mode,
                    routeContract.VersionPadding,
                    _queue.ActiveOutputPaths());
                var names = currentPlan.OutputPaths
                    .OrderBy((pair) => pair.Key, StringComparer.Ordinal)
                    .Select((pair) => Path.GetFileName(pair.Value));
                proposal.Text =
                    $"Version v{currentPlan.Version.ToString().PadLeft(routeContract.VersionPadding, '0')} · "
                    + string.Join(" · ", names);
                validation.Text = currentDraft.RouteStatusMessage;
                add.IsEnabled = true;
            }
            catch (Exception exception)
            {
                currentPlan = null;
                proposal.Text = "";
                validation.Foreground = Brushes.IndianRed;
                validation.Text = exception.Message;
                add.IsEnabled = false;
            }
        }

        foreach (var combo in new[]
        {
            device,
            theme,
            appearance,
            outputMode,
            route,
        })
        {
            combo.SelectionChanged += (_, _) => RefreshProposal();
        }
        baseName.TextChanged += (_, _) => RefreshProposal();

        add.Click += (_, _) =>
        {
            if (currentDraft is null
                || currentPlan is null
                || device.SelectedItem is null
                || theme.SelectedItem is null
                || appearance.SelectedItem is null
                || outputMode.SelectedItem is null
                || route.SelectedItem is null)
            {
                return;
            }
            add.IsEnabled = false;
            validation.Foreground = null;
            validation.Text = "Adding batch to the local queue…";
            try
            {
                var preparation = _snapshots.PlanBatch(
                    currentDraft,
                    device.SelectedItem.Value,
                    theme.SelectedItem.Value,
                    appearance.SelectedItem.Value,
                    outputMode.SelectedItem.Value,
                    route.SelectedItem.Value,
                    baseName.Text ?? "",
                    currentPlan);
                _queue.EnqueuePreparingBatch(
                    preparation.Summaries,
                    (batchRoot, progress, cancellationToken) =>
                        _snapshots.FreezeAsync(
                            preparation,
                            batchRoot,
                            progress,
                            cancellationToken));
                _queue.RememberRoute(
                    currentDraft.ProjectId,
                    route.SelectedItem.Value);
                dialog.Close();
            }
            catch (Exception exception)
            {
                var message = exception.Message;
                RefreshProposal();
                validation.Foreground = Brushes.IndianRed;
                validation.Text = message;
                add.IsEnabled = currentPlan is not null;
            }
        };

        var form = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = shot.Name,
                    FontSize = 19,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                },
                shotDetails,
                Field("Actor", actorValue),
                Field("Device", device),
                Field("Theme", theme),
                Field("Appearance", appearance),
                Field("Output", outputMode),
                Field("Route", route),
                Field("Base name", baseName),
                proposal,
                validation,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { add },
                },
            },
        };
        var close = new Button
        {
            Content = "Close",
            MinWidth = 92,
        };
        close.Click += (_, _) => dialog.Close();
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { close },
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 16,
            Children =
            {
                new ScrollViewer
                {
                    VerticalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = form,
                },
                footer,
            },
        };
        Grid.SetRow(footer, 1);
        dialog.Content = new Border
        {
            Padding = EditorUiDensity.CardThickness(18),
            Child = root,
        };

        using var cancellation = new CancellationTokenSource();
        dialog.Closed += (_, _) => cancellation.Cancel();
        dialog.Opened += async (_, _) =>
        {
            try
            {
                currentDraft = await loadDraft(cancellation.Token);
                device.ItemsSource = currentDraft.Devices;
                device.SelectedItem = currentDraft.Devices.FirstOrDefault(
                    (option) => option.Value.Equals(
                        currentDraft.DefaultDeviceId,
                        StringComparison.Ordinal));
                theme.ItemsSource = currentDraft.Themes;
                theme.SelectedItem = currentDraft.Themes.FirstOrDefault(
                    (option) => option.Value.Equals(
                        currentDraft.DefaultThemeId,
                        StringComparison.Ordinal));
                var routeOptions = currentDraft.Routes.Select(
                    (candidate) => new FieldOption(
                        candidate.EntryId,
                        candidate.RelativeDirectory)).ToList();
                route.ItemsSource = routeOptions;
                var rememberedRoute = _queue.LastRoute(
                    currentDraft.ProjectId);
                route.SelectedItem = routeOptions.FirstOrDefault(
                    (option) => option.Value.Equals(
                        rememberedRoute,
                        StringComparison.Ordinal))
                    ?? routeOptions.FirstOrDefault();
                baseName.Text = currentDraft.SuggestedBaseName;
                actorValue.Text = currentDraft.ActorName;
                shotDetails.Text =
                    $"Shot {currentDraft.ShotNumber} · "
                    + $"{currentDraft.TotalFrames} frames · "
                    + $"{currentDraft.Fps} fps · no audio";
                foreach (var control in draftControls)
                {
                    control.IsEnabled = true;
                }
                route.IsEnabled = currentDraft.Routes.Count > 0;
                RefreshProposal();
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
                // The user closed the modal while its routes were resolving.
            }
            catch (Exception exception)
            {
                actorValue.Text = "Unavailable";
                shotDetails.Text =
                    "This Shot cannot be added until its output route is available.";
                validation.Foreground = Brushes.IndianRed;
                validation.Text = exception.Message;
                RefreshProposal();
            }
        };
        await dialog.ShowDialog(_owner);
    }

    private static EditorInstantComboBox Combo(
        IReadOnlyList<FieldOption> options,
        string? selectedValue)
    {
        return new EditorInstantComboBox
        {
            ItemsSource = options,
            SelectedItem = options.FirstOrDefault((option) =>
                option.Value.Equals(
                    selectedValue,
                    StringComparison.Ordinal)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private static Control Field(
        string label,
        Control control)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("104,*"),
            ColumnSpacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                control,
            },
        };
        Grid.SetColumn(control, 1);
        return grid;
    }
}
