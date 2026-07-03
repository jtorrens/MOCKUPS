using Microsoft.Data.Sqlite;
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
    private static readonly object WriteGate = new();
    private static readonly Dictionary<string, (string[] Light, string[] Dark)> ThemeColorPairPaths = new()
    {
        ["theme.colors.background"] = (["modes", "light", "colors", "background"], ["modes", "dark", "colors", "background"]),
        ["theme.colors.textPrimary"] = (["modes", "light", "colors", "textPrimary"], ["modes", "dark", "colors", "textPrimary"]),
        ["theme.colors.textSecondary"] = (["modes", "light", "colors", "textSecondary"], ["modes", "dark", "colors", "textSecondary"]),
        ["theme.colors.accent"] = (["modes", "light", "colors", "accent"], ["modes", "dark", "colors", "accent"]),
        ["theme.icons.primary"] = (["modes", "light", "colors", "icons.primary"], ["modes", "dark", "colors", "icons.primary"]),
        ["theme.icons.secondary"] = (["modes", "light", "colors", "icons.secondary"], ["modes", "dark", "colors", "icons.secondary"]),
        ["theme.icons.accent"] = (["modes", "light", "colors", "icons.accent"], ["modes", "dark", "colors", "icons.accent"]),
        ["theme.borders.primary"] = (["modes", "light", "colors", "borders.primary"], ["modes", "dark", "colors", "borders.primary"]),
        ["theme.borders.secondary"] = (["modes", "light", "colors", "borders.secondary"], ["modes", "dark", "colors", "borders.secondary"]),
        ["theme.borders.alternate"] = (["modes", "light", "colors", "borders.alternate"], ["modes", "dark", "colors", "borders.alternate"]),
        ["theme.cursor.color"] = (["modes", "light", "colors", "theme.cursor.color"], ["modes", "dark", "colors", "theme.cursor.color"]),
        ["theme.statusBar.foreground"] = (["modes", "light", "statusBar", "foreground"], ["modes", "dark", "statusBar", "foreground"]),
        ["theme.statusBar.background"] = (["modes", "light", "statusBar", "background", "color"], ["modes", "dark", "statusBar", "background", "color"]),
        ["theme.navigationBar.foreground"] = (["modes", "light", "navigationBar", "foreground"], ["modes", "dark", "navigationBar", "foreground"]),
        ["theme.navigationBar.background"] = (["modes", "light", "navigationBar", "background", "color"], ["modes", "dark", "navigationBar", "background", "color"]),
        ["theme.keyboard.background"] = (["modes", "light", "keyboard", "background"], ["modes", "dark", "keyboard", "background"]),
        ["theme.keyboard.keyBackground"] = (["modes", "light", "keyboard", "keyBackground"], ["modes", "dark", "keyboard", "keyBackground"]),
        ["theme.keyboard.specialKeyBackground"] = (["modes", "light", "keyboard", "specialKeyBackground"], ["modes", "dark", "keyboard", "specialKeyBackground"]),
        ["theme.keyboard.pressedKeyBackground"] = (["modes", "light", "keyboard", "pressedKeyBackground"], ["modes", "dark", "keyboard", "pressedKeyBackground"]),
        ["theme.keyboard.popoverBackground"] = (["modes", "light", "keyboard", "popoverBackground"], ["modes", "dark", "keyboard", "popoverBackground"]),
        ["theme.keyboard.text"] = (["modes", "light", "keyboard", "text"], ["modes", "dark", "keyboard", "text"]),
    };
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
        var themes = QueryThemeRows(connection);
        var productionFonts = QueryProductionFontRows(connection);
        var iconThemes = QueryIconThemeRows(connection);
        var statusBars = QueryStatusBarRows(connection);
        var navigationBars = QueryNavigationBarRows(connection);
        var componentClasses = QueryComponentClassRows(connection);
        var referenceUsageIndex = BuildReferenceUsageIndex(
            actors,
            themes,
            paletteColors,
            productionFonts,
            iconThemes,
            statusBars,
            navigationBars,
            componentClasses);

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
        var themeRootNodes = new Dictionary<string, ProjectTreeNode>();
        var productionFontRootNodes = new Dictionary<string, ProjectTreeNode>();
        var iconThemeRootNodes = new Dictionary<string, ProjectTreeNode>();
        var statusBarRootNodes = new Dictionary<string, ProjectTreeNode>();
        var navigationBarRootNodes = new Dictionary<string, ProjectTreeNode>();
        var componentClassRootNodes = new Dictionary<string, ProjectTreeNode>();
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
            var themesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.ThemesRoot,
                $"themes_root_{project.Id}",
                "Themes",
                "Production visual themes.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ThemesRoot),
                productionDataRoot);
            var productionFontsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.ProductionFontsRoot,
                $"production_fonts_root_{project.Id}",
                "Production Fonts",
                "Approved font families copied into this production.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ProductionFontsRoot),
                systemDataRoot);
            var iconThemesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.IconThemesRoot,
                $"icon_themes_root_{project.Id}",
                "Icon Themes",
                "Icon sets and shared semantic icon tokens.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.IconThemesRoot),
                systemDataRoot);
            var statusBarsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.StatusBarsRoot,
                $"status_bars_root_{project.Id}",
                "Status Bars",
                "Reusable device status bar compositions.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.StatusBarsRoot),
                systemDataRoot);
            var navigationBarsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.NavigationBarsRoot,
                $"navigation_bars_root_{project.Id}",
                "Navigation Bars",
                "Reusable device navigation bar compositions.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.NavigationBarsRoot),
                systemDataRoot);
            var componentClassesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentClassesRoot,
                $"component_classes_root_{project.Id}",
                "Component Classes",
                "Reusable visual component defaults.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentClassesRoot),
                systemDataRoot);
            var episodesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.EpisodesRoot,
                $"episodes_root_{project.Id}",
                "Episodes",
                "Episodes and shots for this project.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.EpisodesRoot),
                project);

            productionDataRoot.AddChild(actorsRoot);
            productionDataRoot.AddChild(devicesRoot);
            productionDataRoot.AddChild(themesRoot);
            systemDataRoot.AddChild(paletteRoot);
            systemDataRoot.AddChild(iconThemesRoot);
            systemDataRoot.AddChild(statusBarsRoot);
            systemDataRoot.AddChild(navigationBarsRoot);
            systemDataRoot.AddChild(productionFontsRoot);
            systemDataRoot.AddChild(componentClassesRoot);
            project.AddChild(appsRoot);
            project.AddChild(episodesRoot);
            project.AddChild(productionDataRoot);
            project.AddChild(systemDataRoot);
            appRootNodes[project.Id] = appsRoot;
            paletteRootNodes[project.Id] = paletteRoot;
            deviceRootNodes[project.Id] = devicesRoot;
            actorRootNodes[project.Id] = actorsRoot;
            themeRootNodes[project.Id] = themesRoot;
            productionFontRootNodes[project.Id] = productionFontsRoot;
            iconThemeRootNodes[project.Id] = iconThemesRoot;
            statusBarRootNodes[project.Id] = statusBarsRoot;
            navigationBarRootNodes[project.Id] = navigationBarsRoot;
            componentClassRootNodes[project.Id] = componentClassesRoot;
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
                IsUsed(referenceUsageIndex, ProjectTreeNodeKind.PaletteColor, color.Id)));
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
                devicesRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.Device, device.Id)));
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
                actorsRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.Actor, actor.Id)));
        }

        foreach (var theme in themes.OrderBy((theme) => theme.Name))
        {
            if (!themeRootNodes.TryGetValue(theme.ProjectId, out var themesRoot)) continue;

            themesRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.Theme,
                theme.Id,
                theme.Name,
                $"{theme.Family} · {ThemeReferenceSummary(theme)}",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Theme),
                themesRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.Theme, theme.Id)));
        }

        foreach (var font in productionFonts.OrderBy((font) => font.FamilyName))
        {
            if (!productionFontRootNodes.TryGetValue(font.ProjectId, out var fontsRoot)) continue;

            fontsRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.ProductionFont,
                font.Id,
                font.FamilyName,
                $"{font.Category} · {ProductionFontFileCount(font.FilesJson)} files",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ProductionFont),
                fontsRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ProductionFont, font.Id)));
        }

        foreach (var iconTheme in iconThemes.OrderBy((iconTheme) => iconTheme.Name))
        {
            if (!iconThemeRootNodes.TryGetValue(iconTheme.ProjectId, out var iconThemesRoot)) continue;

            iconThemesRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.IconTheme,
                iconTheme.Id,
                iconTheme.Name,
                $"{IconThemeTokenCount(iconTheme.MappingJson)} tokens · {iconTheme.AssetRoot}",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.IconTheme),
                iconThemesRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.IconTheme, iconTheme.Id)));
        }

        foreach (var statusBar in statusBars.OrderBy((statusBar) => statusBar.Name))
        {
            if (!statusBarRootNodes.TryGetValue(statusBar.ProjectId, out var statusBarsRoot)) continue;

            statusBarsRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.StatusBar,
                statusBar.Id,
                statusBar.Name,
                $"{statusBar.Family} · {StatusBarItemCount(statusBar.ConfigJson)} items",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.StatusBar),
                statusBarsRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.StatusBar, statusBar.Id)));
        }

        foreach (var navigationBar in navigationBars.OrderBy((navigationBar) => navigationBar.Name))
        {
            if (!navigationBarRootNodes.TryGetValue(navigationBar.ProjectId, out var navigationBarsRoot)) continue;

            navigationBarsRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.NavigationBar,
                navigationBar.Id,
                navigationBar.Name,
                $"{navigationBar.Family} · {NavigationBarItemCount(navigationBar.ConfigJson)} buttons",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.NavigationBar),
                navigationBarsRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.NavigationBar, navigationBar.Id)));
        }

        foreach (var componentClass in componentClasses.OrderBy((componentClass) => componentClass.ComponentType).ThenBy((componentClass) => componentClass.Name))
        {
            if (!componentClassRootNodes.TryGetValue(componentClass.ProjectId, out var componentClassesRoot)) continue;

            componentClassesRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentClass,
                componentClass.Id,
                componentClass.Name,
                string.IsNullOrWhiteSpace(componentClass.Notes) ? ComponentTypeLabel(componentClass.ComponentType) : componentClass.Notes,
                componentClass.RecordClassId,
                componentClassesRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ComponentClass, componentClass.Id)));
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

        if (parent.Kind == ProjectTreeNodeKind.ThemesRoot)
        {
            return AddTheme(parent, "custom");
        }

        if (parent.Kind == ProjectTreeNodeKind.ProductionFontsRoot)
        {
            throw new InvalidOperationException("Production fonts are added through the font importer.");
        }

        if (parent.Kind == ProjectTreeNodeKind.IconThemesRoot)
        {
            throw new InvalidOperationException("Icon themes are rebuilt through Refresh Sets.");
        }

        if (parent.Kind == ProjectTreeNodeKind.StatusBarsRoot)
        {
            var project = ProjectAncestor(parent);
            var index = ScalarLong(connection, "SELECT COUNT(*) FROM status_bars WHERE project_id = $projectId", ("$projectId", project.Id)) + 1;
            var id = $"status_bar_{Guid.NewGuid():N}";
            var name = $"Status Bar {index}";
            Execute(
                connection,
                """
                INSERT INTO status_bars (id, project_id, name, family, config_json, metadata_json)
                VALUES ($id, $projectId, $name, 'custom', $configJson, $metadataJson)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$name", name),
                ("$configJson", DefaultStatusBarConfigJson()),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Custom reusable status bar composition." })));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.StatusBar,
                id,
                name,
                $"custom · {DefaultStatusBarItems().Count} items",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.StatusBar),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.NavigationBarsRoot)
        {
            var project = ProjectAncestor(parent);
            var index = ScalarLong(connection, "SELECT COUNT(*) FROM navigation_bars WHERE project_id = $projectId", ("$projectId", project.Id)) + 1;
            var id = $"navigation_bar_{Guid.NewGuid():N}";
            var name = $"Navigation Bar {index}";
            Execute(
                connection,
                """
                INSERT INTO navigation_bars (id, project_id, name, family, config_json, metadata_json)
                VALUES ($id, $projectId, $name, 'custom', $configJson, $metadataJson)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$name", name),
                ("$configJson", DefaultNavigationBarConfigJson()),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Custom reusable navigation bar composition." })));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.NavigationBar,
                id,
                name,
                $"custom · {DefaultNavigationBarItems().Count} buttons",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.NavigationBar),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.ComponentClassesRoot)
        {
            var project = ProjectAncestor(parent);
            var index = ScalarLong(connection, "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId", ("$projectId", project.Id)) + 1;
            var id = $"component_{Guid.NewGuid():N}";
            var name = $"Avatar Component {index}";
            var recordClassId = "component.avatar";
            Execute(
                connection,
                """
                INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                VALUES ($id, $projectId, 'avatar', $recordClassId, $name, $notes, $configJson, $designPreviewJson, $metadataJson)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$recordClassId", recordClassId),
                ("$name", name),
                ("$notes", "Custom reusable avatar component class."),
                ("$configJson", DefaultComponentClassConfigJson("avatar")),
                ("$designPreviewJson", DefaultComponentDesignPreviewJson("avatar")),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Custom reusable component class." })));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentClass,
                id,
                name,
                "Custom reusable avatar component class.",
                recordClassId,
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

    public ProjectTreeNode AddImportedDevice(ProjectTreeNode devicesRoot, DeviceImportDraft device)
    {
        if (devicesRoot.Kind != ProjectTreeNodeKind.DevicesRoot)
        {
            throw new InvalidOperationException("Imported devices can only be added from the Devices root.");
        }

        using var connection = OpenConnection();
        var project = ProjectAncestor(devicesRoot);
        var id = $"device_{Guid.NewGuid():N}";
        Execute(
            connection,
            """
            INSERT INTO devices (id, project_id, name, manufacturer, model, os_family, metrics_json)
            VALUES ($id, $projectId, $name, $manufacturer, $model, $osFamily, $metricsJson)
            """,
            ("$id", id),
            ("$projectId", project.Id),
            ("$name", device.Name),
            ("$manufacturer", device.Manufacturer),
            ("$model", device.Model),
            ("$osFamily", device.OsFamily),
            ("$metricsJson", device.MetricsJson));

        return new ProjectTreeNode(
            ProjectTreeNodeKind.Device,
            id,
            device.Name,
            $"{device.Manufacturer} {device.Model}".Trim(),
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Device),
            devicesRoot);
    }

    public ProjectTreeNode AddTheme(ProjectTreeNode themesRoot, string family)
    {
        if (themesRoot.Kind != ProjectTreeNodeKind.ThemesRoot)
        {
            throw new InvalidOperationException("Themes can only be added from the Themes root.");
        }

        family = family is "ios" or "android" ? family : "custom";
        using var connection = OpenConnection();
        var project = ProjectAncestor(themesRoot);
        var index = ScalarLong(connection, "SELECT COUNT(*) FROM themes WHERE project_id = $projectId", ("$projectId", project.Id)) + 1;
        var id = $"theme_{Guid.NewGuid():N}";
        var name = family switch
        {
            "ios" => $"iOS Theme {index}",
            "android" => $"Android Theme {index}",
            _ => $"Theme {index}",
        };
        var iconThemeId = FirstId(connection, "icon_themes", project.Id);
        var statusBarId = FirstId(connection, "status_bars", project.Id);
        var navigationBarId = FirstId(connection, "navigation_bars", project.Id);
        Execute(
            connection,
            """
            INSERT INTO themes (id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json)
            VALUES ($id, $projectId, $name, $family, $iconThemeId, $statusBarId, $navigationBarId, $tokensJson, $metadataJson)
            """,
            ("$id", id),
            ("$projectId", project.Id),
            ("$name", name),
            ("$family", family),
            ("$iconThemeId", iconThemeId),
            ("$statusBarId", statusBarId),
            ("$navigationBarId", navigationBarId),
            ("$tokensJson", DefaultThemeTokensJson(family)),
            ("$metadataJson", JsonSerializer.Serialize(new { note = $"{family} preset production theme." })));

        return new ProjectTreeNode(
            ProjectTreeNodeKind.Theme,
            id,
            name,
            $"{family} · {ThemeReferenceSummary(iconThemeId, statusBarId, navigationBarId)}",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Theme),
            themesRoot);
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

        if (node.Kind == ProjectTreeNodeKind.Theme)
        {
            var id = $"theme_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO themes (id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json)
                SELECT $id, project_id, $name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json
                FROM themes
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.Theme, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.StatusBar)
        {
            var id = $"status_bar_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO status_bars (id, project_id, name, family, config_json, metadata_json)
                SELECT $id, project_id, $name, family, config_json, metadata_json
                FROM status_bars
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.StatusBar, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.NavigationBar)
        {
            var id = $"navigation_bar_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO navigation_bars (id, project_id, name, family, config_json, metadata_json)
                SELECT $id, project_id, $name, family, config_json, metadata_json
                FROM navigation_bars
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.NavigationBar, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            var id = $"component_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                SELECT $id, project_id, component_type, record_class_id, $name, notes, config_json, design_preview_json, metadata_json
                FROM component_classes
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.ComponentClass, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
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
            ProjectTreeNodeKind.Theme => "themes",
            ProjectTreeNodeKind.ProductionFont => "production_fonts",
            ProjectTreeNodeKind.IconTheme => "icon_themes",
            ProjectTreeNodeKind.StatusBar => "status_bars",
            ProjectTreeNodeKind.NavigationBar => "navigation_bars",
            ProjectTreeNodeKind.ComponentClass => "component_classes",
            _ => throw new InvalidOperationException($"Cannot delete {node.Kind}."),
        };

        var usages = GetReferenceUsages(connection, node.Kind, node.Id, ReferenceSearchValue(connection, node));
        if (usages.Count > 0)
        {
            throw new InvalidOperationException($"This {node.Kind} is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
        }

        if (node.Kind == ProjectTreeNodeKind.ProductionFont)
        {
            DeleteProductionFontFiles(connection, node.Id);
        }

        Execute(connection, $"DELETE FROM {table} WHERE id = $id", ("$id", node.Id));
    }

    public IReadOnlyList<string> GetReferenceUsages(ProjectTreeNode node)
    {
        using var connection = OpenConnection();
        return GetReferenceUsages(connection, node.Kind, node.Id, ReferenceSearchValue(connection, node));
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
            ProjectTreeNodeKind.Theme => "themes",
            ProjectTreeNodeKind.ProductionFont => "production_fonts",
            ProjectTreeNodeKind.IconTheme => "icon_themes",
            ProjectTreeNodeKind.StatusBar => "status_bars",
            ProjectTreeNodeKind.NavigationBar => "navigation_bars",
            ProjectTreeNodeKind.ComponentClass => "component_classes",
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

        if (node.Kind == ProjectTreeNodeKind.ProductionFont)
        {
            Execute(
                connection,
                "UPDATE production_fonts SET family_name = $name WHERE id = $id",
                ("$id", node.Id),
                ("$name", node.Name));
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Theme)
        {
            Execute(
                connection,
                "UPDATE themes SET name = $name WHERE id = $id",
                ("$id", node.Id),
                ("$name", node.Name));
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.IconTheme)
        {
            Execute(
                connection,
                "UPDATE icon_themes SET name = $name WHERE id = $id",
                ("$id", node.Id),
                ("$name", node.Name));
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.StatusBar)
        {
            Execute(
                connection,
                "UPDATE status_bars SET name = $name WHERE id = $id",
                ("$id", node.Id),
                ("$name", node.Name));
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.NavigationBar)
        {
            Execute(
                connection,
                "UPDATE navigation_bars SET name = $name WHERE id = $id",
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
        EnsureComponentClassColumns(connection);
        SeedEditorLayouts(connection);
        SeedIfEmpty(connection);
        SeedPaletteColorsIfEmpty(connection);
        SeedDevicesIfEmpty(connection);
        SeedActorsIfEmpty(connection);
        SeedProductionFontsIfEmpty(connection);
        SeedStatusBarsIfEmpty(connection);
        SeedNavigationBarsIfEmpty(connection);
        SeedComponentClassesIfEmpty(connection);
        SeedThemesIfEmpty(connection);
        EnsureThemeTokens(connection);
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

    private static void SeedProductionFontsIfEmpty(SqliteConnection connection)
    {
        _ = connection;
    }

    private static void SeedStatusBarsIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM status_bars WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            Execute(
                connection,
                """
                INSERT INTO status_bars (id, project_id, name, family, config_json, metadata_json)
                VALUES ($id, $projectId, $name, $family, $configJson, $metadataJson)
                """,
                ("$id", $"status_bar_{projectId}_ios_default"),
                ("$projectId", projectId),
                ("$name", "iOS Default Status Bar"),
                ("$family", "ios"),
                ("$configJson", DefaultStatusBarConfigJson()),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Reusable iOS-style status bar composition." })));
        }
    }

    private static void SeedNavigationBarsIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM navigation_bars WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            Execute(
                connection,
                """
                INSERT INTO navigation_bars (id, project_id, name, family, config_json, metadata_json)
                VALUES ($id, $projectId, $name, $family, $configJson, $metadataJson)
                """,
                ("$id", $"navigation_bar_{projectId}_ios_default"),
                ("$projectId", projectId),
                ("$name", "iOS Default Navigation Bar"),
                ("$family", "ios"),
                ("$configJson", DefaultNavigationBarConfigJson()),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Reusable iOS-style navigation bar composition." })));
        }
    }

    private static void SeedComponentClassesIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM component_classes WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            foreach (var seed in ComponentSeedRows)
            {
                Execute(
                    connection,
                    """
                    INSERT INTO component_classes (id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json)
                    VALUES ($id, $projectId, $componentType, $recordClassId, $name, $notes, $configJson, $designPreviewJson, $metadataJson)
                    """,
                    ("$id", $"component_{projectId}_{seed.ComponentType}"),
                    ("$projectId", projectId),
                    ("$componentType", seed.ComponentType),
                    ("$recordClassId", seed.RecordClassId),
                    ("$name", seed.Name),
                    ("$notes", ComponentTypeLabel(seed.ComponentType)),
                    ("$configJson", seed.ConfigJson),
                    ("$designPreviewJson", seed.DesignPreviewJson),
                    ("$metadataJson", seed.MetadataJson));
            }
        }
    }

    private static void SeedThemesIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM themes WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            var iconThemeId = FirstId(connection, "icon_themes", projectId);
            var statusBarId = FirstId(connection, "status_bars", projectId);
            var navigationBarId = FirstId(connection, "navigation_bars", projectId);
            Execute(
                connection,
                """
                INSERT INTO themes (id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json)
                VALUES ($id, $projectId, $name, $family, $iconThemeId, $statusBarId, $navigationBarId, $tokensJson, $metadataJson)
                """,
                ("$id", $"theme_{projectId}_ios_default"),
                ("$projectId", projectId),
                ("$name", "iOS Default Theme"),
                ("$family", "ios"),
                ("$iconThemeId", iconThemeId),
                ("$statusBarId", statusBarId),
                ("$navigationBarId", navigationBarId),
                ("$tokensJson", DefaultThemeTokensJson("ios")),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Default iOS-style production theme." })));
        }
    }

    private static void EnsureThemeTokens(SqliteConnection connection)
    {
        foreach (var theme in QueryThemeRows(connection))
        {
            var tokens = ParseJsonObject(string.IsNullOrWhiteSpace(theme.TokensJson) ? "{}" : theme.TokensJson);
            var defaults = ParseJsonObject(DefaultThemeTokensJson(theme.Family));
            if (!MergeMissing(tokens, defaults))
            {
                continue;
            }

            Execute(
                connection,
                "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id",
                ("$id", theme.Id),
                ("$tokensJson", tokens.ToJsonString()));
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
            "navigation.themes",
            "navigation.production_fonts",
            "navigation.icon_themes",
            "navigation.status_bars",
            "navigation.navigation_bars",
            "navigation.component_classes",
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
            "theme",
            "production_font",
            "icon_theme",
            "status_bar",
            "navigation_bar",
            "component.avatar",
            "component.text_input_bar",
            "component.keyboard",
            "component.button_icon",
            "component.label",
            "component.audio",
            "component.video",
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
        var generalFields = recordClassId.StartsWith("component.", StringComparison.Ordinal)
            ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "component.type", "order": 20, "visible": true },
                    { "id": "core.notes", "order": 30, "visible": true }
              """
            : recordClassId == "project"
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
            : recordClassId == "theme"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "theme.family", "order": 20, "visible": true },
                    { "id": "theme.defaultMode", "order": 30, "visible": true }
                  """
            : recordClassId == "production_font"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "font.family", "order": 20, "visible": true },
                    { "id": "font.category", "order": 30, "visible": true },
                    { "id": "font.sourceDirectory", "order": 40, "visible": false },
                    { "id": "font.files", "order": 50, "visible": true }
                  """
            : recordClassId == "icon_theme"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "iconTheme.assetRoot", "order": 20, "visible": true },
                    { "id": "iconTheme.tokenCount", "order": 30, "visible": true },
                    { "id": "iconTheme.metadata", "order": 40, "visible": false }
                  """
            : recordClassId == "status_bar"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "statusBar.family", "order": 20, "visible": true }
                  """
            : recordClassId == "navigation_bar"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "navigationBar.family", "order": 20, "visible": true },
                    { "id": "navigationBar.type", "order": 30, "visible": true }
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
        var themeCards = recordClassId == "theme"
            ? $$"""
            ,
            {
              "id": "references",
              "label": "References",
              "subtitle": "Linked icon, status and navigation resources",
              "icon": "{{EditorIcons.Design}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "references",
                  "label": "References",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.iconThemeId", "order": 10, "visible": true },
                    { "id": "theme.statusBarId", "order": 20, "visible": true },
                    { "id": "theme.navigationBarId", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "colors",
              "label": "Colors",
              "subtitle": "Theme color behavior",
              "icon": "{{EditorIcons.Color}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "neutralTint",
                  "label": "Neutral tint",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.neutralTint.hueDeg", "order": 10, "visible": true },
                    { "id": "theme.neutralTint.saturation", "order": 20, "visible": true }
                  ]
                },
                {
                  "id": "appColors",
                  "label": "App colors",
                  "order": 20,
                  "visible": true,
                  "fields": [
                    { "id": "theme.colors.background", "order": 10, "visible": true },
                    { "id": "theme.colors.textPrimary", "order": 20, "visible": true },
                    { "id": "theme.colors.textSecondary", "order": 30, "visible": true },
                    { "id": "theme.colors.accent", "order": 40, "visible": true }
                  ]
                },
                {
                  "id": "borderColors",
                  "label": "Border colors",
                  "order": 30,
                  "visible": true,
                  "fields": [
                    { "id": "theme.borders.primary", "order": 10, "visible": true },
                    { "id": "theme.borders.secondary", "order": 20, "visible": true },
                    { "id": "theme.borders.alternate", "order": 30, "visible": true }
                  ]
                },
                {
                  "id": "cursor",
                  "label": "Cursor",
                  "order": 40,
                  "visible": true,
                  "fields": [
                    { "id": "theme.cursor.color", "order": 10, "visible": true },
                    { "id": "theme.cursor.width", "order": 20, "visible": true },
                    { "id": "theme.cursor.blinkFrames", "order": 30, "visible": true }
                  ]
                },
                {
                  "id": "iconColors",
                  "label": "Icon colors",
                  "order": 50,
                  "visible": true,
                  "fields": [
                    { "id": "theme.icons.primary", "order": 10, "visible": true },
                    { "id": "theme.icons.secondary", "order": 20, "visible": true },
                    { "id": "theme.icons.accent", "order": 30, "visible": true }
                  ]
                },
                {
                  "id": "keyboard",
                  "label": "Keyboard",
                  "order": 60,
                  "visible": true,
                  "fields": [
                    { "id": "theme.keyboard.background", "order": 10, "visible": true },
                    { "id": "theme.keyboard.keyBackground", "order": 20, "visible": true },
                    { "id": "theme.keyboard.specialKeyBackground", "order": 30, "visible": true },
                    { "id": "theme.keyboard.pressedKeyBackground", "order": 40, "visible": true },
                    { "id": "theme.keyboard.popoverBackground", "order": 50, "visible": true },
                    { "id": "theme.keyboard.text", "order": 60, "visible": true }
                  ]
                },
                {
                  "id": "navigationBar",
                  "label": "Navigation bar",
                  "order": 70,
                  "visible": true,
                  "fields": [
                    { "id": "theme.navigationBar.foreground", "order": 10, "visible": true },
                    { "id": "theme.navigationBar.background", "order": 20, "visible": true }
                  ]
                },
                {
                  "id": "statusBar",
                  "label": "Status bar",
                  "order": 80,
                  "visible": true,
                  "fields": [
                    { "id": "theme.statusBar.foreground", "order": 10, "visible": true },
                    { "id": "theme.statusBar.background", "order": 20, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "typography",
              "label": "Typography",
              "subtitle": "Default text and emoji font tokens",
              "icon": "{{EditorIcons.Typography}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "typography",
                  "label": "Typography",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "theme.typography.fontFamilyId", "order": 10, "visible": true },
                    { "id": "theme.typography.emojiFontFamilyId", "order": 20, "visible": true },
                    { "id": "theme.typography.size", "order": 30, "visible": true },
                    { "id": "theme.typography.weight", "order": 40, "visible": true },
                    { "id": "theme.typography.style", "order": 50, "visible": true }
                  ]
                }
              ]
            }
            """
            : "";
        var statusBarCards = recordClassId == "status_bar"
            ? $$"""
            ,
            {
              "id": "layout",
              "label": "Layout",
              "subtitle": "Height, item size, gap and padding",
              "icon": "{{EditorIcons.Layout}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "layout",
                  "label": "Layout values",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "statusBar.layout.height", "order": 10, "visible": true },
                    { "id": "statusBar.layout.itemSize", "order": 20, "visible": true },
                    { "id": "statusBar.layout.gap", "order": 30, "visible": true },
                    { "id": "statusBar.layout.sidePadding", "order": 40, "visible": true }
                  ]
                }
              ]
            }
            """
            : "";
        var navigationBarCards = recordClassId == "navigation_bar"
            ? $$"""
            ,
            {
              "id": "buttons",
              "label": "Buttons",
              "subtitle": "Size and generated button shape",
              "icon": "{{EditorIcons.Navigation}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "layout",
                  "label": "Layout values",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "navigationBar.layout.height", "order": 10, "visible": true },
                    { "id": "navigationBar.layout.itemSize", "order": 20, "visible": true },
                    { "id": "navigationBar.layout.sidePadding", "order": 30, "visible": true },
                    { "id": "navigationBar.layout.strokeWidth", "order": 40, "visible": true },
                    { "id": "navigationBar.layout.cornerRadius", "order": 50, "visible": true },
                    { "id": "navigationBar.layout.filled", "order": 60, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "gestureBar",
              "label": "Gesture Bar",
              "subtitle": "Home indicator dimensions",
              "icon": "{{EditorIcons.Navigation}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "gesture",
                  "label": "Gesture bar values",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "navigationBar.gesture.width", "order": 10, "visible": true },
                    { "id": "navigationBar.gesture.height", "order": 20, "visible": true },
                    { "id": "navigationBar.gesture.cornerRadius", "order": 30, "visible": true }
                  ]
                }
              ]
            }
            """
            : "";
        var componentCards = recordClassId.StartsWith("component.", StringComparison.Ordinal)
            ? ComponentClassLayoutCardsJson(recordClassId)
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
            }{{actorCards}}{{themeCards}}{{statusBarCards}}{{navigationBarCards}}{{componentCards}}
          ]
        }
        """;
    }

    private static string ComponentClassLayoutCardsJson(string recordClassId)
    {
        var typeSpecific = recordClassId switch
        {
            "component.avatar" => $$"""
            ,
            {
              "id": "avatar",
              "label": "Avatar",
              "subtitle": "Reusable avatar presentation defaults",
              "icon": "{{EditorIcons.Avatar}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "avatar", "label": "Avatar", "order": 10, "visible": true, "fields": [
                  { "id": "component.avatar.defaultSize", "order": 10, "visible": true },
                  { "id": "component.avatar.cornerRadiusToken", "order": 20, "visible": true }
                ] }
              ]
            }
            """,
            "component.text_input_bar" => $$"""
            ,
            {
              "id": "textInput",
              "label": "Text Input",
              "subtitle": "Input bar, idle text and cursor behavior",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "textInput", "label": "Text input", "order": 10, "visible": true, "fields": [
                  { "id": "component.textInput.height", "order": 10, "visible": true },
                  { "id": "component.textInput.placeholder", "order": 20, "visible": true },
                  { "id": "component.textInput.idleTextColorToken", "order": 30, "visible": true },
                  { "id": "component.textInput.cursorColorToken", "order": 40, "visible": true },
                  { "id": "component.textInput.cursorWidth", "order": 50, "visible": true },
                  { "id": "component.textInput.cursorBlinkFrames", "order": 60, "visible": true }
                ] }
              ]
            }
            """,
            "component.keyboard" => $$"""
            ,
            {
              "id": "keyboard",
              "label": "Keyboard",
              "subtitle": "Key shape, pressed behavior and icon slots",
              "icon": "{{EditorIcons.Keyboard}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "keys", "label": "Keys", "order": 10, "visible": true, "fields": [
                  { "id": "component.keyboard.keyPadding", "order": 10, "visible": true },
                  { "id": "component.keyboard.keyCornerRadius", "order": 20, "visible": true },
                  { "id": "component.keyboard.keyShadowEnabled", "order": 30, "visible": true },
                  { "id": "component.keyboard.pressedEffect", "order": 40, "visible": true },
                  { "id": "component.keyboard.specialKeyTextScale", "order": 50, "visible": true },
                  { "id": "component.keyboard.emojiScale", "order": 60, "visible": true },
                  { "id": "component.keyboard.bottomIconSlots", "order": 70, "visible": true }
                ] }
              ]
            }
            """,
            "component.button_icon" => $$"""
            ,
            {
              "id": "buttonIcon",
              "label": "Button Icon",
              "subtitle": "Icon padding and optional label",
              "icon": "{{EditorIcons.ButtonIcon}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "buttonIcon", "label": "Button icon", "order": 10, "visible": true, "fields": [
                  { "id": "component.buttonIcon.iconPadding", "order": 10, "visible": true },
                  { "id": "component.buttonIcon.labelEnabled", "order": 20, "visible": true },
                  { "id": "component.buttonIcon.labelPosition", "order": 30, "visible": true },
                  { "id": "component.buttonIcon.labelSize", "order": 40, "visible": true },
                  { "id": "component.buttonIcon.labelPadding", "order": 50, "visible": true }
                ] }
              ]
            }
            """,
            "component.label" => $$"""
            ,
            {
              "id": "label",
              "label": "Label",
              "subtitle": "Centered text label dimensions and colors",
              "icon": "{{EditorIcons.Label}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "label", "label": "Label", "order": 10, "visible": true, "fields": [
                  { "id": "component.label.dimensionMode", "order": 10, "visible": true },
                  { "id": "component.label.size", "order": 20, "visible": true },
                  { "id": "component.label.padding", "order": 30, "visible": true },
                  { "id": "component.label.backgroundVisible", "order": 40, "visible": true },
                  { "id": "component.label.backgroundColorToken", "order": 50, "visible": true },
                  { "id": "component.label.textColorToken", "order": 60, "visible": true },
                  { "id": "component.label.textSize", "order": 70, "visible": true },
                  { "id": "component.label.textStyle", "order": 80, "visible": true }
                ] }
              ]
            }
            """,
            "component.audio" => $$"""
            ,
            {
              "id": "audio",
              "label": "Audio",
              "subtitle": "Audio bubble layout and waveform defaults",
              "icon": "{{EditorIcons.Audio}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "audio", "label": "Audio", "order": 10, "visible": true, "fields": [
                  { "id": "component.audio.size", "order": 10, "visible": true },
                  { "id": "component.audio.avatarPosition", "order": 20, "visible": true },
                  { "id": "component.audio.avatarSize", "order": 30, "visible": true },
                  { "id": "component.audio.textSize", "order": 40, "visible": true },
                  { "id": "component.audio.playColorToken", "order": 50, "visible": true },
                  { "id": "component.audio.waveformColorToken", "order": 60, "visible": true },
                  { "id": "component.audio.knobSize", "order": 70, "visible": true }
                ] }
              ]
            }
            """,
            "component.video" => $$"""
            ,
            {
              "id": "video",
              "label": "Video",
              "subtitle": "Video status bar and play overlay defaults",
              "icon": "{{EditorIcons.Video}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "video", "label": "Video", "order": 10, "visible": true, "fields": [
                  { "id": "component.video.statusVisible", "order": 10, "visible": true },
                  { "id": "component.video.statusHeight", "order": 20, "visible": true },
                  { "id": "component.video.statusIconSlots", "order": 30, "visible": true },
                  { "id": "component.video.playOverlayVisible", "order": 40, "visible": true },
                  { "id": "component.video.playColorToken", "order": 50, "visible": true }
                ] }
              ]
            }
            """,
            _ => "",
        };

        var style = $$"""
        ,
        {
          "id": "style",
          "label": "Style",
          "subtitle": "Shared surface style defaults",
          "icon": "{{EditorIcons.Style}}",
          "order": 90,
          "visible": true,
          "defaultOpen": false,
          "groups": [
            { "id": "style", "label": "Surface style", "order": 10, "visible": true, "fields": [
              { "id": "component.style.shadowEnabled", "order": 10, "visible": true },
              { "id": "component.style.reliefEnabled", "order": 20, "visible": true },
              { "id": "component.style.borderWidth", "order": 30, "visible": true },
              { "id": "component.style.borderColorToken", "order": 40, "visible": true },
              { "id": "component.style.cornerRadiusToken", "order": 50, "visible": true },
              { "id": "component.style.reliefAngle", "order": 60, "visible": true },
              { "id": "component.style.reliefExtent", "order": 70, "visible": true },
              { "id": "component.style.reliefSpread", "order": 80, "visible": true },
              { "id": "component.style.reliefTopIntensity", "order": 90, "visible": true },
              { "id": "component.style.reliefBottomIntensity", "order": 100, "visible": true }
            ] }
          ]
        }
        """;

        return typeSpecific + style;
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

    private static string FirstId(SqliteConnection connection, string table, string projectId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id FROM {table} WHERE project_id = $projectId ORDER BY name, id LIMIT 1";
        command.Parameters.AddWithValue("$projectId", projectId);
        return command.ExecuteScalar() as string ?? "";
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
            .Select((color) => new FieldOption(color.Token, color.Token, color.ValueHex, color.IsNeutral))
            .ToList();
    }

    public IReadOnlyDictionary<string, string> GetPaletteColorMap(string projectId)
    {
        using var connection = OpenConnection();
        return QueryPaletteColorRows(connection)
            .Where((color) => color.ProjectId == projectId)
            .GroupBy((color) => color.Token, StringComparer.Ordinal)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.First().ValueHex,
                StringComparer.Ordinal);
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

    public IReadOnlyList<FieldOption> GetIconThemeOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = QueryIconThemeRows(connection)
            .Where((theme) => theme.ProjectId == projectId)
            .OrderBy((theme) => theme.Name)
            .Select((theme) => new FieldOption(theme.Id, theme.Name))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    public IReadOnlyList<FieldOption> GetStatusBarOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = QueryStatusBarRows(connection)
            .Where((statusBar) => statusBar.ProjectId == projectId)
            .OrderBy((statusBar) => statusBar.Name)
            .Select((statusBar) => new FieldOption(statusBar.Id, statusBar.Name))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    public IReadOnlyList<FieldOption> GetNavigationBarOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = QueryNavigationBarRows(connection)
            .Where((navigationBar) => navigationBar.ProjectId == projectId)
            .OrderBy((navigationBar) => navigationBar.Name)
            .Select((navigationBar) => new FieldOption(navigationBar.Id, navigationBar.Name))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
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

    public IconThemeSettings GetIconThemeSettings(string iconThemeId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, asset_root, mapping_json, metadata_json FROM icon_themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", iconThemeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing icon theme '{iconThemeId}'.");
        }

        return new IconThemeSettings(
            reader.GetString(0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3));
    }

    public string GetIconThemeFieldValue(string iconThemeId, string fieldId)
    {
        var settings = GetIconThemeSettings(iconThemeId);
        return fieldId switch
        {
            "iconTheme.assetRoot" => settings.AssetRoot,
            "iconTheme.tokenCount" => IconThemeTokenCount(settings.MappingJson).ToString(),
            "iconTheme.metadata" => settings.MetadataJson,
            _ => throw new InvalidOperationException($"Unknown icon theme field '{fieldId}'."),
        };
    }

    public IReadOnlyList<IconThemeToken> GetIconThemeTokens(string iconThemeId)
    {
        var settings = GetIconThemeSettings(iconThemeId);
        return IconThemeTokens(settings.MappingJson);
    }

    public IReadOnlyList<FieldOption> GetIconTokenOptions(string projectId, string? currentToken = null)
    {
        using var connection = OpenConnection();
        var tokens = QueryIconThemeRows(connection)
            .Where((row) => row.ProjectId == projectId)
            .SelectMany((row) => IconThemeTokens(row.MappingJson).Select((token) => token.Token))
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(currentToken))
        {
            foreach (var token in currentToken.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                tokens.Add(token);
            }
        }

        return tokens
            .OrderBy((token) => token, StringComparer.Ordinal)
            .Select((token) => new FieldOption(token, token))
            .ToList();
    }

    public string ResolveIconThemeAssetPath(string iconThemeId, string file)
    {
        var settings = GetIconThemeSettings(iconThemeId);
        var projectId = ProjectIdForIconTheme(iconThemeId);
        var mediaRoot = ResolveProjectPath(GetProjectSettings(projectId).MediaRoot);
        return Path.Combine(mediaRoot, settings.AssetRoot, file);
    }

    public string? ResolveIconTokenAssetPath(string projectId, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var connection = OpenConnection();
        var mediaRoot = ResolveProjectPath(GetProjectSettings(projectId).MediaRoot);
        foreach (var row in QueryIconThemeRows(connection).Where((row) => row.ProjectId == projectId))
        {
            var icon = IconThemeTokens(row.MappingJson).FirstOrDefault((candidate) => candidate.Token == token);
            if (icon is null || string.IsNullOrWhiteSpace(icon.File)) continue;

            var path = Path.Combine(mediaRoot, row.AssetRoot, icon.File);
            if (File.Exists(path)) return path;
        }

        return null;
    }

    public IconThemeRefreshResult RefreshIconThemeSets(ProjectTreeNode iconThemesRoot)
    {
        if (iconThemesRoot.Kind != ProjectTreeNodeKind.IconThemesRoot)
        {
            throw new InvalidOperationException("Icon themes can only be refreshed from the Icon Themes root.");
        }

        var project = ProjectAncestor(iconThemesRoot);
        using var connection = OpenConnection();
        return RefreshIconThemeSets(connection, project.Id);
    }

    public IconThemeRefreshResult RefreshIconThemeSetsForTheme(string iconThemeId)
    {
        using var connection = OpenConnection();
        return RefreshIconThemeSets(connection, ProjectIdForIconTheme(connection, iconThemeId));
    }

    public void DeleteIconThemeToken(string iconThemeId, string token)
    {
        if (!ValidIconTokenRegex().IsMatch(token))
        {
            throw new InvalidOperationException("Icon token must be lower_snake_case.");
        }

        using var connection = OpenConnection();
        var projectId = ProjectIdForIconTheme(connection, iconThemeId);
        var rows = QueryIconThemeRows(connection).Where((row) => row.ProjectId == projectId).ToList();
        var mediaRoot = ResolveProjectPath(GetProjectSettings(projectId).MediaRoot);
        foreach (var row in rows)
        {
            var fullPath = Path.Combine(mediaRoot, row.AssetRoot, $"{token}.svg");
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        RefreshIconThemeSets(connection, projectId);
    }

    public IconThemeSearchResult SearchIconThemeSources(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new IconThemeSearchResult([], []);
        }

        var parsed = RunIconThemeScript([
            "--mode",
            "search",
            "--query",
            query.Trim(),
        ]);
        var root = parsed.AsObject();
        return new IconThemeSearchResult(
            IconThemeCandidates(root, "lucide"),
            IconThemeCandidates(root, "material"));
    }

    public IconThemeGenerateResult GenerateIconThemeToken(
        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource)
    {
        token = token.Trim();
        if (!ValidIconTokenRegex().IsMatch(token))
        {
            throw new InvalidOperationException("Icon token must be lower_snake_case.");
        }

        using var connection = OpenConnection();
        var projectId = ProjectIdForIconTheme(connection, iconThemeId);
        var mediaRoot = ResolveProjectPath(GetProjectSettings(projectId).MediaRoot);
        var rows = QueryIconThemeRows(connection).Where((row) => row.ProjectId == projectId).ToList();
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Refresh icon sets before generating tokens.");
        }

        var requestPath = Path.Combine(Path.GetTempPath(), $"mockups-icon-generate-{Guid.NewGuid():N}.json");
        var setsRoot = Path.GetDirectoryName(Path.Combine(mediaRoot, rows[0].AssetRoot)) ?? mediaRoot;
        var request = new JsonObject
        {
            ["token"] = token,
            ["category"] = string.IsNullOrWhiteSpace(category) ? IconTokenCategory(token) : category.Trim(),
            ["description"] = description.Trim(),
            ["iconThemesRoot"] = setsRoot,
            ["mediaRoot"] = mediaRoot,
            ["selectedSources"] = new JsonObject
            {
                ["lucide"] = lucideSource,
                ["material"] = materialSource,
            },
            ["sets"] = new JsonArray(rows.Select((row) => new JsonObject
            {
                ["id"] = row.Id,
                ["name"] = Path.GetFileName(row.AssetRoot),
                ["path"] = Path.Combine(mediaRoot, row.AssetRoot),
                ["iconSet"] = IconSetDefinition(row),
            }).ToArray<JsonNode?>()),
        };
        File.WriteAllText(requestPath, request.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var parsed = RunIconThemeScript([
            "--mode",
            "generate",
            "--request",
            requestPath,
        ]);
        var refresh = RefreshIconThemeSets(connection, projectId);
        UpdateIconThemeTokenMetadata(connection, projectId, token, category, description, lucideSource, materialSource);
        return new IconThemeGenerateResult(token, JsonInt(parsed, ["writtenFileCount"], rows.Count), refresh);
    }

    public StatusBarSettings GetStatusBarSettings(string statusBarId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, family, config_json, metadata_json FROM status_bars WHERE id = $id";
        command.Parameters.AddWithValue("$id", statusBarId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing status bar '{statusBarId}'.");
        }

        return new StatusBarSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4));
    }

    public string GetStatusBarFieldValue(string statusBarId, string fieldId)
    {
        var settings = GetStatusBarSettings(statusBarId);
        var config = StatusBarConfig(settings.ConfigJson);
        return fieldId switch
        {
            "statusBar.family" => settings.Family,
            "statusBar.layout.height" => JsonNumberString(config, ["layout", "height"], "54"),
            "statusBar.layout.itemSize" => JsonNumberString(config, ["layout", "itemSize"], "18"),
            "statusBar.layout.gap" => JsonNumberString(config, ["layout", "gap"], "6"),
            "statusBar.layout.sidePadding" => JsonNumberString(config, ["layout", "sidePadding"], "24"),
            _ => "",
        };
    }

    public IReadOnlyList<StatusBarItem> GetStatusBarItems(string statusBarId)
    {
        return StatusBarItems(StatusBarConfig(GetStatusBarSettings(statusBarId).ConfigJson));
    }

    public void UpdateStatusBarField(string statusBarId, string fieldId, string value)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (fieldId == "statusBar.family")
            {
                Execute(connection, "UPDATE status_bars SET family = $family WHERE id = $id", ("$id", statusBarId), ("$family", value));
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM status_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", statusBarId);
            var config = StatusBarConfig(command.ExecuteScalar() as string ?? "{}");
            var nextValue = int.TryParse(value, out var parsed) ? parsed : 0;
            switch (fieldId)
            {
                case "statusBar.layout.height":
                    SetJsonNumber(config, ["layout", "height"], nextValue);
                    break;
                case "statusBar.layout.itemSize":
                    SetJsonNumber(config, ["layout", "itemSize"], nextValue);
                    break;
                case "statusBar.layout.gap":
                    SetJsonNumber(config, ["layout", "gap"], nextValue);
                    break;
                case "statusBar.layout.sidePadding":
                    SetJsonNumber(config, ["layout", "sidePadding"], nextValue);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown status bar field '{fieldId}'.");
            }

            Execute(
                connection,
                "UPDATE status_bars SET config_json = $configJson WHERE id = $id",
                ("$id", statusBarId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public void UpdateStatusBarItem(string statusBarId, int index, StatusBarItem patch)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM status_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", statusBarId);
            var config = StatusBarConfig(command.ExecuteScalar() as string ?? "{}");
            var items = config["items"] as JsonArray ?? new JsonArray();
            while (items.Count <= index)
            {
                items.Add(JsonSerializer.SerializeToNode(DefaultStatusBarItems().ElementAtOrDefault(items.Count) ?? DefaultStatusBarItems()[0])!);
            }

            items[index] = StatusBarItemToJson(patch);
            config["items"] = items;
            Execute(
                connection,
                "UPDATE status_bars SET config_json = $configJson WHERE id = $id",
                ("$id", statusBarId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public NavigationBarSettings GetNavigationBarSettings(string navigationBarId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, family, config_json, metadata_json FROM navigation_bars WHERE id = $id";
        command.Parameters.AddWithValue("$id", navigationBarId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing navigation bar '{navigationBarId}'.");
        }

        return new NavigationBarSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4));
    }

    public string GetNavigationBarFieldValue(string navigationBarId, string fieldId)
    {
        var settings = GetNavigationBarSettings(navigationBarId);
        var config = NavigationBarConfig(settings.ConfigJson);
        return fieldId switch
        {
            "navigationBar.family" => settings.Family,
            "navigationBar.type" => JsonString(config, ["type"]) is { Length: > 0 } type ? type : "buttons",
            "navigationBar.layout.height" => JsonNumberString(config, ["layout", "height"], "34"),
            "navigationBar.layout.itemSize" => JsonNumberString(config, ["layout", "itemSize"], "18"),
            "navigationBar.layout.sidePadding" => JsonNumberString(config, ["layout", "sidePadding"], "40"),
            "navigationBar.layout.strokeWidth" => JsonNumberString(config, ["layout", "strokeWidth"], "2"),
            "navigationBar.layout.cornerRadius" => JsonNumberString(config, ["layout", "cornerRadius"], "3"),
            "navigationBar.layout.filled" => BoolToString(JsonBool(config, ["layout", "filled"])),
            "navigationBar.gesture.width" => JsonNumberString(config, ["gesture", "width"], "134"),
            "navigationBar.gesture.height" => JsonNumberString(config, ["gesture", "height"], "5"),
            "navigationBar.gesture.cornerRadius" => JsonNumberString(config, ["gesture", "cornerRadius"], "3"),
            _ => "",
        };
    }

    public IReadOnlyList<NavigationBarItem> GetNavigationBarItems(string navigationBarId)
    {
        return NavigationBarItems(NavigationBarConfig(GetNavigationBarSettings(navigationBarId).ConfigJson));
    }

    public void UpdateNavigationBarField(string navigationBarId, string fieldId, string value)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (fieldId == "navigationBar.family")
            {
                Execute(connection, "UPDATE navigation_bars SET family = $family WHERE id = $id", ("$id", navigationBarId), ("$family", value));
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM navigation_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", navigationBarId);
            var config = NavigationBarConfig(command.ExecuteScalar() as string ?? "{}");
            switch (fieldId)
            {
                case "navigationBar.type":
                    var type = value is "gestureBar" ? "gestureBar" : "buttons";
                    SetJsonValue(config, ["type"], JsonValue.Create(type)!);
                    break;
                case "navigationBar.layout.height":
                    SetJsonNumber(config, ["layout", "height"], int.TryParse(value, out var height) ? height : 0);
                    break;
                case "navigationBar.layout.itemSize":
                    SetJsonNumber(config, ["layout", "itemSize"], int.TryParse(value, out var itemSize) ? itemSize : 0);
                    break;
                case "navigationBar.layout.sidePadding":
                    SetJsonNumber(config, ["layout", "sidePadding"], int.TryParse(value, out var sidePadding) ? sidePadding : 0);
                    break;
                case "navigationBar.layout.strokeWidth":
                    SetJsonValue(config, ["layout", "strokeWidth"], NumberNode(value));
                    break;
                case "navigationBar.layout.cornerRadius":
                    SetJsonNumber(config, ["layout", "cornerRadius"], int.TryParse(value, out var cornerRadius) ? cornerRadius : 0);
                    break;
                case "navigationBar.layout.filled":
                    SetJsonValue(config, ["layout", "filled"], JsonValue.Create(value.Equals("true", StringComparison.OrdinalIgnoreCase))!);
                    break;
                case "navigationBar.gesture.width":
                    SetJsonNumber(config, ["gesture", "width"], int.TryParse(value, out var gestureWidth) ? gestureWidth : 0);
                    break;
                case "navigationBar.gesture.height":
                    SetJsonNumber(config, ["gesture", "height"], int.TryParse(value, out var gestureHeight) ? gestureHeight : 0);
                    break;
                case "navigationBar.gesture.cornerRadius":
                    SetJsonNumber(config, ["gesture", "cornerRadius"], int.TryParse(value, out var gestureCornerRadius) ? gestureCornerRadius : 0);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation bar field '{fieldId}'.");
            }

            Execute(
                connection,
                "UPDATE navigation_bars SET config_json = $configJson WHERE id = $id",
                ("$id", navigationBarId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public void UpdateNavigationBarItem(string navigationBarId, int index, NavigationBarItem patch)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM navigation_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", navigationBarId);
            var config = NavigationBarConfig(command.ExecuteScalar() as string ?? "{}");
            var items = config["items"] as JsonArray ?? new JsonArray();
            while (items.Count <= index)
            {
                items.Add(NavigationBarItemToJson(DefaultNavigationBarItems().ElementAtOrDefault(items.Count) ?? DefaultNavigationBarItems()[0]));
            }

            items[index] = NavigationBarItemToJson(patch);
            config["items"] = items;
            Execute(
                connection,
                "UPDATE navigation_bars SET config_json = $configJson WHERE id = $id",
                ("$id", navigationBarId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public ComponentClassSettings GetComponentClassSettings(string componentClassId)
    {
        using var connection = OpenConnection();
        return GetComponentClassSettings(connection, componentClassId);
    }

    private static ComponentClassSettings GetComponentClassSettings(SqliteConnection connection, string componentClassId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json
            FROM component_classes
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", componentClassId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing component class '{componentClassId}'.");
        }

        return new ComponentClassSettings(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7));
    }

    public FieldValue CreateComponentClassFieldValue(string componentClassId, string fieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var value = fieldId == "component.type"
            ? ComponentTypeLabel(settings.ComponentType)
            : ComponentConfigFieldValue(settings.ConfigJson, descriptor);

        return new FieldValue(
            new FieldDefinition(
                descriptor.Id,
                descriptor.Label,
                descriptor.ValueKind,
                descriptor.IsEditable,
                descriptor.DefaultValue,
                Options: descriptor.Options,
                PairLabels: descriptor.PairLabels),
            value);
    }

    public void UpdateComponentClassField(string componentClassId, string fieldId, string value)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        if (!descriptor.IsEditable || descriptor.JsonPath.Length == 0)
        {
            return;
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
            SetJsonValue(config, descriptor.JsonPath, ComponentConfigJsonValue(descriptor.ValueKind, value));
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    private static string ComponentConfigFieldValue(string configJson, ComponentClassFieldDescriptor descriptor)
    {
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var node = GetJsonValue(config, descriptor.JsonPath);
        if (node is null)
        {
            return descriptor.DefaultValue;
        }

        return descriptor.ValueKind switch
        {
            ValueKind.Boolean => BoolToString(node is JsonValue value && value.TryGetValue<bool>(out var boolean) && boolean),
            ValueKind.Integer => JsonNumberString(config, descriptor.JsonPath, descriptor.DefaultValue),
            ValueKind.IntegerPair => node is JsonValue pairValue && pairValue.TryGetValue<string>(out var pairText)
                ? pairText
                : descriptor.DefaultValue,
            ValueKind.IconSlots => node.ToJsonString(),
            _ => node is JsonValue stringValue && stringValue.TryGetValue<string>(out var text)
                ? text
                : node.ToJsonString().Trim('"'),
        };
    }

    private static JsonNode ComponentConfigJsonValue(ValueKind valueKind, string value)
    {
        return valueKind switch
        {
            ValueKind.Boolean => JsonValue.Create(StringToBool(value))!,
            ValueKind.Integer => NumberNode(value),
            ValueKind.IconSlots => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? ComponentClassFieldCatalog.EmptyIconSlots : value)
                ?? JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots)!,
            _ => JsonValue.Create(value)!,
        };
    }

    public DevicePreviewMetrics GetDevicePreviewMetrics(string deviceId)
    {
        var settings = GetDeviceSettings(deviceId);
        var metrics = ParseJsonObject(settings.MetricsJson);
        var canvasWidth = JsonNumberDouble(metrics, ["canvas", "width"], JsonNumberDouble(metrics, ["renderSize", "width"], 1080));
        var canvasHeight = JsonNumberDouble(metrics, ["canvas", "height"], JsonNumberDouble(metrics, ["renderSize", "height"], 1920));
        var screenX = JsonNumberDouble(metrics, ["screen", "x"], 0);
        var screenY = JsonNumberDouble(metrics, ["screen", "y"], 0);
        var screenWidth = JsonNumberDouble(metrics, ["screen", "width"], canvasWidth);
        var screenHeight = JsonNumberDouble(metrics, ["screen", "height"], canvasHeight);
        var cornerRadius = JsonNumberDouble(metrics, ["cornerRadius"], 0);
        var statusBarHeight = JsonNumberDouble(metrics, ["statusBar", "height"], JsonNumberDouble(metrics, ["safeArea", "top"], 0));
        var safeAreaBottom = JsonNumberDouble(metrics, ["safeArea", "bottom"], 0);
        var scaleToPixels = JsonNumberDouble(metrics, ["scaleToPixels"], 0);
        if (scaleToPixels <= 0)
        {
            var renderWidth = JsonNumberDouble(metrics, ["renderSize", "width"], canvasWidth);
            var designWidth = JsonNumberDouble(metrics, ["designSpace", "width"], 0);
            scaleToPixels = designWidth > 0 ? renderWidth / designWidth : JsonNumberDouble(metrics, ["pixelRatio"], 1);
        }

        return new DevicePreviewMetrics(
            settings.Name,
            canvasWidth,
            canvasHeight,
            screenX,
            screenY,
            screenWidth,
            screenHeight,
            cornerRadius,
            statusBarHeight,
            safeAreaBottom,
            scaleToPixels);
    }

    public IReadOnlyList<FieldOption> GetThemeOptions(string projectId)
    {
        using var connection = OpenConnection();
        return QueryThemeRows(connection)
            .Where((theme) => theme.ProjectId == projectId)
            .OrderBy((theme) => theme.Name)
            .Select((theme) => new FieldOption(theme.Id, theme.Name))
            .ToList();
    }

    public IReadOnlyList<ThemeTokenOption> GetThemeTokenOptions(string projectId, string themeId)
    {
        using var connection = OpenConnection();
        var theme = QueryThemeRows(connection)
            .Where((row) => row.ProjectId == projectId)
            .FirstOrDefault((row) => row.Id == themeId)
            ?? QueryThemeRows(connection).FirstOrDefault((row) => row.ProjectId == projectId)
            ?? throw new InvalidOperationException($"No themes available for project '{projectId}'.");
        var tokens = ParseJsonObject(string.IsNullOrWhiteSpace(theme.TokensJson) ? "{}" : theme.TokensJson);
        var palette = QueryPaletteColorRows(connection)
            .Where((color) => color.ProjectId == projectId)
            .ToDictionary((color) => color.Token, (color) => color.ValueHex, StringComparer.Ordinal);

        var options = new List<ThemeTokenOption>();
        foreach (var pair in ThemeColorPairPaths.OrderBy((pair) => pair.Key, StringComparer.Ordinal))
        {
            var lightToken = JsonString(tokens, pair.Value.Light);
            var darkToken = JsonString(tokens, pair.Value.Dark);
            options.Add(new ThemeTokenOption(
                pair.Key,
                pair.Key.Replace("theme.", "", StringComparison.Ordinal),
                "color",
                $"{lightToken} / {darkToken}",
                PaletteHex(palette, lightToken),
                PaletteHex(palette, darkToken)));
        }

        foreach (var option in NumericThemeTokenOptions(tokens))
        {
            options.Add(option);
        }

        return options
            .OrderBy((option) => option.Token, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<ThemeTokenOption> NumericThemeTokenOptions(JsonObject tokens)
    {
        foreach (var (token, path) in new (string Token, string[] Path)[]
        {
            ("theme.cursor.width", ["cursor", "width"]),
            ("theme.cursor.blinkFrames", ["cursor", "blinkFrames"]),
            ("theme.typography.size", ["typography", "size"]),
            ("theme.typography.weight", ["typography", "weight"]),
            ("theme.radii.control", ["radii", "control"]),
            ("theme.radii.card", ["radii", "card"]),
            ("theme.radii.panel", ["radii", "panel"]),
            ("theme.radii.surface", ["radii", "surface"]),
            ("theme.radii.pill", ["radii", "pill"]),
            ("theme.radii.avatar", ["radii", "avatar"]),
            ("theme.radii.full", ["radii", "full"]),
        })
        {
            yield return new ThemeTokenOption(
                token,
                token.Replace("theme.", "", StringComparison.Ordinal),
                "number",
                JsonNumberString(tokens, path, "—"),
                null,
                null);
        }
    }

    private static string? PaletteHex(IReadOnlyDictionary<string, string> palette, string token)
    {
        return palette.TryGetValue(token, out var hex) ? hex : null;
    }

    public ThemeSettings GetThemeSettings(string themeId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json FROM themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", themeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing theme '{themeId}'.");
        }

        return new ThemeSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7));
    }

    public string GetThemeFieldValue(string themeId, string fieldId)
    {
        var settings = GetThemeSettings(themeId);
        var tokens = ParseJsonObject(string.IsNullOrWhiteSpace(settings.TokensJson) ? "{}" : settings.TokensJson);
        if (ThemeColorPairPaths.TryGetValue(fieldId, out var colorPairPaths))
        {
            return $"{JsonString(tokens, colorPairPaths.Light)}|{JsonString(tokens, colorPairPaths.Dark)}";
        }

        return fieldId switch
        {
            "theme.family" => settings.Family,
            "theme.iconThemeId" => settings.IconThemeId,
            "theme.statusBarId" => settings.StatusBarId,
            "theme.navigationBarId" => settings.NavigationBarId,
            "theme.defaultMode" => JsonString(tokens, ["defaultMode"]) is { Length: > 0 } mode ? mode : "light",
            "theme.neutralTint.hueDeg" => JsonNumberString(tokens, ["neutralTint", "hueDeg"]),
            "theme.neutralTint.saturation" => JsonNumberString(tokens, ["neutralTint", "saturation"]),
            "theme.cursor.width" => JsonNumberString(tokens, ["cursor", "width"]),
            "theme.cursor.blinkFrames" => JsonNumberString(tokens, ["cursor", "blinkFrames"]),
            "theme.typography.fontFamilyId" => JsonString(tokens, ["typography", "fontFamilyId"]),
            "theme.typography.emojiFontFamilyId" => JsonString(tokens, ["typography", "emojiFontFamilyId"]),
            "theme.typography.size" => JsonNumberString(tokens, ["typography", "size"]),
            "theme.typography.weight" => JsonNumberString(tokens, ["typography", "weight"]),
            "theme.typography.style" => JsonString(tokens, ["typography", "style"]) is { Length: > 0 } style ? style : "normal",
            _ => throw new InvalidOperationException($"Unknown theme field '{fieldId}'."),
        };
    }

    public void UpdateThemeField(string themeId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "theme.family":
                Execute(connection, "UPDATE themes SET family = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            case "theme.iconThemeId":
                Execute(connection, "UPDATE themes SET icon_theme_id = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            case "theme.statusBarId":
                Execute(connection, "UPDATE themes SET status_bar_id = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            case "theme.navigationBarId":
                Execute(connection, "UPDATE themes SET navigation_bar_id = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            default:
                UpdateThemeToken(connection, themeId, fieldId, value);
                return;
        }
    }

    private static void UpdateThemeToken(SqliteConnection connection, string themeId, string fieldId, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT tokens_json FROM themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", themeId);
        var tokens = ParseJsonObject(command.ExecuteScalar() as string ?? "{}");

        if (ThemeColorPairPaths.TryGetValue(fieldId, out var colorPairPaths))
        {
            SetPair(tokens, value, colorPairPaths.Light, colorPairPaths.Dark, asNumber: false);
            Execute(connection, "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id", ("$id", themeId), ("$tokensJson", tokens.ToJsonString()));
            return;
        }

        switch (fieldId)
        {
            case "theme.defaultMode":
                SetJsonValue(tokens, ["defaultMode"], JsonValue.Create(value)!);
                break;
            case "theme.neutralTint.hueDeg":
                SetJsonValue(tokens, ["neutralTint", "hueDeg"], NumberNode(value));
                break;
            case "theme.neutralTint.saturation":
                SetJsonValue(tokens, ["neutralTint", "saturation"], NumberNode(value));
                break;
            case "theme.cursor.width":
                SetJsonValue(tokens, ["cursor", "width"], NumberNode(value));
                break;
            case "theme.cursor.blinkFrames":
                SetJsonValue(tokens, ["cursor", "blinkFrames"], NumberNode(value));
                break;
            case "theme.typography.fontFamilyId":
                SetJsonValue(tokens, ["typography", "fontFamilyId"], JsonValue.Create(value)!);
                break;
            case "theme.typography.emojiFontFamilyId":
                SetJsonValue(tokens, ["typography", "emojiFontFamilyId"], JsonValue.Create(value)!);
                break;
            case "theme.typography.size":
                SetJsonValue(tokens, ["typography", "size"], NumberNode(value));
                break;
            case "theme.typography.weight":
                SetJsonValue(tokens, ["typography", "weight"], NumberNode(value));
                break;
            case "theme.typography.style":
                SetJsonValue(tokens, ["typography", "style"], JsonValue.Create(value)!);
                break;
            default:
                throw new InvalidOperationException($"Unknown theme field '{fieldId}'.");
        }

        Execute(connection, "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id", ("$id", themeId), ("$tokensJson", tokens.ToJsonString()));
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

    private static List<ThemeRow> QueryThemeRows(SqliteConnection connection)
    {
        var rows = new List<ThemeRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json FROM themes ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ThemeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5),
                ReadString(reader, 6),
                ReadString(reader, 7),
                ReadString(reader, 8)));
        }

        return rows;
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

    private static List<IconThemeRow> QueryIconThemeRows(SqliteConnection connection)
    {
        var rows = new List<IconThemeRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, asset_root, mapping_json, metadata_json FROM icon_themes ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new IconThemeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5)));
        }

        return rows;
    }

    private static List<StatusBarRow> QueryStatusBarRows(SqliteConnection connection)
    {
        var rows = new List<StatusBarRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, family, config_json, metadata_json FROM status_bars ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new StatusBarRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5)));
        }

        return rows;
    }

    private static List<NavigationBarRow> QueryNavigationBarRows(SqliteConnection connection)
    {
        var rows = new List<NavigationBarRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, family, config_json, metadata_json FROM navigation_bars ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new NavigationBarRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5)));
        }

        return rows;
    }

    private static List<ComponentClassRow> QueryComponentClassRows(SqliteConnection connection)
    {
        var rows = new List<ComponentClassRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, project_id, component_type, record_class_id, name, notes, config_json, design_preview_json, metadata_json
            FROM component_classes
            ORDER BY component_type, name
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ComponentClassRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                ReadString(reader, 5),
                ReadString(reader, 6),
                ReadString(reader, 7),
                ReadString(reader, 8)));
        }

        return rows;
    }

    public ActorSettings GetActorSettings(string actorId)
    {
        using var connection = OpenConnection();
        return GetActorSettings(connection, actorId);
    }

    private static ActorSettings GetActorSettings(SqliteConnection connection, string actorId)
    {
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

        var settings = GetActorSettings(connection, actorId);
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
        return GetDeviceSettings(connection, deviceId);
    }

    private static DeviceSettings GetDeviceSettings(SqliteConnection connection, string deviceId)
    {
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

        var settings = GetDeviceSettings(connection, deviceId);
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
        try
        {
            return JsonNode.Parse(filesJson)?.AsArray().Count ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string ThemeReferenceSummary(ThemeRow theme)
    {
        return ThemeReferenceSummary(theme.IconThemeId, theme.StatusBarId, theme.NavigationBarId);
    }

    private static string ThemeReferenceSummary(string iconThemeId, string statusBarId, string navigationBarId)
    {
        var linkedCount = new[] { iconThemeId, statusBarId, navigationBarId }.Count((value) => !string.IsNullOrWhiteSpace(value));
        return $"{linkedCount}/3 refs";
    }

    private static string ProductionFontFilesSummary(string filesJson)
    {
        try
        {
            var files = JsonNode.Parse(filesJson)?.AsArray();
            if (files is null || files.Count == 0) return "No copied font files.";

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
        catch (JsonException)
        {
            return filesJson;
        }
    }

    private static string JsonNodeString(JsonObject node, string key)
    {
        if (!node.TryGetPropertyValue(key, out var value) || value is null) return "";
        return value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : value.ToJsonString();
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

    private static string Slug(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "font" : slug;
    }

    private static string ResolveProjectPath(string path)
    {
        if (Path.IsPathFullyQualified(path)) return path;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", path));
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static void DeleteProductionFontFiles(SqliteConnection connection, string fontId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT projects.media_root, production_fonts.source_directory
            FROM production_fonts
            INNER JOIN projects ON projects.id = production_fonts.project_id
            WHERE production_fonts.id = $id
            """;
        command.Parameters.AddWithValue("$id", fontId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return;

        var mediaRoot = ResolveProjectPath(ReadString(reader, 0));
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

    private static IconThemeRefreshResult RefreshIconThemeSets(SqliteConnection connection, string projectId)
    {
        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, projectId).MediaRoot);
        var iconThemesRoot = Path.Combine(mediaRoot, "icon-themes");
        Directory.CreateDirectory(iconThemesRoot);

        var setDirectories = Directory
            .EnumerateDirectories(iconThemesRoot)
            .Where((directory) => !Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal))
            .Where((directory) => !Path.GetFileName(directory).StartsWith("_", StringComparison.Ordinal))
            .OrderBy(Path.GetFileName)
            .ToList();

        foreach (var directory in setDirectories)
        {
            var setName = Path.GetFileName(directory);
            var id = $"icon_theme_{projectId}_{Slug(setName)}";
            var assetRoot = NormalizeRelativePath(Path.GetRelativePath(mediaRoot, directory));
            var metadata = IconThemeMetadata(directory, setName);
            Execute(
                connection,
                """
                INSERT INTO icon_themes (id, project_id, name, asset_root, mapping_json, metadata_json)
                VALUES ($id, $projectId, $name, $assetRoot, '{}', $metadataJson)
                ON CONFLICT(project_id, name) DO UPDATE SET
                  asset_root = excluded.asset_root,
                  metadata_json = excluded.metadata_json
                """,
                ("$id", id),
                ("$projectId", projectId),
                ("$name", setName),
                ("$assetRoot", assetRoot),
                ("$metadataJson", metadata.ToJsonString()));
        }

        var rows = QueryIconThemeRows(connection).Where((row) => row.ProjectId == projectId).ToList();
        var tokensBySet = rows.ToDictionary(
            (row) => row.Id,
            (row) => SvgTokenSet(Path.Combine(mediaRoot, row.AssetRoot)));
        var commonTokens = tokensBySet.Values.FirstOrDefault()?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var setTokens in tokensBySet.Values.Skip(1))
        {
            commonTokens.IntersectWith(setTokens);
        }

        var allTokens = tokensBySet.Values.SelectMany((set) => set).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var nextMapping = BuildIconThemeMapping(row.MappingJson, commonTokens);
            Execute(
                connection,
                "UPDATE icon_themes SET mapping_json = $mappingJson WHERE id = $id",
                ("$id", row.Id),
                ("$mappingJson", nextMapping.ToJsonString()));
        }

        return new IconThemeRefreshResult(rows.Count, commonTokens.Count, Math.Max(0, allTokens.Count - commonTokens.Count));
    }

    private static ProjectSettings GetProjectSettings(SqliteConnection connection, string projectId)
    {
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

    private static HashSet<string> SvgTokenSet(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory
            .EnumerateFiles(directory, "*.svg", SearchOption.TopDirectoryOnly)
            .Select((file) => Path.GetFileNameWithoutExtension(file))
            .Where((token) => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonObject BuildIconThemeMapping(string currentMappingJson, HashSet<string> commonTokens)
    {
        var current = ParseJsonObject(string.IsNullOrWhiteSpace(currentMappingJson) ? "{}" : currentMappingJson);
        var currentTokens = current["tokens"] as JsonObject ?? [];
        var nextTokens = new JsonObject();
        var categories = new SortedDictionary<string, JsonArray>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in commonTokens.OrderBy((token) => token, StringComparer.OrdinalIgnoreCase))
        {
            var existing = currentTokens[token] as JsonObject ?? [];
            var category = JsonString(existing, ["category"]);
            if (string.IsNullOrWhiteSpace(category)) category = IconTokenCategory(token);
            nextTokens[token] = new JsonObject
            {
                ["category"] = category,
                ["file"] = $"{token}.svg",
                ["description"] = JsonString(existing, ["description"]),
            };
            if (!categories.TryGetValue(category, out var categoryTokens))
            {
                categoryTokens = [];
                categories[category] = categoryTokens;
            }

            categoryTokens.Add(token);
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["tokens"] = nextTokens,
            ["categories"] = new JsonObject(categories.Select((pair) => KeyValuePair.Create<string, JsonNode?>(pair.Key, pair.Value))),
        };
    }

    private static IReadOnlyList<IconThemeToken> IconThemeTokens(string mappingJson)
    {
        var mapping = ParseJsonObject(string.IsNullOrWhiteSpace(mappingJson) ? "{}" : mappingJson);
        var tokens = mapping["tokens"] as JsonObject;
        if (tokens is null) return [];

        return tokens
            .OrderBy((pair) => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select((pair) =>
            {
                var tokenObject = pair.Value as JsonObject ?? [];
                return new IconThemeToken(
                    pair.Key,
                    JsonString(tokenObject, ["category"]),
                    JsonString(tokenObject, ["file"]),
                    JsonString(tokenObject, ["description"]));
            })
            .ToList();
    }

    private static int IconThemeTokenCount(string mappingJson)
    {
        return IconThemeTokens(mappingJson).Count;
    }

    private static string IconTokenCategory(string token)
    {
        var index = token.IndexOf('_', StringComparison.Ordinal);
        return index <= 0 ? "misc" : token[..index];
    }

    private static string ProjectIdForIconTheme(SqliteConnection connection, string iconThemeId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id FROM icon_themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", iconThemeId);
        return command.ExecuteScalar() as string
            ?? throw new InvalidOperationException($"Missing icon theme '{iconThemeId}'.");
    }

    private string ProjectIdForIconTheme(string iconThemeId)
    {
        using var connection = OpenConnection();
        return ProjectIdForIconTheme(connection, iconThemeId);
    }

    private static JsonObject IconThemeMetadata(string directory, string setName)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        var metadata = new JsonObject
        {
            ["iconSet"] = IconSetDefinitionFromName(setName),
        };
        if (!File.Exists(manifestPath)) return metadata;

        try
        {
            var manifest = ParseJsonObject(File.ReadAllText(manifestPath));
            metadata["manifest"] = manifest.DeepClone();
            metadata["iconSet"] = IconSetDefinition(manifest, setName);
        }
        catch (JsonException)
        {
            // A malformed manifest should not block refreshing SVG tokens.
        }

        return metadata;
    }

    private static JsonObject IconSetDefinition(IconThemeRow row)
    {
        var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(row.MetadataJson) ? "{}" : row.MetadataJson);
        return metadata["iconSet"] is JsonObject iconSet
            ? (JsonObject)iconSet.DeepClone()
            : IconSetDefinitionFromName(row.Name);
    }

    private static JsonObject IconSetDefinition(JsonObject manifest, string fallbackName)
    {
        var source = JsonString(manifest, ["source"]);
        var style = JsonString(manifest, ["style"]);
        var weight = JsonNumberString(manifest, ["weight"]);
        var manifestSetName = JsonString(manifest, ["name"]);
        if (string.IsNullOrWhiteSpace(manifestSetName))
        {
            manifestSetName = fallbackName;
        }

        if (source.Contains("lucide", StringComparison.OrdinalIgnoreCase) || style.Equals("lucide", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["provider"] = "lucide",
                ["setName"] = manifestSetName,
                ["package"] = string.IsNullOrWhiteSpace(source) ? "lucide-static" : source,
                ["stroke"] = JsonNumberDouble(manifest, ["stroke"], 2),
                ["fillMode"] = "stroke",
            };
        }

        return new JsonObject
        {
            ["provider"] = "material",
            ["setName"] = manifestSetName,
            ["package"] = string.IsNullOrWhiteSpace(source) ? "material-symbols" : source,
            ["style"] = string.IsNullOrWhiteSpace(style) ? "rounded" : style,
            ["weight"] = int.TryParse(weight, out var parsedWeight) && parsedWeight > 0 ? parsedWeight : 400,
            ["fillMode"] = "filled",
        };
    }

    private static JsonObject IconSetDefinitionFromName(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("lucide") || lower.Contains("lucida"))
        {
            return new JsonObject
            {
                ["provider"] = "lucide",
                ["setName"] = name,
                ["package"] = "lucide-static",
                ["stroke"] = 2,
                ["fillMode"] = "stroke",
            };
        }

        var style = lower.Contains("outlined") ? "outlined" : lower.Contains("sharp") ? "sharp" : "rounded";
        var weightMatch = Regex.Match(lower, "(100|200|300|400|500|600|700)");
        return new JsonObject
        {
            ["provider"] = "material",
            ["setName"] = name,
            ["package"] = "@material-symbols/svg-400",
            ["style"] = style,
            ["weight"] = weightMatch.Success ? int.Parse(weightMatch.Value) : 400,
            ["fillMode"] = "filled",
        };
    }

    private static IReadOnlyList<IconThemeSearchCandidate> IconThemeCandidates(JsonObject root, string provider)
    {
        if (root[provider] is not JsonArray array) return [];
        return array
            .OfType<JsonObject>()
            .Select((entry) => new IconThemeSearchCandidate(
                provider,
                JsonString(entry, ["sourceName"]),
                JsonString(entry, ["previewUrl"])))
            .Where((entry) => !string.IsNullOrWhiteSpace(entry.SourceName))
            .ToList();
    }

    private static JsonNode RunIconThemeScript(string[] arguments)
    {
        var scriptCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "icon-themes", "sync-icon-theme-token.cjs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "scripts", "icon-themes", "sync-icon-theme-token.cjs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "icon-themes", "sync-icon-theme-token.cjs"),
        }
            .Select(Path.GetFullPath)
            .ToList();
        var scriptPath = scriptCandidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException($"Icon theme script not found. Checked: {string.Join(", ", scriptCandidates)}");
        var workingDirectory = Directory.GetParent(scriptPath)?.Parent?.Parent?.FullName ?? AppContext.BaseDirectory;
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start icon theme script.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "Icon theme script failed." : stderr.Trim());
        }

        return JsonNode.Parse(stdout) ?? new JsonObject();
    }

    private static int JsonInt(JsonNode node, IReadOnlyList<string> path, int fallback)
    {
        if (node is not JsonObject root) return fallback;
        var value = GetJsonValue(root, path);
        return value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var parsed) ? parsed : fallback;
    }

    private static void UpdateIconThemeTokenMetadata(
        SqliteConnection connection,
        string projectId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource)
    {
        var rows = QueryIconThemeRows(connection).Where((row) => row.ProjectId == projectId).ToList();
        foreach (var row in rows)
        {
            var mapping = ParseJsonObject(row.MappingJson);
            var tokens = mapping["tokens"] as JsonObject ?? [];
            var tokenObject = tokens[token] as JsonObject ?? [];
            tokenObject["category"] = string.IsNullOrWhiteSpace(category) ? IconTokenCategory(token) : category.Trim();
            tokenObject["description"] = description.Trim();
            tokenObject["file"] = $"{token}.svg";
            tokenObject["sources"] = new JsonObject
            {
                ["lucide"] = lucideSource,
                ["material"] = materialSource,
            };
            tokens[token] = tokenObject;
            mapping["tokens"] = tokens;
            Execute(
                connection,
                "UPDATE icon_themes SET mapping_json = $mappingJson WHERE id = $id",
                ("$id", row.Id),
                ("$mappingJson", mapping.ToJsonString()));
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)*$")]
    private static partial Regex ValidIconTokenRegex();

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

    private static bool MergeMissing(JsonObject target, JsonObject defaults)
    {
        var changed = false;
        foreach (var pair in defaults)
        {
            if (!target.TryGetPropertyValue(pair.Key, out var existing) || existing is null)
            {
                target[pair.Key] = pair.Value?.DeepClone();
                changed = true;
                continue;
            }

            if (existing is JsonObject existingObject && pair.Value is JsonObject defaultObject)
            {
                changed |= MergeMissing(existingObject, defaultObject);
            }
        }

        return changed;
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

    private static string JsonNumberString(JsonObject root, IReadOnlyList<string> path, string fallback)
    {
        var value = JsonNumberString(root, path);
        return value == "0" && GetJsonValue(root, path) is null ? fallback : value;
    }

    private static double JsonNumberDouble(JsonObject root, IReadOnlyList<string> path, double fallback)
    {
        var node = GetJsonValue(root, path);
        if (node is null) return fallback;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var number)) return number;
            if (value.TryGetValue<string>(out var text) && double.TryParse(text, out var parsed)) return parsed;
        }

        return fallback;
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

    private static void SetJsonNumber(JsonObject root, IReadOnlyList<string> path, int value)
    {
        SetJsonValue(root, path, JsonValue.Create(value)!);
    }

    private static JsonNode NumberNode(string value)
    {
        return value.Contains('.', StringComparison.Ordinal)
            ? JsonValue.Create(double.TryParse(value, out var decimalValue) ? decimalValue : 0)!
            : JsonValue.Create(int.TryParse(value, out var integerValue) ? integerValue : 0)!;
    }

    private static string DefaultThemeTokensJson(string family)
    {
        var isAndroid = family.Equals("android", StringComparison.OrdinalIgnoreCase);
        var tokens = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["defaultMode"] = "light",
            ["neutralTint"] = new JsonObject
            {
                ["hueDeg"] = 0,
                ["saturation"] = 0,
            },
            ["cursor"] = new JsonObject
            {
                ["width"] = 2,
                ["blinkFrames"] = 20,
            },
            ["typography"] = new JsonObject
            {
                ["fontFamilyId"] = "",
                ["emojiFontFamilyId"] = "",
                ["size"] = isAndroid ? 15 : 16,
                ["weight"] = 400,
                ["style"] = "normal",
            },
            ["radii"] = new JsonObject
            {
                ["control"] = 8,
                ["card"] = 14,
                ["panel"] = 20,
                ["surface"] = 14,
                ["pill"] = 999,
                ["avatar"] = 999,
                ["full"] = 999,
            },
            ["modes"] = new JsonObject
            {
                ["light"] = new JsonObject
                {
                    ["colors"] = new JsonObject
                    {
                        ["background"] = "gray_100",
                        ["textPrimary"] = "gray_010",
                        ["textSecondary"] = "gray_040",
                        ["accent"] = isAndroid ? "purple" : "blue",
                        ["icons.primary"] = "gray_010",
                        ["icons.secondary"] = "gray_040",
                        ["icons.accent"] = isAndroid ? "purple" : "blue",
                        ["borders.primary"] = "gray_070",
                        ["borders.secondary"] = "gray_080",
                        ["borders.alternate"] = "gray_060",
                        ["theme.cursor.color"] = isAndroid ? "purple" : "blue",
                    },
                    ["statusBar"] = new JsonObject
                    {
                        ["foreground"] = "gray_010",
                        ["background"] = new JsonObject
                        {
                            ["color"] = "gray_100",
                            ["alpha"] = 1,
                        },
                    },
                    ["navigationBar"] = new JsonObject
                    {
                        ["foreground"] = "gray_010",
                        ["background"] = new JsonObject
                        {
                            ["color"] = "gray_100",
                            ["alpha"] = 1,
                        },
                    },
                    ["keyboard"] = new JsonObject
                    {
                        ["background"] = "gray_090",
                        ["keyBackground"] = "gray_100",
                        ["specialKeyBackground"] = "gray_080",
                        ["pressedKeyBackground"] = "gray_070",
                        ["popoverBackground"] = "gray_100",
                        ["text"] = "gray_010",
                    },
                },
                ["dark"] = new JsonObject
                {
                    ["colors"] = new JsonObject
                    {
                        ["background"] = "gray_010",
                        ["textPrimary"] = "gray_100",
                        ["textSecondary"] = "gray_070",
                        ["accent"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["icons.primary"] = "gray_100",
                        ["icons.secondary"] = "gray_070",
                        ["icons.accent"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["borders.primary"] = "gray_040",
                        ["borders.secondary"] = "gray_030",
                        ["borders.alternate"] = "gray_050",
                        ["theme.cursor.color"] = isAndroid ? "purple_tint" : "blue_bright",
                    },
                    ["statusBar"] = new JsonObject
                    {
                        ["foreground"] = "gray_100",
                        ["background"] = new JsonObject
                        {
                            ["color"] = "gray_010",
                            ["alpha"] = 1,
                        },
                    },
                    ["navigationBar"] = new JsonObject
                    {
                        ["foreground"] = "gray_100",
                        ["background"] = new JsonObject
                        {
                            ["color"] = "gray_010",
                            ["alpha"] = 1,
                        },
                    },
                    ["keyboard"] = new JsonObject
                    {
                        ["background"] = "gray_020",
                        ["keyBackground"] = "gray_030",
                        ["specialKeyBackground"] = "gray_040",
                        ["pressedKeyBackground"] = "gray_050",
                        ["popoverBackground"] = "gray_030",
                        ["text"] = "gray_100",
                    },
                },
            },
        };
        return tokens.ToJsonString();
    }

    private static string DefaultStatusBarConfigJson()
    {
        var config = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["layout"] = new JsonObject
            {
                ["height"] = 54,
                ["itemSize"] = 18,
                ["gap"] = 6,
                ["sidePadding"] = 24,
            },
            ["items"] = new JsonArray(DefaultStatusBarItems().Select(StatusBarItemToJson).ToArray<JsonNode?>()),
        };
        return config.ToJsonString();
    }

    private static List<StatusBarItem> DefaultStatusBarItems()
    {
        return
        [
            new("time", "Time", "text", "9:41", "", false, "left", 10),
            new("carrier", "Carrier", "text", "", "", false, "off", 20),
            new("signal", "Signal", "generatedSignal", "4", "", false, "right", 10),
            new("wifi", "Wi‑Fi", "iconToken", "", "status_wifi", false, "right", 20),
            new("soundOff", "Sound Off", "iconToken", "", "media_volume_off", false, "off", 30),
            new("bluetooth", "Bluetooth", "iconToken", "", "status_bluetooth", false, "off", 40),
            new("battery", "Battery", "generatedBattery", "85", "", false, "right", 50),
        ];
    }

    private static JsonObject StatusBarConfig(string json)
    {
        var fallback = ParseJsonObject(DefaultStatusBarConfigJson());
        var parsed = ParseJsonObject(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var layout = parsed["layout"] as JsonObject ?? [];
        var fallbackLayout = fallback["layout"]!.AsObject();
        parsed["schemaVersion"] ??= 2;
        parsed["layout"] = new JsonObject
        {
            ["height"] = GetJsonValue(layout, ["height"])?.DeepClone() ?? fallbackLayout["height"]!.DeepClone(),
            ["itemSize"] = GetJsonValue(layout, ["itemSize"])?.DeepClone() ?? fallbackLayout["itemSize"]!.DeepClone(),
            ["gap"] = GetJsonValue(layout, ["gap"])?.DeepClone() ?? fallbackLayout["gap"]!.DeepClone(),
            ["sidePadding"] = GetJsonValue(layout, ["sidePadding"])?.DeepClone() ?? fallbackLayout["sidePadding"]!.DeepClone(),
        };
        if (parsed["items"] is not JsonArray)
        {
            parsed["items"] = new JsonArray(DefaultStatusBarItems().Select(StatusBarItemToJson).ToArray<JsonNode?>());
        }

        return parsed;
    }

    private static IReadOnlyList<StatusBarItem> StatusBarItems(JsonObject config)
    {
        var defaults = DefaultStatusBarItems();
        var rawItems = config["items"] as JsonArray ?? [];
        return rawItems.Select((raw, index) =>
        {
            var item = raw as JsonObject ?? [];
            var fallback = defaults.ElementAtOrDefault(index) ?? defaults[0];
            var kind = JsonString(item, ["kind"]);
            if (kind is not ("text" or "iconToken" or "generatedBattery" or "generatedSignal"))
            {
                kind = fallback.Kind;
            }

            var zone = JsonString(item, ["zone"]);
            if (zone is not ("off" or "left" or "right"))
            {
                zone = fallback.Zone;
            }

            return new StatusBarItem(
                JsonString(item, ["id"]) is { Length: > 0 } id ? id : fallback.Id,
                JsonString(item, ["label"]) is { Length: > 0 } label ? label : fallback.Label,
                kind,
                JsonString(item, ["value"]) is { Length: > 0 } stringValue
                    ? stringValue
                    : JsonNumberString(item, ["value"]),
                JsonString(item, ["token"]) is { Length: > 0 } token ? token : fallback.Token,
                JsonBool(item, ["charging"]),
                zone,
                int.TryParse(JsonNumberString(item, ["order"]), out var order) ? order : fallback.Order);
        }).ToList();
    }

    private static JsonObject StatusBarItemToJson(StatusBarItem item)
    {
        var json = new JsonObject
        {
            ["id"] = item.Id,
            ["label"] = item.Label,
            ["kind"] = item.Kind,
            ["zone"] = item.Zone,
            ["order"] = item.Order,
        };
        if (item.Kind == "iconToken")
        {
            json["token"] = item.Token;
        }
        else if (item.Kind == "generatedBattery" || item.Kind == "generatedSignal")
        {
            json["value"] = int.TryParse(item.Value, out var number) ? number : 0;
            if (item.Kind == "generatedBattery")
            {
                json["charging"] = item.Charging;
            }
        }
        else
        {
            json["value"] = item.Value;
        }

        return json;
    }

    private static int StatusBarItemCount(string configJson)
    {
        return StatusBarItems(StatusBarConfig(configJson)).Count;
    }

    private static string DefaultNavigationBarConfigJson()
    {
        var config = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["type"] = "buttons",
            ["layout"] = new JsonObject
            {
                ["height"] = 34,
                ["itemSize"] = 18,
                ["sidePadding"] = 40,
                ["strokeWidth"] = 2,
                ["cornerRadius"] = 3,
                ["filled"] = false,
            },
            ["gesture"] = new JsonObject
            {
                ["width"] = 134,
                ["height"] = 5,
                ["cornerRadius"] = 3,
            },
            ["items"] = new JsonArray(DefaultNavigationBarItems().Select(NavigationBarItemToJson).ToArray<JsonNode?>()),
        };
        return config.ToJsonString();
    }

    private static List<NavigationBarItem> DefaultNavigationBarItems()
    {
        return
        [
            new("back", "Back", "generatedBack", "left", 10),
            new("home", "Home", "generatedHome", "center", 10),
            new("recents", "Recents", "generatedRecents", "right", 10),
        ];
    }

    private static JsonObject NavigationBarConfig(string json)
    {
        var fallback = ParseJsonObject(DefaultNavigationBarConfigJson());
        var parsed = ParseJsonObject(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var layout = parsed["layout"] as JsonObject ?? [];
        var gesture = parsed["gesture"] as JsonObject ?? [];
        var fallbackLayout = fallback["layout"]!.AsObject();
        var fallbackGesture = fallback["gesture"]!.AsObject();
        parsed["schemaVersion"] ??= 1;
        parsed["type"] = JsonString(parsed, ["type"]) is "gestureBar" ? "gestureBar" : "buttons";
        parsed["layout"] = new JsonObject
        {
            ["height"] = GetJsonValue(layout, ["height"])?.DeepClone() ?? fallbackLayout["height"]!.DeepClone(),
            ["itemSize"] = GetJsonValue(layout, ["itemSize"])?.DeepClone() ?? fallbackLayout["itemSize"]!.DeepClone(),
            ["sidePadding"] = GetJsonValue(layout, ["sidePadding"])?.DeepClone() ?? fallbackLayout["sidePadding"]!.DeepClone(),
            ["strokeWidth"] = GetJsonValue(layout, ["strokeWidth"])?.DeepClone() ?? fallbackLayout["strokeWidth"]!.DeepClone(),
            ["cornerRadius"] = GetJsonValue(layout, ["cornerRadius"])?.DeepClone() ?? fallbackLayout["cornerRadius"]!.DeepClone(),
            ["filled"] = GetJsonValue(layout, ["filled"])?.DeepClone() ?? fallbackLayout["filled"]!.DeepClone(),
        };
        parsed["gesture"] = new JsonObject
        {
            ["width"] = GetJsonValue(gesture, ["width"])?.DeepClone() ?? fallbackGesture["width"]!.DeepClone(),
            ["height"] = GetJsonValue(gesture, ["height"])?.DeepClone() ?? fallbackGesture["height"]!.DeepClone(),
            ["cornerRadius"] = GetJsonValue(gesture, ["cornerRadius"])?.DeepClone() ?? fallbackGesture["cornerRadius"]!.DeepClone(),
        };
        if (parsed["items"] is not JsonArray)
        {
            parsed["items"] = new JsonArray(DefaultNavigationBarItems().Select(NavigationBarItemToJson).ToArray<JsonNode?>());
        }

        return parsed;
    }

    private static IReadOnlyList<NavigationBarItem> NavigationBarItems(JsonObject config)
    {
        var defaults = DefaultNavigationBarItems();
        var rawItems = config["items"] as JsonArray ?? [];
        return rawItems.Select((raw, index) =>
        {
            var item = raw as JsonObject ?? [];
            var fallback = defaults.ElementAtOrDefault(index) ?? defaults[0];
            var kind = JsonString(item, ["kind"]);
            if (kind is not ("generatedBack" or "generatedHome" or "generatedRecents"))
            {
                kind = fallback.Kind;
            }

            var zone = JsonString(item, ["zone"]);
            if (zone is not ("off" or "left" or "center" or "right"))
            {
                zone = fallback.Zone;
            }

            return new NavigationBarItem(
                JsonString(item, ["id"]) is { Length: > 0 } id ? id : fallback.Id,
                JsonString(item, ["label"]) is { Length: > 0 } label ? label : fallback.Label,
                kind,
                zone,
                int.TryParse(JsonNumberString(item, ["order"]), out var order) ? order : fallback.Order);
        }).ToList();
    }

    private static JsonObject NavigationBarItemToJson(NavigationBarItem item)
    {
        return new JsonObject
        {
            ["id"] = item.Id,
            ["label"] = item.Label,
            ["kind"] = item.Kind,
            ["zone"] = item.Zone,
            ["order"] = item.Order,
        };
    }

    private static int NavigationBarItemCount(string configJson)
    {
        return NavigationBarItems(NavigationBarConfig(configJson)).Count;
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

    private static void EnsureComponentClassColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "component_classes", "notes", "TEXT NOT NULL DEFAULT ''");
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
        lock (WriteGate)
        {
            command.ExecuteNonQuery();
        }
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Key, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }

        lock (WriteGate)
        {
            command.ExecuteNonQuery();
        }
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

        CREATE TABLE IF NOT EXISTS production_fonts (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          family_name TEXT NOT NULL,
          category TEXT NOT NULL DEFAULT 'text',
          source_directory TEXT NOT NULL DEFAULT '',
          files_json TEXT NOT NULL DEFAULT '[]',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, family_name)
        );

        CREATE TABLE IF NOT EXISTS icon_themes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          asset_root TEXT NOT NULL DEFAULT '',
          mapping_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, name)
        );

        CREATE TABLE IF NOT EXISTS status_bars (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          family TEXT NOT NULL DEFAULT '',
          config_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, name)
        );

        CREATE TABLE IF NOT EXISTS navigation_bars (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          family TEXT NOT NULL DEFAULT '',
          config_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, name)
        );

        CREATE TABLE IF NOT EXISTS component_classes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          component_type TEXT NOT NULL,
          record_class_id TEXT NOT NULL,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          config_json TEXT NOT NULL DEFAULT '{}',
          design_preview_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, component_type, name)
        );

        CREATE TABLE IF NOT EXISTS themes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          family TEXT NOT NULL DEFAULT 'ios',
          icon_theme_id TEXT NOT NULL DEFAULT '',
          status_bar_id TEXT NOT NULL DEFAULT '',
          navigation_bar_id TEXT NOT NULL DEFAULT '',
          tokens_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, name)
        );

        CREATE TABLE IF NOT EXISTS editor_layouts (
          record_class_id TEXT PRIMARY KEY,
          layout_json TEXT NOT NULL
        );
        """;

    public sealed record ProjectSettings(string Slug, int DefaultFps, string MediaRoot);
    public sealed record EpisodeSettings(string Slug, int SortOrder);
    public sealed record DeviceSettings(string Name, string Manufacturer, string Model, string OsFamily, string MetricsJson);
    public sealed record DevicePreviewMetrics(
        string Name,
        double CanvasWidth,
        double CanvasHeight,
        double ScreenX,
        double ScreenY,
        double ScreenWidth,
        double ScreenHeight,
        double CornerRadius,
        double StatusBarHeight,
        double SafeAreaBottom,
        double ScaleToPixels);
    public sealed record ActorSettings(
        string ProjectId,
        string DisplayName,
        string ShortName,
        string DefaultDeviceId,
        string DefaultThemeId,
        string MetadataJson);
    public sealed record ThemeSettings(
        string ProjectId,
        string Name,
        string Family,
        string IconThemeId,
        string StatusBarId,
        string NavigationBarId,
        string TokensJson,
        string MetadataJson);
    public sealed record PaletteColorSettings(
        string Token,
        string ValueHex,
        bool IsNeutral,
        string Source,
        bool IsProtected,
        bool HiddenFromPickers,
        string Note);
    public sealed record ProductionFontSettings(
        string FamilyName,
        string Category,
        string SourceDirectory,
        string FilesJson);
    public sealed record IconThemeSettings(
        string Name,
        string AssetRoot,
        string MappingJson,
        string MetadataJson);
    public sealed record IconThemeToken(
        string Token,
        string Category,
        string File,
        string Description);
    public sealed record IconThemeRefreshResult(int ThemeCount, int CommonTokenCount, int OmittedTokenCount);
    public sealed record IconThemeSearchCandidate(string Provider, string SourceName, string PreviewUrl);
    public sealed record IconThemeSearchResult(
        IReadOnlyList<IconThemeSearchCandidate> Lucide,
        IReadOnlyList<IconThemeSearchCandidate> Material);
    public sealed record IconThemeGenerateResult(string Token, int WrittenFileCount, IconThemeRefreshResult RefreshResult);
    public sealed record StatusBarSettings(
        string ProjectId,
        string Name,
        string Family,
        string ConfigJson,
        string MetadataJson);
    public sealed record StatusBarItem(
        string Id,
        string Label,
        string Kind,
        string Value,
        string Token,
        bool Charging,
        string Zone,
        int Order);
    public sealed record NavigationBarSettings(
        string ProjectId,
        string Name,
        string Family,
        string ConfigJson,
        string MetadataJson);
    public sealed record NavigationBarItem(
        string Id,
        string Label,
        string Kind,
        string Zone,
        int Order);
    public sealed record ComponentClassSettings(
        string ProjectId,
        string ComponentType,
        string RecordClassId,
        string Name,
        string Notes,
        string ConfigJson,
        string DesignPreviewJson,
        string MetadataJson);
    public sealed record ThemeTokenOption(
        string Token,
        string Label,
        string Kind,
        string Value,
        string? LightColorHex,
        string? DarkColorHex);
    private sealed record ProjectRow(string Id, string Name, string Notes);
    private sealed record EpisodeRow(string Id, string ProjectId, string Name, string Slug, string Notes, int SortOrder);
    private sealed record AppRow(string Id, string ProjectId, string RecordClassId, string Name, string Notes, int SortOrder);
    private sealed record ModuleRow(string Id, string AppId, string RecordClassId, string Name, string Notes, int SortOrder);
    private sealed record PaletteColorRow(string Id, string ProjectId, string Token, string ValueHex, string Note, bool IsNeutral, string MetadataJson);
    private sealed record DeviceRow(string Id, string ProjectId, string Name, string Manufacturer, string Model, string OsFamily, string MetricsJson);
    private sealed record ActorRow(string Id, string ProjectId, string DisplayName, string ShortName, string DefaultDeviceId, string DefaultThemeId, string MetadataJson);
    private sealed record ThemeRow(string Id, string ProjectId, string Name, string Family, string IconThemeId, string StatusBarId, string NavigationBarId, string TokensJson, string MetadataJson);
    private sealed record ProductionFontRow(string Id, string ProjectId, string FamilyName, string Category, string SourceDirectory, string FilesJson);
    private sealed record IconThemeRow(string Id, string ProjectId, string Name, string AssetRoot, string MappingJson, string MetadataJson);
    private sealed record StatusBarRow(string Id, string ProjectId, string Name, string Family, string ConfigJson, string MetadataJson);
    private sealed record NavigationBarRow(string Id, string ProjectId, string Name, string Family, string ConfigJson, string MetadataJson);
    private sealed record ComponentClassRow(string Id, string ProjectId, string ComponentType, string RecordClassId, string Name, string Notes, string ConfigJson, string DesignPreviewJson, string MetadataJson);
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
    private sealed record ComponentSeedRow(string ComponentType, string RecordClassId, string Name, string ConfigJson, string DesignPreviewJson, string MetadataJson);

    private static readonly ComponentSeedRow[] ComponentSeedRows =
    [
        NewComponentSeed("avatar", "component.avatar", "Default Avatar"),
        NewComponentSeed("textInputBar", "component.text_input_bar", "Default Text Input Bar"),
        NewComponentSeed("keyboard", "component.keyboard", "Default Keyboard"),
        NewComponentSeed("buttonIcon", "component.button_icon", "Default Button Icon"),
        NewComponentSeed("label", "component.label", "Default Label"),
        NewComponentSeed("audio", "component.audio", "Default Audio"),
        NewComponentSeed("video", "component.video", "Default Video"),
    ];

    private static ComponentSeedRow NewComponentSeed(string componentType, string recordClassId, string name)
    {
        return new ComponentSeedRow(
            componentType,
            recordClassId,
            name,
            DefaultComponentClassConfigJson(componentType),
            DefaultComponentDesignPreviewJson(componentType),
            JsonSerializer.Serialize(new { note = "Seeded reusable component class." }));
    }

    private static string ComponentTypeLabel(string componentType)
    {
        return componentType switch
        {
            "avatar" => "Avatar component",
            "textInputBar" => "Text input bar component",
            "keyboard" => "Keyboard component",
            "buttonIcon" => "Button icon component",
            "label" => "Label component",
            "audio" => "Audio component",
            "video" => "Video component",
            _ => componentType,
        };
    }

    private static string DefaultComponentClassConfigJson(string componentType)
    {
        var config = new JsonObject
        {
            ["style"] = new JsonObject
            {
                ["shadowEnabled"] = false,
                ["reliefEnabled"] = false,
                ["borderWidth"] = 0,
                ["borderColorToken"] = "theme.borders.primary",
                ["cornerRadiusToken"] = componentType == "avatar" ? "theme.radii.avatar" : "theme.radii.surface",
                ["reliefAngle"] = -45,
                ["reliefExtent"] = 1,
                ["reliefSpread"] = 0,
                ["reliefTopIntensity"] = 12,
                ["reliefBottomIntensity"] = -10,
            },
        };

        switch (componentType)
        {
            case "avatar":
                config["avatar"] = new JsonObject
                {
                    ["defaultSize"] = 48,
                    ["cornerRadiusToken"] = "theme.radii.avatar",
                };
                break;
            case "textInputBar":
                config["textInput"] = new JsonObject
                {
                    ["height"] = 44,
                    ["placeholder"] = "Message",
                    ["idleTextColorToken"] = "theme.colors.textSecondary",
                    ["cursorColorToken"] = "theme.cursor.color",
                    ["cursorWidth"] = 2,
                    ["cursorBlinkFrames"] = 18,
                };
                break;
            case "keyboard":
                config["keyboard"] = new JsonObject
                {
                    ["keyPadding"] = 4,
                    ["keyCornerRadius"] = 6,
                    ["keyShadowEnabled"] = false,
                    ["pressedEffect"] = "popup",
                    ["specialKeyTextScale"] = "0.65",
                    ["emojiScale"] = "1.2",
                    ["bottomIconSlots"] = JsonNode.Parse(ComponentClassFieldCatalog.EmptyIconSlots),
                };
                break;
            case "buttonIcon":
                config["buttonIcon"] = new JsonObject
                {
                    ["iconPadding"] = 6,
                    ["labelEnabled"] = false,
                    ["labelPosition"] = "bottom",
                    ["labelSize"] = 10,
                    ["labelPadding"] = 3,
                };
                break;
            case "label":
                config["label"] = new JsonObject
                {
                    ["dimensionMode"] = "content",
                    ["size"] = "120|32",
                    ["padding"] = "8|4",
                    ["backgroundVisible"] = true,
                    ["backgroundColorToken"] = "theme.colors.background",
                    ["textColorToken"] = "theme.colors.textPrimary",
                    ["textSize"] = 12,
                    ["textStyle"] = "normal",
                };
                break;
            case "audio":
                config["audio"] = new JsonObject
                {
                    ["size"] = "230|54",
                    ["avatarPosition"] = "right",
                    ["avatarSize"] = 32,
                    ["textSize"] = 13,
                    ["playColorToken"] = "theme.icons.accent",
                    ["waveformColorToken"] = "theme.icons.primary",
                    ["knobSize"] = 10,
                };
                break;
            case "video":
                config["video"] = new JsonObject
                {
                    ["statusVisible"] = true,
                    ["statusHeight"] = 24,
                    ["statusIconSlots"] = JsonNode.Parse("""{"left":["app_camera"],"center":[],"right":[]}"""),
                    ["playOverlayVisible"] = true,
                    ["playColorToken"] = "theme.icons.accent",
                };
                break;
        }

        return config.ToJsonString();
    }

    private static string DefaultComponentDesignPreviewJson(string componentType)
    {
        return JsonSerializer.Serialize(new
        {
            componentType,
            sampleText = componentType switch
            {
                "label" => "Alex",
                "textInputBar" => "Message",
                "audio" => "0:05",
                "video" => "0:12",
                _ => "Sample",
            },
            sampleSize = 256,
        });
    }

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
