using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ModuleInstanceAnimationCommandResult(
    bool Succeeded,
    string ConfirmedAnimationJson,
    ModuleInstanceAnimationSnapshot? Snapshot,
    Exception? Error);

internal sealed class ModuleInstanceAnimationCommandCoordinator
{
    private readonly Func<
        Func<ModuleInstanceAnimationDocument, bool>,
        Task<ModuleInstanceAnimationSnapshot>> _save;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _confirmedAnimationJson;

    public ModuleInstanceAnimationCommandCoordinator(
        string confirmedAnimationJson,
        Func<
            Func<ModuleInstanceAnimationDocument, bool>,
            Task<ModuleInstanceAnimationSnapshot>> save)
    {
        _confirmedAnimationJson =
            confirmedAnimationJson;
        _save = save
            ?? throw new ArgumentNullException(
                nameof(save));
    }

    public async Task<ModuleInstanceAnimationCommandResult>
        ExecuteAsync(
            Func<ModuleInstanceAnimationDocument, bool>
                mutation)
    {
        await _gate.WaitAsync();
        try
        {
            try
            {
                var snapshot = await _save(mutation);
                _confirmedAnimationJson =
                    snapshot.Source.AnimationJson;
                return new ModuleInstanceAnimationCommandResult(
                    true,
                    _confirmedAnimationJson,
                    snapshot,
                    null);
            }
            catch (Exception exception)
            {
                return new ModuleInstanceAnimationCommandResult(
                    false,
                    _confirmedAnimationJson,
                    null,
                    exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
