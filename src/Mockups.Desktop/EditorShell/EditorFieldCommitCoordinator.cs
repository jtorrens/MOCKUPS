using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorFieldCommitCoordinator
{
    private readonly EditorOperationCoordinator _operations;
    private readonly Dictionary<DictionaryFieldControl, long>
        _controlRevisions = [];

    public EditorFieldCommitCoordinator(
        EditorOperationCoordinator operations)
    {
        _operations = operations;
    }

    public async Task CommitAsync(
        DictionaryFieldControl control,
        string draftValue,
        Func<string, string> normalizeForStorage,
        Func<string> currentStoredValue,
        Action<string> persist)
    {
        var revision = NextRevision(control);
        var storedValue = await _operations.ExecuteAsync(
            () =>
            {
                var normalized = normalizeForStorage(draftValue);
                if (currentStoredValue() != normalized)
                {
                    persist(normalized);
                }

                return normalized;
            });
        if (!IsCurrent(control, revision))
        {
            return;
        }

        control.SetValue(storedValue);
        if (control.CommitAsDefault)
        {
            control.AcceptCurrentValueAsDefault();
        }
        else
        {
            control.MarkCurrentValueCommitted();
        }
    }

    public Task ExecuteAsync(Action persist) =>
        _operations.ExecuteAsync(persist);

    private long NextRevision(DictionaryFieldControl control)
    {
        var revision = _controlRevisions.TryGetValue(
            control,
            out var current)
            ? checked(current + 1)
            : 1;
        _controlRevisions[control] = revision;
        return revision;
    }

    private bool IsCurrent(
        DictionaryFieldControl control,
        long revision) =>
        _controlRevisions.TryGetValue(control, out var current)
        && current == revision;
}
