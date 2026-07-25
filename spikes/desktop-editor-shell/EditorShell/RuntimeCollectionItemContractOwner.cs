using Mockups.DesktopEditorShell.Common;
using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeCollectionItemContractOwner
{
    public static string ResolveItemVariantReference(
        JsonObject item,
        RuntimeInputCollectionDefinition collection,
        JsonObject ownerConfig,
        Func<string, JsonObject> componentVariantConfig)
    {
        if (collection.ComponentItems is { } componentItems)
        {
            return RuntimeComponentCollectionItemDocumentContract
                .RequireVariantReference(
                    item,
                    componentItems.DocumentKeys,
                    $"Runtime collection '{collection.Id}' item");
        }
        if (string.IsNullOrWhiteSpace(
                collection.ItemRuntimeVariantReferencePath))
        {
            return "";
        }

        var contractOwnerConfig = ownerConfig;
        if (!string.IsNullOrWhiteSpace(
                collection.ItemRuntimeOwnerVariantReferencePath))
        {
            var ownerReference = RequireReference(
                ownerConfig,
                collection.ItemRuntimeOwnerVariantReferencePath,
                collection.Id,
                "itemRuntimeOwnerVariantReferencePath");
            contractOwnerConfig = componentVariantConfig(ownerReference);
        }
        return RequireReference(
            contractOwnerConfig,
            collection.ItemRuntimeVariantReferencePath,
            collection.Id,
            "itemRuntimeVariantReferencePath");
    }

    private static string RequireReference(
        JsonObject config,
        string path,
        string collectionId,
        string pathLabel)
    {
        var node = JsonPath.Get(
            config,
            path.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries));
        if (node is not JsonValue value
            || !value.TryGetValue<string>(out var reference)
            || string.IsNullOrWhiteSpace(reference)
            || !VariantReferenceId.TryParse(reference, out _, out _))
        {
            throw new InvalidOperationException(
                $"Runtime collection '{collectionId}' {pathLabel} "
                + $"'{path}' must resolve to one complete Variant reference.");
        }
        return reference;
    }
}
