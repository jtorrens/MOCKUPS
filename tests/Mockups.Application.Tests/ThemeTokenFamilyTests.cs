using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

internal static class ThemeTokenFamilyTests
{
    public static void CatalogAndMetadataStayComplete()
    {
        var spacing = ThemeTokenSelectionContract.Tokens("spacing", null, "fixture");
        Require(new[] { "none", "xs", "s", "m", "l", "xl", "xxl" }
            .All(size => spacing.Contains($"theme.spacing.{size}")), "complete spacing scale");
        foreach (var (family, token) in new[] {
            ("radii", "theme.radii.full"), ("iconSizes", "theme.iconSizes.xl"),
            ("typography.sizes", "theme.typography.sizes.xl"),
            ("typography.lineHeights", "theme.typography.lineHeights.loose"),
            ("motion.naturalPace", "theme.motion.naturalPace.veryFast"),
            ("colors", "theme.keyboard.text"), ("colors", "theme.borders.alternate") })
            Require(ThemeTokenSelectionContract.Tokens(family, null, "fixture").Contains(token), family);
        Require(!ThemeTokenSelectionContract.Tokens("colors", null, "fixture").Contains("theme.spacing.l"), "color scope");

        foreach (var field in RecordClassFieldCatalog.All.Where(field =>
            field.Id.EndsWith("Layout.padding", StringComparison.Ordinal)
            || field.Id == "module.core.videoCall.participantOuterPadding"))
        {
            Require(field.ThemeTokenFamily == "spacing" && field.Options is not { Count: > 0 }, field.Id);
            Require(ThemeTokenSelectionContract.Tokens(field.ThemeTokenFamily, field.Options, field.Id)
                .Contains("theme.spacing.xxl"), field.Id);
        }
        var inputs = RuntimeInputDefinitionReader.ReadInputs(JsonNode.Parse("""
            {"inputs":[{"id":"size","label":"Size","jsonKey":"size","kind":"themeToken",
            "valueKind":"ThemeToken","defaultValue":"theme.iconSizes.m","themeTokenFamily":"iconSizes"}]}
            """)!.AsObject(), new JsonObject());
        Require(ComponentInputBindingDefinition.FromRuntimeInput(inputs.Single()).ThemeTokenFamily == "iconSizes", "binding projection");

        Reject(() => ThemeTokenSelectionContract.Tokens("unknown", null, "fixture"));
        Reject(() => ThemeTokenSelectionContract.Tokens("spacing", [new("theme.spacing.s", "s")], "fixture"));
        Reject(() => ThemeTokenSelectionContract.Tokens("", [new("theme.spacing.s", "s"), new("theme.spacing.m", "m")], "fixture"));
        Require(ThemeTokenSelectionContract.Tokens("", [new("theme.keyboard.height", "height")], "fixture").Single()
            == "theme.keyboard.height", "exact metric");
    }

    private static void Require(bool condition, string owner)
    {
        if (!condition) throw new InvalidOperationException($"Theme family regression: {owner}");
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid Theme selection scope was accepted.");
    }
}
