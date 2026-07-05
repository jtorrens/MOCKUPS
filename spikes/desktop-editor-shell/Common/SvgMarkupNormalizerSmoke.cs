using System;

namespace Mockups.DesktopEditorShell.Common;

internal static class SvgMarkupNormalizerSmoke
{
    public static void Validate()
    {
        const string source = """
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <g>
                <svg x="0" y="0" width="22" height="23" viewBox="0 0 22 23" color="#000" fill="#000" stroke="none">
                  <path d="M1 1H20V20H1Z" style="fill:#000;stroke:#000"/>
                </svg>
              </g>
            </svg>
            """;
        const string implicitBlackFillSource = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 -960 960 960">
              <path d="M417-154q-27-27-27-63t27-63h126q27 27 27 63t-27 63Z"/>
            </svg>
            """;
        const string tint = "#FF8A80";

        var normalized = SvgMarkupNormalizer.NormalizeTintableSvg(source);
        if (normalized.Contains("#000", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tintable SVG normalization must remove hardcoded black paint.");
        }

        if (!normalized.Contains("currentColor", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tintable SVG normalization must preserve currentColor placeholders.");
        }

        var painted = SvgMarkupNormalizer.ApplyCurrentColorTint(normalized, tint);
        if (painted.Contains("currentColor", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tinted SVG materialization must not leave currentColor placeholders.");
        }

        if (!painted.Contains(tint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tinted SVG materialization must apply the resolved tint color.");
        }

        var implicitBlackFill = SvgMarkupNormalizer.NormalizeTintableSvg(implicitBlackFillSource);
        if (!implicitBlackFill.Contains("fill=\"currentColor\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tintable SVG normalization must convert implicit black fill to currentColor.");
        }
    }
}
