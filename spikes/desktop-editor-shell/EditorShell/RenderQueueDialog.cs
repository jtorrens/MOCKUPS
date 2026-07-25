using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mockups.DesktopEditorShell.Common;
using SukiUI.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    public async Task Show(RenderQueueShotDraft draft)
    {
        var dialog = new SukiWindow
        {
            Title = "Render Queue",
            Width = 1060,
            Height = 760,
            MinWidth = 900,
            MinHeight = 620,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var device = Combo(
            draft.Devices,
            draft.DefaultDeviceId);
        var theme = Combo(
            draft.Themes,
            draft.DefaultThemeId);
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
        var routeOptions = draft.Routes.Select((route) =>
            new FieldOption(route.EntryId, route.RelativeDirectory)).ToList();
        var rememberedRoute = _queue.LastRoute(draft.ProjectId);
        var route = Combo(routeOptions, rememberedRoute);
        if (string.IsNullOrWhiteSpace(rememberedRoute))
        {
            route.SelectedItem = null;
        }
        var baseName = EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = draft.SuggestedBaseName,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        });
        var proposal = new TextBlock
        {
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };
        var validation = new TextBlock
        {
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
        };
        var add = new Button
        {
            Content = "Add to queue",
            MinWidth = 132,
        };

        RenderOutputPlan? currentPlan = null;
        void RefreshProposal()
        {
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
                        ? "Choose one of the predefined Shot Manager output routes."
                        : "Complete every render option.";
                    proposal.Text = "";
                    add.IsEnabled = false;
                    return;
                }
                var routeContract = draft.Routes.Single((candidate) =>
                    candidate.EntryId.Equals(
                        route.SelectedItem.Value,
                        StringComparison.Ordinal));
                var mode = RenderOutputModes.Require(
                    outputMode.SelectedItem.Value);
                currentPlan = RenderOutputPlanner.Suggest(
                    draft.RootPath,
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
                validation.Text = draft.UsesCachedRoot
                    ? "Shot Manager is offline. Using the last known root for this workstation."
                    : "";
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

        var jobsPanel = new StackPanel { Spacing = 8 };
        var pause = new Button { MinWidth = 92 };
        var clear = new Button
        {
            Content = "Clear finished",
            MinWidth = 110,
        };
        void RefreshJobs()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(RefreshJobs);
                return;
            }
            pause.Content = _queue.Paused ? "Resume" : "Pause";
            var jobs = _queue.Jobs();
            clear.IsEnabled = jobs.Any((job) =>
                RenderQueueStatus.IsTerminal(job.Status));
            jobsPanel.Children.Clear();
            if (jobs.Count == 0)
            {
                jobsPanel.Children.Add(new TextBlock
                {
                    Text = "The local queue is empty.",
                    Opacity = 0.68,
                    Margin = new Thickness(2, 12),
                });
                return;
            }
            foreach (var job in jobs)
            {
                jobsPanel.Children.Add(CreateJobRow(job, RefreshJobs));
            }
        }
        pause.Click += (_, _) => _queue.SetPaused(!_queue.Paused);
        clear.Click += (_, _) => _queue.ClearFinished();

        add.Click += async (_, _) =>
        {
            if (currentPlan is null
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
            validation.Text = "Freezing current Shot frames…";
            var selectedDeviceId = device.SelectedItem.Value;
            var selectedThemeId = theme.SelectedItem.Value;
            var selectedAppearance = appearance.SelectedItem.Value;
            var selectedOutputMode = outputMode.SelectedItem.Value;
            var selectedRouteId = route.SelectedItem.Value;
            var selectedBaseName = baseName.Text ?? "";
            var selectedPlan = currentPlan;
            try
            {
                var snapshots = await _snapshots.BuildAsync(
                    draft,
                    selectedDeviceId,
                    selectedThemeId,
                    selectedAppearance,
                    selectedOutputMode,
                    selectedRouteId,
                    selectedBaseName,
                    selectedPlan);
                _queue.EnqueueBatch(snapshots);
                _queue.RememberRoute(
                    draft.ProjectId,
                    selectedRouteId);
                RefreshJobs();
                RefreshProposal();
                validation.Foreground = null;
                validation.Text =
                    $"{snapshots.Count} render job{(snapshots.Count == 1 ? "" : "s")} added.";
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

        var close = new Button
        {
            Content = "Close",
            MinWidth = 92,
        };
        close.Click += (_, _) => dialog.Close();
        var actorValue = new TextBlock
        {
            Text = draft.ActorName,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var form = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = draft.Shot.Name,
                    FontSize = 19,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = $"{draft.TotalFrames} frames · {draft.Fps} fps · no audio",
                    Opacity = 0.68,
                },
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
        var queueHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Local queue",
                            FontSize = 17,
                            FontWeight = FontWeight.Bold,
                        },
                        new TextBlock
                        {
                            Text = "One job runs at a time. Editing can continue.",
                            Opacity = 0.68,
                        },
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { pause, clear },
                },
            },
        };
        Grid.SetColumn(queueHeader.Children[1], 1);
        var queueContent = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 12,
            Children =
            {
                queueHeader,
                new ScrollViewer
                {
                    VerticalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility =
                        Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = jobsPanel,
                },
            },
        };
        Grid.SetRow(queueContent.Children[1], 1);
        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("420,*"),
            ColumnSpacing = 20,
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
                queueContent,
            },
        };
        Grid.SetColumn(queueContent, 1);
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
            Children = { columns, footer },
        };
        Grid.SetRow(footer, 1);
        dialog.Content = new Border
        {
            Padding = EditorUiDensity.CardThickness(18),
            Child = root,
        };
        void QueueChanged() => RefreshJobs();
        _queue.Changed += QueueChanged;
        dialog.Closed += (_, _) => _queue.Changed -= QueueChanged;
        dialog.Opened += (_, _) =>
        {
            RefreshProposal();
            RefreshJobs();
        };
        await dialog.ShowDialog(_owner);
    }

    private Control CreateJobRow(
        RenderQueueJobView job,
        Action refresh)
    {
        var progressMaximum = Math.Max(1, job.Progress.Total);
        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = progressMaximum,
            Value = Math.Clamp(
                job.Progress.Current,
                0,
                progressMaximum),
            Height = 5,
        };
        var title = Path.GetFileName(job.Summary.Output.OutputPath);
        var content = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(title)
                        ? job.Summary.Context.ShotName
                        : title,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text =
                        $"{job.Status} · {job.Progress.Phase} · "
                        + $"{job.Summary.ThemeName} · {job.Summary.DeviceName}",
                    FontSize = 12,
                    Opacity = 0.72,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                progress,
            },
        };
        if (!string.IsNullOrWhiteSpace(job.Error))
        {
            content.Children.Add(new TextBlock
            {
                Text = job.Error,
                Foreground = Brushes.IndianRed,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 4,
        };
        if (!RenderQueueStatus.IsTerminal(job.Status))
        {
            actions.Children.Add(ActionButton(
                "Cancel",
                () =>
                {
                    _queue.Cancel(job.Id);
                    refresh();
                }));
        }
        else
        {
            if (job.Status is RenderQueueStatus.Failed
                    or RenderQueueStatus.Canceled
                && job.SnapshotAvailable)
            {
                actions.Children.Add(ActionButton(
                    "Retry",
                    () =>
                    {
                        _queue.Retry(job.Id);
                        refresh();
                    }));
            }
            if (job.Status == RenderQueueStatus.Completed)
            {
                actions.Children.Add(ActionButton(
                    "Reveal",
                    () => Reveal(job.Summary.Output.OutputPath)));
            }
            actions.Children.Add(ActionButton(
                "Remove",
                () =>
                {
                    _queue.Remove(job.Id);
                    refresh();
                }));
        }
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children = { content, actions },
        };
        Grid.SetColumn(actions, 1);
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = EditorUiVisuals.ConnectorBrush(
                _owner.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark),
            Child = grid,
        };
    }

    private static EditorInstantComboBox Combo(
        System.Collections.Generic.IReadOnlyList<FieldOption> options,
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

    private static Control Field(string label, Control control)
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

    private static Button ActionButton(string label, Action activate)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 64,
            Padding = new Thickness(8, 4),
        };
        button.Click += (_, _) => activate();
        return button;
    }

    private static void Reveal(string outputPath)
    {
        var target = Directory.Exists(outputPath)
            ? outputPath
            : Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(target)
            || !Directory.Exists(target))
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Revealing is a convenience; the completed output remains valid.
        }
    }
}
