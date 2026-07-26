using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public IReadOnlyList<FieldOption> GetProductionFontOptions(string projectId, string? category = null)
    {
        using var connection = OpenConnection();
        var fonts = _productionFontRepository.QueryAll(connection)
            .Where((font) => font.ProjectId == projectId)
            .Where((font) => string.IsNullOrWhiteSpace(category) || font.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy((font) => font.FamilyName)
            .Select((font) => new FieldOption(font.Id, font.FamilyName))
            .ToList();

        fonts.Insert(0, new FieldOption("", "System default"));
        return fonts;
    }

    public IReadOnlyList<ProductionFontFace> GetProductionFontFaces(string projectId)
    {
        using var connection = OpenConnection();
        return _productionFontRepository.QueryAll(connection)
            .Where((font) => font.ProjectId == projectId)
            .SelectMany(FontFaces)
            .ToList();
    }

    public ProjectTreeNode ImportProductionFont(ProjectTreeNode fontsRoot, IReadOnlyList<string> selectedFilePaths)
    {
        if (fontsRoot.Kind != ProjectTreeNodeKind.ProductionFontsRoot)
        {
            throw new InvalidOperationException("Production fonts can only be imported from the Production Fonts root.");
        }

        var sourceFiles = ExpandFontFamilyFiles(selectedFilePaths);
        if (sourceFiles.Count == 0)
        {
            throw new InvalidOperationException("No supported font files were selected.");
        }

        var project = ProjectAncestor(fontsRoot);
        var projectSettings = GetProjectSettings(project.Id);
        var familyName = InferFontFamilyName(sourceFiles[0]);
        var category = IsEmojiFontFamily(familyName) ? "emoji" : "text";
        var familySlug = Slug(familyName);
        var relativeDirectory = Path.Combine("fonts", familySlug);
        var mediaRoot = ResolveProjectPath(projectSettings.MediaRoot);
        var targetDirectory = Path.Combine(mediaRoot, relativeDirectory);
        Directory.CreateDirectory(targetDirectory);

        var copiedFiles = new JsonArray();
        foreach (var sourceFile in sourceFiles.OrderBy(Path.GetFileName))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
            if (!Path.GetFullPath(sourceFile).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceFile, targetPath, overwrite: true);
            }

            copiedFiles.Add(new JsonObject
            {
                ["fileName"] = Path.GetFileName(sourceFile),
                ["relativePath"] = NormalizeRelativePath(Path.Combine(relativeDirectory, Path.GetFileName(sourceFile))),
                ["style"] = InferFontStyle(sourceFile),
                ["weight"] = InferFontWeight(sourceFile),
            });
        }

        using var connection = OpenConnection();
        var imported = _productionFontRepository.UpsertImported(
            connection,
            project.Id,
            familyName,
            category,
            NormalizeRelativePath(relativeDirectory),
            copiedFiles.ToJsonString());

        return new ProjectTreeNode(
            ProjectTreeNodeKind.ProductionFont,
            imported.Id,
            familyName,
            $"{category} · {copiedFiles.Count} files",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ProductionFont),
            fontsRoot);
    }

    public ProductionFontSettings GetProductionFontSettings(string fontId)
    {
        var record = _productionFontRepository.Get(fontId);

        return new ProductionFontSettings(
            record.FamilyName,
            record.Category,
            record.SourceDirectory,
            record.FilesJson);
    }

    public string GetProductionFontFieldValue(string fontId, string fieldId)
    {
        var settings = GetProductionFontSettings(fontId);
        return fieldId switch
        {
            "font.family" => settings.FamilyName,
            "font.category" => settings.Category,
            "font.sourceDirectory" => settings.SourceDirectory,
            "font.files" => ProductionFontFilesSummary(settings.FilesJson),
            _ => throw new InvalidOperationException($"Unknown production font field '{fieldId}'."),
        };
    }

    public void UpdateProductionFontField(string fontId, string fieldId, string value)
    {
        _productionFontRepository.UpdateField(fontId, fieldId, value);
    }

    private static int ProductionFontFileCount(string filesJson)
    {
        return ProductionFontFilesContract.ParseRequired(filesJson, "Production Font files").Count;
    }

    private static string ProductionFontFilesSummary(string filesJson)
    {
        var files = ProductionFontFilesContract.ParseRequired(filesJson, "Production Font files");
        if (files.Count == 0) return "No copied font files.";

        return string.Join(
            Environment.NewLine,
            files.Select((file) =>
                $"{file.FileName} · {file.Style} · {file.Weight} · {file.RelativePath}"));
    }

    private static IEnumerable<ProductionFontFace> FontFaces(ProductionFontRecord font)
    {
        var files = ProductionFontFilesContract.ParseRequired(
            font.FilesJson,
            $"Production Font '{font.Id}' files");

        foreach (var file in files)
        {
            yield return new ProductionFontFace(
                font.Id,
                font.FamilyName,
                font.Category,
                file.RelativePath,
                file.Weight,
                file.Style);
        }
    }

    private static IReadOnlyList<string> ExpandFontFamilyFiles(IReadOnlyList<string> selectedFilePaths)
    {
        var selected = selectedFilePaths
            .Where(IsSupportedFontFile)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selected.Count == 0) return selected;

        var first = selected[0];
        var directory = Path.GetDirectoryName(first);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return selected;
        }

        var family = InferFontFamilyName(first);
        var familySlug = Slug(family);
        var matchingFamilyFiles = Directory
            .EnumerateFiles(directory)
            .Where(IsSupportedFontFile)
            .Where((file) => Slug(InferFontFamilyName(file)) == familySlug)
            .Select(Path.GetFullPath);

        return selected
            .Concat(matchingFamilyFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(Path.GetFileName)
            .ToList();
    }

    private static bool IsSupportedFontFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".ttf" or ".otf" or ".ttc" or ".woff" or ".woff2";
    }

    private static string InferFontFamilyName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var dashIndex = name.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            return CleanFontFamilyName(name[..dashIndex]);
        }

        return CleanFontFamilyName(FontStyleSuffixRegex().Replace(name, ""));
    }

    private static string CleanFontFamilyName(string value)
    {
        var clean = value.Replace('_', ' ').Trim();
        clean = Regex.Replace(clean, "\\s+", " ");
        return string.IsNullOrWhiteSpace(clean) ? "Imported Font" : clean;
    }

    private static string InferFontStyle(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return name.Contains("italic", StringComparison.OrdinalIgnoreCase)
            || name.Contains("oblique", StringComparison.OrdinalIgnoreCase)
                ? "italic"
                : "normal";
    }

    private static int InferFontWeight(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        if (ContainsWeight(name, "Thin")) return 100;
        if (ContainsWeight(name, "ExtraLight") || ContainsWeight(name, "UltraLight")) return 200;
        if (ContainsWeight(name, "Light")) return 300;
        if (ContainsWeight(name, "Medium")) return 500;
        if (ContainsWeight(name, "SemiBold") || ContainsWeight(name, "Semibold") || ContainsWeight(name, "DemiBold")) return 600;
        if (ContainsWeight(name, "ExtraBold") || ContainsWeight(name, "UltraBold")) return 800;
        if (ContainsWeight(name, "Black") || ContainsWeight(name, "Heavy")) return 900;
        if (ContainsWeight(name, "Bold")) return 700;
        return 400;
    }

    private static bool ContainsWeight(string name, string token)
    {
        return name.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmojiFontFamily(string familyName)
    {
        return familyName.Contains("emoji", StringComparison.OrdinalIgnoreCase);
    }

    private void DeleteProductionFontFiles(SqliteConnection connection, string fontId)
    {
        var font = _productionFontRepository.Get(connection, fontId);
        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, font.ProjectId).MediaRoot);
        var sourceDirectory = font.SourceDirectory;
        if (string.IsNullOrWhiteSpace(mediaRoot) || string.IsNullOrWhiteSpace(sourceDirectory)) return;

        var targetDirectory = Path.GetFullPath(Path.Combine(mediaRoot, sourceDirectory));
        var fullMediaRoot = Path.GetFullPath(mediaRoot);
        var relative = Path.GetRelativePath(fullMediaRoot, targetDirectory);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative)) return;
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }
    }

    [GeneratedRegex("(Regular|Bold|Italic|Light|Medium|SemiBold|Semibold|Black|Thin|ExtraLight|UltraLight|ExtraBold|UltraBold|Condensed|Oblique|Variable|VF|Roman)$", RegexOptions.IgnoreCase)]
    private static partial Regex FontStyleSuffixRegex();
}
