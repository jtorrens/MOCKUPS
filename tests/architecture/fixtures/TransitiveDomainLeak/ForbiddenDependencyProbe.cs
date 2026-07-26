using Mockups.DesktopEditorShell.Common;

namespace TransitiveDomainLeak;

public static class ForbiddenDependencyProbe
{
    public static PaletteAlphaValue Create() =>
        new("theme.color.primary", 1d);
}
