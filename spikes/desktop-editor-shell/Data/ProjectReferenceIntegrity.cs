using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal enum ProjectReferenceKind
{
    Actor,
    Device,
    IconTheme,
    RenderPreset,
    Theme,
}

internal static class ProjectReferenceIntegrity
{
    public static void ValidateCurrentDatabase(SqliteConnection connection)
    {
        foreach (var actor in ActorReferences(connection))
        {
            RequireSameProjectReference(
                connection,
                actor.ProjectId,
                ProjectReferenceKind.Device,
                actor.DefaultDeviceId,
                $"Actor '{actor.Id}' default Device");
            RequireSameProjectReference(
                connection,
                actor.ProjectId,
                ProjectReferenceKind.Theme,
                actor.DefaultThemeId,
                $"Actor '{actor.Id}' default Theme");
        }

        foreach (var shot in ShotReferences(connection))
        {
            RequireSameProjectReference(
                connection,
                shot.ProjectId,
                ProjectReferenceKind.Actor,
                shot.OwnerActorId,
                $"Shot '{shot.Id}' owner Actor",
                required: true);
            RequireSameProjectReference(
                connection,
                shot.ProjectId,
                ProjectReferenceKind.RenderPreset,
                shot.RenderPresetId,
                $"Shot '{shot.Id}' Render Preset");
        }

        foreach (var theme in ThemeReferences(connection))
        {
            RequireSameProjectReference(
                connection,
                theme.ProjectId,
                ProjectReferenceKind.IconTheme,
                theme.IconThemeId,
                $"Theme '{theme.Id}' Icon Theme");
            RequireComponentVariantReference(
                connection,
                theme.ProjectId,
                theme.StatusBarId,
                StatusBarComponentConfigContract.ComponentType,
                $"Theme '{theme.Id}' Status Bar");
            RequireComponentVariantReference(
                connection,
                theme.ProjectId,
                theme.NavigationBarId,
                NavigationBarComponentConfigContract.ComponentType,
                $"Theme '{theme.Id}' Navigation Bar");
        }
    }

    public static void RequireSameProjectReference(
        SqliteConnection connection,
        string ownerProjectId,
        ProjectReferenceKind referenceKind,
        string referenceId,
        string context,
        bool required = false)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            if (required)
            {
                throw new InvalidOperationException($"{context} requires an explicit reference.");
            }
            return;
        }

        var targetProjectId = SqliteCommandExecutor.ScalarString(
            connection,
            $"SELECT project_id FROM {TableName(referenceKind)} WHERE id = $id",
            ("$id", referenceId));
        if (targetProjectId is null)
        {
            throw new InvalidOperationException($"{context} references missing {ReferenceLabel(referenceKind)} '{referenceId}'.");
        }
        if (!targetProjectId.Equals(ownerProjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{context} references {ReferenceLabel(referenceKind)} '{referenceId}' from another Project.");
        }
    }

    public static void RequireComponentVariantReference(
        SqliteConnection connection,
        string ownerProjectId,
        string reference,
        string expectedComponentType,
        string context)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return;
        }
        if (!VariantReferenceId.TryParse(reference, out var componentClassId, out var variantId)
            || !VariantReferenceId.Format(componentClassId, variantId).Equals(reference, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{context} has invalid complete Component Variant reference '{reference}'.");
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id, component_type, metadata_json
            FROM component_classes
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", componentClassId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"{context} references missing Component Class '{componentClassId}'.");
        }

        var projectId = reader.GetString(0);
        var componentType = reader.GetString(1);
        var metadataJson = reader.GetString(2);
        if (!projectId.Equals(ownerProjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{context} references Component Class '{componentClassId}' from another Project.");
        }
        if (!componentType.Equals(expectedComponentType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{context} requires Component type '{expectedComponentType}', not '{componentType}'.");
        }

        var metadata = JsonPath.ParseRequiredObject(
            metadataJson,
            $"Component Class '{componentClassId}' metadata_json");
        if (VariantEnvelopeContract.Read(
                metadata,
                "variants",
                $"Component Class '{componentClassId}'")
            .All((variant) => !variant.Id.Equals(variantId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{context} references missing Variant '{variantId}' on Component Class '{componentClassId}'.");
        }
    }

    private static IReadOnlyList<ActorReferenceRow> ActorReferences(SqliteConnection connection)
    {
        var rows = new List<ActorReferenceRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, default_device_id, default_theme_id
            FROM actors
            ORDER BY id
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ActorReferenceRow(
                reader.GetString(0),
                reader.GetString(1),
                SqliteCommandExecutor.ReadString(reader, 2),
                SqliteCommandExecutor.ReadString(reader, 3)));
        }
        return rows;
    }

    private static IReadOnlyList<ShotReferenceRow> ShotReferences(SqliteConnection connection)
    {
        var rows = new List<ShotReferenceRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, e.project_id, s.owner_actor_id, s.render_preset_id
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            ORDER BY s.id
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ShotReferenceRow(
                reader.GetString(0),
                reader.GetString(1),
                SqliteCommandExecutor.ReadString(reader, 2),
                SqliteCommandExecutor.ReadString(reader, 3)));
        }
        return rows;
    }

    private static IReadOnlyList<ThemeReferenceRow> ThemeReferences(SqliteConnection connection)
    {
        var rows = new List<ThemeReferenceRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, icon_theme_id, status_bar_id, navigation_bar_id
            FROM themes
            ORDER BY id
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ThemeReferenceRow(
                reader.GetString(0),
                reader.GetString(1),
                SqliteCommandExecutor.ReadString(reader, 2),
                SqliteCommandExecutor.ReadString(reader, 3),
                SqliteCommandExecutor.ReadString(reader, 4)));
        }
        return rows;
    }

    private static string TableName(ProjectReferenceKind referenceKind) =>
        referenceKind switch
        {
            ProjectReferenceKind.Actor => "actors",
            ProjectReferenceKind.Device => "devices",
            ProjectReferenceKind.IconTheme => "icon_themes",
            ProjectReferenceKind.RenderPreset => "render_presets",
            ProjectReferenceKind.Theme => "themes",
            _ => throw new InvalidOperationException($"Unsupported Project reference kind '{referenceKind}'."),
        };

    private static string ReferenceLabel(ProjectReferenceKind referenceKind) =>
        referenceKind switch
        {
            ProjectReferenceKind.Actor => "Actor",
            ProjectReferenceKind.Device => "Device",
            ProjectReferenceKind.IconTheme => "Icon Theme",
            ProjectReferenceKind.RenderPreset => "Render Preset",
            ProjectReferenceKind.Theme => "Theme",
            _ => throw new InvalidOperationException($"Unsupported Project reference kind '{referenceKind}'."),
        };

    private sealed record ActorReferenceRow(
        string Id,
        string ProjectId,
        string DefaultDeviceId,
        string DefaultThemeId);

    private sealed record ShotReferenceRow(
        string Id,
        string ProjectId,
        string OwnerActorId,
        string RenderPresetId);

    private sealed record ThemeReferenceRow(
        string Id,
        string ProjectId,
        string IconThemeId,
        string StatusBarId,
        string NavigationBarId);
}
