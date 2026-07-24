// Generated from scaffolding/modules/*.json. Do not edit manually.
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class GeneratedModuleScaffoldFieldCatalog
{
    public static void AddFields(
        Dictionary<string, RecordClassFieldDescriptor> fields)
    {
        fields.Add("module.core.chatList.horizontalAlignment", new("module.core.chatList.horizontalAlignment", "Horizontal alignment", ValueKind.OptionToken, Options: [new("left", "Left"), new("center", "Center"), new("right", "Right")]));
        fields.Add("module.core.chatList.list", new("module.core.chatList.list", "List", ValueKind.ComponentVariantSlot, ComponentVariantType: "list"));
        fields.Add("module.core.chatList.topInset", new("module.core.chatList.topInset", "Top inset", ValueKind.ThemeToken, Options: ComponentClassFieldCatalog.SpacingTokenOptions));
    }
}
