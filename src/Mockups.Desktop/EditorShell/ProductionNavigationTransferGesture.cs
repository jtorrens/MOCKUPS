using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ProductionNavigationTransferGesture
{
    private static readonly DataFormat<TransferDrag> TransferFormat =
        DataFormat.CreateInProcessFormat<TransferDrag>(
            "mockups-production-hierarchy-transfer");

    internal static void Attach(
        Border row,
        ProjectTreeNode node,
        Func<
            ProjectTreeNode,
            ProjectTreeNode,
            ProductionHierarchyTransferMode,
            Task> transfer)
    {
        if (node.Kind is ProjectTreeNodeKind.Shot
            or ProjectTreeNodeKind.ModuleInstance)
        {
            row.AddHandler(
                InputElement.PointerPressedEvent,
                OnPointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
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

        async void OnPointerPressed(
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

            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(
                TransferFormat,
                new TransferDrag(node, mode)));
            args.Handled = true;
            await DragDrop.DoDragDropAsync(
                args,
                data,
                Effect(mode));
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

    private static TransferDrag? Transfer(DragEventArgs args) =>
        args.DataTransfer.TryGetValue(TransferFormat);

    private static DragDropEffects Effect(
        ProductionHierarchyTransferMode mode) =>
        mode == ProductionHierarchyTransferMode.Copy
            ? DragDropEffects.Copy
            : DragDropEffects.Move;

    private sealed record TransferDrag(
        ProjectTreeNode Source,
        ProductionHierarchyTransferMode Mode);
}
