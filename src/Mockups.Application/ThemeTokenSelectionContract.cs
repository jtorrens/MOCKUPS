using System;
using System.Collections.Generic;
using System.Linq;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>Dictionary selection scopes over the authoritative Theme catalogs.</summary>
public static class ThemeTokenSelectionContract
{
    public static IReadOnlyList<string> Tokens(
        string family,
        IReadOnlyList<FieldOption>? options,
        string owner)
    {
        if (!string.IsNullOrEmpty(family))
        {
            if (options is { Count: > 0 })
                throw new InvalidOperationException($"{owner} cannot combine a Theme family with fixed options.");
            return FamilyTokens(family);
        }

        // A field tied to one semantic metric may declare that exact catalog token.
        if (options is { Count: 1 }
            && (ThemeNumericTokenCatalog.TryGet(options[0].Value, out _)
                || ThemeColorTokenCatalog.TryGet(options[0].Value, out _)))
            return [options[0].Value];

        throw new InvalidOperationException($"{owner} requires a Theme token family or one exact catalog token.");
    }

    public static IReadOnlyList<string> FamilyTokens(string family)
    {
        if (family == "colors")
            return ThemeColorTokenCatalog.ColorTokens.Select(token => token.Id).ToArray();

        if (family is not ("spacing" or "radii" or "iconSizes"
            or "typography.sizes" or "typography.lineHeights" or "motion.naturalPace"))
            throw new InvalidOperationException($"Unknown Theme token family '{family}'.");

        return ThemeNumericTokenCatalog.NumericTokens
            .Where(token => token.Id.StartsWith($"theme.{family}.", StringComparison.Ordinal))
            .Select(token => token.Id).ToArray();
    }

    public static FieldOption[] Options(string family) => FamilyTokens(family)
        .Select(token => new FieldOption(token, token[6..])).ToArray();

    public static void Validate(ValueKind kind, string family,
        IReadOnlyList<FieldOption>? options, string owner)
    {
        if (kind is ValueKind.ThemeToken or ValueKind.ThemeTokenPair or ValueKind.BehaviorTiming)
            _ = Tokens(family, options, owner);
        else if (!string.IsNullOrEmpty(family))
            throw new InvalidOperationException($"{owner} cannot declare a Theme token family for {kind}.");
    }
}
