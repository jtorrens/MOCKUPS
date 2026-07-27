using Microsoft.Data.Sqlite;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteCurrentDatabaseValidator
{
    private void ValidateCurrentModuleRuntimeDocuments(SqliteConnection connection)
    {
        var modules = _designOwner.AppModuleRepository.QueryModules(connection)
            .ToDictionary((module) => module.Id, StringComparer.Ordinal);
        var actorIdsByProject = _resourceOwner.ActorRepository.QueryAll(connection)
            .GroupBy((actor) => actor.ProjectId, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.Select((actor) => actor.Id).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        foreach (var instance in _productionOwner.ModuleInstanceRepository.QueryAll(connection))
        {
            if (!modules.TryGetValue(instance.ModuleId, out var module))
            {
                continue;
            }
            var projectActorIds = actorIdsByProject.TryGetValue(module.ProjectId, out var knownActorIds)
                ? knownActorIds
                : new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            try
            {
                var content = ParseRequiredObject(
                    instance.ContentJson,
                    $"Module Instance '{instance.Id}' content_json");
                _productionOwner.ValidateModuleInstanceRuntimeContent(
                    connection,
                    instance.Id,
                    content,
                    projectActorIds);
            }
            catch (InvalidOperationException exception)
            {
                throw InvalidCurrentDatabase(exception.Message);
            }
        }
    }
}
