using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

// External import snapshot, never an override of an existing authored row.
public sealed record IconThemeImportDocument(string Id, string Name, string MappingJson, string MetadataJson)
{
    public static IconThemeImportDocument Read(string json)
    {
        var root = JsonPath.ParseRequiredObject(json, "Icon Theme import manifest");
        if (root["schemaVersion"]?.GetValue<int>() != 1 || root.Count != 5)
            throw new InvalidOperationException("Invalid Icon Theme import manifest version or fields.");
        var id = JsonPath.RequiredString(root, "id", "Icon Theme import");
        var name = JsonPath.RequiredString(root, "name", id);
        var mapping = JsonPath.RequiredObject(root, "mapping", id).ToJsonString();
        var metadata = JsonPath.RequiredObject(root, "metadata", id).ToJsonString();
        _ = IconTokenRules.Tokens(mapping);
        ValidateMetadata(metadata);
        return new(id, name, mapping, metadata);
    }

    public static JsonObject ValidateMetadata(string json)
    {
        var metadata = JsonPath.ParseRequiredObject(json, "Icon Theme metadata");
        if (metadata.ContainsKey("manifest"))
            throw new InvalidOperationException("Icon Theme metadata cannot contain a parallel manifest snapshot.");
        var definition = JsonPath.RequiredObject(metadata, "iconSet", "Icon Theme metadata");
        var provider = JsonPath.RequiredString(definition, "provider", "Icon Theme provider");
        _ = JsonPath.RequiredString(definition, "setName", "Icon Theme provider");
        _ = JsonPath.RequiredString(definition, "package", "Icon Theme provider");
        var fill = JsonPath.RequiredString(definition, "fillMode", "Icon Theme provider");
        if (provider == "lucide")
        {
            var stroke = definition["stroke"]?.GetValue<double>() ?? throw new InvalidOperationException("Missing Lucide stroke.");
            if (!double.IsFinite(stroke) || stroke <= 0 || fill != "stroke")
                throw new InvalidOperationException("Invalid Lucide definition.");
        }
        else if (provider == "material")
        {
            var style = JsonPath.RequiredString(definition, "style", "Material definition");
            var weight = definition["weight"]?.GetValue<int>() ?? throw new InvalidOperationException("Missing Material weight.");
            if (style is not ("rounded" or "outlined" or "sharp") || weight < 100 || weight > 700 || weight % 100 != 0 || fill != "filled")
                throw new InvalidOperationException("Invalid Material definition.");
        }
        else throw new InvalidOperationException($"Unknown explicit icon provider '{provider}'.");
        return metadata;
    }

    public string ToJson()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Icon Theme import requires explicit id and name.");
        _ = IconTokenRules.Tokens(MappingJson);
        return new JsonObject
        {
            ["schemaVersion"] = 1, ["id"] = Id, ["name"] = Name,
            ["mapping"] = JsonPath.ParseRequiredObject(MappingJson, Id),
            ["metadata"] = ValidateMetadata(MetadataJson),
        }.ToJsonString();
    }
}
