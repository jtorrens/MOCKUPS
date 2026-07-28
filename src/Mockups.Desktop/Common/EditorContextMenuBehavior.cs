using Avalonia.Input;
using Avalonia.Interactivity;
using System.Runtime.CompilerServices;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorContextMenuBehavior
{
    private static readonly ConditionalWeakTable<Interactive, Registration> Registrations = new();

    public static T Configure<T>(T root)
        where T : Interactive
    {
        Registrations.GetValue(
            root,
            static configuredRoot => new Registration(configuredRoot));
        return root;
    }

    internal static bool IsConfigured(Interactive root) =>
        Registrations.TryGetValue(root, out _);

    private sealed class Registration
    {
        public Registration(Interactive root)
        {
            root.AddHandler(
                InputElement.ContextRequestedEvent,
                (_, args) => args.Handled = true,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }
    }
}
