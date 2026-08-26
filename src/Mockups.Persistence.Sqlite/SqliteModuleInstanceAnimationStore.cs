using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteModuleInstanceAnimationStore(
    SqliteProductionOwner production,
    SqliteResourceOwner resources)
    : IModuleInstanceAnimationStore
{
    public void UpdateModuleInstanceAnimationJson(
        string moduleInstanceId,
        string animationJson)
    {
        var module = production.GetModuleInstanceVariantSettings(moduleInstanceId);
        var recordIds = new Dictionary<string, IReadOnlySet<string>>(
            StringComparer.Ordinal)
        {
            ["actors"] = resources.GetRequiredActorOptions(module.ProjectId)
                .Select((option) => option.Value)
                .ToHashSet(StringComparer.Ordinal),
        };
        RuntimeInputAnimationRecordReferenceContract.Validate(
            JsonPath.ParseRequiredObject(
                production.GetModuleInstanceRuntimePreviewJson(moduleInstanceId),
                $"Module Instance '{moduleInstanceId}' Runtime Preview"),
            ModuleInstanceAnimationDocumentContract.Parse(
                animationJson,
                $"Module Instance '{moduleInstanceId}' animation_json"),
            recordIds,
            $"Module Instance '{moduleInstanceId}' animation_json");
        production.UpdateModuleInstanceAnimationJson(
            moduleInstanceId,
            animationJson);
    }
}
