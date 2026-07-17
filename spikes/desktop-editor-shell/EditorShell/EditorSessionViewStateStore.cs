using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorSessionViewStateStore
{
    private readonly Dictionary<string, EditorViewState> _statesByRecordClassId = new(StringComparer.Ordinal);

    public void Set(string recordClassId, EditorViewState state)
    {
        _statesByRecordClassId[RequiredRecordClassId(recordClassId)] = state;
    }

    public EditorViewState? Get(string recordClassId)
    {
        return _statesByRecordClassId.GetValueOrDefault(RequiredRecordClassId(recordClassId));
    }

    private static string RequiredRecordClassId(string recordClassId)
    {
        if (string.IsNullOrWhiteSpace(recordClassId))
        {
            throw new InvalidOperationException("Editor session view state requires an exact record class id.");
        }

        return recordClassId;
    }
}
