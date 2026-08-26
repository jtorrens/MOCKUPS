using Avalonia.Input;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorContinuousCommitBehavior
{
    public static void Attach(
        InputElement input,
        FieldDefinition definition,
        Action commit)
    {
        if (ValueKindCommitContract.Require(definition).Continuous
            != ContinuousCommitTrigger.InteractionEnd)
        {
            throw new InvalidOperationException(
                $"Field '{definition.Id}' does not declare continuous interaction commits.");
        }

        input.AddHandler(
            InputElement.PointerReleasedEvent,
            (_, _) => commit(),
            handledEventsToo: true);
        input.KeyUp += (_, args) =>
        {
            if (args.Key is Key.Left or Key.Right or Key.Up or Key.Down
                or Key.Home or Key.End or Key.PageUp or Key.PageDown)
                commit();
        };
        input.LostFocus += (_, _) => commit();
    }
}
