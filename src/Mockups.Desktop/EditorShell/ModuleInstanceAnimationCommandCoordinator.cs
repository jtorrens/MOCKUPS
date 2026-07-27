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
    private readonly Func<string,
        Task<ModuleInstanceAnimationSnapshot>> _save;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _confirmedAnimationJson;

    public ModuleInstanceAnimationCommandCoordinator(
        string confirmedAnimationJson,
        Func<string,
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
                var candidate =
                    new ModuleInstanceAnimationDocument(
                        _confirmedAnimationJson);
                if (!mutation(candidate))
                {
                    return new ModuleInstanceAnimationCommandResult(
                        true,
                        _confirmedAnimationJson,
                        null,
                        null);
                }

                var snapshot = await _save(
                    candidate.ToJson());
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
