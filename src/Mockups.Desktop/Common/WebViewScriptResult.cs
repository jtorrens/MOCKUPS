using System.Text.Json;

namespace Mockups.DesktopEditorShell.Common;

internal static class WebViewScriptResult
{
    public static string Text(object? result)
    {
        var text = result?.ToString() ?? "";
        if (text.Length < 2 || text[0] != '"')
        {
            return text;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(text) ?? "";
        }
        catch (JsonException)
        {
            return text;
        }
    }
}
