using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Linq;
using System.Text.Json.Nodes;

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
            var shot = _productionOwner.ShotRepository.Get(connection, instance.ShotId);
            var projectActorIds = actorIdsByProject.TryGetValue(shot.ProjectId, out var knownActorIds)
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
                RuntimeInputAnimationValueContract.Validate(
                    ValidateProductionRuntimeFixtures(instance.Id),
                    ParseRequiredObject(
                        instance.AnimationJson,
                        $"Module Instance '{instance.Id}' animation_json"),
                    new System.Collections.Generic.Dictionary<
                        string,
                        System.Collections.Generic.IReadOnlySet<string>>(
                        StringComparer.Ordinal)
                    {
                        ["actors"] = projectActorIds,
                    },
                    $"Module Instance '{instance.Id}' animation_json");
            }
            catch (InvalidOperationException exception)
            {
                throw InvalidCurrentDatabase(exception.Message);
            }
        }
    }

    private JsonObject ValidateProductionRuntimeFixtures(string moduleInstanceId)
    {
        var runtime = ParseRequiredObject(
            _productionOwner.GetModuleInstanceRuntimePreviewJson(moduleInstanceId),
            $"Module Instance '{moduleInstanceId}' Runtime Preview");
        var config = ParseRequiredObject(
            _productionOwner.GetModuleInstanceVariantSettings(moduleInstanceId).ConfigJson,
            $"Module Instance '{moduleInstanceId}' Variant config");
        ProductionRuntimeFixtureIsolationContract.Validate(
            runtime,
            config,
            $"Module Instance '{moduleInstanceId}'");
        return runtime;
    }
}
