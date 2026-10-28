using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.EditorShell;

// Workstation visual preferences only; no editor content or authoring state.
internal sealed record PreviewUtilityLayoutState(
    [property: JsonRequired] PreviewUtilityGroupState[] Groups,
    [property: JsonRequired] PreviewUtilityTabState[] Tabs,
    [property: JsonRequired] Dictionary<string, double> EditorHeights)
{
    public void Validate(string[] tabIds, string[] rootIds, string[] stackRootIds)
    {
        static bool Positive(double value) => double.IsFinite(value) && value > 0;
        if (Groups is null || Tabs is null || EditorHeights is null
            || Groups.Any(group => group is null || string.IsNullOrWhiteSpace(group.Id))
            || Tabs.Any(tab => tab is null || string.IsNullOrWhiteSpace(tab.Id))
            || Groups.Select(group => group.Id).Distinct().Count() != Groups.Length
            || !Tabs.Select(tab => tab.Id).Order().SequenceEqual(tabIds.Order())
            || !EditorHeights.Keys.Order().SequenceEqual(stackRootIds.Order())
            || EditorHeights.Values.Any(height => !Positive(height))
            || rootIds.Any(id => !Groups.Any(group => group.Id == id && group.RootId == id)))
            throw Invalid();
        var groups = Groups.ToDictionary(group => group.Id);
        if (Groups.Any(group => !rootIds.Contains(group.RootId) || !Positive(group.Height)
                || group.Id != group.RootId && !stackRootIds.Contains(group.RootId)
                || group.SelectedTabId is { } selected && !Tabs.Any(tab => tab.Id == selected && tab.GroupId == group.Id))
            || Tabs.Any(tab => tab.GroupId is { } id && !groups.ContainsKey(id)
                || string.IsNullOrWhiteSpace(tab.ReturnGroupId) || !groups.ContainsKey(tab.ReturnGroupId)
                || tab.GroupId is { } dock && dock != tab.ReturnGroupId
                || tab.X.HasValue != tab.Y.HasValue
                || !Positive(tab.Width) || !Positive(tab.Height)))
            throw Invalid();
    }

    private static InvalidDataException Invalid() => new(
        "The utility layout must contain the exact declared tabs, roots and valid group references and geometry.");
}

internal sealed record PreviewUtilityGroupState(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string RootId,
    [property: JsonRequired] double Height,
    [property: JsonRequired] string? SelectedTabId);

internal sealed record PreviewUtilityTabState(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string? GroupId,
    [property: JsonRequired] string ReturnGroupId,
    [property: JsonRequired] int? X,
    [property: JsonRequired] int? Y,
    [property: JsonRequired] double Width,
    [property: JsonRequired] double Height);
