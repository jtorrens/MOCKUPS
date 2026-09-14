using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ProductionNavigationTransferGesture
{
    private static readonly DataFormat<string> TransferFormat =
        DataFormat.CreateStringApplicationFormat(
            "mockups-production-hierarchy-transfer");

    internal static void Attach(
        Border row,
        ProjectTreeNode node,
        Func<
            ProjectTreeNode,
            ProjectTreeNode,
            ProductionHierarchyTransferMode,
            Task> transfer,
        Action<Exception> reportFailure)
    {
        if (node.Kind is ProjectTreeNodeKind.Shot
            or ProjectTreeNodeKind.ModuleInstance)
        {
            AttachSourceHandlers();
        }

        if (node.Kind is not ProjectTreeNodeKind.Episode
            and not ProjectTreeNodeKind.Shot)
        {
            return;
        }

        var originalBrush = row.BorderBrush;
        var originalThickness = row.BorderThickness;
        DragDrop.SetAllowDrop(row, true);
        DragDrop.AddDragOverHandler(row, (_, args) =>
        {
            var drag = Transfer(args);
            if (drag is null
                || !ProductionHierarchyTransferContract.CanTransfer(
                    drag.Source,
                    node))
            {
                args.DragEffects = DragDropEffects.None;
                RestoreTarget();
                return;
            }

            args.DragEffects = Effect(drag.Mode);
            row.BorderBrush = EditorAnimationVisuals.ActiveTrackBrush;
            row.BorderThickness = new Thickness(2);
            args.Handled = true;
        });
        DragDrop.AddDragLeaveHandler(row, (_, _) => RestoreTarget());
        DragDrop.AddDropHandler(row, async (_, args) =>
        {
            var drag = Transfer(args);
            RestoreTarget();
            if (drag is null
                || !ProductionHierarchyTransferContract.CanTransfer(
                    drag.Source,
                    node))
            {
                args.DragEffects = DragDropEffects.None;
                return;
            }

            args.DragEffects = Effect(drag.Mode);
            args.Handled = true;
            await transfer(drag.Source, node, drag.Mode);
        });

        void AttachSourceHandlers()
        {
            PointerPressedEventArgs? pendingPress = null;
            Point pendingOrigin = default;
            ProductionHierarchyTransferMode? pendingMode = null;

            row.AddHandler(
                InputElement.PointerPressedEvent,
                OnPointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            row.AddHandler(
                InputElement.PointerMovedEvent,
                OnPointerMoved,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            row.AddHandler(
                InputElement.PointerReleasedEvent,
                (_, _) => CancelPendingDrag(),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            row.PointerCaptureLost += (_, _) => CancelPendingDrag();

            void OnPointerPressed(
                object? sender,
                PointerPressedEventArgs args)
            {
                if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed
                    || args.Source is Visual source
                    && source.FindAncestorOfType<Button>() is not null
                    || TransferMode(args.KeyModifiers) is not { } mode)
                {
                    return;
                }

                pendingPress = args;
                pendingOrigin = args.GetPosition(row);
                pendingMode = mode;
                args.Pointer.Capture(row);
                args.Handled = true;
            }

            void OnPointerMoved(
                object? sender,
                PointerEventArgs args)
            {
                if (pendingPress is null
                    || pendingMode is not { } mode)
                {
                    return;
                }
                if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                {
                    CancelPendingDrag();
                    return;
                }

                var current = args.GetPosition(row);
                if (Math.Abs(current.X - pendingOrigin.X) < 4
                    && Math.Abs(current.Y - pendingOrigin.Y) < 4)
                {
                    return;
                }

                var trigger = pendingPress;
                pendingPress = null;
                pendingMode = null;
                args.Pointer.Capture(null);
                args.Handled = true;
                StartDrag(trigger, mode);
            }

            async void StartDrag(
                PointerPressedEventArgs trigger,
                ProductionHierarchyTransferMode mode)
            {
                try
                {
                    var data = new DataTransfer();
                    data.Add(DataTransferItem.Create(
                        TransferFormat,
                        Serialize(node, mode)));
                    await DragDrop.DoDragDropAsync(
                        trigger,
                        data,
                        Effect(mode));
                }
                catch (Exception exception)
                {
                    reportFailure(exception);
                }
            }

            void CancelPendingDrag()
            {
                var pointer = pendingPress?.Pointer;
                pendingPress = null;
                pendingMode = null;
                pointer?.Capture(null);
            }
        }

        void RestoreTarget()
        {
            row.BorderBrush = originalBrush;
            row.BorderThickness = originalThickness;
        }
    }

    internal static ProductionHierarchyTransferMode? TransferMode(
        KeyModifiers modifiers)
    {
        var command = modifiers.HasFlag(KeyModifiers.Meta);
        var option = modifiers.HasFlag(KeyModifiers.Alt);
        return (command, option) switch
        {
            (true, false) => ProductionHierarchyTransferMode.Copy,
            (false, true) => ProductionHierarchyTransferMode.Move,
            _ => null,
        };
    }

    private static TransferDrag? Transfer(DragEventArgs args)
    {
        var serialized = args.DataTransfer.TryGetValue(TransferFormat);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<TransferPayload>(
                serialized);
            if (payload is null
                || payload.SourceKind is not ProjectTreeNodeKind.Shot
                    and not ProjectTreeNodeKind.ModuleInstance
                || !Enum.IsDefined(payload.Mode)
                || string.IsNullOrWhiteSpace(payload.SourceId)
                || string.IsNullOrWhiteSpace(payload.SourceParentId)
                || string.IsNullOrWhiteSpace(payload.ProjectId))
            {
                return null;
            }

            var project = new ProjectTreeNode(
                ProjectTreeNodeKind.Project,
                payload.ProjectId,
                payload.ProjectId,
                "",
                "project");
            var parent = new ProjectTreeNode(
                payload.SourceKind == ProjectTreeNodeKind.Shot
                    ? ProjectTreeNodeKind.Episode
                    : ProjectTreeNodeKind.Shot,
                payload.SourceParentId,
                payload.SourceParentId,
                "",
                "",
                project);
            var source = new ProjectTreeNode(
                payload.SourceKind,
                payload.SourceId,
                payload.SourceName,
                payload.SourceNotes,
                payload.SourceRecordClassId,
                parent);
            return new TransferDrag(source, payload.Mode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Serialize(
        ProjectTreeNode source,
        ProductionHierarchyTransferMode mode)
    {
        var project = ProjectAncestor(source)
            ?? throw new InvalidOperationException(
                $"Missing Project owner for {source.Kind} '{source.Id}'.");
        return JsonSerializer.Serialize(new TransferPayload(
            source.Kind,
            source.Id,
            source.Name,
            source.Notes,
            source.RecordClassId,
            source.Parent!.Id,
            project.Id,
            mode));
    }

    private static ProjectTreeNode? ProjectAncestor(ProjectTreeNode node)
    {
        for (ProjectTreeNode? current = node;
             current is not null;
             current = current.Parent)
        {
            if (current.Kind == ProjectTreeNodeKind.Project)
            {
                return current;
            }
        }
        return null;
    }

    private static DragDropEffects Effect(
        ProductionHierarchyTransferMode mode) =>
        mode == ProductionHierarchyTransferMode.Copy
            ? DragDropEffects.Copy
            : DragDropEffects.Move;

    private sealed record TransferDrag(
        ProjectTreeNode Source,
        ProductionHierarchyTransferMode Mode);

    private sealed record TransferPayload(
        ProjectTreeNodeKind SourceKind,
        string SourceId,
        string SourceName,
        string SourceNotes,
        string SourceRecordClassId,
        string SourceParentId,
        string ProjectId,
        ProductionHierarchyTransferMode Mode);
}
