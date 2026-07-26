using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public void UpdateShotField(string shotId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId == "shot.fps" && value == "inherited")
        {
            _productionOwner.ShotRepository.ClearFpsOverride(connection, shotId);
            return;
        }

        if (fieldId == "shot.ownerActorId")
        {
            _productionOwner.ModuleInstanceThemeContextService.RequireShotOwnerChange(connection, shotId, value);
        }

        _productionOwner.ShotRepository.UpdateField(connection, shotId, fieldId, value);
        if (fieldId == "shot.ownerActorId")
        {
            SynchronizeTimelineDurations(connection, shotId);
        }
    }

    public string GetShotOwnerDeviceName(string shotId)
    {
        using var connection = OpenConnection();
        var shot = _productionOwner.ShotRepository.Get(connection, shotId);
        var actor = _resourceOwner.ActorRepository.QueryAll(connection)
            .SingleOrDefault((candidate) => candidate.Id == shot.OwnerActorId)
            ?? throw new InvalidOperationException($"Missing Actor '{shot.OwnerActorId}'.");
        if (string.IsNullOrWhiteSpace(actor.DefaultDeviceId)) return "No default device";
        return _resourceOwner.DeviceRepository.QueryAll(connection)
            .SingleOrDefault((candidate) => candidate.Id == actor.DefaultDeviceId)?.Name
            ?? throw new InvalidOperationException($"Missing Device '{actor.DefaultDeviceId}'.");
    }

}
