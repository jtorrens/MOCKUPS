using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public JsonObject GetComponentVariantRuntimeInputs(
        string variantReference)
    {
        var effective =
            GetComponentVariantRuntimeContract(variantReference);
        return ParseJsonObject(
            DesignPreviewTestValues.RuntimeJson(
                effective.ToJsonString()));
    }

    public JsonObject GetComponentVariantRuntimeContract(
        string variantReference)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out _))
        {
            throw new InvalidOperationException(
                $"Invalid component Variant reference '{variantReference}'.");
        }

        var settings = GetComponentClassSettings(componentClassId);
        var config = GetComponentVariantConfig(variantReference);
        var effective = RuntimePreviewDocumentContract.PrepareFixture(
            ParseJsonObject(settings.DesignPreviewJson),
            config);
        return effective;
    }

    public IReadOnlyList<ComponentInputBindingDefinition>
        GetComponentVariantRuntimeInputBindings(
            string variantReference)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out _))
        {
            return [];
        }

        var settings = GetComponentClassSettings(componentClassId);
        var config = GetComponentVariantConfig(variantReference);
        var effective = RuntimePreviewDocumentContract.PrepareFixture(
            ParseJsonObject(settings.DesignPreviewJson),
            config);
        return RuntimeInputDefinitionReader.ReadInputs(
                effective,
                config)
            .Select(ComponentInputBindingDefinition.FromRuntimeInput)
            .ToList();
    }

    public IReadOnlyList<RuntimeInputCollectionDefinition>
        GetComponentVariantRuntimeCollections(
            string variantReference)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out _))
        {
            return [];
        }

        var settings = GetComponentClassSettings(componentClassId);
        var config = GetComponentVariantConfig(variantReference);
        var effective = RuntimePreviewDocumentContract.PrepareFixture(
            ParseJsonObject(settings.DesignPreviewJson),
            config);
        return RuntimeInputDefinitionReader.ReadCollections(
            effective,
            config);
    }

    public JsonObject GetComponentVariantConfig(string variantReference)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component Variant reference '{variantReference}'.");
        }

        using var connection = OpenConnection();
        var row = _componentClassRepository.Get(
            connection,
            componentClassId);
        var variant = RequiredComponentClassVariants(row)
            .Single((candidate) =>
                candidate.Id.Equals(
                    variantId,
                    StringComparison.Ordinal));
        return ParseJsonObject(variant.ConfigJson);
    }

    public ComponentVariantSelectionSettings
        GetComponentVariantSelectionSettings(
            string variantReference)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component Variant reference '{variantReference}'.");
        }

        using var connection = OpenConnection();
        var row = _componentClassRepository.Get(
            connection,
            componentClassId);
        var variant = RequiredComponentClassVariants(row)
            .Single((candidate) =>
                candidate.Id.Equals(
                    variantId,
                    StringComparison.Ordinal));
        return new ComponentVariantSelectionSettings(
            row.ProjectId,
            row.ComponentType,
            row.RecordClassId,
            variant.ConfigJson);
    }

    public string GetRuntimeComponentVariantName(
        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid component Variant reference '{variantReference}'.");
        }

        using var connection = OpenConnection();
        var row = _componentClassRepository.Get(
            connection,
            componentClassId);
        var variant = RequiredComponentClassVariants(row)
            .Single((candidate) =>
                candidate.Id.Equals(
                    variantId,
                    StringComparison.Ordinal));
        if (slots.Count == 0)
        {
            return variant.Name;
        }

        var ownerConfig = ParseJsonObject(variant.ConfigJson);
        ComponentConfigOverrideMerger.MergeInto(
            ownerConfig,
            overrides);
        return GetEmbeddedComponentVariantName(
            connection,
            row.ProjectId,
            ownerConfig,
            slots);
    }
}
