using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ModuleInstanceTimeline
{
    public static int DurationFrames(SpikeDatabase database, string moduleInstanceId)
    {
        var instance = database.GetModuleInstanceSettings(moduleInstanceId);
        var module = database.GetModuleSettings(instance.ModuleId);
        return RuntimeTimeline.DurationFrames(
            module.DesignPreviewJson,
            instance.ContentJson,
            instance.AnimationJson,
            instance.DurationFrames);
    }

    public static int ShotDurationFrames(SpikeDatabase database, string shotId) =>
        database.GetShotModuleInstanceSlots(shotId).Sum((slot) => DurationFrames(database, slot.Id));

}
