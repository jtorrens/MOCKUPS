using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public void UpdateShotField(string shotId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (_productionOwner.UpdateShotField(
                connection,
                shotId,
                fieldId,
                value))
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
