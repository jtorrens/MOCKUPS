using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ComponentInputGrouping
{
    public static IReadOnlyList<ComponentInputDefinition> OwnInputs(
        IReadOnlyList<ComponentInputDefinition> inputs)
    {
        return inputs
            .Where((input) => input.UiOrigin != ComponentInputUiOrigin.Embedded)
            .ToList();
    }

    public static IReadOnlyDictionary<string, List<ComponentInputDefinition>> EmbeddedGroups(
        IReadOnlyList<ComponentInputDefinition> inputs)
    {
        return inputs
            .Where((input) => input.UiOrigin == ComponentInputUiOrigin.Embedded)
            .GroupBy((input) => string.IsNullOrWhiteSpace(input.UiGroupId) ? input.Id : input.UiGroupId)
            .ToDictionary((group) => group.Key, (group) => group.ToList(), StringComparer.Ordinal);
    }

    public static IEnumerable<string> TopLevelGroupIds(
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groupsById)
    {
        return groupsById
            .Where((group) =>
            {
                var parent = ParentGroupId(group.Value);
                return string.IsNullOrWhiteSpace(parent) || !groupsById.ContainsKey(parent);
            })
            .Select((group) => group.Key);
    }

    public static IEnumerable<string> ChildGroupIds(
        string parentGroupId,
        IReadOnlyDictionary<string, List<ComponentInputDefinition>> groupsById)
    {
        return groupsById
            .Where((group) => string.Equals(ParentGroupId(group.Value), parentGroupId, StringComparison.Ordinal))
            .Select((group) => group.Key);
    }

    public static string GroupLabel(IReadOnlyList<ComponentInputDefinition> groupInputs)
    {
        return groupInputs
            .Select((input) => input.UiGroupLabel)
            .FirstOrDefault((label) => !string.IsNullOrWhiteSpace(label)) ?? "Embedded inputs";
    }

    private static string ParentGroupId(IReadOnlyList<ComponentInputDefinition> groupInputs)
    {
        return groupInputs
            .Select((input) => input.UiParentGroupId)
            .FirstOrDefault((parent) => !string.IsNullOrWhiteSpace(parent)) ?? "";
    }
}
