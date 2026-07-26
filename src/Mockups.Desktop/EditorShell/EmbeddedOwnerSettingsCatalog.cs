using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EmbeddedOwnerSettingsDefinition(
    string Label,
    string Subtitle,
    string Icon,
    IReadOnlyList<string> FieldIds);

internal static class EmbeddedOwnerSettingsCatalog
{
    private static readonly IReadOnlyDictionary<string, EmbeddedOwnerSettingsDefinition> Definitions =
        new Dictionary<string, EmbeddedOwnerSettingsDefinition>
        {
            ["module.conversation.headerLeftIconRow.editor"] = new(
                "Slots", "Content owned by Conversation", EditorIcons.Content,
                ["module.conversation.headerLeftIconRow.inputs"]),
            ["module.conversation.headerRightIconRow.editor"] = new(
                "Slots", "Content owned by Conversation", EditorIcons.Content,
                ["module.conversation.headerRightIconRow.inputs"]),
        };

    public static bool TryGet(string slotFieldId, out EmbeddedOwnerSettingsDefinition definition) =>
        Definitions.TryGetValue(slotFieldId, out definition!);
}
