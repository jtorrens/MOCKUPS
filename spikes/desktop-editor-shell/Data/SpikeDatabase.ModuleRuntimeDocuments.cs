using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private void ValidateModuleInstanceRuntimeContent(
        SqliteConnection connection,
        string moduleInstanceId,
        JsonObject content)
    {
        var instance = _moduleInstanceRepository.Get(connection, moduleInstanceId);
        var module = _appModuleRepository.GetModule(connection, instance.ModuleId);
        var actorIds = _actorRepository.QueryAll(connection)
            .Where((actor) => actor.ProjectId.Equals(module.ProjectId, StringComparison.Ordinal))
            .Select((actor) => actor.Id)
            .ToHashSet(StringComparer.Ordinal);
        var contract = EffectiveModuleInstanceContract(
            module.Id,
            module.MetadataJson,
            instance.MetadataJson,
            module.DesignPreviewJson);
        ValidateCurrentRuntimeCollections(
            contract,
            content,
            $"Module Instance '{moduleInstanceId}' content_json");
        ModuleRuntimeDocumentContracts.ValidateCurrent(
            module.RecordClassId,
            $"Module Instance '{moduleInstanceId}' content_json",
            content,
            actorIds);
    }

    private void ValidateCurrentModuleRuntimeDocuments(SqliteConnection connection)
    {
        var modules = _appModuleRepository.QueryModules(connection)
            .ToDictionary((module) => module.Id, StringComparer.Ordinal);
        var actorIdsByProject = _actorRepository.QueryAll(connection)
            .GroupBy((actor) => actor.ProjectId, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.Select((actor) => actor.Id).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        foreach (var instance in _moduleInstanceRepository.QueryAll(connection))
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
                var contract = EffectiveModuleInstanceContract(
                    module.Id,
                    module.MetadataJson,
                    instance.MetadataJson,
                    module.DesignPreviewJson);
                ValidateCurrentRuntimeCollections(
                    contract,
                    content,
                    $"Module Instance '{instance.Id}' content_json");
                ModuleRuntimeDocumentContracts.ValidateCurrent(
                    module.RecordClassId,
                    $"Module Instance '{instance.Id}' content_json",
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
