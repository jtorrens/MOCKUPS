using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteEditorChildStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteProductionOwner _production;
    private readonly SqliteResourceOwner _resources;

    internal SqliteEditorChildStore(
        SqliteProjectContext context,
        SqliteDesignOwner design,
        SqliteProductionOwner production,
        SqliteResourceOwner resources)
    {
        _context = context;
        _design = design;
        _production = production;
        _resources = resources;
    }

    internal ProjectTreeNode AddChild(ProjectTreeNode parent)
    {
        using var connection = _context.OpenConnection();
        if (parent.Kind == ProjectTreeNodeKind.Project)
        {
            throw new InvalidOperationException(
                "Project children are created through explicit Apps/Episodes roots.");
        }

        if (parent.Kind == ProjectTreeNodeKind.PaletteRoot)
        {
            var project = ProjectAncestor(parent);
            var color = _resources.PaletteRepository.Create(
                connection,
                project.Id);
            return new ProjectTreeNode(
                ProjectTreeNodeKind.PaletteColor,
                color.Id,
                color.Token,
                color.Note,
                ProjectTreeNode.DefaultRecordClassId(
                    ProjectTreeNodeKind.PaletteColor),
                parent,
                color.ValueHex,
                false);
        }

        if (parent.Kind == ProjectTreeNodeKind.DevicesRoot)
        {
            var project = ProjectAncestor(parent);
            var device = _resources.DeviceRepository.Create(
                connection,
                project.Id);
            return new ProjectTreeNode(
                ProjectTreeNodeKind.Device,
                device.Id,
                device.Name,
                "",
                ProjectTreeNode.DefaultRecordClassId(
                    ProjectTreeNodeKind.Device),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.ActorsRoot)
        {
            var project = ProjectAncestor(parent);
            var actor = _resources.ActorRepository.Create(
                connection,
                project.Id);
            return new ProjectTreeNode(
                ProjectTreeNodeKind.Actor,
                actor.Id,
                actor.DisplayName,
                actor.ShortName,
                ProjectTreeNode.DefaultRecordClassId(
                    ProjectTreeNodeKind.Actor),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.ThemesRoot)
        {
            return AddTheme(parent, "custom");
        }

        if (parent.Kind == ProjectTreeNodeKind.ProductionFontsRoot)
        {
            throw new InvalidOperationException(
                "Production fonts are added through the font importer.");
        }

        if (parent.Kind == ProjectTreeNodeKind.IconThemesRoot)
        {
            throw new InvalidOperationException(
                "Icon themes are rebuilt through Refresh Sets.");
        }

        if (parent.Kind == ProjectTreeNodeKind.EpisodesRoot)
        {
            var project = ProjectAncestor(parent);
            var episode = _production.ProjectEpisodeRepository
                .CreateEpisode(connection, project.Id);
            return new ProjectTreeNode(
                ProjectTreeNodeKind.Episode,
                episode.Id,
                episode.Name,
                episode.Notes,
                ProjectTreeNode.DefaultRecordClassId(
                    ProjectTreeNodeKind.Episode),
                parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException(
                "Shots require an explicit owner Actor and must be created through AddShot.");
        }

        throw new InvalidOperationException(
            $"Cannot add a child to {parent.Kind}.");
    }

    internal ProjectTreeNode AddImportedDevice(
        ProjectTreeNode devicesRoot,
        DeviceImportDraft device)
    {
        if (devicesRoot.Kind != ProjectTreeNodeKind.DevicesRoot)
        {
            throw new InvalidOperationException(
                "Imported devices can only be added from the Devices root.");
        }

        using var connection = _context.OpenConnection();
        var project = ProjectAncestor(devicesRoot);
        var imported = _resources.DeviceRepository.CreateImported(
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
            ProjectTreeNode.DefaultRecordClassId(
                ProjectTreeNodeKind.Device),
            devicesRoot);
    }

    internal ProjectTreeNode AddShot(
        ProjectTreeNode episode,
        string actorId,
        int shotNumber)
    {
        if (episode.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException(
                "Shots can only be added to an Episode.");
        }

        using var connection = _context.OpenConnection();
        _production.ModuleInstanceThemeContextService
            .RequireEpisodeActor(
                connection,
                episode.Id,
                actorId);
        var shot = _production.CreateShot(
            connection,
            episode.Id,
            actorId,
            shotNumber);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.Shot,
            shot.Id,
            shot.Name,
            shot.Notes,
            ProjectTreeNode.DefaultRecordClassId(
                ProjectTreeNodeKind.Shot),
            episode);
    }

    internal ProjectTreeNode AddTheme(
        ProjectTreeNode themesRoot,
        string family)
    {
        if (themesRoot.Kind != ProjectTreeNodeKind.ThemesRoot)
        {
            throw new InvalidOperationException(
                "Themes can only be added from the Themes root.");
        }

        family = family is "ios" or "android" ? family : "custom";
        using var connection = _context.OpenConnection();
        var project = ProjectAncestor(themesRoot);
        var iconThemeId = _resources.IconThemeRepository
            .QueryAll(connection)
            .Where((theme) => theme.ProjectId == project.Id)
            .OrderBy((theme) => theme.Name)
            .ThenBy((theme) => theme.Id)
            .Select((theme) => theme.Id)
            .FirstOrDefault() ?? "";
        var productionFonts = _resources.ProductionFontRepository
            .QueryAll(connection)
            .Where((font) => font.ProjectId == project.Id)
            .ToList();
        var textFontId = FontId(productionFonts, "text");
        var emojiFontId = FontId(productionFonts, "emoji");
        var statusBarId = _design.DefaultComponentVariantReference(
            connection,
            project.Id,
            "status_bar");
        var navigationBarId = _design.DefaultComponentVariantReference(
            connection,
            project.Id,
            "navigation_bar");
        var created = _resources.ThemeRepository.Create(
            connection,
            project.Id,
            family,
            iconThemeId,
            statusBarId,
            navigationBarId,
            SqliteResourceOwner.DefaultThemeTokensJson(
                family,
                textFontId,
                emojiFontId),
            JsonSerializer.Serialize(
                new { note = $"{family} production theme." }));
        return new ProjectTreeNode(
            ProjectTreeNodeKind.Theme,
            created.Id,
            created.Name,
            $"{created.Family} · {SqliteResourceOwner.ThemeReferenceSummary(created)}",
            ProjectTreeNode.DefaultRecordClassId(
                ProjectTreeNodeKind.Theme),
            themesRoot);
    }

    internal int SuggestShotNumber(string episodeId)
    {
        using var connection = _context.OpenConnection();
        return _production.ShotRepository.SuggestShotNumber(
            connection,
            episodeId);
    }

    internal ProjectSettings GetProjectSettings(string projectId) =>
        _production.GetProjectSettings(projectId);

    internal IReadOnlyList<FieldOption> GetRequiredActorOptions(
        string projectId) =>
        _resources.GetRequiredActorOptions(projectId);

    internal ProjectTreeNode ImportProductionFont(
        ProjectTreeNode fontsRoot,
        IReadOnlyList<string> selectedFilePaths) =>
        _resources.ImportProductionFont(
            fontsRoot,
            selectedFilePaths);

    internal IconThemeRefreshResult RefreshIconThemeSets(
        ProjectTreeNode iconThemesRoot) =>
        _resources.RefreshIconThemeSets(iconThemesRoot);

    private static string FontId(
        IReadOnlyList<ProductionFontRecord> fonts,
        string category) =>
        fonts
            .Where((font) => font.Category == category)
            .OrderBy((font) => font.FamilyName)
            .ThenBy((font) => font.Id)
            .Select((font) => font.Id)
            .FirstOrDefault() ?? "";

    private static ProjectTreeNode ProjectAncestor(
        ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent
                ?? throw new InvalidOperationException(
                    $"{node.Kind} has no project ancestor.");
        }

        return current;
    }
}
