using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ThemeRepository : IThemeRepository
{
    private readonly SqliteProjectContext _context;

    public ThemeRepository(SqliteProjectContext context)
    {
        _context = context;
    }

    public ThemeRecord Get(string themeId)
    {
        using var connection = _context.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json FROM themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", themeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing theme '{themeId}'.");
        }

        return ReadRecord(reader);
    }

    public IReadOnlyList<ThemeRecord> QueryAll(SqliteConnection connection)
    {
        var rows = new List<ThemeRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json FROM themes ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadRecord(reader));
        }

        return rows;
    }

    public void UpdateDirectField(string themeId, string fieldId, string value)
    {
        var column = fieldId switch
        {
            "theme.family" => "family",
            "theme.iconThemeId" => "icon_theme_id",
            "theme.statusBarId" => "status_bar_id",
            "theme.navigationBarId" => "navigation_bar_id",
            _ => throw new InvalidOperationException($"Unknown direct theme field '{fieldId}'."),
        };
        using var connection = _context.OpenConnection();
        var theme = Get(connection, themeId);
        ValidateDirectReference(connection, theme, fieldId, value);
        SqliteCommandExecutor.Execute(
            connection,
            $"UPDATE themes SET {column} = $value WHERE id = $id",
            ("$id", themeId),
            ("$value", value));
    }

    public void UpdateTokens(string themeId, string tokensJson)
    {
        JsonPath.ParseRequiredObject(tokensJson, $"Theme '{themeId}' tokens_json");
        using var connection = _context.OpenConnection();
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id",
            ("$id", themeId),
            ("$tokensJson", tokensJson));
    }

    public ThemeRecord Create(
        SqliteConnection connection,
        string projectId,
        string family,
        string iconThemeId,
        string statusBarId,
        string navigationBarId,
        string tokensJson,
        string metadataJson)
    {
        JsonPath.ParseRequiredObject(tokensJson, "New Theme tokens_json");
        JsonPath.ParseRequiredObject(metadataJson, "New Theme metadata_json");
        ValidateReferences(
            connection,
            projectId,
            iconThemeId,
            statusBarId,
            navigationBarId,
            "New Theme");
        var index = SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM themes WHERE project_id = $projectId",
            ("$projectId", projectId)) + 1;
        var id = $"theme_{Guid.NewGuid():N}";
        var name = family switch
        {
            "ios" => $"iOS Theme {index}",
            "android" => $"Android Theme {index}",
            _ => $"Theme {index}",
        };
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO themes (id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json)
            VALUES ($id, $projectId, $name, $family, $iconThemeId, $statusBarId, $navigationBarId, $tokensJson, $metadataJson)
            """,
            ("$id", id),
            ("$projectId", projectId),
            ("$name", name),
            ("$family", family),
            ("$iconThemeId", iconThemeId),
            ("$statusBarId", statusBarId),
            ("$navigationBarId", navigationBarId),
            ("$tokensJson", tokensJson),
            ("$metadataJson", metadataJson));

        return new ThemeRecord(
            id,
            projectId,
            name,
            family,
            iconThemeId,
            statusBarId,
            navigationBarId,
            tokensJson,
            metadataJson);
    }

    public ThemeRecord Duplicate(SqliteConnection connection, string sourceId, string copyName)
    {
        var source = QueryAll(connection).SingleOrDefault((row) => row.Id == sourceId)
            ?? throw new InvalidOperationException($"Missing theme '{sourceId}'.");
        var copy = source with
        {
            Id = $"theme_{Guid.NewGuid():N}",
            Name = copyName,
        };
        ValidateReferences(
            connection,
            copy.ProjectId,
            copy.IconThemeId,
            copy.StatusBarId,
            copy.NavigationBarId,
            $"Theme '{copy.Id}'");
        SqliteCommandExecutor.Execute(
            connection,
            """
            INSERT INTO themes (id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json)
            VALUES ($id, $projectId, $name, $family, $iconThemeId, $statusBarId, $navigationBarId, $tokensJson, $metadataJson)
            """,
            ("$id", copy.Id),
            ("$projectId", copy.ProjectId),
            ("$name", copy.Name),
            ("$family", copy.Family),
            ("$iconThemeId", copy.IconThemeId),
            ("$statusBarId", copy.StatusBarId),
            ("$navigationBarId", copy.NavigationBarId),
            ("$tokensJson", copy.TokensJson),
            ("$metadataJson", copy.MetadataJson));
        return copy;
    }

    public void Delete(SqliteConnection connection, string themeId)
    {
        SqliteCommandExecutor.Execute(connection, "DELETE FROM themes WHERE id = $id", ("$id", themeId));
    }

    public void Rename(SqliteConnection connection, string themeId, string name)
    {
        SqliteCommandExecutor.Execute(
            connection,
            "UPDATE themes SET name = $name WHERE id = $id",
            ("$id", themeId),
            ("$name", name));
    }

    private static ThemeRecord ReadRecord(SqliteDataReader reader)
    {
        return new ThemeRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            SqliteCommandExecutor.ReadString(reader, 3),
            SqliteCommandExecutor.ReadString(reader, 4),
            SqliteCommandExecutor.ReadString(reader, 5),
            SqliteCommandExecutor.ReadString(reader, 6),
            SqliteCommandExecutor.ReadString(reader, 7),
            SqliteCommandExecutor.ReadString(reader, 8));
    }

    private static ThemeRecord Get(SqliteConnection connection, string themeId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json FROM themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", themeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing theme '{themeId}'.");
        }
        return ReadRecord(reader);
    }

    private static void ValidateDirectReference(
        SqliteConnection connection,
        ThemeRecord theme,
        string fieldId,
        string value)
    {
        switch (fieldId)
        {
            case "theme.family":
                return;
            case "theme.iconThemeId":
                ProjectReferenceIntegrity.RequireSameProjectReference(
                    connection,
                    theme.ProjectId,
                    ProjectReferenceKind.IconTheme,
                    value,
                    $"Theme '{theme.Id}' Icon Theme");
                return;
            case "theme.statusBarId":
                ProjectReferenceIntegrity.RequireComponentVariantReference(
                    connection,
                    theme.ProjectId,
                    value,
                    StatusBarComponentConfigContract.ComponentType,
                    $"Theme '{theme.Id}' Status Bar");
                return;
            case "theme.navigationBarId":
                ProjectReferenceIntegrity.RequireComponentVariantReference(
                    connection,
                    theme.ProjectId,
                    value,
                    NavigationBarComponentConfigContract.ComponentType,
                    $"Theme '{theme.Id}' Navigation Bar");
                return;
            default:
                throw new InvalidOperationException($"Unknown direct theme field '{fieldId}'.");
        }
    }

    private static void ValidateReferences(
        SqliteConnection connection,
        string projectId,
        string iconThemeId,
        string statusBarId,
        string navigationBarId,
        string context)
    {
        ProjectReferenceIntegrity.RequireSameProjectReference(
            connection,
            projectId,
            ProjectReferenceKind.IconTheme,
            iconThemeId,
            $"{context} Icon Theme");
        ProjectReferenceIntegrity.RequireComponentVariantReference(
            connection,
            projectId,
            statusBarId,
            StatusBarComponentConfigContract.ComponentType,
            $"{context} Status Bar");
        ProjectReferenceIntegrity.RequireComponentVariantReference(
            connection,
            projectId,
            navigationBarId,
            NavigationBarComponentConfigContract.ComponentType,
            $"{context} Navigation Bar");
    }
}
