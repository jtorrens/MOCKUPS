using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public List<ProjectTreeNode> LoadProjectTree()
    {
        using var connection = OpenConnection();
        var projects = QueryProjectRows(connection);
        var episodes = QueryEpisodeRows(connection);
        var shots = QueryShotRows(connection);
        var moduleInstances = QueryModuleInstanceRows(connection);
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
        var referenceUsageIndex = _referenceUsageService.BuildIndex(connection);

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
        var componentClassGroupNodes = new Dictionary<string, Dictionary<DesktopPreviewComponentCategory, ProjectTreeNode>>();
        var episodeRootNodes = new Dictionary<string, ProjectTreeNode>();
        var episodeNodes = new Dictionary<string, ProjectTreeNode>();
        var shotNodes = new Dictionary<string, ProjectTreeNode>();
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

            var moduleNode = new ProjectTreeNode(
                ProjectTreeNodeKind.Module,
                module.Id,
                module.Name,
                module.Notes,
                module.RecordClassId,
                app);
            app.AddChild(moduleNode);
            foreach (var variant in ModuleVariants(module.MetadataJson))
            {
                var reference = ModuleVariantNodeId(module.Id, variant.Id);
                var used = IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ModuleVariant, reference);
                moduleNode.AddChild(new ProjectTreeNode(
                    ProjectTreeNodeKind.ModuleVariant,
                    reference,
                    variant.Name,
                    variant.IsProtected ? "Protected module variant" : "Module variant",
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleVariant),
                    moduleNode,
                    isUsed: used,
                    isProtected: variant.IsProtected,
                    isLocked: variant.IsLocked));
            }
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
            var groupNode = componentGroups[DesktopPreviewManifest.ComponentCategory(componentClass.ComponentType)];

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
                    preset.IsProtected ? "Protected component variant" : "Component variant",
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentPreset),
                    componentNode,
                    isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ComponentPreset, ComponentPresetNodeId(componentClass.Id, preset.Id)),
                    isProtected: preset.IsProtected,
                    isLocked: preset.IsLocked));
            }
        }

        foreach (var shot in shots.OrderBy((shot) => shot.SortOrder).ThenBy((shot) => shot.Name))
        {
            if (!episodeNodes.TryGetValue(shot.EpisodeId, out var episode)) continue;

            var shotNode = new ProjectTreeNode(
                ProjectTreeNodeKind.Shot,
                shot.Id,
                shot.Name,
                shot.Notes,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot),
                episode);
            episode.AddChild(shotNode);
            shotNodes[shot.Id] = shotNode;
        }

        foreach (var moduleInstance in moduleInstances)
        {
            if (!shotNodes.TryGetValue(moduleInstance.ShotId, out var shot)) continue;

            shot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.ModuleInstance,
                moduleInstance.Id,
                moduleInstance.Name,
                $"{moduleInstance.ModuleName} · {moduleInstance.DurationFrames} frames · {ModuleTransitionLabel(moduleInstance.TransitionJson)}",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
                shot));
        }

        return projectNodes.Values
            .OrderBy((node) => node.Name)
            .ToList();
    }

    private static string ModuleTransitionLabel(string transitionJson)
    {
        var transition = ParseJsonObject(transitionJson);
        var type = transition["type"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(type) ? "Cut" : char.ToUpperInvariant(type[0]) + type[1..];
    }

    private static IReadOnlyList<DesktopPreviewComponentCategory> ComponentClassNavigationGroups()
    {
        return
        [
            DesktopPreviewComponentCategory.Component,
            DesktopPreviewComponentCategory.Atom,
            DesktopPreviewComponentCategory.System,
        ];
    }

    private static Dictionary<DesktopPreviewComponentCategory, ProjectTreeNode> CreateComponentClassGroupNodes(
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

    private static string ComponentClassNavigationGroupId(DesktopPreviewComponentCategory group)
    {
        return group switch
        {
            DesktopPreviewComponentCategory.Component => "components",
            DesktopPreviewComponentCategory.Atom => "atoms",
            DesktopPreviewComponentCategory.System => "system",
            _ => throw new InvalidOperationException($"Unknown component class group {group}."),
        };
    }

    private static string ComponentClassNavigationGroupTitle(DesktopPreviewComponentCategory group)
    {
        return group switch
        {
            DesktopPreviewComponentCategory.Component => "Components",
            DesktopPreviewComponentCategory.Atom => "Atoms",
            DesktopPreviewComponentCategory.System => "System",
            _ => throw new InvalidOperationException($"Unknown component class group {group}."),
        };
    }

    private static string ComponentClassNavigationGroupSubtitle(DesktopPreviewComponentCategory group)
    {
        return group switch
        {
            DesktopPreviewComponentCategory.Component => "Reusable composed component classes",
            DesktopPreviewComponentCategory.Atom => "Primitive component building blocks",
            DesktopPreviewComponentCategory.System => "System UI component classes",
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

        if (parent.Kind == ProjectTreeNodeKind.PaletteRoot)
        {
            var project = ProjectAncestor(parent);
            var color = _paletteRepository.Create(connection, project.Id);

            return new ProjectTreeNode(
                ProjectTreeNodeKind.PaletteColor,
                color.Id,
                color.Token,
                color.Note,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.PaletteColor),
                parent,
                color.ValueHex,
                false);
        }

        if (parent.Kind == ProjectTreeNodeKind.DevicesRoot)
        {
            var project = ProjectAncestor(parent);
            var device = _deviceRepository.Create(connection, project.Id);

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Device,
                device.Id,
                device.Name,
                "",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Device),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.ActorsRoot)
        {
            var project = ProjectAncestor(parent);
            var actor = _actorRepository.Create(connection, project.Id);

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Actor,
                actor.Id,
                actor.DisplayName,
                actor.ShortName,
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
            var preset = _renderPresetRepository.Create(connection, project.Id);

            return new ProjectTreeNode(
                ProjectTreeNodeKind.RenderPreset,
                preset.Id,
                preset.Name,
                "1x1 · 1 fps · mov",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.RenderPreset),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.EpisodesRoot)
        {
            var project = ProjectAncestor(parent);
            var episode = _projectEpisodeRepository.CreateEpisode(connection, project.Id);

            return new ProjectTreeNode(
                ProjectTreeNodeKind.Episode,
                episode.Id,
                episode.Name,
                episode.Notes,
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
                INSERT INTO shots (id, episode_id, name, slug, notes, sort_order, duration_frames)
                VALUES ($id, $episodeId, $name, $slug, $notes, $sortOrder, 240)
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
        var imported = _deviceRepository.CreateImported(
            connection,
            project.Id,
            device.Name,
            device.Manufacturer,
            device.Model,
            device.OsFamily,
            device.MetricsJson);

        return new ProjectTreeNode(
            ProjectTreeNodeKind.Device,
            imported.Id,
            imported.Name,
            $"{imported.Manufacturer} {imported.Model}".Trim(),
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
        var textFontId = ScalarString(
            connection,
            "SELECT id FROM production_fonts WHERE project_id = $projectId AND category = 'text' ORDER BY family_name, id LIMIT 1",
            ("$projectId", project.Id)) ?? "";
        var emojiFontId = ScalarString(
            connection,
            "SELECT id FROM production_fonts WHERE project_id = $projectId AND category = 'emoji' ORDER BY family_name, id LIMIT 1",
            ("$projectId", project.Id)) ?? "";
        var statusBarId = DefaultComponentPresetReference(connection, project.Id, "status_bar");
        var navigationBarId = DefaultComponentPresetReference(connection, project.Id, "navigation_bar");
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
            ("$tokensJson", DefaultThemeTokensJson(family, textFontId, emojiFontId)),
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
            var copy = _projectEpisodeRepository.DuplicateEpisode(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.Episode, copy.Id, copy.Name, copy.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Shot)
        {
            var id = $"shot_{Guid.NewGuid():N}";
            var sortOrder = NextSortOrder(connection, "shots", "episode_id", node.Parent!.Id);
            Execute(
                connection,
                """
                INSERT INTO shots (id, episode_id, name, slug, version, notes, sort_order, fps_override, duration_frames, owner_actor_id, canvas_json, metadata_json)
                SELECT $id, episode_id, $name, slug || '-copy', version, notes, $sortOrder, fps_override, duration_frames, owner_actor_id, canvas_json, metadata_json
                FROM shots
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", $"{node.Name} copy"),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));

            return new ProjectTreeNode(ProjectTreeNodeKind.Shot, id, $"{node.Name} copy", node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            var settings = GetModuleInstanceSettings(node.Id);
            var id = $"module_instance_{Guid.NewGuid():N}";
            var sortOrder = NextSortOrder(connection, "module_instances", "shot_id", settings.ShotId);
            var copyName = UniqueModuleInstanceName(connection, settings.ShotId, $"{node.Name} copy");
            Execute(
                connection,
                """
                INSERT INTO module_instances (
                  id, shot_id, app_id, module_id, name, notes, sort_order, duration_frames,
                  transition_json, content_json, behavior_json, animation_json, metadata_json)
                SELECT $id, shot_id, app_id, module_id, $name, notes, $sortOrder, duration_frames,
                       transition_json, content_json, behavior_json, animation_json, metadata_json
                FROM module_instances
                WHERE id = $sourceId
                """,
                ("$id", id),
                ("$name", copyName),
                ("$sortOrder", sortOrder),
                ("$sourceId", node.Id));
            SynchronizeTimelineDurations(connection);

            return new ProjectTreeNode(
                ProjectTreeNodeKind.ModuleInstance,
                id,
                copyName,
                node.Notes,
                node.RecordClassId,
                node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            var copy = _paletteRepository.Duplicate(connection, node.Id);

            return new ProjectTreeNode(
                ProjectTreeNodeKind.PaletteColor,
                copy.Id,
                copy.Token,
                copy.Note,
                node.RecordClassId,
                node.Parent,
                copy.ValueHex,
                false);
        }

        if (node.Kind == ProjectTreeNodeKind.Device)
        {
            var copy = _deviceRepository.Duplicate(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.Device, copy.Id, copy.Name, node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            var copy = _actorRepository.Duplicate(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.Actor, copy.Id, copy.DisplayName, node.Notes, node.RecordClassId, node.Parent);
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
            var copy = _renderPresetRepository.Duplicate(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.RenderPreset, copy.Id, copy.Name, node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentPreset)
        {
            return DuplicateComponentPreset(node);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            return SaveModuleVariant(node, $"{node.Name} copy");
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

        if (node.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            DeleteModuleVariant(node);
            return;
        }

        using var connection = OpenConnection();
        var table = node.Kind switch
        {
            ProjectTreeNodeKind.Shot => "shots",
            ProjectTreeNodeKind.ModuleInstance => "module_instances",
            ProjectTreeNodeKind.Theme => "themes",
            ProjectTreeNodeKind.ProductionFont => "production_fonts",
            ProjectTreeNodeKind.IconTheme => "icon_themes",
            ProjectTreeNodeKind.Episode or ProjectTreeNodeKind.RenderPreset
                or ProjectTreeNodeKind.PaletteColor or ProjectTreeNodeKind.Device or ProjectTreeNodeKind.Actor => "",
            _ => throw new InvalidOperationException($"Cannot delete {node.Kind}."),
        };

        var usages = GetReferenceUsages(connection, node.Kind, node.Id);
        if (usages.Count > 0)
        {
            throw new InvalidOperationException($"This {node.Kind} is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
        }

        if (node.Kind == ProjectTreeNodeKind.ProductionFont)
        {
            DeleteProductionFontFiles(connection, node.Id);
        }

        if (node.Kind == ProjectTreeNodeKind.Episode)
        {
            _projectEpisodeRepository.DeleteEpisode(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.RenderPreset)
        {
            _renderPresetRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            _paletteRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Device)
        {
            _deviceRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            _actorRepository.Delete(connection, node.Id);
            return;
        }

        Execute(connection, $"DELETE FROM {table} WHERE id = $id", ("$id", node.Id));
        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            SynchronizeTimelineDurations(connection);
        }
    }

    public IReadOnlyList<string> GetReferenceUsages(ProjectTreeNode node)
    {
        using var connection = OpenConnection();
        return GetReferenceUsages(connection, node.Kind, node.Id);
    }

    public void UpdateNode(ProjectTreeNode node)
    {
        using var connection = OpenConnection();
        if (node.Kind == ProjectTreeNodeKind.Project)
        {
            _projectEpisodeRepository.UpdateProjectNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Episode)
        {
            _projectEpisodeRepository.UpdateEpisodeNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.RenderPreset)
        {
            _renderPresetRepository.Rename(connection, node.Id, node.Name);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            _paletteRepository.UpdateNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Device)
        {
            _deviceRepository.Rename(connection, node.Id, node.Name);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            _actorRepository.Rename(connection, node.Id, node.Name);
            return;
        }

        var table = node.Kind switch
        {
            ProjectTreeNodeKind.App => "apps",
            ProjectTreeNodeKind.Module => "modules",
            ProjectTreeNodeKind.Shot => "shots",
            ProjectTreeNodeKind.Theme => "themes",
            ProjectTreeNodeKind.ProductionFont => "production_fonts",
            ProjectTreeNodeKind.IconTheme => "icon_themes",
            ProjectTreeNodeKind.ComponentClass => "component_classes",
            _ => "",
        };

        if (string.IsNullOrWhiteSpace(table)) return;

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
            ProjectTreeNodeKind.App => RenameApp(node, name),
            ProjectTreeNodeKind.ComponentClass => RenameComponentClass(node, name),
            ProjectTreeNodeKind.ComponentPreset => RenameComponentPreset(node, name),
            ProjectTreeNodeKind.Module => RenameModuleClass(node, name),
            ProjectTreeNodeKind.ModuleVariant => RenameModuleVariant(node, name),
            ProjectTreeNodeKind.ModuleInstance => RenameModuleInstance(node, name),
            _ => throw new InvalidOperationException($"Cannot rename {node.Kind} directly."),
        };
    }

    private ProjectTreeNode RenameApp(ProjectTreeNode node, string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException("App name cannot be empty.");
        }

        using var connection = OpenConnection();
        Execute(connection, "UPDATE apps SET name = $name WHERE id = $id", ("$name", nextName), ("$id", node.Id));
        return new ProjectTreeNode(ProjectTreeNodeKind.App, node.Id, nextName, node.Notes,
            node.RecordClassId, node.Parent, isUsed: node.IsUsed, isProtected: node.IsProtected, isLocked: node.IsLocked);
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
