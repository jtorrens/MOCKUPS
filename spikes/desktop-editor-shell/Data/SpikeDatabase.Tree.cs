using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private enum ComponentClassNavigationGroup
    {
        Components,
        Atoms,
        System,
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
        var renderPresets = QueryRenderPresetRows(connection);
        var componentClasses = QueryComponentClassRows(connection);
        var referenceUsageIndex = BuildReferenceUsageIndex(
            shots,
            actors,
            themes,
            paletteColors,
            productionFonts,
            iconThemes,
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
        var renderPresetRootNodes = new Dictionary<string, ProjectTreeNode>();
        var componentClassGroupNodes = new Dictionary<string, Dictionary<ComponentClassNavigationGroup, ProjectTreeNode>>();
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
            var componentGroups = CreateComponentClassGroupNodes(project.Id, componentClassesRoot);
            foreach (var group in ComponentClassNavigationGroups())
            {
                componentClassesRoot.AddChild(componentGroups[group]);
            }
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
            renderPresetRootNodes[project.Id] = renderPresetsRoot;
            componentClassGroupNodes[project.Id] = componentGroups;
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
            if (!componentClassGroupNodes.TryGetValue(componentClass.ProjectId, out var componentGroups)) continue;
            var groupNode = componentGroups[ComponentClassNavigationGroupFor(componentClass.ComponentType)];

            var componentNode = new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentClass,
                componentClass.Id,
                componentClass.Name,
                string.IsNullOrWhiteSpace(componentClass.Notes) ? ComponentTypeLabel(componentClass.ComponentType) : componentClass.Notes,
                componentClass.RecordClassId,
                groupNode,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ComponentClass, componentClass.Id));
            groupNode.AddChild(componentNode);

            foreach (var preset in ComponentClassPresets(componentClass.MetadataJson))
            {
                componentNode.AddChild(new ProjectTreeNode(
                    ProjectTreeNodeKind.ComponentPreset,
                    ComponentPresetNodeId(componentClass.Id, preset.Id),
                    preset.Name,
                    preset.IsProtected ? "Protected component preset" : "Component preset",
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentPreset),
                    componentNode,
                    isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ComponentPreset, ComponentPresetNodeId(componentClass.Id, preset.Id)),
                    isProtected: preset.IsProtected));
            }
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

    private static IReadOnlyList<ComponentClassNavigationGroup> ComponentClassNavigationGroups()
    {
        return
        [
            ComponentClassNavigationGroup.Components,
            ComponentClassNavigationGroup.Atoms,
            ComponentClassNavigationGroup.System,
        ];
    }

    private static Dictionary<ComponentClassNavigationGroup, ProjectTreeNode> CreateComponentClassGroupNodes(
        string projectId,
        ProjectTreeNode root)
    {
        return ComponentClassNavigationGroups()
            .ToDictionary(
                (group) => group,
                (group) => new ProjectTreeNode(
                    ProjectTreeNodeKind.ComponentClassGroup,
                    $"component_classes_{ComponentClassNavigationGroupId(group)}_{projectId}",
                    ComponentClassNavigationGroupTitle(group),
                    ComponentClassNavigationGroupSubtitle(group),
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentClassGroup),
                    root));
    }

    private static ComponentClassNavigationGroup ComponentClassNavigationGroupFor(string componentType)
    {
        return componentType switch
        {
            "status_bar" or "navigation_bar" or "keyboard" => ComponentClassNavigationGroup.System,
            "surface" or "cursor" or "textBox" => ComponentClassNavigationGroup.Atoms,
            _ => ComponentClassNavigationGroup.Components,
        };
    }

    private static string ComponentClassNavigationGroupId(ComponentClassNavigationGroup group)
    {
        return group switch
        {
            ComponentClassNavigationGroup.Components => "components",
            ComponentClassNavigationGroup.Atoms => "atoms",
            ComponentClassNavigationGroup.System => "system",
            _ => throw new InvalidOperationException($"Unknown component class group {group}."),
        };
    }

    private static string ComponentClassNavigationGroupTitle(ComponentClassNavigationGroup group)
    {
        return group switch
        {
            ComponentClassNavigationGroup.Components => "Components",
            ComponentClassNavigationGroup.Atoms => "Atoms",
            ComponentClassNavigationGroup.System => "System",
            _ => throw new InvalidOperationException($"Unknown component class group {group}."),
        };
    }

    private static string ComponentClassNavigationGroupSubtitle(ComponentClassNavigationGroup group)
    {
        return group switch
        {
            ComponentClassNavigationGroup.Components => "Reusable composed component classes",
            ComponentClassNavigationGroup.Atoms => "Primitive component building blocks",
            ComponentClassNavigationGroup.System => "System UI component classes",
            _ => throw new InvalidOperationException($"Unknown component class group {group}."),
        };
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
        var statusBarId = NormalizeComponentPresetReference(connection, project.Id, "status_bar", "");
        var navigationBarId = NormalizeComponentPresetReference(connection, project.Id, "navigation_bar", "");
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

        if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            return DuplicateComponentPreset(node);
        }

        throw new InvalidOperationException($"Cannot duplicate {node.Kind}.");
    }

    public void Delete(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            DeleteComponentPreset(node);
            return;
        }

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
            ProjectTreeNodeKind.RenderPreset => "render_presets",
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
        if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            using var presetConnection = OpenConnection();
            return GetComponentPresetReferenceUsages(presetConnection, node);
        }

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

    public ProjectTreeNode RenameDirectNode(ProjectTreeNode node, string name)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => RenameComponentClass(node, name),
            ProjectTreeNodeKind.ComponentPreset => RenameComponentPreset(node, name),
            _ => throw new InvalidOperationException($"Cannot rename {node.Kind} directly."),
        };
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
}
