using Avalonia.Automation;
using Avalonia.Controls;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorAccessibility
{
    public static T Describe<T>(T control, string name, string? helpText = null, bool showToolTip = true)
        where T : Control
    {
        AutomationProperties.SetName(control, name);
        AutomationProperties.SetHelpText(control, helpText ?? name);
        if (showToolTip)
        {
            ToolTip.SetTip(control, helpText ?? name);
        }

        return control;
    }
}
