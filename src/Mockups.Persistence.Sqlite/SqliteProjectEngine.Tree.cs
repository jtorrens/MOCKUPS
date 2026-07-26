using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public List<ProjectTreeNode> LoadProjectTree()
    {
        using var connection = OpenConnection();
        var projects = _productionOwner.QueryProjectRows(connection);
        var episodes = _productionOwner.QueryEpisodeRows(connection);
        var shots = _productionOwner.ShotRepository.QueryAll(connection);
        var moduleInstances = _productionOwner.ModuleInstanceRepository.QueryAll(connection);
        var apps = _designOwner.AppModuleRepository.QueryApps(connection);
        var modules = _designOwner.AppModuleRepository.QueryModules(connection);
        var moduleNames = modules.ToDictionary(
            (module) => module.Id,
            (module) => module.Name,
            StringComparer.Ordinal);
        var paletteColors = _resourceOwner.QueryPaletteColorRows(connection);
        var devices = _resourceOwner.QueryDeviceRows(connection);
        var actors = _resourceOwner.QueryActorRows(connection);
        var themes = _resourceOwner.ThemeRepository.QueryAll(connection);
        var productionFonts = _resourceOwner.ProductionFontRepository.QueryAll(connection);
        var iconThemes = _resourceOwner.IconThemeRepository.QueryAll(connection);
        var componentClasses =
            _designOwner.QueryComponentClassRows(connection);
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
                systemDataRoot);
            var productionFontsRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.ProductionFontsRoot,
                $"production_fonts_root_{project.Id}",
                "Production Fonts",
                "Approved font families copied into this production.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ProductionFontsRoot),
                productionDataRoot);
            var iconThemesRoot = new ProjectTreeNode(
                ProjectTreeNodeKind.IconThemesRoot,
                $"icon_themes_root_{project.Id}",
                "Icon Themes",
                "Icon sets and shared semantic icon tokens.",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.IconThemesRoot),
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
            productionDataRoot.AddChild(productionFontsRoot);
            systemDataRoot.AddChild(themesRoot);
            systemDataRoot.AddChild(paletteRoot);
            systemDataRoot.AddChild(iconThemesRoot);
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
                var reference = VariantReferenceId.Format(module.Id, variant.Id);
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
                    isLocked: IsVariantLockedForEditing(module.Id, variant.Id, variant.IsLocked)));
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
                $"{theme.Family} · {SqliteResourceOwner.ThemeReferenceSummary(theme)}",
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
                $"{font.Category} · {SqliteResourceOwner.ProductionFontFileCount(font.FilesJson)} files",
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
                $"{SqliteResourceOwner.IconThemeTokenCount(iconTheme.MappingJson)} tokens · {iconTheme.AssetRoot}",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.IconTheme),
                iconThemesRoot,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.IconTheme, iconTheme.Id)));
        }

        foreach (var componentClass in componentClasses.OrderBy((componentClass) => componentClass.ComponentType).ThenBy((componentClass) => componentClass.Name))
        {
            if (!componentClassGroupNodes.TryGetValue(componentClass.ProjectId, out var componentGroups)) continue;
            var groupNode = componentGroups[DesktopPreviewManifest.ComponentCategory(componentClass.ComponentType)];

            var componentNode = new ProjectTreeNode(
                ProjectTreeNodeKind.ComponentClass,
                componentClass.Id,
                componentClass.Name,
                string.IsNullOrWhiteSpace(componentClass.Notes) ? EditorUiText.IdentifierLabel(componentClass.ComponentType) : componentClass.Notes,
                componentClass.RecordClassId,
                groupNode,
                isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ComponentClass, componentClass.Id));
            groupNode.AddChild(componentNode);

            foreach (var variant in
                     SqliteDesignOwner.ComponentClassVariants(
                         componentClass.MetadataJson))
            {
                componentNode.AddChild(new ProjectTreeNode(
                    ProjectTreeNodeKind.ComponentVariant,
                    VariantReferenceId.Format(componentClass.Id, variant.Id),
                    variant.Name,
                    variant.IsProtected ? "Protected component variant" : "Component variant",
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentVariant),
                    componentNode,
                    isUsed: IsUsed(referenceUsageIndex, ProjectTreeNodeKind.ComponentVariant, VariantReferenceId.Format(componentClass.Id, variant.Id)),
                    isProtected: variant.IsProtected,
                    isLocked: IsVariantLockedForEditing(componentClass.Id, variant.Id, variant.IsLocked)));
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
            if (!moduleNames.TryGetValue(moduleInstance.ModuleId, out var moduleName))
            {
                throw new InvalidOperationException($"Missing module '{moduleInstance.ModuleId}'.");
            }

            shot.AddChild(new ProjectTreeNode(
                ProjectTreeNodeKind.ModuleInstance,
                moduleInstance.Id,
                moduleInstance.Name,
                $"{moduleName} · {moduleInstance.DurationFrames} frames · {ModuleTransitionLabel(moduleInstance.TransitionJson)}",
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
            var color = _resourceOwner.PaletteRepository.Create(connection, project.Id);

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
            var device = _resourceOwner.DeviceRepository.Create(connection, project.Id);

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
            var actor = _resourceOwner.ActorRepository.Create(connection, project.Id);

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

        if (parent.Kind == ProjectTreeNodeKind.EpisodesRoot)
        {
            var project = ProjectAncestor(parent);
            if (_productionOwner.ShotManagerIntegrationRepository.GetAssociation(project.Id) is not null)
            {
                throw new InvalidOperationException(
                    "Shot Manager governs this Project's Episodes.");
            }
            var episode = _productionOwner.ProjectEpisodeRepository.CreateEpisode(connection, project.Id);

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
            throw new InvalidOperationException(
                "Shots require an explicit owner Actor and must be created through AddShot.");
        }

        throw new InvalidOperationException($"Cannot add a child to {parent.Kind}.");
    }

    public int SuggestShotNumber(string episodeId)
    {
        using var connection = OpenConnection();
        return _productionOwner.ShotRepository.SuggestShotNumber(
            connection,
            episodeId);
    }

    public ProjectTreeNode AddShot(
        ProjectTreeNode episode,
        string actorId,
        int shotNumber)
    {
        if (episode.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException("Shots can only be added to an Episode.");
        }

        using var connection = OpenConnection();
        _productionOwner.ModuleInstanceThemeContextService.RequireEpisodeActor(connection, episode.Id, actorId);
        var shot = _productionOwner.ShotRepository.Create(
            connection,
            episode.Id,
            actorId,
            shotNumber);

        return new ProjectTreeNode(
            ProjectTreeNodeKind.Shot,
            shot.Id,
            shot.Name,
            shot.Notes,
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot),
            episode);
    }

    public ProjectTreeNode AddImportedDevice(ProjectTreeNode devicesRoot, DeviceImportDraft device)
    {
        if (devicesRoot.Kind != ProjectTreeNodeKind.DevicesRoot)
        {
            throw new InvalidOperationException("Imported devices can only be added from the Devices root.");
        }

        using var connection = OpenConnection();
        var project = ProjectAncestor(devicesRoot);
        var imported = _resourceOwner.DeviceRepository.CreateImported(
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
        var iconThemeId = _resourceOwner.IconThemeRepository.QueryAll(connection)
            .Where((iconTheme) => iconTheme.ProjectId == project.Id)
            .OrderBy((iconTheme) => iconTheme.Name)
            .ThenBy((iconTheme) => iconTheme.Id)
            .Select((iconTheme) => iconTheme.Id)
            .FirstOrDefault() ?? "";
        var productionFonts = _resourceOwner.ProductionFontRepository.QueryAll(connection)
            .Where((font) => font.ProjectId == project.Id)
            .ToList();
        var textFontId = productionFonts
            .Where((font) => font.Category == "text")
            .OrderBy((font) => font.FamilyName)
            .ThenBy((font) => font.Id)
            .Select((font) => font.Id)
            .FirstOrDefault() ?? "";
        var emojiFontId = productionFonts
            .Where((font) => font.Category == "emoji")
            .OrderBy((font) => font.FamilyName)
            .ThenBy((font) => font.Id)
            .Select((font) => font.Id)
            .FirstOrDefault() ?? "";
        var statusBarId = _designOwner.DefaultComponentVariantReference(
            connection,
            project.Id,
            "status_bar");
        var navigationBarId =
            _designOwner.DefaultComponentVariantReference(
                connection,
                project.Id,
                "navigation_bar");
        var created = _resourceOwner.ThemeRepository.Create(
            connection,
            project.Id,
            family,
            iconThemeId,
            statusBarId,
            navigationBarId,
            SqliteResourceOwner.DefaultThemeTokensJson(family, textFontId, emojiFontId),
            JsonSerializer.Serialize(new { note = $"{family} production theme." }));

        return new ProjectTreeNode(
            ProjectTreeNodeKind.Theme,
            created.Id,
            created.Name,
            $"{created.Family} · {SqliteResourceOwner.ThemeReferenceSummary(created)}",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Theme),
            themesRoot);
    }

    public ProjectTreeNode Duplicate(ProjectTreeNode node)
    {
        using var connection = OpenConnection();

        if (node.Kind == ProjectTreeNodeKind.Episode)
        {
            if (_productionOwner.ShotManagerIntegrationRepository.GetEpisodeBinding(node.Id) is not null)
            {
                throw new InvalidOperationException(
                    "Shot Manager governs this Episode and it cannot be duplicated locally.");
            }
            var copy = _productionOwner.ProjectEpisodeRepository.DuplicateEpisode(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.Episode, copy.Id, copy.Name, copy.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Shot)
        {
            var id = $"shot_{Guid.NewGuid():N}";
            var source = _productionOwner.ShotRepository.Get(connection, node.Id);
            var duplicate = _productionOwner.ShotRepository.Duplicate(
                connection,
                node.Id,
                id,
                $"{node.Name} copy",
                source.OwnerActorId,
                _productionOwner.ShotRepository.SuggestShotNumber(
                    connection,
                    source.EpisodeId));
            return new ProjectTreeNode(
                ProjectTreeNodeKind.Shot,
                duplicate.Id,
                duplicate.Name,
                duplicate.Notes,
                node.RecordClassId,
                node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            var settings = GetModuleInstanceSettings(node.Id);
            var id = $"module_instance_{Guid.NewGuid():N}";
            var sortOrder = _productionOwner.ModuleInstanceRepository.NextSortOrder(connection, settings.ShotId);
            var copyName = _productionOwner.ModuleInstanceRepository.UniqueName(connection, settings.ShotId, $"{node.Name} copy");
            _productionOwner.ModuleInstanceRepository.Duplicate(
                connection,
                node.Id,
                id,
                copyName,
                sortOrder);
            _productionOwner.SynchronizeTimelineDurations(connection);

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
            var copy = _resourceOwner.PaletteRepository.Duplicate(connection, node.Id);

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
            var copy = _resourceOwner.DeviceRepository.Duplicate(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.Device, copy.Id, copy.Name, node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            var copy = _resourceOwner.ActorRepository.Duplicate(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.Actor, copy.Id, copy.DisplayName, node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.Theme)
        {
            var copy = _resourceOwner.ThemeRepository.Duplicate(connection, node.Id, $"{node.Name} copy");

            return new ProjectTreeNode(ProjectTreeNodeKind.Theme, copy.Id, copy.Name, node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.IconTheme)
        {
            var source = _resourceOwner.IconThemeRepository.Get(connection, node.Id);
            var id = $"icon_theme_{Guid.NewGuid():N}";
            var duplicatedAssets = _resourceOwner.DuplicateIconThemeAssets(connection, source, $"{node.Name} copy");
            var name = duplicatedAssets.Name;
            var assetRoot = duplicatedAssets.AssetRoot;
            var metadata = SqliteResourceOwner.IconThemeMetadata(
                _resourceOwner.IconThemeAssetDirectory(
                    connection,
                    source.ProjectId,
                    assetRoot),
                name);
            try
            {
                _resourceOwner.IconThemeRepository.CreateDuplicate(
                    connection,
                    node.Id,
                    id,
                    name,
                    assetRoot,
                    metadata.ToJsonString());
            }
            catch
            {
                _resourceOwner.DeleteIconThemeAssetDirectory(connection, source.ProjectId, assetRoot);
                throw;
            }

            return new ProjectTreeNode(ProjectTreeNodeKind.IconTheme, id, name, node.Notes, node.RecordClassId, node.Parent);
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentVariant)
        {
            return DuplicateComponentVariant(node);
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            return SaveModuleVariant(node, $"{node.Name} copy");
        }

        throw new InvalidOperationException($"Cannot duplicate {node.Kind}.");
    }

    public ProjectTreeNode DuplicateShot(
        ProjectTreeNode shot,
        string actorId,
        int shotNumber)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot
            || shot.Parent?.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException(
                "Only a concrete Shot inside an Episode can be duplicated.");
        }
        using var connection = OpenConnection();
        var duplicate = _productionOwner.ShotRepository.Duplicate(
            connection,
            shot.Id,
            $"shot_{Guid.NewGuid():N}",
            $"{shot.Name} copy",
            actorId,
            shotNumber);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.Shot,
            duplicate.Id,
            duplicate.Name,
            duplicate.Notes,
            shot.RecordClassId,
            shot.Parent);
    }

    public void Delete(ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.ComponentVariant)
        {
            DeleteComponentVariant(node);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            DeleteModuleVariant(node);
            return;
        }

        using var connection = OpenConnection();
        if (node.Kind is not (
            ProjectTreeNodeKind.Shot
            or ProjectTreeNodeKind.ModuleInstance
            or ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Theme
            or ProjectTreeNodeKind.PaletteColor
            or ProjectTreeNodeKind.Device
            or ProjectTreeNodeKind.Actor
            or ProjectTreeNodeKind.ProductionFont
            or ProjectTreeNodeKind.IconTheme))
        {
            throw new InvalidOperationException($"Cannot delete {node.Kind}.");
        }

        var usages = GetReferenceUsages(connection, node.Kind, node.Id);
        if (usages.Count > 0)
        {
            throw new InvalidOperationException($"This {node.Kind} is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
        }

        if (node.Kind == ProjectTreeNodeKind.ProductionFont)
        {
            _resourceOwner.DeleteProductionFontFiles(connection, node.Id);
            _resourceOwner.ProductionFontRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.IconTheme)
        {
            var iconTheme = _resourceOwner.IconThemeRepository.Get(connection, node.Id);
            _resourceOwner.DeleteIconThemeAssetDirectory(
                connection,
                iconTheme.ProjectId,
                iconTheme.AssetRoot);
            _resourceOwner.IconThemeRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Episode)
        {
            if (_productionOwner.ShotManagerIntegrationRepository.GetEpisodeBinding(node.Id) is not null)
            {
                throw new InvalidOperationException(
                    "Shot Manager governs this Episode and it cannot be deleted locally.");
            }
            _productionOwner.ProjectEpisodeRepository.DeleteEpisode(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            _resourceOwner.PaletteRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Device)
        {
            _resourceOwner.DeviceRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            _resourceOwner.ActorRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Theme)
        {
            _resourceOwner.ThemeRepository.Delete(connection, node.Id);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            _productionOwner.ModuleInstanceRepository.Delete(connection, node.Id);
            _productionOwner.SynchronizeTimelineDurations(connection);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Shot)
        {
            _productionOwner.ShotRepository.Delete(connection, node.Id);
            return;
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
            _productionOwner.ProjectEpisodeRepository.UpdateProjectNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Episode)
        {
            if (_productionOwner.ShotManagerIntegrationRepository.GetEpisodeBinding(node.Id) is not null)
            {
                throw new InvalidOperationException(
                    "Shot Manager governs this Episode. Change it there and synchronize.");
            }
            _productionOwner.ProjectEpisodeRepository.UpdateEpisodeNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.PaletteColor)
        {
            _resourceOwner.PaletteRepository.UpdateNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Device)
        {
            _resourceOwner.DeviceRepository.Rename(connection, node.Id, node.Name);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Actor)
        {
            _resourceOwner.ActorRepository.Rename(connection, node.Id, node.Name);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Theme)
        {
            _resourceOwner.ThemeRepository.Rename(connection, node.Id, node.Name);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.ProductionFont)
        {
            _resourceOwner.ProductionFontRepository.Rename(connection, node.Id, node.Name);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.App)
        {
            _designOwner.AppModuleRepository.UpdateAppNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Module)
        {
            _designOwner.AppModuleRepository.UpdateModuleNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            _designOwner.ComponentClassRepository.UpdateNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.Shot)
        {
            _productionOwner.ShotRepository.UpdateNode(connection, node.Id, node.Name, node.Notes);
            return;
        }

        if (node.Kind == ProjectTreeNodeKind.IconTheme)
        {
            var row = _resourceOwner.IconThemeRepository.Get(connection, node.Id);
            var renamedAssets = _resourceOwner.RenameIconThemeAssets(
                connection,
                row,
                node.Name);
            var metadata = SqliteResourceOwner.IconThemeMetadata(
                _resourceOwner.IconThemeAssetDirectory(
                    connection,
                    row.ProjectId,
                    renamedAssets.AssetRoot),
                renamedAssets.Name);
            _resourceOwner.IconThemeRepository.UpdateIdentity(
                connection,
                node.Id,
                renamedAssets.Name,
                renamedAssets.AssetRoot,
                metadata.ToJsonString());
            return;
        }
    }

    public ProjectTreeNode RenameDirectNode(ProjectTreeNode node, string name)
    {
        return node.Kind switch
        {
            ProjectTreeNodeKind.Project => RenameStoredNode(node, name),
            ProjectTreeNodeKind.App => RenameApp(node, name),
            ProjectTreeNodeKind.ComponentClass => RenameComponentClass(node, name),
            ProjectTreeNodeKind.ComponentVariant => RenameComponentVariant(node, name),
            ProjectTreeNodeKind.Module => RenameModuleClass(node, name),
            ProjectTreeNodeKind.ModuleVariant => RenameModuleVariant(node, name),
            ProjectTreeNodeKind.ModuleInstance => RenameModuleInstance(node, name),
            ProjectTreeNodeKind.Episode
                or ProjectTreeNodeKind.Shot
                or ProjectTreeNodeKind.PaletteColor
                or ProjectTreeNodeKind.IconTheme
                or ProjectTreeNodeKind.Device
                or ProjectTreeNodeKind.Actor
                or ProjectTreeNodeKind.Theme
                or ProjectTreeNodeKind.ProductionFont => RenameStoredNode(node, name),
            _ => throw new InvalidOperationException($"Cannot rename {node.Kind} directly."),
        };
    }

    private ProjectTreeNode RenameStoredNode(ProjectTreeNode node, string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException($"{node.Kind} name cannot be empty.");
        }

        var renamed = new ProjectTreeNode(
            node.Kind,
            node.Id,
            nextName,
            node.Notes,
            node.RecordClassId,
            node.Parent,
            node.ColorHex,
            node.IsUsed,
            node.IsProtected,
            node.IsLocked);
        UpdateNode(renamed);
        return renamed;
    }

    private ProjectTreeNode RenameApp(ProjectTreeNode node, string name)
    {
        var nextName = name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            throw new InvalidOperationException("App name cannot be empty.");
        }

        using var connection = OpenConnection();
        _designOwner.AppModuleRepository.RenameApp(connection, node.Id, nextName);
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
