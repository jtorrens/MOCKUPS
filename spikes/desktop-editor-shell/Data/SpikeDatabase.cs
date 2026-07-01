using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        var devices = QueryDeviceRows(connection);
        var actors = QueryActorRows(connection);

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
        var deviceRootNodes = new Dictionary<string, ProjectTreeNode>();
        var actorRootNodes = new Dictionary<string, ProjectTreeNode>();
        var episodeRootNodes = new Dictionary<string, ProjectTreeNode>();
        var episodeNodes = new Dictionary<string, ProjectTreeNode>();
        foreach (var project in projectNodes.Values)
        {
            var productionDataRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.ProductionDataRoot,
                $"production_data_root_{project.Id}",
                "Production Data",
                "Project-specific production records.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ProductionDataRoot),
                project);
            var systemDataRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.SystemDataRoot,
                $"system_data_root_{project.Id}",
                "System Data",
                "Shared system resources for this project.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.SystemDataRoot),
                project);
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
                systemDataRoot);
            var devicesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.DevicesRoot,
                $"devices_root_{project.Id}",
                "Devices",
                "Device metrics available in this project.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.DevicesRoot),
                productionDataRoot);
            var actorsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.ActorsRoot,
                $"actors_root_{project.Id}",
                "Actors",
                "People and identities used by the production.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ActorsRoot),
                productionDataRoot);
            var episodesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.EpisodesRoot,
                $"episodes_root_{project.Id}",
                "Episodes",
                "Episodes and shots for this project.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.EpisodesRoot),
                project);

            productionDataRoot.AddChild(actorsRoot);
            productionDataRoot.AddChild(devicesRoot);
            systemDataRoot.AddChild(paletteRoot);
            project.AddChild(appsRoot);
            project.AddChild(episodesRoot);
            project.AddChild(productionDataRoot);
            project.AddChild(systemDataRoot);
            appRootNodes[project.Id] = appsRoot;
            paletteRootNodes[project.Id] = paletteRoot;
            deviceRootNodes[project.Id] = devicesRoot;
            actorRootNodes[project.Id] = actorsRoot;
            episodeRootNodes[project.Id] = episodesRoot;
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
            if (!episodeRootNodes.TryGetValue(episode.ProjectId, out var episodesRoot)) continue;

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

        foreach (var device in devices.OrderBy((device) => device.Name))
        {
            if (!deviceRootNodes.TryGetValue(device.ProjectId, out var devicesRoot)) continue;

            devicesRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.Device,
                device.Id,
                device.Name,
                $"{device.Manufacturer} {device.Model}".Trim(),
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Device),
                devicesRoot));
        }

        foreach (var actor in actors.OrderBy((actor) => actor.DisplayName))
        {
            if (!actorRootNodes.TryGetValue(actor.ProjectId, out var actorsRoot)) continue;

            actorsRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.Actor,
                actor.Id,
                actor.DisplayName,
                actor.ShortName,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Actor),
                actorsRoot));
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
            var project = ProjectAncestor(parent);
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
            var project = ProjectAncestor(parent);
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

        if (parent.Kind == ProjectTreeNodeKind.DevicesRoot)
        {
            var project = ProjectAncestor(parent);
            var index = ScalarLong(connection, "SELECT COUNT(*) FROM devices WHERE project_id = $projectId", ("$projectId", project.Id)) + 1;
            var id = $"device_{Guid.NewGuid():N}";
            var metricsJson = DefaultDeviceMetricsJson(1170, 2532, 3);
            Execute(
                connection,
                """
                INSERT INTO devices (id, project_id, name, manufacturer, model, os_family, metrics_json)
                VALUES ($id, $projectId, $name, $manufacturer, $model, $osFamily, $metricsJson)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$name", $"Device {index}"),
                ("$manufacturer", ""),
                ("$model", ""),
                ("$osFamily", "ios"),
                ("$metricsJson", metricsJson));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Device,
                id,
                $"Device {index}",
                "",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Device),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.ActorsRoot)
        {
            var project = ProjectAncestor(parent);
            var index = ScalarLong(connection, "SELECT COUNT(*) FROM actors WHERE project_id = $projectId", ("$projectId", project.Id)) + 1;
            var id = $"actor_{Guid.NewGuid():N}";
            var displayName = $"Actor {index}";
            Execute(
                connection,
                """
                INSERT INTO actors (id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json)
                VALUES ($id, $projectId, $displayName, $shortName, '', '', $metadataJson)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$displayName", displayName),
                ("$shortName", $"A{index}"),
                ("$metadataJson", DefaultActorMetadataJson("blue", "gray_010")));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Actor,
                id,
                displayName,
                $"A{index}",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Actor),
                parent);
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
            var project = ProjectAncestor(parent);
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

        if (node.Kind == ProjectTreeNodeKind.Device)
        {
            var id = $"device_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO devices (id, project_id, name, manufacturer, model, os_family, metrics_json)
                SELECT $id, project_id, $name, manufacturer, model, os_family, metrics_json
                FROM devices
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.Device, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            var id = $"actor_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO actors (id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json)
                SELECT $id, project_id, $name, short_name, default_device_id, default_theme_id, metadata_json
                FROM actors
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.Actor, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
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
            ProjectTreeNodeKind.Device => "devices",
            ProjectTreeNodeKind.Actor => "actors",
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
            ProjectTreeNodeKind.Device => "devices",
            ProjectTreeNodeKind.Actor => "actors",
            _ => "",
        };

        if (string.IsNullOrWhiteSpace(table)) return;

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            Execute(connection, "UPDATE palette_colors SET token = $token WHERE id = $id", ("$id", node.Id), ("$token", node.Name));
            UpdatePaletteMetadata(connection, node.Id, "note", node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Device)
        {
            Execute(
                connection,
                "UPDATE devices SET name = $name WHERE id = $id",
                ("$id", node.Id),
                ("$name", node.Name));
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            Execute(
                connection,
                "UPDATE actors SET display_name = $name WHERE id = $id",
                ("$id", node.Id),
                ("$name", node.Name));
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
        SeedDevicesIfEmpty(connection);
        SeedActorsIfEmpty(connection);
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

    private static void SeedDevicesIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM devices WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            foreach (var seed in DeviceSeedRows)
            {
                Execute(
                    connection,
                    """
                    INSERT INTO devices (id, project_id, name, manufacturer, model, os_family, metrics_json)
                    VALUES ($id, $projectId, $name, $manufacturer, $model, $osFamily, $metricsJson)
                    """,
                    ("$id", seed.Id),
                    ("$projectId", projectId),
                    ("$name", seed.Name),
                    ("$manufacturer", seed.Manufacturer),
                    ("$model", seed.Model),
                    ("$osFamily", seed.OsFamily),
                    ("$metricsJson", seed.MetricsJson));
            }
        }
    }

    private static void SeedActorsIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM actors WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            foreach (var seed in ActorSeedRows)
            {
                Execute(
                    connection,
                    """
                    INSERT INTO actors (id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json)
                    VALUES ($id, $projectId, $displayName, $shortName, '', '', $metadataJson)
                    """,
                    ("$id", seed.Id),
                    ("$projectId", projectId),
                    ("$displayName", seed.DisplayName),
                    ("$shortName", seed.ShortName),
                    ("$metadataJson", seed.MetadataJson));
            }
        }
    }

    private static void SeedEditorLayouts(SqliteConnection connection)
    {
        foreach (var recordClassId in new[]
        {
            "project",
            "navigation.production_data",
            "navigation.system_data",
            "navigation.apps",
            "navigation.palette",
            "navigation.devices",
            "navigation.actors",
            "navigation.episodes",
            "app.generic",
            "app.core.chat",
            "module.generic",
            "module.core.chat",
            "episode",
            "shot",
            "palette_color",
            "device",
            "actor",
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

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        }

        return current;
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
            : recordClassId == "device"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "device.manufacturer", "order": 20, "visible": true },
                    { "id": "device.model", "order": 30, "visible": true },
                    { "id": "device.osFamily", "order": 40, "visible": true },
                    { "id": "device.metrics.designSpace.size", "order": 110, "visible": true },
                    { "id": "device.metrics.renderSize", "order": 120, "visible": true },
                    { "id": "device.metrics.scaleToPixels", "order": 130, "visible": true },
                    { "id": "device.metrics.pixelRatio", "order": 140, "visible": true },
                    { "id": "device.metrics.defaultScreenScale", "order": 150, "visible": true },
                    { "id": "device.metrics.canvas.size", "order": 160, "visible": true },
                    { "id": "device.metrics.screen.position", "order": 210, "visible": true },
                    { "id": "device.metrics.screen.size", "order": 220, "visible": true },
                    { "id": "device.metrics.cornerRadius", "order": 230, "visible": true },
                    { "id": "device.metrics.viewport.position", "order": 310, "visible": true },
                    { "id": "device.metrics.viewport.size", "order": 320, "visible": true },
                    { "id": "device.metrics.safeArea.vertical", "order": 410, "visible": true },
                    { "id": "device.metrics.safeArea.horizontal", "order": 420, "visible": true },
                    { "id": "device.metrics.statusBar.position", "order": 510, "visible": true },
                    { "id": "device.metrics.statusBar.size", "order": 520, "visible": true },
                    { "id": "device.metrics.dynamicIsland.position", "order": 610, "visible": true },
                    { "id": "device.metrics.dynamicIsland.size", "order": 620, "visible": true }
                  """
            : recordClassId == "actor"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "actor.shortName", "order": 20, "visible": true },
                    { "id": "actor.defaultDeviceId", "order": 30, "visible": true },
                    { "id": "actor.defaultThemeId", "order": 40, "visible": true }
                  """
            : """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "core.kind", "order": 20, "visible": false },
                    { "id": "core.notes", "order": 30, "visible": true }
              """;

        var actorCards = recordClassId == "actor"
            ? $$"""
            ,
            {
              "id": "colors",
              "label": "Colors",
              "subtitle": "Light and dark actor palette tokens",
              "icon": "{{EditorIcons.Color}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "modes",
                  "label": "Modes",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "actor.color.modes", "order": 10, "visible": true },
                    { "id": "actor.avatarTextColor.modes", "order": 20, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "avatar",
              "label": "Avatar",
              "subtitle": "Image crop and initials fallback",
              "icon": "{{EditorIcons.Avatar}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "avatar",
                  "label": "Avatar image",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "actor.avatar.filePath", "order": 10, "visible": true },
                    { "id": "actor.avatar.scale", "order": 20, "visible": true },
                    { "id": "actor.avatar.offset", "order": 30, "visible": true },
                    { "id": "actor.avatar.useInitials", "order": 40, "visible": true },
                    { "id": "actor.avatar.initialsPadding", "order": 50, "visible": true }
                  ]
                }
              ]
            }
            """
            : "";

        return $$"""
        {
          "cards": [
            {
              "id": "general",
              "label": "General",
              "subtitle": "Identity",
              "icon": "{{EditorIcons.General}}",
              "order": 10,
              "visible": true,
              "defaultOpen": true,
              "groups": [
                {
                  "id": "identity",
                  "label": "Identity",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    {{generalFields}}
                  ]
                }
              ]
            }{{actorCards}}
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

    public IReadOnlyList<FieldOption> GetPaletteColorOptions(string projectId)
    {
        using var connection = OpenConnection();
        return QueryPaletteColorRows(connection)
            .Where((color) => color.ProjectId == projectId)
            .OrderBy((color) => color.Token)
            .Select((color) => new FieldOption(color.Token, color.Token, color.ValueHex))
            .ToList();
    }

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId)
    {
        using var connection = OpenConnection();
        return QueryDeviceRows(connection)
            .Where((device) => device.ProjectId == projectId)
            .OrderBy((device) => device.Name)
            .Select((device) => new FieldOption(device.Id, device.Name))
            .ToList();
    }

    public IReadOnlyList<FieldOption> GetThemeOptions(string projectId)
    {
        // Theme rows are not part of this new shell yet, but this keeps the field
        // on the dictionary option-control path instead of falling back to text.
        return [new FieldOption("", "No theme table yet")];
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

    private static List<DeviceRow> QueryDeviceRows(SqliteConnection connection)
    {
        var rows = new List<DeviceRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, manufacturer, model, os_family, metrics_json FROM devices ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new DeviceRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5),
                reader.GetString(6)));
        }

        return rows;
    }

    private static List<ActorRow> QueryActorRows(SqliteConnection connection)
    {
        var rows = new List<ActorRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json FROM actors ORDER BY display_name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ActorRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5),
                ReadString(reader, 6)));
        }

        return rows;
    }

    public ActorSettings GetActorSettings(string actorId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, display_name, short_name, default_device_id, default_theme_id, metadata_json FROM actors WHERE id = $id";
        command.Parameters.AddWithValue("$id", actorId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing actor '{actorId}'.");
        }

        return new ActorSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5));
    }

    public void UpdateActorField(string actorId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "actor.shortName":
                Execute(connection, "UPDATE actors SET short_name = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
            case "actor.defaultDeviceId":
                Execute(connection, "UPDATE actors SET default_device_id = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
            case "actor.defaultThemeId":
                Execute(connection, "UPDATE actors SET default_theme_id = $value WHERE id = $id", ("$id", actorId), ("$value", value));
                return;
        }

        var settings = GetActorSettings(actorId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        switch (fieldId)
        {
            case "actor.color.modes":
                SetPair(metadata, value, ["modes", "light", "color"], ["modes", "dark", "color"], asNumber: false);
                break;
            case "actor.avatarTextColor.modes":
                SetPair(metadata, value, ["modes", "light", "avatarTextColor"], ["modes", "dark", "avatarTextColor"], asNumber: false);
                break;
            case "actor.avatar.filePath":
                SetJsonValue(metadata, ["avatar", "filePath"], JsonValue.Create(value)!);
                break;
            case "actor.avatar.scale":
                SetJsonValue(metadata, ["avatar", "scale"], NumberNode(value));
                break;
            case "actor.avatar.offset":
                SetPair(metadata, value, ["avatar", "offsetX"], ["avatar", "offsetY"]);
                break;
            case "actor.avatar.useInitials":
                SetJsonValue(metadata, ["avatar", "useInitials"], JsonValue.Create(StringToBool(value))!);
                break;
            case "actor.avatar.initialsPadding":
                SetJsonValue(metadata, ["avatar", "initialsPadding"], NumberNode(value));
                break;
            default:
                throw new InvalidOperationException($"Unknown actor field '{fieldId}'.");
        }

        Execute(
            connection,
            "UPDATE actors SET metadata_json = $metadataJson WHERE id = $id",
            ("$id", actorId),
            ("$metadataJson", metadata.ToJsonString()));
    }

    public string GetActorFieldValue(string actorId, string fieldId)
    {
        var settings = GetActorSettings(actorId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        return fieldId switch
        {
            "actor.shortName" => settings.ShortName,
            "actor.defaultDeviceId" => settings.DefaultDeviceId,
            "actor.defaultThemeId" => settings.DefaultThemeId,
            "actor.color.modes" => MetricPair(settings.MetadataJson, ["modes", "light", "color"], ["modes", "dark", "color"]),
            "actor.avatarTextColor.modes" => MetricPair(settings.MetadataJson, ["modes", "light", "avatarTextColor"], ["modes", "dark", "avatarTextColor"]),
            "actor.avatar.filePath" => JsonString(metadata, ["avatar", "filePath"]),
            "actor.avatar.scale" => JsonNumberString(metadata, ["avatar", "scale"]),
            "actor.avatar.offset" => MetricPair(settings.MetadataJson, ["avatar", "offsetX"], ["avatar", "offsetY"]),
            "actor.avatar.useInitials" => BoolToString(JsonBool(metadata, ["avatar", "useInitials"])),
            "actor.avatar.initialsPadding" => JsonNumberString(metadata, ["avatar", "initialsPadding"]),
            _ => throw new InvalidOperationException($"Unknown actor field '{fieldId}'."),
        };
    }

    public DeviceSettings GetDeviceSettings(string deviceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, manufacturer, model, os_family, metrics_json FROM devices WHERE id = $id";
        command.Parameters.AddWithValue("$id", deviceId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing device '{deviceId}'.");
        }

        return new DeviceSettings(
            reader.GetString(0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            reader.GetString(4));
    }

    public void UpdateDeviceField(string deviceId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "device.manufacturer":
                Execute(connection, "UPDATE devices SET manufacturer = $value WHERE id = $id", ("$id", deviceId), ("$value", value));
                return;
            case "device.model":
                Execute(connection, "UPDATE devices SET model = $value WHERE id = $id", ("$id", deviceId), ("$value", value));
                return;
            case "device.osFamily":
                Execute(connection, "UPDATE devices SET os_family = $value WHERE id = $id", ("$id", deviceId), ("$value", value));
                return;
        }

        if (!fieldId.StartsWith("device.metrics.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unknown device field '{fieldId}'.");
        }

        var settings = GetDeviceSettings(deviceId);
        var metrics = ParseJsonObject(settings.MetricsJson);
        switch (fieldId)
        {
            case "device.metrics.designSpace.size":
                SetPair(metrics, value, ["designSpace", "width"], ["designSpace", "height"]);
                break;
            case "device.metrics.renderSize":
                SetPair(metrics, value, ["renderSize", "width"], ["renderSize", "height"]);
                break;
            case "device.metrics.canvas.size":
                SetPair(metrics, value, ["canvas", "width"], ["canvas", "height"]);
                break;
            case "device.metrics.screen.position":
                SetPair(metrics, value, ["screen", "x"], ["screen", "y"]);
                break;
            case "device.metrics.screen.size":
                SetPair(metrics, value, ["screen", "width"], ["screen", "height"]);
                break;
            case "device.metrics.viewport.position":
                SetPair(metrics, value, ["viewport", "x"], ["viewport", "y"]);
                break;
            case "device.metrics.viewport.size":
                SetPair(metrics, value, ["viewport", "width"], ["viewport", "height"]);
                break;
            case "device.metrics.safeArea.vertical":
                SetPair(metrics, value, ["safeArea", "top"], ["safeArea", "bottom"]);
                break;
            case "device.metrics.safeArea.horizontal":
                SetPair(metrics, value, ["safeArea", "left"], ["safeArea", "right"]);
                break;
            case "device.metrics.statusBar.position":
                SetPair(metrics, value, ["statusBar", "x"], ["statusBar", "y"]);
                break;
            case "device.metrics.statusBar.size":
                SetPair(metrics, value, ["statusBar", "width"], ["statusBar", "height"]);
                break;
            case "device.metrics.dynamicIsland.position":
                SetPair(metrics, value, ["dynamicIsland", "x"], ["dynamicIsland", "y"]);
                break;
            case "device.metrics.dynamicIsland.size":
                SetPair(metrics, value, ["dynamicIsland", "width"], ["dynamicIsland", "height"]);
                break;
            case "device.metrics.scaleToPixels":
                SetJsonValue(metrics, ["scaleToPixels"], NumberNode(value));
                break;
            case "device.metrics.pixelRatio":
                SetJsonValue(metrics, ["pixelRatio"], NumberNode(value));
                break;
            case "device.metrics.defaultScreenScale":
                SetJsonValue(metrics, ["defaultScreenScale"], NumberNode(value));
                break;
            case "device.metrics.cornerRadius":
                SetJsonValue(metrics, ["cornerRadius"], NumberNode(value));
                break;
            default:
                throw new InvalidOperationException($"Unknown device metrics field '{fieldId}'.");
        }

        Execute(
            connection,
            "UPDATE devices SET metrics_json = $metricsJson WHERE id = $id",
            ("$id", deviceId),
            ("$metricsJson", metrics.ToJsonString()));
    }

    public string GetDeviceMetricFieldValue(string deviceId, string fieldId)
    {
        var settings = GetDeviceSettings(deviceId);
        return fieldId switch
        {
            "device.metrics.designSpace.size" => MetricPair(settings.MetricsJson, ["designSpace", "width"], ["designSpace", "height"]),
            "device.metrics.renderSize" => MetricPair(settings.MetricsJson, ["renderSize", "width"], ["renderSize", "height"]),
            "device.metrics.canvas.size" => MetricPair(settings.MetricsJson, ["canvas", "width"], ["canvas", "height"]),
            "device.metrics.screen.position" => MetricPair(settings.MetricsJson, ["screen", "x"], ["screen", "y"]),
            "device.metrics.screen.size" => MetricPair(settings.MetricsJson, ["screen", "width"], ["screen", "height"]),
            "device.metrics.viewport.position" => MetricPair(settings.MetricsJson, ["viewport", "x"], ["viewport", "y"]),
            "device.metrics.viewport.size" => MetricPair(settings.MetricsJson, ["viewport", "width"], ["viewport", "height"]),
            "device.metrics.safeArea.vertical" => MetricPair(settings.MetricsJson, ["safeArea", "top"], ["safeArea", "bottom"]),
            "device.metrics.safeArea.horizontal" => MetricPair(settings.MetricsJson, ["safeArea", "left"], ["safeArea", "right"]),
            "device.metrics.statusBar.position" => MetricPair(settings.MetricsJson, ["statusBar", "x"], ["statusBar", "y"]),
            "device.metrics.statusBar.size" => MetricPair(settings.MetricsJson, ["statusBar", "width"], ["statusBar", "height"]),
            "device.metrics.dynamicIsland.position" => MetricPair(settings.MetricsJson, ["dynamicIsland", "x"], ["dynamicIsland", "y"]),
            "device.metrics.dynamicIsland.size" => MetricPair(settings.MetricsJson, ["dynamicIsland", "width"], ["dynamicIsland", "height"]),
            "device.metrics.scaleToPixels" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["scaleToPixels"]),
            "device.metrics.pixelRatio" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["pixelRatio"]),
            "device.metrics.defaultScreenScale" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["defaultScreenScale"]),
            "device.metrics.cornerRadius" => JsonNumberString(ParseJsonObject(settings.MetricsJson), ["cornerRadius"]),
            _ => throw new InvalidOperationException($"Unknown device metrics field '{fieldId}'."),
        };
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

    private static string BoolToString(bool value)
    {
        return value ? "true" : "false";
    }

    private static JsonObject ParseJsonObject(string json)
    {
        return JsonNode.Parse(json)?.AsObject() ?? [];
    }

    private static string MetricPair(string metricsJson, IReadOnlyList<string> firstPath, IReadOnlyList<string> secondPath)
    {
        var metrics = ParseJsonObject(metricsJson);
        return $"{JsonNumberString(metrics, firstPath)}|{JsonNumberString(metrics, secondPath)}";
    }

    private static string JsonNumberString(JsonObject root, IReadOnlyList<string> path)
    {
        var node = GetJsonValue(root, path);
        if (node is null) return "0";
        if (node.GetValueKind() == JsonValueKind.Number)
        {
            return node.ToJsonString();
        }

        return node.GetValue<string?>() ?? "0";
    }

    private static string JsonString(JsonObject root, IReadOnlyList<string> path)
    {
        var node = GetJsonValue(root, path);
        if (node is null) return "";
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return node.ToJsonString().Trim('"');
    }

    private static bool JsonBool(JsonObject root, IReadOnlyList<string> path)
    {
        var node = GetJsonValue(root, path);
        return node is JsonValue value && value.TryGetValue<bool>(out var boolean) && boolean;
    }

    private static JsonNode? GetJsonValue(JsonObject root, IReadOnlyList<string> path)
    {
        JsonNode? current = root;
        foreach (var part in path)
        {
            if (current is not JsonObject currentObject || !currentObject.TryGetPropertyValue(part, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static void SetPair(
        JsonObject root,
        string pairValue,
        IReadOnlyList<string> firstPath,
        IReadOnlyList<string> secondPath,
        bool asNumber = true)
    {
        var parts = pairValue.Split('|', 2);
        var first = parts.ElementAtOrDefault(0) ?? "";
        var second = parts.ElementAtOrDefault(1) ?? "";
        SetJsonValue(root, firstPath, asNumber ? NumberNode(first) : JsonValue.Create(first)!);
        SetJsonValue(root, secondPath, asNumber ? NumberNode(second) : JsonValue.Create(second)!);
    }

    private static void SetJsonValue(JsonObject root, IReadOnlyList<string> path, JsonNode value)
    {
        var current = root;
        for (var index = 0; index < path.Count - 1; index++)
        {
            var part = path[index];
            if (current[part] is not JsonObject child)
            {
                child = [];
                current[part] = child;
            }

            current = child;
        }

        current[path[^1]] = value;
    }

    private static JsonNode NumberNode(string value)
    {
        return value.Contains('.', StringComparison.Ordinal)
            ? JsonValue.Create(double.TryParse(value, out var decimalValue) ? decimalValue : 0)!
            : JsonValue.Create(int.TryParse(value, out var integerValue) ? integerValue : 0)!;
    }

    private static string DefaultDeviceMetricsJson(int width, int height, double scale)
    {
        return DeviceMetricsJson(width, height, scale, includeDynamicIsland: false);
    }

    private static string DefaultActorMetadataJson(string colorToken, string avatarTextColorToken)
    {
        var root = new JsonObject
        {
            ["modes"] = new JsonObject
            {
                ["light"] = new JsonObject
                {
                    ["color"] = colorToken,
                    ["avatarTextColor"] = avatarTextColorToken,
                },
                ["dark"] = new JsonObject
                {
                    ["color"] = colorToken,
                    ["avatarTextColor"] = avatarTextColorToken,
                },
            },
            ["avatar"] = new JsonObject
            {
                ["useInitials"] = true,
                ["filePath"] = "",
                ["scale"] = 1,
                ["offsetX"] = 0,
                ["offsetY"] = 0,
                ["baseSize"] = 640,
                ["initialsPadding"] = 96,
            },
        };

        return root.ToJsonString();
    }

    private static string DeviceMetricsJson(int width, int height, double scale, bool includeDynamicIsland)
    {
        var statusBarHeight = (int)Math.Round(height * 0.063);
        var bottomInset = (int)Math.Round(height * 0.0365);
        var cornerRadius = (int)Math.Round(width * 0.128);
        var root = new JsonObject
        {
            ["designSpace"] = new JsonObject
            {
                ["width"] = (int)Math.Round(width / scale),
                ["height"] = (int)Math.Round(height / scale),
                ["unit"] = "logical",
            },
            ["renderSize"] = new JsonObject { ["width"] = width, ["height"] = height },
            ["scaleToPixels"] = scale,
            ["canvas"] = new JsonObject { ["width"] = width, ["height"] = height },
            ["screen"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height },
            ["viewport"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = height },
            ["safeArea"] = new JsonObject { ["top"] = statusBarHeight, ["right"] = 0, ["bottom"] = bottomInset, ["left"] = 0 },
            ["statusBar"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = width, ["height"] = statusBarHeight },
            ["cornerRadius"] = cornerRadius,
            ["pixelRatio"] = scale,
            ["defaultScreenScale"] = 1,
        };

        if (includeDynamicIsland)
        {
            root["dynamicIsland"] = new JsonObject
            {
                ["x"] = 462,
                ["y"] = 33,
                ["width"] = 366,
                ["height"] = 111,
            };
        }

        return root.ToJsonString();
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

        CREATE TABLE IF NOT EXISTS devices (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          manufacturer TEXT NOT NULL DEFAULT '',
          model TEXT NOT NULL DEFAULT '',
          os_family TEXT NOT NULL DEFAULT '',
          metrics_json TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS actors (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          display_name TEXT NOT NULL,
          short_name TEXT NOT NULL DEFAULT '',
          default_device_id TEXT NOT NULL DEFAULT '',
          default_theme_id TEXT NOT NULL DEFAULT '',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS editor_layouts (
          record_class_id TEXT PRIMARY KEY,
          layout_json TEXT NOT NULL
        );
        """;

    public sealed record ProjectSettings(string Slug, int DefaultFps, string MediaRoot);
    public sealed record EpisodeSettings(string Slug, int SortOrder);
    public sealed record DeviceSettings(string Name, string Manufacturer, string Model, string OsFamily, string MetricsJson);
    public sealed record ActorSettings(
        string ProjectId,
        string DisplayName,
        string ShortName,
        string DefaultDeviceId,
        string DefaultThemeId,
        string MetadataJson);
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
    private sealed record DeviceRow(string Id, string ProjectId, string Name, string Manufacturer, string Model, string OsFamily, string MetricsJson);
    private sealed record ActorRow(string Id, string ProjectId, string DisplayName, string ShortName, string DefaultDeviceId, string DefaultThemeId, string MetadataJson);
    private sealed record ShotRow(
        string Id,
        string EpisodeId,
        string Name,
        string Notes,
        int SortOrder,
        int Fps,
        int DurationFrames);

    private sealed record PaletteSeedRow(string Token, string ValueHex, bool IsNeutral, string MetadataJson);
    private sealed record DeviceSeedRow(string Id, string Name, string Manufacturer, string Model, string OsFamily, string MetricsJson);
    private sealed record ActorSeedRow(string Id, string DisplayName, string ShortName, string MetadataJson);

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

    private static readonly DeviceSeedRow[] DeviceSeedRows =
    [
        new("device_iphone_15_pro", "iPhone 15 Pro", "Apple", "iPhone 15 Pro", "ios", DeviceMetricsJson(1179, 2556, 3, includeDynamicIsland: false)),
        new("device_iphone_generic", "iPhone 15 Pro Max", "Apple", "iPhone 15 Pro Max", "ios", DeviceMetricsJson(1290, 2796, 3, includeDynamicIsland: true)),
        new("device_iphone_14_pro", "iPhone 14 Pro", "Apple", "iPhone 14 Pro", "ios", DeviceMetricsJson(1179, 2556, 3, includeDynamicIsland: false)),
        new("device_samsung_galaxy_s24", "Samsung Galaxy S24", "Samsung", "Galaxy S24", "android", DeviceMetricsJson(1080, 2340, 3, includeDynamicIsland: false)),
        new("device_samsung_galaxy_s24_ultra", "Samsung Galaxy S24 Ultra", "Samsung", "Galaxy S24 Ultra", "android", DeviceMetricsJson(1440, 3120, 3, includeDynamicIsland: false)),
        new("device_google_pixel_8_pro", "Google Pixel 8 Pro", "Google", "Pixel 8 Pro", "android", DeviceMetricsJson(1344, 2992, 3, includeDynamicIsland: false)),
    ];

    private static readonly ActorSeedRow[] ActorSeedRows =
    [
        new("actor_alex", "Alex", "Alex", DefaultActorMetadataJson("pastel_sky", "gray_010")),
        new("actor_sam", "Sam", "Sam", DefaultActorMetadataJson("pastel_mint", "gray_010")),
        new("actor_alex_b", "Alex B", "Alex B", DefaultActorMetadataJson("pastel_yellow", "gray_010")),
    ];
}
