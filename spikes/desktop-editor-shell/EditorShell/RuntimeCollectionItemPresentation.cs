using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record RuntimeCollectionItemPresentationResult(string Title, string Subtitle, string Icon);

internal static class RuntimeCollectionItemPresentation
{
    public static RuntimeCollectionItemPresentationResult Resolve(
        RuntimeInputCollectionDefinition collection,
        JsonObject item,
        int itemIndex,
        string defaultTitle,
        string defaultSubtitle,
        string defaultIcon)
    {
        var presentation = collection.ItemPresentation;
        if (presentation is null)
        {
            return new RuntimeCollectionItemPresentationResult(defaultTitle, defaultSubtitle, defaultIcon);
        }

        var title = defaultTitle;
        var titleField = collection.Fields.FirstOrDefault((field) => field.Id == presentation.TitleFieldId);
        if (titleField is not null)
        {
            var value = DisplayValue(titleField, item);
            if (!string.IsNullOrWhiteSpace(value)) title = value;
        }
        if (itemIndex == 0 && !string.IsNullOrWhiteSpace(presentation.FirstItemBadge))
        {
            title = $"{title} · {presentation.FirstItemBadge}";
        }

        var subtitle = string.Join(
            " · ",
            presentation.SubtitleFieldIds
                .Select((fieldId) => collection.Fields.FirstOrDefault((field) => field.Id == fieldId))
                .Where((field) => field is not null)
                .Select((field) => DisplayValue(field!, item))
                .Where((value) => !string.IsNullOrWhiteSpace(value)));
        subtitle = Compact(subtitle, presentation.SubtitleMaxCharacters);
        if (string.IsNullOrWhiteSpace(subtitle)) subtitle = defaultSubtitle;

        var icon = presentation.FallbackIcon;
        var iconField = collection.Fields.FirstOrDefault((field) => field.Id == presentation.IconFieldId);
        if (iconField is not null)
        {
            var value = ItemString(item, iconField.JsonKey);
            if (presentation.IconValueMap.TryGetValue(value, out var mapped)
                && !string.IsNullOrWhiteSpace(mapped))
            {
                icon = mapped;
            }
        }
        if (string.IsNullOrWhiteSpace(icon)) icon = defaultIcon;

        return new RuntimeCollectionItemPresentationResult(title, subtitle, icon);
    }

    private static string DisplayValue(ComponentInputDefinition field, JsonObject item)
    {
        var value = ItemString(item, field.JsonKey);
        return field.Options?.FirstOrDefault((option) => option.Value == value)?.Label ?? value;
    }

    private static string ItemString(JsonObject item, string key) =>
        item[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static string Compact(string value, int maximumCharacters)
    {
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= maximumCharacters) return normalized;
        return $"{normalized[..Math.Max(1, maximumCharacters - 1)].TrimEnd()}…";
    }
}
