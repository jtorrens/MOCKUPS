using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProductionOwner
{
    public ModuleInstanceSettings GetModuleInstanceSettings(
        string moduleInstanceId)
    {
        var record = _moduleInstanceRepository.Get(moduleInstanceId);
        return new ModuleInstanceSettings(
            record.ShotId,
            record.AppId,
            record.ModuleId,
            record.Name,
            record.Notes,
            record.SortOrder,
            record.DurationFrames,
            record.TransitionJson,
            record.ContentJson,
            record.BehaviorJson,
            record.AnimationJson,
            record.MetadataJson);
    }

    public ModuleSettings GetModuleInstanceVariantSettings(
        string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var reference = GetModuleInstanceVariantReference(
            moduleInstanceId);
        if (!VariantReferenceId.TryParse(
                reference,
                out var moduleId,
                out var variantId)
            || !moduleId.Equals(
                instance.ModuleId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Module instance '{moduleInstanceId}' has an invalid module variant reference.");
        }

        var settings = _moduleVariantCatalog.GetModuleSettings(moduleId);
        var variant = _moduleVariantCatalog.GetModuleVariants(moduleId)
            .FirstOrDefault(
                (candidate) => candidate.Id.Equals(
                    variantId,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Missing module variant '{reference}'.");
        return settings with { ConfigJson = variant.ConfigJson };
    }

    public string GetModuleInstanceEffectiveContractJson(
        string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        return ResolveModuleInstanceContract(
            instance.ModuleId,
            instance.MetadataJson).ToJsonString();
    }

    public string GetModuleInstanceVariantReference(
        string moduleInstanceId)
    {
        var instance = GetModuleInstanceSettings(moduleInstanceId);
        var metadata = ParseJsonObject(instance.MetadataJson);
        return metadata["moduleVariantReference"]?.GetValue<string>()
            ?? throw new InvalidOperationException(
                $"Module instance '{moduleInstanceId}' has no explicit module variant reference.");
    }

    public string GetModuleInstanceVariantName(
        string moduleInstanceId)
    {
        var reference = GetModuleInstanceVariantReference(
            moduleInstanceId);
        if (!VariantReferenceId.TryParse(
                reference,
                out var moduleId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid module variant reference '{reference}'.");
        }

        return _moduleVariantCatalog.GetModuleVariants(moduleId)
            .First(
                (variant) => variant.Id.Equals(
                    variantId,
                    StringComparison.Ordinal))
            .Name;
    }

    internal JsonObject ResolveModuleInstanceContract(
        string moduleId,
        string instanceMetadataJson)
    {
        var instanceMetadata = ParseJsonObject(instanceMetadataJson);
        var reference =
            instanceMetadata["moduleVariantReference"]?.GetValue<string>()
            ?? throw new InvalidOperationException(
                "Module instance has no explicit module variant reference.");
        if (!VariantReferenceId.TryParse(
                reference,
                out var referencedModuleId,
                out var variantId)
            || referencedModuleId != moduleId)
        {
            throw new InvalidOperationException(
                $"Invalid module variant reference '{reference}'.");
        }

        var module = _moduleVariantCatalog.GetModuleSettings(moduleId);
        var variant = _moduleVariantCatalog.GetModuleVariants(moduleId)
            .FirstOrDefault((candidate) => candidate.Id == variantId)
            ?? throw new InvalidOperationException(
                $"Missing module variant '{reference}'.");
        return RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(module.DesignPreviewJson),
            ParseJsonObject(variant.ConfigJson));
    }

    private static JsonObject ParseJsonObject(string json) =>
        JsonPath.ParseRequiredObject(
            json,
            "Current persisted JSON object");
}
