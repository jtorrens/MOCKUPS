using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class ModuleAppearanceModeContract
{
    public const string Inherit = "inherit";
    public const string Light = "light";
    public const string Dark = "dark";

    public static string Read(JsonObject config, string owner)
    {
        if (config["appearanceMode"] is not JsonValue value
            || !value.TryGetValue<string>(out var mode))
        {
            throw new InvalidOperationException(
                $"{owner} must contain a string 'appearanceMode'.");
        }

        return Require(mode, owner);
    }

    public static string Require(string mode, string owner)
    {
        return mode is Inherit or Light or Dark
            ? mode
            : throw new InvalidOperationException(
                $"{owner} appearanceMode must be 'inherit', 'light' or 'dark'.");
    }

    public static string Resolve(JsonObject config, string inheritedMode, string owner)
    {
        var mode = Read(config, owner);
        var resolvedInheritedMode = RequireResolved(inheritedMode, "Inherited Preview Theme mode");
        return mode == Inherit ? resolvedInheritedMode : mode;
    }

    public static string RequireResolved(string mode, string owner)
    {
        return mode is Light or Dark
            ? mode
            : throw new InvalidOperationException(
                $"{owner} must be 'light' or 'dark'.");
    }
}
