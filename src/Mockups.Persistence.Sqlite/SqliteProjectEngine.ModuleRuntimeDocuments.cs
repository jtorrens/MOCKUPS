using Microsoft.Data.Sqlite;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    private IReadOnlySet<string> ModuleInstanceProjectActorIds(
        SqliteConnection connection,
        string moduleInstanceId)
    {
        var instance = _productionOwner.ModuleInstanceRepository.Get(
            connection,
            moduleInstanceId);
        var module = _designOwner.AppModuleRepository.GetModule(
            connection,
            instance.ModuleId);
        return ProjectActorIds(connection, module.ProjectId);
    }

    private IReadOnlySet<string> ProjectActorIds(
        SqliteConnection connection,
        string projectId) =>
        _resourceOwner.ActorRepository.QueryAll(connection)
            .Where((actor) => actor.ProjectId.Equals(
                projectId,
                StringComparison.Ordinal))
            .Select((actor) => actor.Id)
            .ToHashSet(StringComparer.Ordinal);

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
