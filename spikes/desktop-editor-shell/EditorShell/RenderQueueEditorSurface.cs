using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RenderQueueEditorSurface
{
    private readonly Window _owner;
    private readonly RenderQueueManager _queue;

    public RenderQueueEditorSurface(
        Window owner,
        RenderQueueManager queue)
    {
        _owner = owner;
        _queue = queue;
    }

    public IReadOnlyList<InstantEditorCard>? CreateCards(
        ProjectTreeNode node)
    {
        if (!Owns(node))
        {
            return null;
        }

        return
        [
            new InstantEditorCard(
                EditorCardHeader.Create(
                    "Render Queue",
                    "Workstation-local jobs and render history",
                    EditorIcons.Create(EditorIcons.Render, 18)),
                new Border
                {
                    Padding = EditorUiDensity.CardThickness(10),
                    Child = new RenderQueueMonitorControl(
                        _owner,
                        _queue),
                },
                isExpanded: true)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SessionStateId = "render-queue:monitor",
            },
        ];
    }

    internal static bool Owns(ProjectTreeNode node) =>
        node.Kind == ProjectTreeNodeKind.RenderQueueRoot;
}

internal sealed class RenderQueueMonitorControl : StackPanel
{
    private readonly Window _owner;
    private readonly RenderQueueManager _queue;
    private readonly TextBlock _summary;
    private readonly Button _pause;
    private readonly Button _clear;
    private readonly StackPanel _batches;
    private bool _observing;

    public RenderQueueMonitorControl(
        Window owner,
        RenderQueueManager queue)
    {
        _owner = owner;
        _queue = queue;
        Name = "RenderQueueMonitor";
        Spacing = EditorUiDensity.Card(12);

        _summary = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _pause = ActionButton("Pause", TogglePause);
        _pause.Name = "RenderQueuePauseButton";
        _clear = ActionButton("Clear finished", ClearFinished);
        _clear.Name = "RenderQueueClearFinishedButton";

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { _pause, _clear },
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children = { _summary, actions },
        };
        Grid.SetColumn(actions, 1);
        Children.Add(header);

        if (!string.IsNullOrWhiteSpace(_queue.InitializationError))
        {
            Children.Add(new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.Parse("#22CD5C5C")),
                Child = new TextBlock
                {
                    Text = _queue.InitializationError,
                    Foreground = Brushes.IndianRed,
                    TextWrapping = TextWrapping.Wrap,
                },
            });
        }

        _batches = new StackPanel
        {
            Name = "RenderQueueBatches",
            Spacing = EditorUiDensity.Card(10),
        };
        Children.Add(_batches);

        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        Refresh();
    }

    private void OnAttached(
        object? sender,
        VisualTreeAttachmentEventArgs args)
    {
        if (_observing) return;
        _queue.Changed += QueueChanged;
        _observing = true;
        Refresh();
    }

    private void OnDetached(
        object? sender,
        VisualTreeAttachmentEventArgs args)
    {
        if (!_observing) return;
        _queue.Changed -= QueueChanged;
        _observing = false;
    }

    private void QueueChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
            return;
        }
        Dispatcher.UIThread.Post(Refresh);
    }

    private void Refresh()
    {
        var jobs = _queue.Jobs();
        var active = jobs.Count((job) =>
            !RenderQueueStatus.IsTerminal(job.Status));
        var failed = jobs.Count((job) =>
            job.Status == RenderQueueStatus.Failed);
        _summary.Text = jobs.Count == 0
            ? "The local queue is empty. Add a concrete Shot from its render icon."
            : $"{jobs.Count} job{(jobs.Count == 1 ? "" : "s")} · "
                + $"{active} active or pending"
                + (failed == 0 ? "" : $" · {failed} failed");
        _pause.Content = _queue.Paused ? "Resume" : "Pause";
        _pause.IsEnabled = string.IsNullOrWhiteSpace(
            _queue.InitializationError);
        _clear.IsEnabled = string.IsNullOrWhiteSpace(
                _queue.InitializationError)
            && jobs.Any((job) =>
                RenderQueueStatus.IsTerminal(job.Status));

        _batches.Children.Clear();
        if (jobs.Count == 0)
        {
            _batches.Children.Add(new Border
            {
                Padding = new Thickness(14, 20),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = ConnectorBrush(),
                Child = new TextBlock
                {
                    Text = "No render jobs yet.",
                    Opacity = 0.68,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            });
            return;
        }

        foreach (var batch in jobs
                     .GroupBy((job) => job.BatchId)
                     .Reverse())
        {
            _batches.Children.Add(CreateBatch(batch.ToList()));
        }
    }

    private Control CreateBatch(
        IReadOnlyList<RenderQueueJobView> jobs)
    {
        var first = jobs[0];
        var appearances = string.Join(
            " + ",
            jobs.Select((job) => job.Summary.Appearance)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(DisplayAppearance));
        var batchHeader = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = first.Summary.Context.ShotName,
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text =
                        $"{appearances} · {jobs.Count} output"
                        + (jobs.Count == 1 ? "" : "s")
                        + " · "
                        + RenderOutputModes.Require(
                            first.Summary.Output.OutputModeId).Label,
                    FontSize = 12,
                    Opacity = 0.72,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
        var rows = new StackPanel
        {
            Spacing = 8,
        };
        foreach (var job in jobs)
        {
            rows.Children.Add(CreateJobRow(job));
        }
        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = ConnectorBrush(),
            Child = new StackPanel
            {
                Spacing = 10,
                Children = { batchHeader, rows },
            },
        };
    }

    private Control CreateJobRow(RenderQueueJobView job)
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
        var outputName = Path.GetFileName(
            job.Summary.Output.OutputPath);
        var details = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(outputName)
                        ? DisplayAppearance(job.Summary.Appearance)
                        : outputName,
                    FontWeight = FontWeight.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text =
                        $"{job.Status} · {job.Progress.Phase} · "
                        + $"{job.Progress.Current}/{job.Progress.Total} frames · "
                        + $"{job.Summary.ThemeName} · {job.Summary.DeviceName}",
                    FontSize = 12,
                    Opacity = 0.72,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                progress,
                new TextBlock
                {
                    Text = job.Summary.Output.OutputPath,
                    FontSize = 11,
                    Opacity = 0.62,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
        if (!string.IsNullOrWhiteSpace(job.Error))
        {
            details.Children.Add(new TextBlock
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
                () => _queue.Cancel(job.Id)));
        }
        else
        {
            if ((job.Status is RenderQueueStatus.Failed
                    or RenderQueueStatus.Canceled)
                && job.SnapshotAvailable)
            {
                actions.Children.Add(ActionButton(
                    "Retry",
                    () => _queue.Retry(job.Id)));
            }
            if (job.Status == RenderQueueStatus.Completed)
            {
                actions.Children.Add(ActionButton(
                    "Reveal",
                    () => Reveal(job.Summary.Output.OutputPath)));
            }
            actions.Children.Add(ActionButton(
                "Remove",
                () => _queue.Remove(job.Id)));
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children = { details, actions },
        };
        Grid.SetColumn(actions, 1);
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.Parse(
                _owner.ActualThemeVariant
                    == Avalonia.Styling.ThemeVariant.Dark
                    ? "#14FFFFFF"
                    : "#0C000000")),
            Child = grid,
        };
    }

    private Button ActionButton(
        string label,
        Action activate)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 64,
            Padding = new Thickness(8, 4),
        };
        button.Click += (_, _) =>
        {
            activate();
            Refresh();
        };
        return EditorAccessibility.Describe(button, label);
    }

    private void TogglePause()
    {
        _queue.SetPaused(!_queue.Paused);
    }

    private void ClearFinished()
    {
        _queue.ClearFinished();
    }

    private IBrush ConnectorBrush() =>
        EditorUiVisuals.ConnectorBrush(
            _owner.ActualThemeVariant
                == Avalonia.Styling.ThemeVariant.Dark);

    private static string DisplayAppearance(string value) =>
        value.Equals(RenderQueueAppearance.Light, StringComparison.Ordinal)
            ? "Light"
            : value.Equals(RenderQueueAppearance.Dark, StringComparison.Ordinal)
                ? "Dark"
                : value;

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
