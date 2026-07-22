using System;

namespace Mockups.DesktopEditorShell.Common;

internal static class VariantReferenceId
{
    public const string Separator = "::variant::";

    public static string Format(string ownerId, string variantId) =>
        $"{ownerId}{Separator}{variantId}";

    public static bool TryParse(string reference, out string ownerId, out string variantId)
    {
        var separatorIndex = reference.IndexOf(Separator, StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex + Separator.Length >= reference.Length)
        {
            ownerId = "";
            variantId = "";
            return false;
        }

        ownerId = reference[..separatorIndex];
        variantId = reference[(separatorIndex + Separator.Length)..];
        return true;
    }

    public static bool HasVariantId(string reference, string variantId) =>
        TryParse(reference, out _, out var parsedVariantId)
        && parsedVariantId.Equals(variantId, StringComparison.Ordinal);
}
