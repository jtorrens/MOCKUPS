using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SpikeDatabase
{
    private readonly string _connectionString;

    public SpikeDatabase(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();

        Initialize();
    }

    public static string DefaultDatabasePath()
    {
        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, "..", "..", "..", "data", "desktop-editor-spike.sqlite"));
    }

    public List<ProjectTreeNode> LoadProjectTree()
    {
        var rootNodes = LoadProjectHierarchy();
        rootNodes.AddRange(LoadDataNavigationGroups());
        return rootNodes;
    }

    private List<ProjectTreeNode> LoadProjectHierarchy()
    {
        using var connection = OpenConnection();
        var projects = QueryProjectRows(connection);
        var episodes = QueryEpisodeRows(connection);
        var shots = QueryShotRows(connection);

        var projectNodes = projects
            .Select((project) => new ProjectTreeNode(
                ProjectTreeNodeKind.Project,
                project.Id,
                project.Name,
                project.Notes))
            .ToDictionary((node) => node.Id);

        var episodeNodes = new Dictionary<string, ProjectTreeNode>();
        foreach (var episode in episodes.OrderBy((episode) => episode.SortOrder).ThenBy((episode) => episode.Name))
        {
            if (!projectNodes.TryGetValue(episode.ProjectId, out var project)) continue;

            var node = new ProjectTreeNode(
                ProjectTreeNodeKind.Episode,
                episode.Id,
                episode.Name,
                episode.Notes,
                project);
            project.AddChild(node);
            episodeNodes[node.Id] = node;
        }

        foreach (var shot in shots.OrderBy((shot) => shot.SortOrder).ThenBy((shot) => shot.Name))
        {
            if (!episodeNodes.TryGetValue(shot.EpisodeId, out var episode)) continue;

            episode.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.Shot,
                shot.Id,
                shot.Name,
                shot.Notes,
                episode));
        }

        return projectNodes.Values
            .OrderBy((node) => node.Name)
            .ToList();
    }

    private List<ProjectTreeNode> LoadDataNavigationGroups()
    {
        using var connection = OpenConnection();

        var productionData = new ProjectTreeNode(
            ProjectTreeNodeKind.NavigationGroup,
            "nav_production_data",
            "Production Data",
            "Editor navigation group. Not a persisted table.");
        foreach (var item in LoadNamedTableNodes(
            connection,
            productionData,
            [
                ("actors", "Actors", ProjectTreeNodeKind.Actor),
                ("devices", "Devices", ProjectTreeNodeKind.Device),
                ("apps", "Apps", ProjectTreeNodeKind.App),
                ("modules", "Modules", ProjectTreeNodeKind.Module),
                ("themes", "Themes", ProjectTreeNodeKind.Theme),
                ("palette_colors", "Palette", ProjectTreeNodeKind.PaletteColor),
                ("production_fonts", "Production Fonts", ProjectTreeNodeKind.ProductionFont),
                ("component_classes", "Component Classes", ProjectTreeNodeKind.ComponentClass),
            ]))
        {
            productionData.AddChild(item);
        }

        var systemData = new ProjectTreeNode(
            ProjectTreeNodeKind.NavigationGroup,
            "nav_system_data",
            "System Data",
            "Editor navigation group. Not a persisted table.");
        foreach (var item in LoadNamedTableNodes(
            connection,
            systemData,
            [
                ("icon_themes", "Icon Themes", ProjectTreeNodeKind.IconTheme),
                ("status_bars", "Status Bars", ProjectTreeNodeKind.StatusBar),
                ("navigation_bars", "Navigation Bars", ProjectTreeNodeKind.NavigationBar),
                ("render_presets", "Render Presets", ProjectTreeNodeKind.RenderPreset),
            ]))
        {
            systemData.AddChild(item);
        }

        return [productionData, systemData];
    }

    private static List<ProjectTreeNode> LoadNamedTableNodes(
        SqliteConnection connection,
        ProjectTreeNode group,
        IReadOnlyList<(string Table, string Label, ProjectTreeNodeKind Kind)> tables)
    {
        var nodes = new List<ProjectTreeNode>();
        foreach (var (table, label, kind) in tables)
        {
            var tableNode = new ProjectTreeNode(
                ProjectTreeNodeKind.TableGroup,
                $"table_{table}",
                label,
                $"{label} table.",
                group);

            foreach (var row in QueryNamedRows(connection, table))
            {
                tableNode.AddChild(new ProjectTreeNode(
                    kind,
                    row.Id,
                    row.Name,
                    row.Notes,
                    tableNode));
            }

            nodes.Add(tableNode);
        }

        return nodes;
    }

    public ProjectTreeNode AddChild(ProjectTreeNode parent)
    {
        using var connection = OpenConnection();

        if (parent.Kind == ProjectTreeNodeKind.Project)
        {
            var index = NextSortOrder(connection, "episodes", "project_id", parent.Id);
            var id = $"episode_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO episodes (id, project_id, name, notes, sort_order)
                VALUES ($id, $projectId, $name, $notes, $sortOrder)
                """,
                ("$id", id),
                ("$projectId", parent.Id),
                ("$name", $"Episode {index + 1}"),
                ("$notes", "New episode created in the desktop shell spike."),
                ("$sortOrder", index));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Episode,
                id,
                $"Episode {index + 1}",
                "New episode created in the desktop shell spike.",
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.Episode)
        {
            var index = NextSortOrder(connection, "shots", "episode_id", parent.Id);
            var id = $"shot_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO shots (id, episode_id, name, notes, sort_order, fps, duration_frames)
                VALUES ($id, $episodeId, $name, $notes, $sortOrder, 25, 240)
                """,
                ("$id", id),
                ("$episodeId", parent.Id),
                ("$name", $"Shot {index + 1:00}"),
                ("$notes", "New shot created in the desktop shell spike."),
                ("$sortOrder", index));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Shot,
                id,
                $"Shot {index + 1:00}",
                "New shot created in the desktop shell spike.",
                parent);
        }

        throw new InvalidOperationException($"Cannot add a child to {parent.Kind}.");
    }

    public ProjectTreeNode Duplicate(ProjectTreeNode node)
    {
        using var connection = OpenConnection();

        if (node.Kind == ProjectTreeNodeKind.Episode)
        {
            var id = $"episode_{Guid.NewGuid():N}";
            var sortOrder = NextSortOrder(connection, "episodes", "project_id", node.Parent!.Id);
            Execute(
                connection,
                """
                INSERT INTO episodes (id, project_id, name, notes, sort_order)
                SELECT $id, project_id, $name, notes, $sortOrder
                FROM episodes
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            DuplicateShots(connection, node.Id, id);

            return new ProjectTreeNode(ProjectTreeNodeKind.Episode, id, $"{node.Name} copy", node.Notes, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Shot)
        {
            var id = $"shot_{Guid.NewGuid():N}";
            var sortOrder = NextSortOrder(connection, "shots", "episode_id", node.Parent!.Id);
            Execute(
                connection,
                """
                INSERT INTO shots (id, episode_id, name, notes, sort_order, fps, duration_frames)
                SELECT $id, episode_id, $name, notes, $sortOrder, fps, duration_frames
                FROM shots
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.Shot, id, $"{node.Name} copy", node.Notes, node.Parent);
        }

        throw new InvalidOperationException($"Cannot duplicate {node.Kind}.");
    }

    public void Delete(ProjectTreeNode node)
    {
        using var connection = OpenConnection();
        var table = node.Kind switch
        {
            ProjectTreeNodeKind.Episode => "episodes",
            ProjectTreeNodeKind.Shot => "shots",
            _ => throw new InvalidOperationException($"Cannot delete {node.Kind}."),
        };

        Execute(connection, $"DELETE FROM {table} WHERE id = $id", ("$id", node.Id));
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        ExecuteScript(connection, SchemaSql);
        SeedIfEmpty(connection);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void SeedIfEmpty(SqliteConnection connection)
    {
        if (ScalarLong(connection, "SELECT COUNT(*) FROM projects") > 0) return;

        Execute(
            connection,
            "INSERT INTO projects (id, name, notes, media_root) VALUES ($id, $name, $notes, $mediaRoot)",
            ("$id", "project_foqn_s2"),
            ("$name", "FOQN S2"),
            ("$notes", "Spike project seeded from the current production structure."),
            ("$mediaRoot", "assets/FOQN_S2"));

        Execute(
            connection,
            "INSERT INTO episodes (id, project_id, name, notes, sort_order) VALUES ($id, $projectId, $name, $notes, $sortOrder)",
            ("$id", "episode_001"),
            ("$projectId", "project_foqn_s2"),
            ("$name", "Episode 1"),
            ("$notes", "First seeded episode."),
            ("$sortOrder", 0));

        Execute(
            connection,
            "INSERT INTO episodes (id, project_id, name, notes, sort_order) VALUES ($id, $projectId, $name, $notes, $sortOrder)",
            ("$id", "episode_002"),
            ("$projectId", "project_foqn_s2"),
            ("$name", "Episode 2"),
            ("$notes", "Second seeded episode."),
            ("$sortOrder", 1));

        SeedShot(connection, "shot_001", "episode_001", "Shot 01 · Opening chat", 0);
        SeedShot(connection, "shot_002", "episode_001", "Shot 02 · Incoming media", 1);
        SeedShot(connection, "shot_003", "episode_001", "Shot 03 · Audio reply", 2);
        SeedShot(connection, "shot_004", "episode_002", "Shot 01 · Lock to chat", 0);
        SeedShot(connection, "shot_005", "episode_002", "Shot 02 · Message escalation", 1);

        SeedTopLevelRows(connection);
    }

    private static void SeedShot(SqliteConnection connection, string id, string episodeId, string name, int sortOrder)
    {
        Execute(
            connection,
            """
            INSERT INTO shots (id, episode_id, name, notes, sort_order, fps, duration_frames)
            VALUES ($id, $episodeId, $name, $notes, $sortOrder, 25, 240)
            """,
            ("$id", id),
            ("$episodeId", episodeId),
            ("$name", name),
            ("$notes", "Seed shot. Screen/app/module levels are intentionally added later."),
            ("$sortOrder", sortOrder));
    }

    private static void SeedTopLevelRows(SqliteConnection connection)
    {
        InsertNamedRow(connection, "actors", "actor_alex", "Alex");
        InsertNamedRow(connection, "actors", "actor_sam", "Sam");
        InsertNamedRow(connection, "devices", "device_iphone_mock", "iPhone Mock");
        InsertNamedRow(connection, "apps", "app_core_chat", "Chat");
        InsertNamedRow(connection, "modules", "module_core_chat", "Chat Module", ("app_id", "app_core_chat"));
        InsertNamedRow(connection, "themes", "theme_ios_warm", "iOS Warm");
        InsertNamedRow(connection, "palette_colors", "palette_gray_000", "gray_000");
        InsertNamedRow(connection, "palette_colors", "palette_blue", "blue");
        InsertNamedRow(connection, "production_fonts", "font_sf_pro", "SF Pro");
        InsertNamedRow(connection, "component_classes", "component_avatar_default", "Default Avatar", ("component_type", "avatar"));
        InsertNamedRow(connection, "component_classes", "component_keyboard_default", "Default Keyboard", ("component_type", "keyboard"));
        InsertNamedRow(connection, "icon_themes", "icon_theme_lucide_basic", "Lucide Basic");
        InsertNamedRow(connection, "status_bars", "status_bar_ios_default", "iOS Default");
        InsertNamedRow(connection, "navigation_bars", "navigation_bar_android_default", "Android Default");
        InsertNamedRow(connection, "render_presets", "render_preset_preview", "Preview");
    }

    private static void InsertNamedRow(
        SqliteConnection connection,
        string table,
        string id,
        string name,
        params (string Column, object Value)[] extraColumns)
    {
        var columns = new List<string> { "id", "name" };
        var parameters = new List<string> { "$id", "$name" };
        var values = new List<(string Key, object? Value)> { ("$id", id), ("$name", name) };

        foreach (var (column, value) in extraColumns)
        {
            columns.Add(column);
            parameters.Add($"${column}");
            values.Add(($"${column}", value));
        }

        Execute(
            connection,
            $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})",
            values.ToArray());
    }

    private static void DuplicateShots(SqliteConnection connection, string sourceEpisodeId, string targetEpisodeId)
    {
        var sourceShots = QueryShotRows(connection)
            .Where((shot) => shot.EpisodeId == sourceEpisodeId)
            .OrderBy((shot) => shot.SortOrder)
            .ToList();

        for (var index = 0; index < sourceShots.Count; index++)
        {
            var shot = sourceShots[index];
            Execute(
                connection,
                """
                INSERT INTO shots (id, episode_id, name, notes, sort_order, fps, duration_frames)
                VALUES ($id, $episodeId, $name, $notes, $sortOrder, $fps, $durationFrames)
                """,
                ("$id", $"shot_{Guid.NewGuid():N}"),
                ("$episodeId", targetEpisodeId),
                ("$name", shot.Name),
                ("$notes", shot.Notes),
                ("$sortOrder", index),
                ("$fps", shot.Fps),
                ("$durationFrames", shot.DurationFrames));
        }
    }

    private static int NextSortOrder(SqliteConnection connection, string table, string parentColumn, string parentId)
    {
        return (int)ScalarLong(
            connection,
            $"SELECT COALESCE(MAX(sort_order), -1) + 1 FROM {table} WHERE {parentColumn} = $parentId",
            ("$parentId", parentId));
    }

    private static List<ProjectRow> QueryProjectRows(SqliteConnection connection)
    {
        var rows = new List<ProjectRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, notes FROM projects ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ProjectRow(reader.GetString(0), reader.GetString(1), ReadString(reader, 2)));
        }

        return rows;
    }

    private static List<EpisodeRow> QueryEpisodeRows(SqliteConnection connection)
    {
        var rows = new List<EpisodeRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, notes, sort_order FROM episodes ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new EpisodeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                reader.GetInt32(4)));
        }

        return rows;
    }

    private static List<ShotRow> QueryShotRows(SqliteConnection connection)
    {
        var rows = new List<ShotRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, episode_id, name, notes, sort_order, fps, duration_frames FROM shots ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ShotRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6)));
        }

        return rows;
    }

    private static List<NamedRow> QueryNamedRows(SqliteConnection connection, string table)
    {
        var rows = new List<NamedRow>();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id, name, metadata_json FROM {table} ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new NamedRow(
                reader.GetString(0),
                reader.GetString(1),
                ReadString(reader, 2)));
        }

        return rows;
    }

    private static string ReadString(SqliteDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? "" : reader.GetString(index);
    }

    private static void ExecuteScript(SqliteConnection connection, string script)
    {
        using var command = connection.CreateCommand();
        command.CommandText = script;
        command.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Key, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    private static long ScalarLong(SqliteConnection connection, string sql, params (string Key, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }

        return Convert.ToInt64(command.ExecuteScalar());
    }

    private const string SchemaSql =
        """
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS projects (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          media_root TEXT NOT NULL DEFAULT '',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS episodes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS shots (
          id TEXT PRIMARY KEY,
          episode_id TEXT NOT NULL REFERENCES episodes(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          fps INTEGER NOT NULL DEFAULT 25,
          duration_frames INTEGER NOT NULL DEFAULT 240,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS actors (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS devices (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS apps (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS modules (
          id TEXT PRIMARY KEY,
          app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS screen_instances (
          id TEXT PRIMARY KEY,
          shot_id TEXT NOT NULL REFERENCES shots(id) ON DELETE CASCADE,
          app_id TEXT NOT NULL REFERENCES apps(id),
          name TEXT NOT NULL,
          sort_order INTEGER NOT NULL DEFAULT 0,
          duration_frames INTEGER NOT NULL DEFAULT 240,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS module_instances (
          id TEXT PRIMARY KEY,
          screen_instance_id TEXT NOT NULL REFERENCES screen_instances(id) ON DELETE CASCADE,
          module_id TEXT NOT NULL REFERENCES modules(id),
          name TEXT NOT NULL,
          sort_order INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS themes (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS palette_colors (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          value_hex TEXT NOT NULL DEFAULT '#000000',
          is_neutral INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS production_fonts (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          family_name TEXT NOT NULL DEFAULT '',
          category TEXT NOT NULL DEFAULT 'text',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS component_classes (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          component_type TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS icon_themes (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          asset_root TEXT NOT NULL DEFAULT '',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS status_bars (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS navigation_bars (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS render_presets (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );
        """;

    private sealed record ProjectRow(string Id, string Name, string Notes);
    private sealed record EpisodeRow(string Id, string ProjectId, string Name, string Notes, int SortOrder);
    private sealed record NamedRow(string Id, string Name, string Notes);
    private sealed record ShotRow(
        string Id,
        string EpisodeId,
        string Name,
        string Notes,
        int SortOrder,
        int Fps,
        int DurationFrames);
}
