using System.Text.Json;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>A metadata-declared path inside a dictionary field; item ids are never ordinals.</summary>
public sealed record ComponentOverridePathSegment(string Property, string ItemId)
{
    public static ComponentOverridePathSegment Field(string property) => new(property, "");
    public static ComponentOverridePathSegment Item(string id) => new("", id);
}

public sealed record ComponentOverrideAddress(
    string FieldId,
    IReadOnlyList<ComponentOverridePathSegment> Path,
    string VariantReferenceKey,
    string OverridesKey,
    ThemeComponentVariantSource ThemeSource = ThemeComponentVariantSource.None)
{
    public static ComponentOverrideAddress Slot(string fieldId) =>
        new(fieldId, [], "variantReference", "overrides");

    public static ComponentOverrideAddress Overrides(string fieldId) =>
        new(fieldId, [], "", "");

    public string Identity => JsonSerializer.Serialize(new
    {
        FieldId, Path, VariantReferenceKey, OverridesKey, ThemeSource,
    });
}
