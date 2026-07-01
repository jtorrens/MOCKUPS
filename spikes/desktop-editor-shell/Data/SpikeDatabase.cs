using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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
        var paletteColors = QueryPaletteColorRows(connection);

        var projectNodes = projects
            .Select((project) => new ProjectTreeNode(
                ProjectTreeNodeKind.Project,
                project.Id,
                project.Name,
                project.Notes,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Project)))
            .ToDictionary((node) => node.Id);

        var appRootNodes = new Dictionary<string, ProjectTreeNode>();
        var paletteRootNodes = new Dictionary<string, ProjectTreeNode>();
        var episodeNodes = new Dictionary<string, ProjectTreeNode>();
        foreach (var project in projectNodes.Values)
        {
            var appsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.AppsRoot,
                $"apps_root_{project.Id}",
                "Apps",
                "Apps available in this project.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.AppsRoot),
                project);
            var paletteRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.PaletteRoot,
                $"palette_root_{project.Id}",
                "Palette Colors",
                "Project primitive color tokens.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.PaletteRoot),
                project);
            var episodesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.EpisodesRoot,
                $"episodes_root_{project.Id}",
                "Episodes",
                "Episodes and shots for this project.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.EpisodesRoot),
                project);

            project.AddChild(appsRoot);
            project.AddChild(paletteRoot);
            project.AddChild(episodesRoot);
            appRootNodes[project.Id] = appsRoot;
            paletteRootNodes[project.Id] = paletteRoot;
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
                app.RecordClassId,
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
                module.RecordClassId,
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
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Episode),
                episodesRoot);
            episodesRoot.AddChild(node);
            episodeNodes[node.Id] = node;
        }

        foreach (var color in paletteColors.OrderBy((color) => color.Token))
        {
            if (!paletteRootNodes.TryGetValue(color.ProjectId, out var paletteRoot)) continue;

            paletteRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.PaletteColor,
                color.Id,
                color.Token,
                color.Note,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.PaletteColor),
                paletteRoot,
                color.ValueHex,
                IsPaletteColorUsed(connection, color.ProjectId, color.Token, color.Id)));
        }

        foreach (var shot in shots.OrderBy((shot) => shot.SortOrder).ThenBy((shot) => shot.Name))
        {
            if (!episodeNodes.TryGetValue(shot.EpisodeId, out var episode)) continue;

            episode.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.Shot,
                shot.Id,
                shot.Name,
                shot.Notes,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot),
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
                INSERT INTO apps (id, project_id, record_class_id, name, notes, sort_order)
                VALUES ($id, $projectId, $recordClassId, $name, $notes, $sortOrder)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$recordClassId", "app.generic"),
                ("$name", $"App {index + 1}"),
                ("$notes", "New app created in the desktop shell spike."),
                ("$sortOrder", index));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.App,
                id,
                $"App {index + 1}",
                "New app created in the desktop shell spike.",
                "app.generic",
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.PaletteRoot)
        {
            var project = parent.Parent ?? throw new InvalidOperationException("Palette root has no project parent.");
            var id = $"palette_{Guid.NewGuid():N}";
            var token = $"color_{ScalarLong(connection, "SELECT COUNT(*) FROM palette_colors WHERE project_id = $projectId", ("$projectId", project.Id)) + 1}";
            Execute(
                connection,
                """
                INSERT INTO palette_colors (id, project_id, token, value_hex, metadata_json, is_neutral)
                VALUES ($id, $projectId, $token, '#808080', $metadataJson, 1)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$token", token),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Project palette primitive color." })));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.PaletteColor,
                id,
                token,
                "Project palette primitive color.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.PaletteColor),
                parent,
                "#808080",
                false);
        }

        if (parent.Kind == ProjectTreeNodeKind.App)
        {
            var index = NextSortOrder(connection, "modules", "app_id", parent.Id);
            var id = $"module_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order)
                VALUES ($id, $appId, $recordClassId, $name, $notes, $sortOrder)
                """,
                ("$id", id),
                ("$appId", parent.Id),
                ("$recordClassId", "module.generic"),
                ("$name", $"Module {index + 1}"),
                ("$notes", "New module created in the desktop shell spike."),
                ("$sortOrder", index));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Module,
                id,
                $"Module {index + 1}",
                "New module created in the desktop shell spike.",
                "module.generic",
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
            Execute(
                connection,
                "UPDATE episodes SET slug = $slug WHERE id = $id",
                ("$id", id),
                ("$slug", $"episode-{index + 1}"));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Episode,
                id,
                $"Episode {index + 1}",
                "New episode created in the desktop shell spike.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Episode),
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
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot),
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
                INSERT INTO episodes (id, project_id, name, slug, notes, sort_order)
                SELECT $id, project_id, $name, slug || '-copy', notes, $sortOrder
                FROM episodes
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            DuplicateShots(connection, node.Id, id);

            return new ProjectTreeNode(ProjectTreeNodeKind.Episode, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
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

            return new ProjectTreeNode(ProjectTreeNodeKind.Shot, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.App)
        {
            var id = $"app_{Guid.NewGuid():N}";
            var project = node.Parent?.Parent ?? throw new InvalidOperationException("App has no project parent.");
            var sortOrder = NextSortOrder(connection, "apps", "project_id", project.Id);
            Execute(
                connection,
                """
                INSERT INTO apps (id, project_id, record_class_id, name, notes, sort_order)
                SELECT $id, project_id, record_class_id, $name, notes, $sortOrder
                FROM apps
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            DuplicateModules(connection, node.Id, id);

            return new ProjectTreeNode(ProjectTreeNodeKind.App, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            var id = $"palette_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO palette_colors (id, project_id, token, value_hex, metadata_json, is_neutral)
                SELECT $id, project_id, token || '_copy', value_hex, metadata_json, is_neutral
                FROM palette_colors
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.PaletteColor,
                id,
                $"{node.Name}_copy",
                node.Notes,
                node.RecordClassId,
                node.Parent,
                node.ColorHex,
                false);
        }

        if (node.Kind == ProjectTreeNodeKind.Module)
        {
            var id = $"module_{Guid.NewGuid():N}";
            var sortOrder = NextSortOrder(connection, "modules", "app_id", node.Parent!.Id);
            Execute(
                connection,
                """
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order)
                SELECT $id, app_id, record_class_id, $name, notes, $sortOrder
                FROM modules
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.Module, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
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
            ProjectTreeNodeKind.PaletteColor => "palette_colors",
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
            ProjectTreeNodeKind.PaletteColor => "palette_colors",
            _ => "",
        };

        if (string.IsNullOrWhiteSpace(table)) return;

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            Execute(connection, "UPDATE palette_colors SET token = $token WHERE id = $id", ("$id", node.Id), ("$token", node.Name));
            UpdatePaletteMetadata(connection, node.Id, "note", node.Notes);
            return;
        }

        Execute(
            connection,
            $"UPDATE {table} SET name = $name, notes = $notes WHERE id = $id",
            ("$id", node.Id),
            ("$name", node.Name),
            ("$notes", node.Notes));
    }

    public EditorLayout LoadEditorLayout(string recordClassId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT layout_json FROM editor_layouts WHERE record_class_id = $recordClassId";
        command.Parameters.AddWithValue("$recordClassId", recordClassId);
        var json = command.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing editor layout for record class '{recordClassId}'.");

        return JsonSerializer.Deserialize<EditorLayout>(json)
            ?? throw new InvalidOperationException($"Invalid editor layout JSON for record class '{recordClassId}'.");
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        ExecuteScript(connection, SchemaSql);
        EnsureProjectColumns(connection);
        EnsureEpisodeColumns(connection);
        SeedEditorLayouts(connection);
        SeedIfEmpty(connection);
        SeedPaletteColorsIfEmpty(connection);
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
            "UPDATE projects SET slug = $slug, default_fps = $defaultFps WHERE id = $id",
            ("$id", "project_foqn_s2"),
            ("$slug", "foqn_s2"),
            ("$defaultFps", 25));

        Execute(
            connection,
            "INSERT INTO episodes (id, project_id, name, slug, notes, sort_order) VALUES ($id, $projectId, $name, $slug, $notes, $sortOrder)",
            ("$id", "episode_001"),
            ("$projectId", "project_foqn_s2"),
            ("$name", "Episode 1"),
            ("$slug", "episode-1"),
            ("$notes", "First seeded episode."),
            ("$sortOrder", 0));

        Execute(
            connection,
            "INSERT INTO episodes (id, project_id, name, slug, notes, sort_order) VALUES ($id, $projectId, $name, $slug, $notes, $sortOrder)",
            ("$id", "episode_002"),
            ("$projectId", "project_foqn_s2"),
            ("$name", "Episode 2"),
            ("$slug", "episode-2"),
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
            INSERT INTO apps (id, project_id, record_class_id, name, notes, sort_order)
            VALUES ($id, $projectId, $recordClassId, $name, $notes, 0)
            """,
            ("$id", "app_core_chat"),
            ("$projectId", "project_foqn_s2"),
            ("$recordClassId", "app.core.chat"),
            ("$name", "Chat"),
            ("$notes", "Seed app. Modules hang below their app."));

        Execute(
            connection,
            """
            INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order)
            VALUES ($id, $appId, $recordClassId, $name, $notes, 0)
            """,
            ("$id", "module_core_chat"),
            ("$appId", "app_core_chat"),
            ("$recordClassId", "module.core.chat"),
            ("$name", "Chat Module"),
            ("$notes", "Seed module linked to Chat app."));
    }

    private static void SeedPaletteColorsIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM palette_colors WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            foreach (var seed in PaletteSeedRows)
            {
                Execute(
                    connection,
                    """
                    INSERT INTO palette_colors (id, project_id, token, value_hex, metadata_json, is_neutral)
                    VALUES ($id, $projectId, $token, $valueHex, $metadataJson, $isNeutral)
                    """,
                    ("$id", $"palette_{projectId}_{seed.Token}"),
                    ("$projectId", projectId),
                    ("$token", seed.Token),
                    ("$valueHex", seed.ValueHex),
                    ("$metadataJson", seed.MetadataJson),
                    ("$isNeutral", seed.IsNeutral ? 1 : 0));
            }
        }
    }

    private static void SeedEditorLayouts(SqliteConnection connection)
    {
        foreach (var recordClassId in new[]
        {
            "project",
            "navigation.apps",
            "navigation.palette",
            "navigation.episodes",
            "app.generic",
            "app.core.chat",
            "module.generic",
            "module.core.chat",
            "episode",
            "shot",
            "palette_color",
        })
        {
            Execute(
                connection,
                """
                INSERT OR REPLACE INTO editor_layouts (record_class_id, layout_json)
                VALUES ($recordClassId, $layoutJson)
                """,
                ("$recordClassId", recordClassId),
                ("$layoutJson", MinimalEditorLayoutJson(recordClassId)));
        }
    }

    private static string MinimalEditorLayoutJson(string recordClassId)
    {
        var generalFields = recordClassId == "project"
            ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "project.slug", "order": 20, "visible": true },
                    { "id": "project.defaultFps", "order": 30, "visible": true },
                    { "id": "project.mediaRoot", "order": 40, "visible": true },
                    { "id": "core.kind", "order": 50, "visible": false },
                    { "id": "core.notes", "order": 60, "visible": true }
              """
            : recordClassId == "episode"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "episode.slug", "order": 20, "visible": true },
                    { "id": "episode.sortOrder", "order": 30, "visible": true },
                    { "id": "core.kind", "order": 40, "visible": false },
                    { "id": "core.notes", "order": 50, "visible": true }
                  """
            : recordClassId == "palette_color"
                ? """
                    { "id": "palette.token", "order": 10, "visible": true },
                    { "id": "palette.valueHex", "order": 20, "visible": true },
                    { "id": "palette.isNeutral", "order": 30, "visible": true },
                    { "id": "palette.source", "order": 40, "visible": true },
                    { "id": "palette.protected", "order": 50, "visible": true },
                    { "id": "palette.hiddenFromPickers", "order": 60, "visible": true },
                    { "id": "palette.note", "order": 70, "visible": true }
                  """
            : """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "core.kind", "order": 20, "visible": false },
                    { "id": "core.notes", "order": 30, "visible": true }
              """;

        return $$"""
        {
          "cards": [
            {
              "id": "general",
              "label": "General",
              "icon": "{{EditorIcons.General}}",
              "order": 10,
              "visible": true,
              "defaultOpen": true,
              "groups": [
                {
                  "id": "identity",
                  "label": "",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    {{generalFields}}
                  ]
                }
              ]
            }
          ]
        }
        """;
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
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order)
                VALUES ($id, $appId, $recordClassId, $name, $notes, $sortOrder)
                """,
                ("$id", $"module_{Guid.NewGuid():N}"),
                ("$appId", targetAppId),
                ("$recordClassId", module.RecordClassId),
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

    private static List<PaletteColorRow> QueryPaletteColorRows(SqliteConnection connection)
    {
        var rows = new List<PaletteColorRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, token, value_hex, metadata_json, is_neutral FROM palette_colors ORDER BY token";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var metadataJson = ReadString(reader, 4);
            rows.Add(new PaletteColorRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                MetadataString(metadataJson, "note"),
                reader.GetInt32(5) != 0,
                metadataJson));
        }

        return rows;
    }

    public PaletteColorSettings GetPaletteColorSettings(string colorId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT token, value_hex, metadata_json, is_neutral FROM palette_colors WHERE id = $id";
        command.Parameters.AddWithValue("$id", colorId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing palette color '{colorId}'.");
        }

        var metadataJson = ReadString(reader, 2);
        return new PaletteColorSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(3) != 0,
            MetadataString(metadataJson, "source"),
            MetadataBool(metadataJson, "protected"),
            MetadataBool(metadataJson, "hiddenFromPickers"),
            MetadataString(metadataJson, "note"));
    }

    public void UpdatePaletteColorField(string colorId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "palette.token":
                Execute(connection, "UPDATE palette_colors SET token = $value WHERE id = $id", ("$id", colorId), ("$value", value));
                return;
            case "palette.valueHex":
                Execute(connection, "UPDATE palette_colors SET value_hex = $value WHERE id = $id", ("$id", colorId), ("$value", NormalizeHex(value)));
                return;
            case "palette.isNeutral":
                Execute(connection, "UPDATE palette_colors SET is_neutral = $value WHERE id = $id", ("$id", colorId), ("$value", StringToBool(value) ? 1 : 0));
                return;
            case "palette.source":
                UpdatePaletteMetadata(connection, colorId, "source", value);
                return;
            case "palette.protected":
                UpdatePaletteMetadata(connection, colorId, "protected", StringToBool(value));
                return;
            case "palette.hiddenFromPickers":
                UpdatePaletteMetadata(connection, colorId, "hiddenFromPickers", StringToBool(value));
                return;
            case "palette.note":
                UpdatePaletteMetadata(connection, colorId, "note", value);
                return;
            default:
                throw new InvalidOperationException($"Unknown palette field '{fieldId}'.");
        }
    }

    public ProjectSettings GetProjectSettings(string projectId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT slug, default_fps, media_root FROM projects WHERE id = $id";
        command.Parameters.AddWithValue("$id", projectId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing project '{projectId}'.");
        }

        return new ProjectSettings(
            ReadString(reader, 0),
            reader.IsDBNull(1) ? 25 : reader.GetInt32(1),
            ReadString(reader, 2));
    }

    public void UpdateProjectField(string projectId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var column = fieldId switch
        {
            "project.slug" => "slug",
            "project.defaultFps" => "default_fps",
            "project.mediaRoot" => "media_root",
            _ => throw new InvalidOperationException($"Unknown project field '{fieldId}'."),
        };

        Execute(
            connection,
            $"UPDATE projects SET {column} = $value WHERE id = $id",
            ("$id", projectId),
            ("$value", fieldId == "project.defaultFps" && int.TryParse(value, out var fps) ? fps : value));
    }

    public EpisodeSettings GetEpisodeSettings(string episodeId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT slug, sort_order FROM episodes WHERE id = $id";
        command.Parameters.AddWithValue("$id", episodeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing episode '{episodeId}'.");
        }

        return new EpisodeSettings(
            ReadString(reader, 0),
            reader.IsDBNull(1) ? 0 : reader.GetInt32(1));
    }

    public void UpdateEpisodeField(string episodeId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var column = fieldId switch
        {
            "episode.slug" => "slug",
            "episode.sortOrder" => "sort_order",
            _ => throw new InvalidOperationException($"Unknown episode field '{fieldId}'."),
        };

        Execute(
            connection,
            $"UPDATE episodes SET {column} = $value WHERE id = $id",
            ("$id", episodeId),
            ("$value", fieldId == "episode.sortOrder" && int.TryParse(value, out var sortOrder) ? sortOrder : value));
    }

    private static List<EpisodeRow> QueryEpisodeRows(SqliteConnection connection)
    {
        var rows = new List<EpisodeRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, slug, notes, sort_order FROM episodes ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new EpisodeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                reader.GetInt32(5)));
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
        command.CommandText = "SELECT id, project_id, record_class_id, name, notes, sort_order FROM apps ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new AppRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ReadString(reader, 4),
                reader.GetInt32(5)));
        }

        return rows;
    }

    private static List<ModuleRow> QueryModuleRows(SqliteConnection connection)
    {
        var rows = new List<ModuleRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, app_id, record_class_id, name, notes, sort_order FROM modules ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ModuleRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ReadString(reader, 4),
                reader.GetInt32(5)));
        }

        return rows;
    }

    private static string ReadString(SqliteDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? "" : reader.GetString(index);
    }

    private static bool IsPaletteColorUsed(SqliteConnection connection, string projectId, string token, string colorId)
    {
        // The new shell does not yet reference palette tokens from theme/component tables.
        // The flag is still part of the model now, so the navigation can show the correct
        // used/unused marker as soon as those references land.
        _ = connection;
        _ = projectId;
        _ = token;
        _ = colorId;
        return false;
    }

    private static string MetadataString(string metadataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return "";

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static bool MetadataBool(string metadataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return false;

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (!document.RootElement.TryGetProperty(key, out var value))
            {
                return false;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => StringToBool(value.GetString() ?? ""),
                JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
                _ => false,
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void UpdatePaletteMetadata(SqliteConnection connection, string colorId, string key, object value)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT metadata_json FROM palette_colors WHERE id = $id";
        select.Parameters.AddWithValue("$id", colorId);
        var metadataJson = select.ExecuteScalar() as string ?? "{}";

        var metadata = new Dictionary<string, object?>();
        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                metadata[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when property.Value.TryGetInt64(out var number) => number,
                    JsonValueKind.String => property.Value.GetString(),
                    _ => property.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
            // Invalid metadata is replaced by the field being edited. This is project data,
            // but the table is internal to the spike and metadata must remain valid JSON.
        }

        metadata[key] = value;
        Execute(
            connection,
            "UPDATE palette_colors SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", colorId),
            ("$metadataJson", JsonSerializer.Serialize(metadata)));
    }

    private static string NormalizeHex(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 6 && !trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            trimmed = $"#{trimmed}";
        }

        return trimmed;
    }

    private static bool StringToBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static void EnsureProjectColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "projects", "slug", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "projects", "default_fps", "INTEGER NOT NULL DEFAULT 25");
    }

    private static void EnsureEpisodeColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "episodes", "slug", "TEXT NOT NULL DEFAULT ''");
        Execute(
            connection,
            """
            UPDATE episodes
            SET slug = lower(replace(trim(name), ' ', '-'))
            WHERE trim(slug) = ''
            """);
    }

    private static void AddColumnIfMissing(
        SqliteConnection connection,
        string table,
        string column,
        string definition)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        Execute(connection, $"ALTER TABLE {table} ADD COLUMN {column} {definition}");
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
          slug TEXT NOT NULL DEFAULT '',
          default_fps INTEGER NOT NULL DEFAULT 25,
          notes TEXT NOT NULL DEFAULT '',
          media_root TEXT NOT NULL DEFAULT '',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS episodes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          slug TEXT NOT NULL DEFAULT '',
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
          record_class_id TEXT NOT NULL,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS modules (
          id TEXT PRIMARY KEY,
          app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
          record_class_id TEXT NOT NULL,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS palette_colors (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          token TEXT NOT NULL,
          value_hex TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}',
          is_neutral INTEGER NOT NULL DEFAULT 0,
          UNIQUE(project_id, token)
        );

        CREATE TABLE IF NOT EXISTS editor_layouts (
          record_class_id TEXT PRIMARY KEY,
          layout_json TEXT NOT NULL
        );
        """;

    public sealed record ProjectSettings(string Slug, int DefaultFps, string MediaRoot);
    public sealed record EpisodeSettings(string Slug, int SortOrder);
    public sealed record PaletteColorSettings(
        string Token,
        string ValueHex,
        bool IsNeutral,
        string Source,
        bool IsProtected,
        bool HiddenFromPickers,
        string Note);
    private sealed record ProjectRow(string Id, string Name, string Notes);
    private sealed record EpisodeRow(string Id, string ProjectId, string Name, string Slug, string Notes, int SortOrder);
    private sealed record AppRow(string Id, string ProjectId, string RecordClassId, string Name, string Notes, int SortOrder);
    private sealed record ModuleRow(string Id, string AppId, string RecordClassId, string Name, string Notes, int SortOrder);
    private sealed record PaletteColorRow(string Id, string ProjectId, string Token, string ValueHex, string Note, bool IsNeutral, string MetadataJson);
    private sealed record ShotRow(
        string Id,
        string EpisodeId,
        string Name,
        string Notes,
        int SortOrder,
        int Fps,
        int DurationFrames);

    private sealed record PaletteSeedRow(string Token, string ValueHex, bool IsNeutral, string MetadataJson);

    private static readonly PaletteSeedRow[] PaletteSeedRows =
    [
        new("aqua_green", "#79D2B0", false, """{"note":"Production palette primitive color."}"""),
        new("blue", "#007AFF", false, """{"source":"ios_seed_theme","note":"Primitive color seeded from the original iOS theme values."}"""),
        new("blue_bright", "#0A84FF", false, """{"source":"ios_seed_theme","note":"Primitive color seeded from the original iOS theme values."}"""),
        new("debug_red", "#FF00FF", false, """{"source":"debug_sentinel","protected":true,"hiddenFromPickers":true,"note":"Protected sentinel color for unresolved theme/component color decisions."}"""),
        new("gray_000", "#000000", true, """{"note":"Neutral palette color."}"""),
        new("gray_010", "#1A1A1A", true, """{"note":"Neutral palette color."}"""),
        new("gray_020", "#333333", true, """{"note":"Neutral palette color."}"""),
        new("gray_030", "#4D4D4D", true, """{"note":"Neutral palette color."}"""),
        new("gray_040", "#666666", true, """{"note":"Neutral palette color."}"""),
        new("gray_050", "#808080", true, """{"note":"Neutral palette color."}"""),
        new("gray_060", "#999999", true, """{"note":"Neutral palette color."}"""),
        new("gray_070", "#B3B3B3", true, """{"note":"Neutral palette color."}"""),
        new("gray_080", "#CCCCCC", true, """{"note":"Neutral palette color."}"""),
        new("gray_090", "#E6E6E6", true, """{"note":"Neutral palette color."}"""),
        new("gray_100", "#FFFFFF", true, """{"note":"Neutral palette color."}"""),
        new("pastel_coral", "#FF8A80", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_lavender", "#B39DDB", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_mint", "#66D9A3", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_orange", "#FFB74D", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_sky", "#64B5F6", false, """{"note":"Actor differentiator palette color."}"""),
        new("pastel_yellow", "#FFF176", false, """{"note":"Actor differentiator palette color."}"""),
        new("purple", "#6750A4", false, """{"source":"android_seed_theme","note":"Primitive color seeded from the Android theme values."}"""),
        new("purple_tint", "#D0BCFF", false, """{"source":"android_seed_theme","note":"Primitive color seeded from the Android theme values."}"""),
        new("red", "#DF2020", false, """{"note":"Production palette primitive color."}"""),
    ];
}
