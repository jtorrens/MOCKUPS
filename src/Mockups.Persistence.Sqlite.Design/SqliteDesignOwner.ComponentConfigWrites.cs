using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    internal JsonObject ComponentVariantConfigForUpdate(
        SqliteConnection connection,
        ProjectTreeNode variantNode,
        out string componentClassId,
        out JsonObject metadata)
    {
        if (variantNode.Kind != ProjectTreeNodeKind.ComponentVariant
            || !VariantReferenceId.TryParse(
                variantNode.Id,
                out componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{variantNode.Id}'.");
        }

        var settings = GetComponentClassSettings(
            connection,
            componentClassId);
        metadata = ParseJsonObject(settings.MetadataJson);
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Component class '{componentClassId}'");
        var variant = VariantEnvelopeContract.FindSource(
            variants,
            variantId)
            ?? throw new InvalidOperationException(
                $"Missing component variant '{variantId}'.");
        if (IsVariantLockedForEditing(
                componentClassId,
                variantId,
                JsonBool(variant, ["locked"])))
        {
            throw new InvalidOperationException(
                $"Component variant '{variantId}' is locked.");
        }

        return variant["config"] as JsonObject
            ?? throw new InvalidOperationException(
                $"Component variant '{variantId}' has no config.");
    }

    public void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(
                connection,
                componentClassId);
            var config = ParseJsonObject(settings.ConfigJson);
            var metadata = ParseJsonObject(settings.MetadataJson);
            SetJsonValue(
                config,
                descriptor.JsonPath,
                ComponentConfigJsonValue(
                    descriptor,
                    value));
            ApplyComponentInputBindingsProjections(
                connection,
                settings.ProjectId,
                config,
                ComponentInputBindingsProjectionCatalog.ComponentOwners());
            CurrentComponentConfigContract.Validate(
                settings.ComponentType,
                config,
                $"Component class '{componentClassId}' config_json");
            ValidateEmbeddedSlotVariantReferences(
                connection,
                settings.ProjectId,
                config);
            SetDefaultComponentVariantConfig(metadata, config);
            _componentClassRepository.UpdateConfigAndMetadata(
                connection,
                componentClassId,
                config.ToJsonString(),
                metadata.ToJsonString());
        }
    }

    public void UpdateComponentVariantField(
        ProjectTreeNode variantNode,
        string fieldId,
        string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = ComponentVariantConfigForUpdate(
                connection,
                variantNode,
                out var componentClassId,
                out var metadata);
            SetJsonValue(
                config,
                descriptor.JsonPath,
                ComponentConfigJsonValue(
                    descriptor,
                    value));
            PersistComponentVariantUpdate(
                connection,
                variantNode,
                componentClassId,
                config,
                metadata);
        }
    }

    public void ReplaceComponentVariantConfig(
        ProjectTreeNode node,
        string configJson)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{node.Id}'.");
        }

        var nextConfig = ParseJsonObject(configJson);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(
                connection,
                componentClassId);
            ApplyComponentInputBindingsProjections(
                connection,
                settings.ProjectId,
                nextConfig,
                ComponentInputBindingsProjectionCatalog.ComponentOwners());
            CurrentComponentConfigContract.Validate(
                settings.ComponentType,
                nextConfig,
                $"Component class '{componentClassId}' Variant '{variantId}' config");
            ValidateEmbeddedSlotVariantReferences(
                connection,
                settings.ProjectId,
                nextConfig);
            var metadata = ParseJsonObject(settings.MetadataJson);
            var variants = VariantEnvelopeContract.RequiredArray(
                metadata,
                "variants",
                $"Component class '{componentClassId}'");
            var variant = VariantEnvelopeContract.FindSource(
                variants,
                variantId)
                ?? throw new InvalidOperationException(
                    $"Missing component variant '{variantId}'.");
            if (IsVariantLockedForEditing(
                    componentClassId,
                    variantId,
                    JsonBool(variant, ["locked"])))
            {
                throw new InvalidOperationException(
                    $"Component variant '{variantId}' is locked.");
            }

            variant["config"] = nextConfig;
            _componentClassRepository.UpdateMetadata(
                connection,
                componentClassId,
                metadata.ToJsonString());
        }
    }

    internal void PersistDefaultComponentConfig(
        SqliteConnection connection,
        string componentClassId,
        JsonObject config,
        JsonObject metadata)
    {
        var component = _componentClassRepository.Get(
            connection,
            componentClassId);
        ApplyComponentInputBindingsProjections(
            connection,
            component.ProjectId,
            config,
            ComponentInputBindingsProjectionCatalog.ComponentOwners());
        CurrentComponentConfigContract.Validate(
            component.ComponentType,
            config,
            $"Component class '{componentClassId}' Default Variant config");
        ValidateEmbeddedSlotVariantReferences(
            connection,
            component.ProjectId,
            config);
        SetDefaultComponentVariantConfig(metadata, config);
        _componentClassRepository.UpdateConfigAndMetadata(
            connection,
            componentClassId,
            config.ToJsonString(),
            metadata.ToJsonString());
    }

    internal void PersistComponentVariantUpdate(
        SqliteConnection connection,
        ProjectTreeNode variantNode,
        string componentClassId,
        JsonObject config,
        JsonObject metadata)
    {
        if (!VariantReferenceId.TryParse(
                variantNode.Id,
                out _,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{variantNode.Id}'.");
        }

        var component = _componentClassRepository.Get(
            connection,
            componentClassId);
        ApplyComponentInputBindingsProjections(
            connection,
            component.ProjectId,
            config,
            ComponentInputBindingsProjectionCatalog.ComponentOwners());
        CurrentComponentConfigContract.Validate(
            component.ComponentType,
            config,
            $"Component class '{componentClassId}' Variant '{variantId}' config");
        ValidateEmbeddedSlotVariantReferences(
            connection,
            component.ProjectId,
            config);
        if (variantId.Equals(
                VariantEnvelopeContract.DefaultId,
                StringComparison.Ordinal))
        {
            PersistDefaultComponentConfig(
                connection,
                componentClassId,
                config,
                metadata);
            return;
        }

        _componentClassRepository.UpdateMetadata(
            connection,
            componentClassId,
            metadata.ToJsonString());
    }

    internal static JsonNode ComponentConfigJsonValue(
        ComponentClassFieldDescriptor descriptor,
        string value)
    {
        var node = RuntimeInputValueKindContract.ParseValue(
            descriptor.ValueKind,
            value,
            $"Component field '{descriptor.Id}' value");
        ScalarValuePatternContract.Validate(
            descriptor.ValuePattern,
            descriptor.ValuePatternMessage,
            node,
            $"Component field '{descriptor.Id}' value");
        return node;
    }

    private static void SetDefaultComponentVariantConfig(
        JsonObject metadata,
        JsonObject config)
    {
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            "Component class metadata");
        var defaultVariant = VariantEnvelopeContract.FindSource(
            variants,
            VariantEnvelopeContract.DefaultId)
            ?? throw new InvalidOperationException(
                "Component class has no Default variant.");
        defaultVariant["config"] = config.DeepClone();
    }
}
