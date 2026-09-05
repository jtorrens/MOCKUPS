using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteEditorNavigationStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _designOwner;
    private readonly SqliteProductionOwner _productionOwner;
    private readonly SqliteResourceOwner _resourceOwner;
    private readonly ReferenceUsageService _referenceUsageService;

    internal SqliteEditorNavigationStore(
        SqliteProjectContext context,
        SqliteDesignOwner designOwner,
        SqliteProductionOwner productionOwner,
        SqliteResourceOwner resourceOwner,
        ReferenceUsageService referenceUsageService)
    {
        _context = context;
        _designOwner = designOwner;
        _productionOwner = productionOwner;
        _resourceOwner = resourceOwner;
        _referenceUsageService = referenceUsageService;
    }

    internal IReadOnlyList<ProjectTreeNode> LoadProjectTree()
    {
        using var connection = _context.OpenConnection();
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
            foreach (var variant in SqliteDesignOwner.ModuleVariants(module.MetadataJson))
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
                    isLocked: _designOwner.IsVariantLockedForEditing(module.Id, variant.Id, variant.IsLocked)));
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
                    isLocked: _designOwner.IsVariantLockedForEditing(componentClass.Id, variant.Id, variant.IsLocked)));
            }
        }

        foreach (var shot in shots
                     .OrderBy((shot) => shot.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy((shot) => shot.Name, StringComparer.Ordinal)
                     .ThenBy((shot) => shot.Id, StringComparer.Ordinal))
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

    private static bool IsUsed(
        IReadOnlyDictionary<ReferenceTarget, IReadOnlyList<ReferenceUsageRecord>> index,
        ProjectTreeNodeKind kind,
        string id)
    {
        return index.ContainsKey(new ReferenceTarget(kind, id));
    }

    private static string ModuleTransitionLabel(string transitionJson)
    {
        var type = MotionVariantValue.Parse(
            transitionJson).Transition;
        return char.ToUpperInvariant(type[0])
            + type[1..];
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
}
