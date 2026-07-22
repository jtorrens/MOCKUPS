using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record ProductionFontFileDocument(
    string FileName,
    string RelativePath,
    string Style,
    int Weight);

internal static class ProductionFontFilesContract
{
    private static readonly HashSet<string> Styles = new(StringComparer.Ordinal)
    {
        "normal",
        "italic",
    };

    public static IReadOnlyList<ProductionFontFileDocument> ParseRequired(
        string json,
        string context)
    {
        var files = JsonPath.ParseRequiredArray(json, context);
        var result = new List<ProductionFontFileDocument>(files.Count);
        var relativePaths = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < files.Count; index++)
        {
            var fileContext = $"{context}[{index}]";
            var file = files[index] as JsonObject
                ?? throw new InvalidOperationException($"{fileContext} must be an object.");
            var fileName = JsonPath.RequiredString(file, "fileName", fileContext);
            var relativePath = JsonPath.RequiredString(file, "relativePath", fileContext);
            var style = JsonPath.RequiredString(file, "style", fileContext);
            var weight = JsonPath.RequiredInteger(file, "weight", fileContext);

            RequireFileName(fileName, fileContext);
            RequireRelativePath(relativePath, fileName, fileContext);
            if (!Styles.Contains(style))
            {
                throw new InvalidOperationException(
                    $"{fileContext}.style must be 'normal' or 'italic'.");
            }
            if (weight is < 1 or > 1000)
            {
                throw new InvalidOperationException(
                    $"{fileContext}.weight must be an integer from 1 through 1000.");
            }
            if (!relativePaths.Add(relativePath))
            {
                throw new InvalidOperationException(
                    $"{context} contains duplicate relativePath '{relativePath}'.");
            }

            result.Add(new ProductionFontFileDocument(fileName, relativePath, style, weight));
        }

        return result;
    }

    private static void RequireFileName(string fileName, string context)
    {
        if (fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new InvalidOperationException(
                $"{context}.fileName must contain only the final file name.");
        }
    }

    private static void RequireRelativePath(
        string relativePath,
        string fileName,
        string context)
    {
        var segments = relativePath.Split('/');
        if (relativePath.StartsWith("/", StringComparison.Ordinal)
            || relativePath.Contains('\\')
            || segments.Any((segment) => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new InvalidOperationException(
                $"{context}.relativePath must be a normalized safe relative path.");
        }
        if (!segments[^1].Equals(fileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{context}.fileName must match the final relativePath segment.");
        }
    }
}
