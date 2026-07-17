using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public IReadOnlyList<FieldOption> GetProductionFontOptions(string projectId, string? category = null)
    {
        using var connection = OpenConnection();
        var fonts = QueryProductionFontRows(connection)
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
        return QueryProductionFontRows(connection)
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
        var existingId = ExistingProductionFontId(connection, project.Id, familyName);
        var id = string.IsNullOrWhiteSpace(existingId) ? $"font_{Guid.NewGuid():N}" : existingId;
        if (string.IsNullOrWhiteSpace(existingId))
        {
            Execute(
                connection,
                """
                INSERT INTO production_fonts (id, project_id, family_name, category, source_directory, files_json)
                VALUES ($id, $projectId, $familyName, $category, $sourceDirectory, $filesJson)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$familyName", familyName),
                ("$category", category),
                ("$sourceDirectory", NormalizeRelativePath(relativeDirectory)),
                ("$filesJson", copiedFiles.ToJsonString()));
        }
        else
        {
            Execute(
                connection,
                """
                UPDATE production_fonts
                SET category = $category,
                    source_directory = $sourceDirectory,
                    files_json = $filesJson
                WHERE id = $id
                """,
                ("$id", id),
                ("$category", category),
                ("$sourceDirectory", NormalizeRelativePath(relativeDirectory)),
                ("$filesJson", copiedFiles.ToJsonString()));
        }

        return new ProjectTreeNode(
            ProjectTreeNodeKind.ProductionFont,
            id,
            familyName,
            $"{category} · {copiedFiles.Count} files",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ProductionFont),
            fontsRoot);
    }

    public ProductionFontSettings GetProductionFontSettings(string fontId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT family_name, category, source_directory, files_json FROM production_fonts WHERE id = $id";
        command.Parameters.AddWithValue("$id", fontId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing production font '{fontId}'.");
        }

        return new ProductionFontSettings(
            reader.GetString(0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3));
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
        using var connection = OpenConnection();
        var column = fieldId switch
        {
            "font.family" => "family_name",
            "font.category" => "category",
            _ => throw new InvalidOperationException($"Unknown editable production font field '{fieldId}'."),
        };

        Execute(
            connection,
            $"UPDATE production_fonts SET {column} = $value WHERE id = $id",
            ("$id", fontId),
            ("$value", value));
    }

    private static List<ProductionFontRow> QueryProductionFontRows(SqliteConnection connection)
    {
        var rows = new List<ProductionFontRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, family_name, category, source_directory, files_json FROM production_fonts ORDER BY family_name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ProductionFontRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5)));
        }

        return rows;
    }

    private static string ExistingProductionFontId(SqliteConnection connection, string projectId, string familyName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM production_fonts WHERE project_id = $projectId AND family_name = $familyName";
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$familyName", familyName);
        return command.ExecuteScalar() as string ?? "";
    }

    private static int ProductionFontFileCount(string filesJson)
    {
        return JsonPath.ParseRequiredArray(filesJson, "Production Font files").Count;
    }

    private static string ProductionFontFilesSummary(string filesJson)
    {
        var files = JsonPath.ParseRequiredArray(filesJson, "Production Font files");
        if (files.Count == 0) return "No copied font files.";

        return string.Join(
            Environment.NewLine,
            files
                .OfType<JsonObject>()
                .Select((file) =>
                {
                    var name = JsonNodeString(file, "fileName");
                    var style = JsonNodeString(file, "style");
                    var weight = JsonNodeString(file, "weight");
                    var relativePath = JsonNodeString(file, "relativePath");
                    return $"{name} · {style} · {weight} · {relativePath}";
                }));
    }

    private static string JsonNodeString(JsonObject node, string key)
    {
        if (!node.TryGetPropertyValue(key, out var value) || value is null) return "";
        return value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : value.ToJsonString();
    }

    private static IEnumerable<ProductionFontFace> FontFaces(ProductionFontRow font)
    {
        var files = JsonPath.ParseRequiredArray(font.FilesJson, $"Production Font '{font.Id}' files");

        foreach (var file in files.OfType<JsonObject>())
        {
            var relativePath = JsonNodeString(file, "relativePath");
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var weightText = JsonNodeString(file, "weight");
            yield return new ProductionFontFace(
                font.Id,
                font.FamilyName,
                font.Category,
                NormalizeRelativePath(relativePath),
                int.TryParse(weightText, out var weight) ? weight : 400,
                JsonNodeString(file, "style") == "italic" ? "italic" : "normal");
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
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id, source_directory
            FROM production_fonts
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", fontId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return;

        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, ReadString(reader, 0)).MediaRoot);
        var sourceDirectory = ReadString(reader, 1);
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
