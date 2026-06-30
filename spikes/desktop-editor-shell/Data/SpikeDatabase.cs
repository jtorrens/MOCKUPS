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
        using var connection = OpenConnection();
        var projects = QueryProjectRows(connection);
        var episodes = QueryEpisodeRows(connection);
        var shots = QueryShotRows(connection);
        var apps = QueryAppRows(connection);
        var modules = QueryModuleRows(connection);

        var projectNodes = projects
            .Select((project) => new ProjectTreeNode(
                ProjectTreeNodeKind.Project,
                project.Id,
                project.Name,
                project.Notes))
            .ToDictionary((node) => node.Id);

        var appRootNodes = new Dictionary<string, ProjectTreeNode>();
        var episodeNodes = new Dictionary<string, ProjectTreeNode>();
        foreach (var project in projectNodes.Values)
        {
            var appsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.AppsRoot,
                $"apps_root_{project.Id}",
                "Apps",
                "Apps available in this project.",
                project);
            var episodesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.EpisodesRoot,
                $"episodes_root_{project.Id}",
                "Episodes",
                "Episodes and shots for this project.",
                project);

            project.AddChild(appsRoot);
            project.AddChild(episodesRoot);
            appRootNodes[project.Id] = appsRoot;
            episodeNodes[episodesRoot.Id] = episodesRoot;
        }

        var appNodes = new Dictionary<string, ProjectTreeNode>();
        foreach (var app in apps.OrderBy((app) => app.SortOrder).ThenBy((app) => app.Name))
        {
            if (!appRootNodes.TryGetValue(app.ProjectId, out var appsRoot)) continue;

            var node = new ProjectTreeNode(
                ProjectTreeNodeKind.App,
                app.Id,
                app.Name,
                app.Notes,
                appsRoot);
            appsRoot.AddChild(node);
            appNodes[node.Id] = node;
        }

        foreach (var module in modules.OrderBy((module) => module.SortOrder).ThenBy((module) => module.Name))
        {
            if (!appNodes.TryGetValue(module.AppId, out var app)) continue;

            app.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.Module,
                module.Id,
                module.Name,
                module.Notes,
                app));
        }

        foreach (var episode in episodes.OrderBy((episode) => episode.SortOrder).ThenBy((episode) => episode.Name))
        {
            if (!projectNodes.TryGetValue(episode.ProjectId, out var project)) continue;
            var episodesRoot = project.Children.FirstOrDefault((child) => child.Kind == ProjectTreeNodeKind.EpisodesRoot);
            if (episodesRoot is null) continue;

            var node = new ProjectTreeNode(
                ProjectTreeNodeKind.Episode,
                episode.Id,
                episode.Name,
                episode.Notes,
                episodesRoot);
            episodesRoot.AddChild(node);
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

    public ProjectTreeNode AddChild(ProjectTreeNode parent)
    {
        using var connection = OpenConnection();

        if (parent.Kind == ProjectTreeNodeKind.Project)
        {
            throw new InvalidOperationException("Project children are created through explicit Apps/Episodes roots.");
        }

        if (parent.Kind == ProjectTreeNodeKind.AppsRoot)
        {
            var project = parent.Parent ?? throw new InvalidOperationException("Apps root has no project parent.");
            var index = NextSortOrder(connection, "apps", "project_id", project.Id);
            var id = $"app_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO apps (id, project_id, name, notes, sort_order)
                VALUES ($id, $projectId, $name, $notes, $sortOrder)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$name", $"App {index + 1}"),
                ("$notes", "New app created in the desktop shell spike."),
                ("$sortOrder", index));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.App,
                id,
                $"App {index + 1}",
                "New app created in the desktop shell spike.",
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.App)
        {
            var index = NextSortOrder(connection, "modules", "app_id", parent.Id);
            var id = $"module_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO modules (id, app_id, name, notes, sort_order)
                VALUES ($id, $appId, $name, $notes, $sortOrder)
                """,
                ("$id", id),
                ("$appId", parent.Id),
                ("$name", $"Module {index + 1}"),
                ("$notes", "New module created in the desktop shell spike."),
                ("$sortOrder", index));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Module,
                id,
                $"Module {index + 1}",
                "New module created in the desktop shell spike.",
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.EpisodesRoot)
        {
            var project = parent.Parent ?? throw new InvalidOperationException("Episodes root has no project parent.");
            var index = NextSortOrder(connection, "episodes", "project_id", project.Id);
            var id = $"episode_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO episodes (id, project_id, name, notes, sort_order)
                VALUES ($id, $projectId, $name, $notes, $sortOrder)
                """,
                ("$id", id),
                ("$projectId", project.Id),
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
            var project = node.Parent?.Parent ?? throw new InvalidOperationException("Episode has no project parent.");
            var sortOrder = NextSortOrder(connection, "episodes", "project_id", project.Id);
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

        if (node.Kind == ProjectTreeNodeKind.App)
        {
            var id = $"app_{Guid.NewGuid():N}";
            var project = node.Parent?.Parent ?? throw new InvalidOperationException("App has no project parent.");
            var sortOrder = NextSortOrder(connection, "apps", "project_id", project.Id);
            Execute(
                connection,
                """
                INSERT INTO apps (id, project_id, name, notes, sort_order)
                SELECT $id, project_id, $name, notes, $sortOrder
                FROM apps
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            DuplicateModules(connection, node.Id, id);

            return new ProjectTreeNode(ProjectTreeNodeKind.App, id, $"{node.Name} copy", node.Notes, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Module)
        {
            var id = $"module_{Guid.NewGuid():N}";
            var sortOrder = NextSortOrder(connection, "modules", "app_id", node.Parent!.Id);
            Execute(
                connection,
                """
                INSERT INTO modules (id, app_id, name, notes, sort_order)
                SELECT $id, app_id, $name, notes, $sortOrder
                FROM modules
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.Module, id, $"{node.Name} copy", node.Notes, node.Parent);
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
            ProjectTreeNodeKind.App => "apps",
            ProjectTreeNodeKind.Module => "modules",
            _ => throw new InvalidOperationException($"Cannot delete {node.Kind}."),
        };

        Execute(connection, $"DELETE FROM {table} WHERE id = $id", ("$id", node.Id));
    }

    public void UpdateNode(ProjectTreeNode node)
    {
        using var connection = OpenConnection();
        var table = node.Kind switch
        {
            ProjectTreeNodeKind.Project => "projects",
            ProjectTreeNodeKind.App => "apps",
            ProjectTreeNodeKind.Module => "modules",
            ProjectTreeNodeKind.Episode => "episodes",
            ProjectTreeNodeKind.Shot => "shots",
            _ => "",
        };

        if (string.IsNullOrWhiteSpace(table)) return;

        Execute(
            connection,
            $"UPDATE {table} SET name = $name, notes = $notes WHERE id = $id",
            ("$id", node.Id),
            ("$name", node.Name),
            ("$notes", node.Notes));
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

        Execute(
            connection,
            """
            INSERT INTO apps (id, project_id, name, notes, sort_order)
            VALUES ($id, $projectId, $name, $notes, 0)
            """,
            ("$id", "app_core_chat"),
            ("$projectId", "project_foqn_s2"),
            ("$name", "Chat"),
            ("$notes", "Seed app. Modules hang below their app."));

        Execute(
            connection,
            """
            INSERT INTO modules (id, app_id, name, notes, sort_order)
            VALUES ($id, $appId, $name, $notes, 0)
            """,
            ("$id", "module_core_chat"),
            ("$appId", "app_core_chat"),
            ("$name", "Chat Module"),
            ("$notes", "Seed module linked to Chat app."));
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
            ("$notes", "Seed shot. Screen level is intentionally added later."),
            ("$sortOrder", sortOrder));
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

    private static void DuplicateModules(SqliteConnection connection, string sourceAppId, string targetAppId)
    {
        var sourceModules = QueryModuleRows(connection)
            .Where((module) => module.AppId == sourceAppId)
            .OrderBy((module) => module.SortOrder)
            .ToList();

        for (var index = 0; index < sourceModules.Count; index++)
        {
            var module = sourceModules[index];
            Execute(
                connection,
                """
                INSERT INTO modules (id, app_id, name, notes, sort_order)
                VALUES ($id, $appId, $name, $notes, $sortOrder)
                """,
                ("$id", $"module_{Guid.NewGuid():N}"),
                ("$appId", targetAppId),
                ("$name", module.Name),
                ("$notes", module.Notes),
                ("$sortOrder", index));
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

    private static List<AppRow> QueryAppRows(SqliteConnection connection)
    {
        var rows = new List<AppRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, notes, sort_order FROM apps ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new AppRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                reader.GetInt32(4)));
        }

        return rows;
    }

    private static List<ModuleRow> QueryModuleRows(SqliteConnection connection)
    {
        var rows = new List<ModuleRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, app_id, name, notes, sort_order FROM modules ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ModuleRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                reader.GetInt32(4)));
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

        CREATE TABLE IF NOT EXISTS apps (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS modules (
          id TEXT PRIMARY KEY,
          app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );
        """;

    private sealed record ProjectRow(string Id, string Name, string Notes);
    private sealed record EpisodeRow(string Id, string ProjectId, string Name, string Notes, int SortOrder);
    private sealed record AppRow(string Id, string ProjectId, string Name, string Notes, int SortOrder);
    private sealed record ModuleRow(string Id, string AppId, string Name, string Notes, int SortOrder);
    private sealed record ShotRow(
        string Id,
        string EpisodeId,
        string Name,
        string Notes,
        int SortOrder,
        int Fps,
        int DurationFrames);
}
