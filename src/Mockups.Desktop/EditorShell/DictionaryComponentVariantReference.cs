using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryComponentVariantReference
{
    public static string Read(string value, string fieldId)
    {
        if (VariantReferenceId.TryParse(value, out _, out _))
        {
            return value;
        }
        var owner = $"Component Variant field '{fieldId}'";
        return ComponentVariantSlotDocumentContract.VariantReference(
            JsonPath.ParseRequiredObject(value, owner),
            owner);
    }
}
