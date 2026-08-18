using System;

namespace Mockups.DesktopEditorShell.Common;

public static class VariantReferenceId
{
    public const string Separator = "::variant::";

    public static string Format(string ownerId, string variantId) =>
        $"{ownerId}{Separator}{variantId}";

    public static bool TryParse(string reference, out string ownerId, out string variantId)
    {
        var separatorIndex = reference.IndexOf(Separator, StringComparison.Ordinal);
        if (separatorIndex <= 0
            || separatorIndex != reference.LastIndexOf(Separator, StringComparison.Ordinal)
            || separatorIndex + Separator.Length >= reference.Length)
        {
            ownerId = "";
            variantId = "";
            return false;
        }

        ownerId = reference[..separatorIndex];
        variantId = reference[(separatorIndex + Separator.Length)..];
        if (!IsStableId(ownerId) || !IsStableId(variantId))
        {
            ownerId = "";
            variantId = "";
            return false;
        }

        return true;
    }

    public static bool HasVariantId(string reference, string variantId) =>
        TryParse(reference, out _, out var parsedVariantId)
        && parsedVariantId.Equals(variantId, StringComparison.Ordinal);

    private static bool IsStableId(string value)
    {
        if (value.Length == 0) return false;
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character)
                || character is '_' or '.' or '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
