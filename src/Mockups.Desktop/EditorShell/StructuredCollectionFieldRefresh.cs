using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class StructuredCollectionFieldRefresh
{
    public static void RefreshCalculatedDependents(
        RuntimeInputCollectionDefinition collection,
        ComponentInputDefinition source,
        IDictionary<string, DictionaryFieldControl> itemControls)
    {
        foreach (var dependent in collection.Fields.Where((candidate) =>
                     candidate.BehaviorTiming?.SourceFieldId.Equals(
                         source.Id,
                         StringComparison.Ordinal) == true))
        {
            if (itemControls.TryGetValue(
                    dependent.Id,
                    out var control))
            {
                control.SetPresentedValue(control.Value);
            }
        }
    }
}
