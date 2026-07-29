using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ModuleInstanceTimelineSource(
    string ShotId,
    int PersistedDurationFrames,
    string TransitionJson,
    string ContentJson,
    string AnimationJson,
    string EffectiveContractJson,
    string RuntimePreviewJson,
    string ThemeTokensJson);

internal sealed class ModuleInstanceTimelineDataSource
{
    private readonly IModuleInstanceTimelineStore _database;
    private readonly IModuleInstanceThemeTokenQuery _themeTokens;

    public ModuleInstanceTimelineDataSource(
        IModuleInstanceTimelineStore database,
        IModuleInstanceThemeTokenQuery themeTokens)
    {
        _database = database;
        _themeTokens = themeTokens;
    }

    public ModuleInstanceTimelineSource Load(string moduleInstanceId)
    {
        var instance = _database.GetModuleInstanceSettings(moduleInstanceId);
        return new ModuleInstanceTimelineSource(
            instance.ShotId,
            instance.DurationFrames,
            instance.TransitionJson,
            instance.ContentJson,
            instance.AnimationJson,
            _database.GetModuleInstanceEffectiveContractJson(moduleInstanceId),
            _database.GetModuleInstanceRuntimePreviewJson(moduleInstanceId),
            _themeTokens.GetModuleInstanceThemeTokensJson(
                moduleInstanceId));
    }

    public IReadOnlyList<string> ShotSlotIds(string shotId)
    {
        return _database.GetShotModuleInstanceSlots(shotId)
            .Select((slot) => slot.Id)
            .ToList();
    }
}
