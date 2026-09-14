using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ProductionNavigationTransferGesture
{
    private static readonly ConditionalWeakTable<
        Border,
        TransferTarget> Targets = new();

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
        if (node.Kind is ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot)
        {
            Targets.Add(
                row,
                new TransferTarget(
                    row,
                    node,
                    row.BorderBrush,
                    row.BorderThickness));
        }

        if (node.Kind is not ProjectTreeNodeKind.Shot
            and not ProjectTreeNodeKind.ModuleInstance)
        {
            return;
        }

        Point pressOrigin = default;
        IPointer? pointer = null;
        ProductionHierarchyTransferMode? mode = null;
        TransferTarget? activeTarget = null;
        var dragging = false;

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
            OnPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        row.PointerCaptureLost += (_, _) => Cancel();

        void OnPointerPressed(
            object? sender,
            PointerPressedEventArgs args)
        {
            if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed
                || args.Source is Visual source
                && source.FindAncestorOfType<Button>() is not null
                || TransferMode(args.KeyModifiers) is not { } selectedMode)
            {
                return;
            }

            pressOrigin = args.GetPosition(row);
            pointer = args.Pointer;
            mode = selectedMode;
            dragging = false;
            pointer.Capture(row);
            args.Handled = true;
        }

        void OnPointerMoved(
            object? sender,
            PointerEventArgs args)
        {
            if (pointer is null
                || mode is null)
            {
                return;
            }
            if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            {
                Cancel();
                return;
            }

            if (!dragging)
            {
                var current = args.GetPosition(row);
                if (Math.Abs(current.X - pressOrigin.X) < 4
                    && Math.Abs(current.Y - pressOrigin.Y) < 4)
                {
                    return;
                }
                dragging = true;
            }

            SetActiveTarget(TargetAt(args));
            args.Handled = true;
        }

        async void OnPointerReleased(
            object? sender,
            PointerReleasedEventArgs args)
        {
            if (pointer is null
                || mode is not { } selectedMode)
            {
                return;
            }

            var target = dragging
                ? TargetAt(args) ?? activeTarget
                : null;
            Cancel();
            args.Handled = true;
            if (target is null)
            {
                return;
            }

            try
            {
                await transfer(
                    node,
                    target.Node,
                    selectedMode);
            }
            catch (Exception exception)
            {
                reportFailure(exception);
            }
        }

        TransferTarget? TargetAt(PointerEventArgs args)
        {
            if (TopLevel.GetTopLevel(row) is not { } topLevel)
            {
                return null;
            }
            var hit = topLevel.InputHitTest(
                args.GetPosition(topLevel));
            for (var visual = hit as Visual;
                 visual is not null;
                 visual = visual.GetVisualParent())
            {
                if (visual is Border candidate
                    && Targets.TryGetValue(candidate, out var target)
                    && ProductionHierarchyTransferContract.CanTransfer(
                        node,
                        target.Node))
                {
                    return target;
                }
            }
            return null;
        }

        void SetActiveTarget(TransferTarget? target)
        {
            if (ReferenceEquals(activeTarget, target))
            {
                return;
            }
            activeTarget?.Restore();
            activeTarget = target;
            activeTarget?.Activate();
        }

        void Cancel()
        {
            var capturedPointer = pointer;
            pointer = null;
            mode = null;
            dragging = false;
            SetActiveTarget(null);
            capturedPointer?.Capture(null);
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

    private sealed class TransferTarget(
        Border row,
        ProjectTreeNode node,
        IBrush? originalBrush,
        Thickness originalThickness)
    {
        private readonly WeakReference<Border> _row = new(row);

        public ProjectTreeNode Node { get; } = node;

        public void Activate()
        {
            if (_row.TryGetTarget(out var current))
            {
                current.BorderBrush =
                    EditorAnimationVisuals.ActiveTrackBrush;
                current.BorderThickness = new Thickness(2);
            }
        }

        public void Restore()
        {
            if (_row.TryGetTarget(out var current))
            {
                current.BorderBrush = originalBrush;
                current.BorderThickness = originalThickness;
            }
        }
    }
}
