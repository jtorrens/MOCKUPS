using System;
using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorFieldCommitCoordinator
{
    private readonly EditorOperationCoordinator _operations;
    private readonly IVariantFieldChangeStore? _variantChanges;
    private readonly Func<RecordCreationDefinition, Task<RecordCreationDraft?>>? _completeRuntime;
    private readonly Dictionary<DictionaryFieldControl, long>
        _controlRevisions = [];

    public EditorFieldCommitCoordinator(
        EditorOperationCoordinator operations,
        IVariantFieldChangeStore? variantChanges = null,
        Func<RecordCreationDefinition, Task<RecordCreationDraft?>>? completeRuntime = null)
    {
        _operations = operations;
        _variantChanges = variantChanges;
        _completeRuntime = completeRuntime;
    }

    public async Task<bool> CommitAsync(
        DictionaryFieldControl control,
        string draftValue,
        Func<string, string> normalizeForStorage,
        Func<string> currentStoredValue,
        Action<string> persist,
        ProjectTreeNode? node = null,
        string? fieldId = null)
    {
        var revision = NextRevision(control);
        var prepared = await _operations.ExecuteAsync(() =>
        {
            var normalized = normalizeForStorage(draftValue);
            var previous = currentStoredValue();
            var change = previous != normalized && node is not null && fieldId is not null
                ? _variantChanges?.Prepare(node, fieldId, normalized) : null;
            return (Normalized: normalized, Previous: previous, Change: change);
        });
        if (!IsCurrent(control, revision)) return false;
        if (prepared.Change is { } change)
        {
            var drafts = new Dictionary<string, RecordCreationDraft>(StringComparer.Ordinal);
            foreach (var screen in change.Screens)
            {
                var complete = _completeRuntime
                    ?? throw new InvalidOperationException("Variant changes require the shared Runtime creation form.");
                var draft = await complete(screen.Definition);
                if (!IsCurrent(control, revision)) return false;
                if (draft is null)
                {
                    control.SetValue(prepared.Previous);
                    return false;
                }
                drafts.Add(screen.ScreenId, draft);
            }
            await _operations.ExecuteAsync(() => _variantChanges!.Commit(
                node!, fieldId!, prepared.Normalized, change.Revision, drafts));
        }
        else if (prepared.Previous != prepared.Normalized)
            await _operations.ExecuteAsync(() => persist(prepared.Normalized));
        if (!IsCurrent(control, revision)) return false;
        control.SetValue(prepared.Normalized);
        if (control.CommitAsDefault) control.AcceptCurrentValueAsDefault();
        else control.MarkCurrentValueCommitted();
        return true;
    }

    public IDisposable BeginPreviewUpdate() =>
        _operations.BeginActivity("Actualizando Preview…", EditorActivitySurface.Preview);

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
