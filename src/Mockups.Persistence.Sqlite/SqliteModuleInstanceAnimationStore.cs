using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteModuleInstanceAnimationStore(
    SqliteProjectContext context,
    SqliteProductionOwner production,
    SqliteResourceOwner resources)
    : IModuleInstanceAnimationStore
{
    public void UpdateModuleInstanceAnimationJson(
        string moduleInstanceId,
        string animationJson)
    {
        lock (context.WriteGate)
        {
            using var connection = context.OpenConnection();
            var instance = production.ModuleInstanceRepository.Get(connection, moduleInstanceId);
            var shot = production.ShotRepository.Get(connection, instance.ShotId);
            var actorIds = resources.ActorRepository.QueryAll(connection)
                .Where(actor => actor.ProjectId == shot.ProjectId)
                .Select(actor => actor.Id)
                .ToHashSet(StringComparer.Ordinal);
            production.UpdateModuleInstanceAnimationJson(
                connection, moduleInstanceId, animationJson, actorIds);
        }
    }
}
