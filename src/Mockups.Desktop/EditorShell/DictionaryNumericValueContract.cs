using System;
using System.Globalization;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryNumericValueContract
{
    public static decimal ParseRequired(FieldDefinition definition, string value)
    {
        var parsed = RuntimeInputValueKindContract.ParseNumber(
            definition.ValueKind,
            value,
            $"Dictionary field '{definition.Id}'");
        return RequireDeclaredRange(definition, parsed);
    }

    public static bool TryParseDraft(
        FieldDefinition definition,
        string? value,
        out decimal parsed)
    {
        var text = value ?? "";
        switch (definition.ValueKind)
        {
            case ValueKind.Integer:
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                    && !int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out integer))
                {
                    parsed = 0;
                    return false;
                }
                parsed = integer;
                break;
            case ValueKind.Decimal:
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                    && !decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
                {
                    return false;
                }
                break;
            default:
                throw new InvalidOperationException(
                    $"Dictionary field '{definition.Id}' has unsupported numeric ValueKind '{definition.ValueKind}'.");
        }

        return IsWithinDeclaredRange(definition, parsed);
    }

    private static decimal RequireDeclaredRange(FieldDefinition definition, decimal value)
    {
        if (!IsWithinDeclaredRange(definition, value))
        {
            throw new InvalidOperationException(
                $"Dictionary field '{definition.Id}' value '{value.ToString(CultureInfo.InvariantCulture)}' is outside its declared range.");
        }

        return value;
    }

    private static bool IsWithinDeclaredRange(FieldDefinition definition, decimal value)
    {
        return (definition.Number?.Minimum is not { } minimum || value >= minimum)
            && (definition.Number?.Maximum is not { } maximum || value <= maximum);
    }
}
