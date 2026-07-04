using Avalonia.Controls;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class SvgIconPreview
{
    public static Control CreateIconThemePreview(SpikeDatabase database, string iconThemeId, string file, double size)
    {
        try
        {
            var path = database.ResolveIconThemeAssetPath(iconThemeId, file);
            if (!File.Exists(path)) return EditorIcons.Create(EditorIcons.Icon, size);

            return CreateFromSvg(File.ReadAllText(path), size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    public static Control CreateProjectIconTokenPreview(SpikeDatabase database, string projectId, string token, double size)
    {
        try
        {
            var firstToken = token
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstToken)) return EditorIcons.Create(EditorIcons.Icon, size);

            var path = database.ResolveIconTokenAssetPath(projectId, firstToken);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return EditorIcons.Create(EditorIcons.Icon, size);

            return CreateFromSvg(File.ReadAllText(path), size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    public static Control CreateSearchPreview(string previewUrl, double size)
    {
        const string prefix = "data:image/svg+xml;base64,";
        if (string.IsNullOrWhiteSpace(previewUrl) || !previewUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }

        try
        {
            var svg = Encoding.UTF8.GetString(Convert.FromBase64String(previewUrl[prefix.Length..]));
            return CreateFromSvg(svg, size);
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }

    public static Control CreateFromSvg(string svg, double size)
    {
        try
        {
            var validatedSvg = SvgReplacementService.Validate(svg);
            var geometry = SvgReplacementService.TryGeometry(validatedSvg);
            var webView = new NativeWebView
            {
                Width = size,
                Height = size,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
            };
            webView.NavigateToString(
                SvgMarkupPreview.CreateHtml(validatedSvg, geometry, 0, false),
                new Uri("https://mockups.local/svg-icon-preview/"));

            return new Border
            {
                Width = size,
                Height = size,
                ClipToBounds = true,
                Child = webView,
            };
        }
        catch
        {
            return EditorIcons.Create(EditorIcons.Icon, size);
        }
    }
}
