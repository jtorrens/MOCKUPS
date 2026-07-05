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
        var renderPresets = QueryRenderPresetRows(connection);
        var componentClasses = QueryComponentClassRows(connection);
        var referenceUsageIndex = BuildReferenceUsageIndex(
            shots,
            actors,
            themes,
            paletteColors,
            productionFonts,
            iconThemes,
            statusBars,
            navigationBars,
            renderPresets,
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
        var renderPresetRootNodes = new Dictionary<string, ProjectTreeNode>();
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
            var renderPresetsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.RenderPresetsRoot,
                $"render_presets_root_{project.Id}",
                "Render Presets",
                "Reusable render output definitions.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.RenderPresetsRoot),
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
            systemDataRoot.AddChild(renderPresetsRoot);
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
            renderPresetRootNodes[project.Id] = renderPresetsRoot;
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

        foreach (var renderPreset in renderPresets.OrderBy((renderPreset) => renderPreset.Name))
        {
            if (!renderPresetRootNodes.TryGetValue(renderPreset.ProjectId, out var renderPresetsRoot)) continue;

            renderPresetsRoot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.RenderPreset,
                renderPreset.Id,
                renderPreset.Name,
                $"{renderPreset.Width}x{renderPreset.Height} · {renderPreset.Fps} fps · {renderPreset.Format}",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.RenderPreset),
                renderPresetsRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.RenderPreset, renderPreset.Id)));
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

        if (parent.Kind == ProjectTreeNodeKind.RenderPresetsRoot)
        {
            var project = ProjectAncestor(parent);
            var index = ScalarLong(connection, "SELECT COUNT(*) FROM render_presets WHERE project_id = $projectId", ("$projectId", project.Id)) + 1;
            var id = $"render_preset_{Guid.NewGuid():N}";
            var name = $"Render Preset {index}";
            Execute(
                connection,
                """
                INSERT INTO render_presets (id, project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json)
                VALUES ($id, $projectId, $name, 1, 1, 1, 'mov', $codecJson, $colorJson, $qualityJson, $exportJson)
                """,
                ("$id", id),
                ("$projectId", project.Id),
                ("$name", name),
                ("$codecJson", DefaultRenderPresetCodecJson()),
                ("$colorJson", DefaultRenderPresetColorJson()),
                ("$qualityJson", DefaultRenderPresetQualityJson()),
                ("$exportJson", DefaultRenderPresetExportJson()));

            return new ProjectTreeNode(
                ProjectTreeNodeKind.RenderPreset,
                id,
                name,
                "1x1 · 1 fps · mov",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.RenderPreset),
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
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, metadata_json)
                VALUES ($id, $appId, $recordClassId, $name, $notes, $sortOrder, $metadataJson)
                """,
                ("$id", id),
                ("$appId", parent.Id),
                ("$recordClassId", "module.generic"),
                ("$name", $"Module {index + 1}"),
                ("$notes", "New module created in the desktop shell spike."),
                ("$sortOrder", index),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "New module created in the desktop shell spike." })));

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
                INSERT INTO shots (id, episode_id, name, slug, notes, sort_order, fps, duration_frames)
                VALUES ($id, $episodeId, $name, $slug, $notes, $sortOrder, 25, 240)
                """,
                ("$id", id),
                ("$episodeId", parent.Id),
                ("$name", $"Shot {index + 1:00}"),
                ("$slug", $"shot-{index + 1:00}"),
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
                INSERT INTO shots (id, episode_id, name, slug, version, notes, sort_order, fps, duration_frames, owner_actor_id, canvas_json, metadata_json)
                SELECT $id, episode_id, $name, slug || '-copy', version, notes, $sortOrder, fps, duration_frames, owner_actor_id, canvas_json, metadata_json
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
                INSERT INTO apps (id, project_id, record_class_id, name, bundle_key, app_type, notes, sort_order, config_json, metadata_json)
                SELECT $id, project_id, record_class_id, $name, bundle_key || '-copy', app_type, notes, $sortOrder, config_json, metadata_json
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
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, metadata_json)
                SELECT $id, app_id, record_class_id, $name, notes, $sortOrder, metadata_json
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

        if (node.Kind == ProjectTreeNodeKind.IconTheme)
        {
            var source = QueryIconThemeRows(connection).FirstOrDefault((row) => row.Id == node.Id)
                ?? throw new InvalidOperationException($"Missing icon theme '{node.Id}'.");
            var id = $"icon_theme_{Guid.NewGuid():N}";
            var duplicatedAssets = DuplicateIconThemeAssets(connection, source, $"{node.Name} copy");
            var name = duplicatedAssets.Name;
            var assetRoot = duplicatedAssets.AssetRoot;
            var metadata = IconThemeMetadata(IconThemeAssetDirectory(connection, source.ProjectId, assetRoot), name);
            try
            {
                Execute(
                    connection,
                    """
                    INSERT INTO icon_themes (id, project_id, name, asset_root, mapping_json, metadata_json)
                    SELECT $id, project_id, $name, $assetRoot, mapping_json, $metadataJson
                    FROM icon_themes
                    WHERE id = $sourceId
                    """,
                    ("$id", id),
                    ("$name", name),
                    ("$assetRoot", assetRoot),
                    ("$metadataJson", metadata.ToJsonString()),
                    ("$sourceId", node.Id));
            }
            catch
            {
                DeleteIconThemeAssetDirectory(connection, source.ProjectId, assetRoot);
                throw;
            }

            return new ProjectTreeNode(ProjectTreeNodeKind.IconTheme, id, name, node.Notes, node.RecordClassId, node.Parent);
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

        if (node.Kind == ProjectTreeNodeKind.RenderPreset)
        {
            var id = $"render_preset_{Guid.NewGuid():N}";
            Execute(
                connection,
                """
                INSERT INTO render_presets (id, project_id, name, width, height, fps, format, codec_json, color_json, quality_json, export_json, metadata_json)
                SELECT $id, project_id, $name, width, height, fps, format, codec_json, color_json, quality_json, export_json, metadata_json
                FROM render_presets
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.RenderPreset, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
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
            ProjectTreeNodeKind.RenderPreset => "render_presets",
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
            ProjectTreeNodeKind.RenderPreset => "render_presets",
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
            var row = QueryIconThemeRows(connection).FirstOrDefault((candidate) => candidate.Id == node.Id)
                ?? throw new InvalidOperationException($"Missing icon theme '{node.Id}'.");
            var renamedAssets = RenameIconThemeAssets(connection, row, node.Name);
            var metadata = IconThemeMetadata(IconThemeAssetDirectory(connection, row.ProjectId, renamedAssets.AssetRoot), renamedAssets.Name);
            Execute(
                connection,
                "UPDATE icon_themes SET name = $name, asset_root = $assetRoot, metadata_json = $metadataJson WHERE id = $id",
                ("$id", node.Id),
                ("$name", renamedAssets.Name),
                ("$assetRoot", renamedAssets.AssetRoot),
                ("$metadataJson", metadata.ToJsonString()));
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

        if (node.Kind == ProjectTreeNodeKind.RenderPreset)
        {
            Execute(
                connection,
                "UPDATE render_presets SET name = $name WHERE id = $id",
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
        EnsureShotColumns(connection);
        EnsureAppColumns(connection);
        EnsureComponentClassColumns(connection);
        SeedEditorLayouts(connection);
        SeedIfEmpty(connection);
        SeedPaletteColorsIfEmpty(connection);
        SeedDevicesIfEmpty(connection);
        SeedActorsIfEmpty(connection);
        SeedProductionFontsIfEmpty(connection);
        SeedStatusBarsIfEmpty(connection);
        SeedNavigationBarsIfEmpty(connection);
        SeedRenderPresetsIfEmpty(connection);
        SeedComponentClassesIfEmpty(connection);
        SeedThemesIfEmpty(connection);
        ClearShotRenderPresetReferences(connection);
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
            INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, metadata_json)
            VALUES ($id, $appId, $recordClassId, $name, $notes, 0, $metadataJson)
            """,
            ("$id", "module_core_chat"),
            ("$appId", "app_core_chat"),
            ("$recordClassId", "module.core.chat"),
            ("$name", "Chat Module"),
            ("$notes", "Seed module linked to Chat app."),
            ("$metadataJson", JsonSerializer.Serialize(new { note = "Seed module linked to Chat app." })));
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
            "navigation.render_presets",
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
            "render_preset",
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
            : recordClassId == "shot"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "shot.slug", "order": 20, "visible": true },
                    { "id": "shot.version", "order": 30, "visible": true },
                    { "id": "shot.renderName", "order": 40, "visible": true },
                    { "id": "shot.durationFrames", "order": 50, "visible": true },
                    { "id": "shot.fps", "order": 60, "visible": true },
                    { "id": "shot.ownerActorId", "order": 70, "visible": true },
                    { "id": "shot.ownerDevice", "order": 80, "visible": true },
                    { "id": "core.notes", "order": 90, "visible": true }
                  """
            : recordClassId is "app.generic" or "app.core.chat"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "app.bundleKey", "order": 20, "visible": true },
                    { "id": "app.appType", "order": 30, "visible": true },
                    { "id": "core.kind", "order": 40, "visible": false }
                  """
            : recordClassId is "module.generic" or "module.core.chat"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "module.recordClassId", "order": 20, "visible": false },
                    { "id": "module.sortOrder", "order": 30, "visible": true },
                    { "id": "core.notes", "order": 40, "visible": true },
                    { "id": "module.metadata", "order": 50, "visible": false }
                  """
            : recordClassId == "render_preset"
                ? """
                    { "id": "core.name", "order": 10, "visible": true },
                    { "id": "renderPreset.format", "order": 20, "visible": true },
                    { "id": "renderPreset.codec", "order": 30, "visible": true },
                    { "id": "renderPreset.export.ffmpegArgs", "order": 40, "visible": true }
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
        var appCards = recordClassId is "app.generic" or "app.core.chat"
            ? $$"""
            ,
            {
              "id": "wallpaper",
              "label": "Wallpaper",
              "subtitle": "Wallpaper color, image and opacity",
              "icon": "{{EditorIcons.Image}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "wallpaper",
                  "label": "Wallpaper",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "app.wallpaper.kind", "order": 10, "visible": true },
                    { "id": "app.wallpaper.opacity", "order": 20, "visible": true },
                    { "id": "app.wallpaper.color", "order": 30, "visible": true },
                    { "id": "app.wallpaper.image.filePath", "order": 40, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "icon",
              "label": "Icon",
              "subtitle": "App icon image crop",
              "icon": "{{EditorIcons.Icon}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "icon",
                  "label": "Icon",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "app.icon.filePath", "order": 10, "visible": true },
                    { "id": "app.icon.scale", "order": 20, "visible": true },
                    { "id": "app.icon.offset", "order": 30, "visible": true }
                  ]
                }
              ]
            },
            {
              "id": "notes",
              "label": "Notes",
              "subtitle": "App notes",
              "icon": "{{EditorIcons.Content}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                {
                  "id": "notes",
                  "label": "Notes",
                  "order": 10,
                  "visible": true,
                  "fields": [
                    { "id": "app.note", "order": 10, "visible": true }
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
            }{{appCards}}{{actorCards}}{{themeCards}}{{statusBarCards}}{{navigationBarCards}}{{componentCards}}
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
                INSERT INTO shots (id, episode_id, name, slug, version, notes, sort_order, fps, duration_frames, owner_actor_id, canvas_json, metadata_json)
                VALUES ($id, $episodeId, $name, $slug, $version, $notes, $sortOrder, $fps, $durationFrames, $ownerActorId, $canvasJson, $metadataJson)
                """,
                ("$id", $"shot_{Guid.NewGuid():N}"),
                ("$episodeId", targetEpisodeId),
                ("$name", shot.Name),
                ("$slug", shot.Slug),
                ("$version", shot.Version),
                ("$notes", shot.Notes),
                ("$sortOrder", index),
                ("$fps", shot.Fps),
                ("$durationFrames", shot.DurationFrames),
                ("$ownerActorId", shot.OwnerActorId),
                ("$canvasJson", shot.CanvasJson),
                ("$metadataJson", shot.MetadataJson));
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
                INSERT INTO modules (id, app_id, record_class_id, name, notes, sort_order, metadata_json)
                VALUES ($id, $appId, $recordClassId, $name, $notes, $sortOrder, $metadataJson)
                """,
                ("$id", $"module_{Guid.NewGuid():N}"),
                ("$appId", targetAppId),
                ("$recordClassId", module.RecordClassId),
                ("$name", module.Name),
                ("$notes", module.Notes),
                ("$sortOrder", index),
                ("$metadataJson", module.MetadataJson));
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

    public IReadOnlyList<FieldOption> GetDeviceOptions(string projectId)
    {
        using var connection = OpenConnection();
        return QueryDeviceRows(connection)
            .Where((device) => device.ProjectId == projectId)
            .OrderBy((device) => device.Name)
            .Select((device) => new FieldOption(device.Id, device.Name))
            .ToList();
    }

    public IReadOnlyList<FieldOption> GetActorOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = QueryActorRows(connection)
            .Where((actor) => actor.ProjectId == projectId)
            .OrderBy((actor) => actor.DisplayName)
            .Select((actor) => new FieldOption(actor.Id, actor.DisplayName))
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

    public ShotSettings GetShotSettings(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.slug, s.version, s.sort_order, s.fps, s.duration_frames, s.owner_actor_id, s.render_preset_id, s.canvas_json, s.metadata_json, e.project_id
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            WHERE s.id = $id
            """;
        command.Parameters.AddWithValue("$id", shotId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing shot '{shotId}'.");
        }

        return new ShotSettings(
            ReadString(reader, 9),
            ReadString(reader, 0),
            reader.IsDBNull(1) ? 1 : reader.GetInt32(1),
            reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            reader.IsDBNull(3) ? 25 : reader.GetInt32(3),
            reader.IsDBNull(4) ? 240 : reader.GetInt32(4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7),
            ReadString(reader, 8));
    }

    public void UpdateShotField(string shotId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        var column = fieldId switch
        {
            "shot.slug" => "slug",
            "shot.version" => "version",
            "shot.sortOrder" => "sort_order",
            "shot.fps" => "fps",
            "shot.durationFrames" => "duration_frames",
            "shot.ownerActorId" => "owner_actor_id",
            "shot.renderPresetId" => "render_preset_id",
            "shot.canvas" => "canvas_json",
            "shot.metadata" => "metadata_json",
            _ => throw new InvalidOperationException($"Unknown shot field '{fieldId}'."),
        };
        object nextValue = fieldId is "shot.version" or "shot.sortOrder" or "shot.fps" or "shot.durationFrames"
            ? int.TryParse(value, out var parsed) ? parsed : 0
            : value;

        Execute(
            connection,
            $"UPDATE shots SET {column} = $value WHERE id = $id",
            ("$id", shotId),
            ("$value", nextValue));
    }

    public string GetShotRenderName(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.slug, p.name, e.slug, e.name, s.slug, s.name, s.version
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            JOIN projects p ON p.id = e.project_id
            WHERE s.id = $id
            """;
        command.Parameters.AddWithValue("$id", shotId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing shot '{shotId}'.");
        }

        var projectSlug = SlugOrName(ReadString(reader, 0), reader.GetString(1), "project");
        var episodeSlug = SlugOrName(ReadString(reader, 2), reader.GetString(3), "episode");
        var shotSlug = SlugOrName(ReadString(reader, 4), reader.GetString(5), "shot");
        var version = reader.IsDBNull(6) ? 1 : reader.GetInt32(6);
        return $"{projectSlug}_{episodeSlug}_{shotSlug}_v{Math.Max(0, version):00}";
    }

    public string GetShotOwnerDeviceName(string shotId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.name
            FROM shots s
            JOIN actors a ON a.id = s.owner_actor_id
            JOIN devices d ON d.id = a.default_device_id
            WHERE s.id = $id
            """;
        command.Parameters.AddWithValue("$id", shotId);
        return command.ExecuteScalar() as string ?? "No default device";
    }

    public AppSettings GetAppSettings(string appId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, bundle_key, app_type, config_json, metadata_json FROM apps WHERE id = $id";
        command.Parameters.AddWithValue("$id", appId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing app '{appId}'.");
        }

        return new AppSettings(
            reader.GetString(0),
            ReadString(reader, 1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4));
    }

    public ModuleSettings GetModuleSettings(string moduleId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT record_class_id, sort_order, metadata_json FROM modules WHERE id = $id";
        command.Parameters.AddWithValue("$id", moduleId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing module '{moduleId}'.");
        }

        return new ModuleSettings(
            reader.GetString(0),
            reader.GetInt32(1),
            ReadString(reader, 2));
    }

    public void UpdateModuleField(string moduleId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "module.sortOrder":
                Execute(
                    connection,
                    "UPDATE modules SET sort_order = $value WHERE id = $id",
                    ("$id", moduleId),
                    ("$value", int.TryParse(value, out var parsed) ? parsed : 0));
                return;
            case "module.metadata":
                Execute(
                    connection,
                    "UPDATE modules SET metadata_json = $value WHERE id = $id",
                    ("$id", moduleId),
                    ("$value", value));
                return;
            case "module.recordClassId":
                return;
            default:
                throw new InvalidOperationException($"Unknown module field '{fieldId}'.");
        }
    }

    public void UpdateAppField(string appId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        if (fieldId.StartsWith("app.wallpaper.", StringComparison.Ordinal))
        {
            UpdateAppConfigField(connection, appId, fieldId, value);
            return;
        }

        if (fieldId.StartsWith("app.icon.", StringComparison.Ordinal) || fieldId == "app.note")
        {
            UpdateAppMetadataField(connection, appId, fieldId, value);
            return;
        }

        var column = fieldId switch
        {
            "app.bundleKey" => "bundle_key",
            "app.appType" => "app_type",
            "app.config" => "config_json",
            "app.metadata" => "metadata_json",
            _ => throw new InvalidOperationException($"Unknown app field '{fieldId}'."),
        };

        Execute(
            connection,
            $"UPDATE apps SET {column} = $value WHERE id = $id",
            ("$id", appId),
            ("$value", value));
    }

    public string GetAppConfigFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var config = ParseJsonObject(string.IsNullOrWhiteSpace(settings.ConfigJson) ? "{}" : settings.ConfigJson);
        var lightWallpaperColor = JsonString(config, ["modes", "light", "wallpaper", "color"]);
        if (string.IsNullOrWhiteSpace(lightWallpaperColor)) lightWallpaperColor = "gray_100";
        var darkWallpaperColor = JsonString(config, ["modes", "dark", "wallpaper", "color"]);
        if (string.IsNullOrWhiteSpace(darkWallpaperColor)) darkWallpaperColor = "gray_000";
        return fieldId switch
        {
            "app.wallpaper.kind" => JsonString(config, ["wallpaper", "kind"]) is { Length: > 0 } kind ? kind : "solid",
            "app.wallpaper.opacity" => JsonNumberString(config, ["wallpaper", "opacity"], "1"),
            "app.wallpaper.color" => $"{lightWallpaperColor}|{darkWallpaperColor}",
            "app.wallpaper.image.filePath" => JsonString(config, ["wallpaper", "image", "filePath"]),
            _ => throw new InvalidOperationException($"Unknown app config field '{fieldId}'."),
        };
    }

    public string GetAppMetadataFieldValue(string appId, string fieldId)
    {
        var settings = GetAppSettings(appId);
        var metadata = ParseJsonObject(string.IsNullOrWhiteSpace(settings.MetadataJson) ? "{}" : settings.MetadataJson);
        return fieldId switch
        {
            "app.note" => JsonString(metadata, ["note"]),
            "app.icon.filePath" => JsonString(metadata, ["icon", "filePath"]),
            "app.icon.scale" => JsonNumberString(metadata, ["icon", "scale"], "1"),
            "app.icon.offset" => $"{JsonNumberString(metadata, ["icon", "offsetX"], "0")}|{JsonNumberString(metadata, ["icon", "offsetY"], "0")}",
            _ => throw new InvalidOperationException($"Unknown app metadata field '{fieldId}'."),
        };
    }

    private static void UpdateAppConfigField(SqliteConnection connection, string appId, string fieldId, string value)
    {
        var config = ParseJsonObject(ScalarString(connection, "SELECT config_json FROM apps WHERE id = $id", ("$id", appId)) ?? "{}");
        switch (fieldId)
        {
            case "app.wallpaper.kind":
                SetJsonValue(config, ["wallpaper", "kind"], JsonValue.Create(value)!);
                break;
            case "app.wallpaper.opacity":
                SetJsonValue(config, ["wallpaper", "opacity"], NumberNode(value));
                break;
            case "app.wallpaper.color":
                SetPair(
                    config,
                    value,
                    ["modes", "light", "wallpaper", "color"],
                    ["modes", "dark", "wallpaper", "color"],
                    asNumber: false);
                break;
            case "app.wallpaper.image.filePath":
                SetJsonValue(config, ["wallpaper", "image", "filePath"], JsonValue.Create(value)!);
                break;
            default:
                throw new InvalidOperationException($"Unknown app config field '{fieldId}'.");
        }

        Execute(connection, "UPDATE apps SET config_json = $configJson WHERE id = $id", ("$id", appId), ("$configJson", config.ToJsonString()));
    }

    private static void UpdateAppMetadataField(SqliteConnection connection, string appId, string fieldId, string value)
    {
        var metadata = ParseJsonObject(ScalarString(connection, "SELECT metadata_json FROM apps WHERE id = $id", ("$id", appId)) ?? "{}");
        switch (fieldId)
        {
            case "app.note":
                SetJsonValue(metadata, ["note"], JsonValue.Create(value)!);
                break;
            case "app.icon.filePath":
                SetJsonValue(metadata, ["icon", "filePath"], JsonValue.Create(value)!);
                break;
            case "app.icon.scale":
                SetJsonValue(metadata, ["icon", "scale"], NumberNode(value));
                break;
            case "app.icon.offset":
                SetPair(metadata, value, ["icon", "offsetX"], ["icon", "offsetY"]);
                break;
            default:
                throw new InvalidOperationException($"Unknown app metadata field '{fieldId}'.");
        }

        Execute(connection, "UPDATE apps SET metadata_json = $metadataJson WHERE id = $id", ("$id", appId), ("$metadataJson", metadata.ToJsonString()));
    }

    private static List<ShotRow> QueryShotRows(SqliteConnection connection)
    {
        var rows = new List<ShotRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, episode_id, name, slug, version, notes, sort_order, fps, duration_frames, owner_actor_id, render_preset_id, canvas_json, metadata_json FROM shots ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ShotRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                reader.GetInt32(4),
                ReadString(reader, 5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                ReadString(reader, 9),
                ReadString(reader, 10),
                ReadString(reader, 11),
                ReadString(reader, 12)));
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
        command.CommandText = "SELECT id, app_id, record_class_id, name, notes, sort_order, metadata_json FROM modules ORDER BY sort_order, name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ModuleRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ReadString(reader, 4),
                reader.GetInt32(5),
                ReadString(reader, 6)));
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

    private static string ReadString(SqliteDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? "" : reader.GetString(index);
    }

    private static string Slug(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "font" : slug;
    }

    private static string SlugOrName(string slug, string name, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(slug)) return SlugValue(slug, fallback);
        return SlugValue(name, fallback);
    }

    private static string SlugValue(string value, string fallback)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? fallback : slug;
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
            ? JsonValue.Create(double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var decimalValue) ? decimalValue : 0)!
            : JsonValue.Create(int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integerValue) ? integerValue : 0)!;
    }

    private static void EnsureShotColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "shots", "slug", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "shots", "version", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "shots", "owner_actor_id", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "shots", "render_preset_id", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "shots", "canvas_json", "TEXT NOT NULL DEFAULT '{}'");
        Execute(
            connection,
            """
            UPDATE shots
            SET slug = lower(replace(trim(name), ' ', '-'))
            WHERE trim(slug) = ''
            """);
    }

    private static void EnsureAppColumns(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "apps", "bundle_key", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "apps", "app_type", "TEXT NOT NULL DEFAULT 'chat'");
        AddColumnIfMissing(connection, "apps", "config_json", "TEXT NOT NULL DEFAULT '{}'");
        Execute(
            connection,
            """
            UPDATE apps
            SET bundle_key = lower(replace(trim(name), ' ', '-'))
            WHERE trim(bundle_key) = ''
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

    private static string? ScalarString(SqliteConnection connection, string sql, params (string Key, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }

        return command.ExecuteScalar() as string;
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
          slug TEXT NOT NULL DEFAULT '',
          version INTEGER NOT NULL DEFAULT 1,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          fps INTEGER NOT NULL DEFAULT 25,
          duration_frames INTEGER NOT NULL DEFAULT 240,
          owner_actor_id TEXT NOT NULL DEFAULT '',
          render_preset_id TEXT NOT NULL DEFAULT '',
          canvas_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS apps (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          record_class_id TEXT NOT NULL,
          name TEXT NOT NULL,
          bundle_key TEXT NOT NULL DEFAULT '',
          app_type TEXT NOT NULL DEFAULT 'chat',
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          config_json TEXT NOT NULL DEFAULT '{}',
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

        CREATE TABLE IF NOT EXISTS render_presets (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          width INTEGER NOT NULL DEFAULT 1080,
          height INTEGER NOT NULL DEFAULT 1920,
          fps INTEGER NOT NULL DEFAULT 25,
          format TEXT NOT NULL DEFAULT 'mov',
          codec_json TEXT NOT NULL DEFAULT '{}',
          color_json TEXT NOT NULL DEFAULT '{}',
          quality_json TEXT NOT NULL DEFAULT '{}',
          export_json TEXT NOT NULL DEFAULT '{}',
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
    public sealed record ShotSettings(
        string ProjectId,
        string Slug,
        int Version,
        int SortOrder,
        int Fps,
        int DurationFrames,
        string OwnerActorId,
        string RenderPresetId,
        string CanvasJson,
        string MetadataJson);
    public sealed record AppSettings(
        string ProjectId,
        string BundleKey,
        string AppType,
        string ConfigJson,
        string MetadataJson);
    public sealed record ModuleSettings(string RecordClassId, int SortOrder, string MetadataJson);
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
    public sealed record IconThemeTokenSvg(string Token, string File, string SvgText);
    public sealed record IconThemeRefreshResult(int ThemeCount, int CommonTokenCount, int OmittedTokenCount);
    public sealed record IconThemeReplaceSvgResult(string Token, string File);
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
    public sealed record RenderPresetSettings(
        string ProjectId,
        string Name,
        int Width,
        int Height,
        int Fps,
        string Format,
        string CodecJson,
        string ColorJson,
        string QualityJson,
        string ExportJson,
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
    private sealed record ModuleRow(string Id, string AppId, string RecordClassId, string Name, string Notes, int SortOrder, string MetadataJson);
    private sealed record PaletteColorRow(string Id, string ProjectId, string Token, string ValueHex, string Note, bool IsNeutral, string MetadataJson);
    private sealed record DeviceRow(string Id, string ProjectId, string Name, string Manufacturer, string Model, string OsFamily, string MetricsJson);
    private sealed record ActorRow(string Id, string ProjectId, string DisplayName, string ShortName, string DefaultDeviceId, string DefaultThemeId, string MetadataJson);
    private sealed record ThemeRow(string Id, string ProjectId, string Name, string Family, string IconThemeId, string StatusBarId, string NavigationBarId, string TokensJson, string MetadataJson);
    private sealed record ProductionFontRow(string Id, string ProjectId, string FamilyName, string Category, string SourceDirectory, string FilesJson);
    private sealed record IconThemeRow(string Id, string ProjectId, string Name, string AssetRoot, string MappingJson, string MetadataJson);
    private sealed record IconThemeAssetMoveResult(string AssetRoot, string Name);
    private sealed record StatusBarRow(string Id, string ProjectId, string Name, string Family, string ConfigJson, string MetadataJson);
    private sealed record NavigationBarRow(string Id, string ProjectId, string Name, string Family, string ConfigJson, string MetadataJson);
    private sealed record RenderPresetRow(
        string Id,
        string ProjectId,
        string Name,
        int Width,
        int Height,
        int Fps,
        string Format,
        string CodecJson,
        string ColorJson,
        string QualityJson,
        string ExportJson,
        string MetadataJson);
    private sealed record ComponentClassRow(string Id, string ProjectId, string ComponentType, string RecordClassId, string Name, string Notes, string ConfigJson, string DesignPreviewJson, string MetadataJson);
    private sealed record ShotRow(
        string Id,
        string EpisodeId,
        string Name,
        string Slug,
        int Version,
        string Notes,
        int SortOrder,
        int Fps,
        int DurationFrames,
        string OwnerActorId,
        string RenderPresetId,
        string CanvasJson,
        string MetadataJson);

    private sealed record PaletteSeedRow(string Token, string ValueHex, bool IsNeutral, string MetadataJson);
    private sealed record DeviceSeedRow(string Id, string Name, string Manufacturer, string Model, string OsFamily, string MetricsJson);
    private sealed record ActorSeedRow(string Id, string DisplayName, string ShortName, string MetadataJson);
    private sealed record RenderPresetSeedRow(string IdSuffix, string Name, string Format, string Codec);
    private sealed record ComponentSeedRow(string ComponentType, string RecordClassId, string Name, string ConfigJson, string DesignPreviewJson, string MetadataJson);

}
