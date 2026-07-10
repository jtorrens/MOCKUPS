using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorFieldCommitCoordinator
{
    public void Commit(
        DictionaryFieldControl control,
        string draftValue,
        Func<string, string> normalizeForStorage,
        Func<string> currentStoredValue,
        Action<string> persist)
    {
        var storedValue = normalizeForStorage(draftValue);
        if (currentStoredValue() == storedValue)
        {
            control.SetValue(storedValue);
            if (control.CommitAsDefault)
            {
                control.AcceptCurrentValueAsDefault();
            }
            else
            {
                control.MarkCurrentValueCommitted();
            }
            return;
        }

        persist(storedValue);
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
}
