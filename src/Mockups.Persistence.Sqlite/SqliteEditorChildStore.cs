using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteEditorChildStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteProductionOwner _production;
    private readonly SqliteResourceOwner _resources;
    private readonly IReadOnlyDictionary<string, Func<ProjectTreeNode, RecordCreationDefinition>>
        _creationPreparers;
    private readonly IReadOnlyDictionary<string, Func<ProjectTreeNode, IReadOnlyDictionary<string, string>, ProjectTreeNode>>
        _creationCommitters;

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
        _creationPreparers = new Dictionary<string, Func<ProjectTreeNode, RecordCreationDefinition>>(StringComparer.Ordinal)
        {
            ["palette"] = PreparePaletteCreation,
            ["device"] = PrepareBlankDeviceCreation,
            ["actor"] = PrepareActorCreation,
            ["theme"] = PrepareThemeCreation,
            ["episode"] = PrepareEpisodeCreation,
            ["shot"] = PrepareShotCreation,
        };
        _creationCommitters = new Dictionary<string, Func<ProjectTreeNode, IReadOnlyDictionary<string, string>, ProjectTreeNode>>(StringComparer.Ordinal)
        {
            ["palette"] = CreatePalette,
            ["device"] = CreateBlankDevice,
            ["actor"] = CreateActor,
            ["theme"] = CreateTheme,
            ["episode"] = CreateEpisode,
            ["shot"] = CreateShot,
        };
    }

    internal RecordCreationDefinition PrepareRecordCreation(
        ProjectTreeNode parent,
        string creationId)
    {
        if (!_creationPreparers.TryGetValue(creationId, out var prepare))
        {
            throw new InvalidOperationException(
                $"Record creation '{creationId}' is not registered.");
        }
        return prepare(parent);
    }

    internal ProjectTreeNode CreateRecord(
        ProjectTreeNode parent,
        RecordCreationDraft draft)
    {
        var definition = PrepareRecordCreation(parent, draft.DefinitionId);
        if (definition.ValidationError(draft.Values) is { } error)
        {
            throw new InvalidOperationException(error);
        }
        if (!_creationCommitters.TryGetValue(draft.DefinitionId, out var commit))
        {
            throw new InvalidOperationException(
                $"Record creation '{draft.DefinitionId}' has no commit owner.");
        }
        return commit(parent, draft.Values);
    }

    private RecordCreationDefinition PreparePaletteCreation(ProjectTreeNode parent)
    {
        RequireParent(parent, ProjectTreeNodeKind.PaletteRoot, "palette");
        return EmptyCreation("palette", "paletteColor", "Add palette color");
    }

    private RecordCreationDefinition PrepareBlankDeviceCreation(ProjectTreeNode parent)
    {
        RequireParent(parent, ProjectTreeNodeKind.DevicesRoot, "device");
        return EmptyCreation("device", "device", "Add blank device");
    }

    private RecordCreationDefinition PrepareActorCreation(ProjectTreeNode parent)
    {
        RequireParent(parent, ProjectTreeNodeKind.ActorsRoot, "actor");
        using var connection = _context.OpenConnection();
        var projectId = ProjectAncestor(parent).Id;
        var index = SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM actors WHERE project_id = $projectId",
            ("$projectId", projectId)) + 1;
        var palette = _resources.GetPaletteColorOptions(projectId);
        return new RecordCreationDefinition(
            "actor", "actor", "Add Actor",
            "Complete every required Actor value before creating the record.", "Add",
            [
                Field("core.name", "Name", ValueKind.StringSingleLine, $"Actor {index}"),
                Field(RecordClassFieldCatalog.Get("actor.shortName"), $"A{index}"),
                Field(RecordClassFieldCatalog.Get("actor.defaultDeviceId"), "", _resources.GetDeviceOptions(projectId)),
                Field(RecordClassFieldCatalog.Get("actor.defaultThemeId"), "", _resources.GetThemeOptions(projectId)),
                Field(RecordClassFieldCatalog.Get("actor.color.modes"), "|", palette),
                Field(RecordClassFieldCatalog.Get("actor.avatarTextColor.modes"), "|", palette),
                Field(RecordClassFieldCatalog.Get("actor.wallpaper.color"), "|", palette),
            ]);
    }

    private RecordCreationDefinition PrepareThemeCreation(ProjectTreeNode parent)
    {
        RequireParent(parent, ProjectTreeNodeKind.ThemesRoot, "theme");
        var projectId = ProjectAncestor(parent).Id;
        return new RecordCreationDefinition(
            "theme", "theme", "Create theme",
            "Choose the complete reference set used by the new Theme.", "Create",
            [
                Field(RecordClassFieldCatalog.Get("theme.family"), ""),
                Field(RecordClassFieldCatalog.Get("theme.iconThemeId"), "", _resources.GetIconThemeOptions(projectId)),
                Field(RecordClassFieldCatalog.Get("theme.statusBarId"), "", _design.GetComponentVariantReferenceOptionsByType(projectId, "status_bar")),
                Field(RecordClassFieldCatalog.Get("theme.navigationBarId"), "", _design.GetComponentVariantReferenceOptionsByType(projectId, "navigation_bar")),
                Field(RecordClassFieldCatalog.Get("theme.typography.fontFamilyId"), "", _resources.GetProductionFontOptions(projectId, "text")),
                Field(RecordClassFieldCatalog.Get("theme.typography.emojiFontFamilyId"), "", _resources.GetProductionFontOptions(projectId, "emoji")),
            ]);
    }

    private RecordCreationDefinition PrepareEpisodeCreation(ProjectTreeNode parent)
    {
        RequireParent(parent, ProjectTreeNodeKind.EpisodesRoot, "episode");
        return EmptyCreation("episode", "episode", "Add episode");
    }

    private RecordCreationDefinition PrepareShotCreation(ProjectTreeNode parent)
    {
        RequireParent(parent, ProjectTreeNodeKind.Episode, "shot");
        var projectId = ProjectAncestor(parent).Id;
        return new RecordCreationDefinition(
            "shot", "shot", "Add Shot",
            "Choose the Actor that owns this Shot. A Shot can never be ownerless.", "Add",
            [
                Field(RecordClassFieldCatalog.Get("shot.ownerActorId"), "", _resources.GetRequiredActorOptions(projectId)),
                Field("shot.creation.shotNumber", "Shot number", ValueKind.Integer,
                    SuggestShotNumber(parent.Id).ToString(),
                    new NumberDefinition(1, 99_999_999, 1, 0)),
            ]);
    }

    private ProjectTreeNode CreatePalette(ProjectTreeNode parent, IReadOnlyDictionary<string, string> values)
    {
        using var connection = _context.OpenConnection();
        var color = _resources.PaletteRepository.Create(connection, ProjectAncestor(parent).Id);
        return new ProjectTreeNode(ProjectTreeNodeKind.PaletteColor, color.Id, color.Token, color.Note,
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.PaletteColor), parent, color.ValueHex, false);
    }

    private ProjectTreeNode CreateBlankDevice(ProjectTreeNode parent, IReadOnlyDictionary<string, string> values)
    {
        using var connection = _context.OpenConnection();
        var device = _resources.DeviceRepository.Create(connection, ProjectAncestor(parent).Id);
        return new ProjectTreeNode(ProjectTreeNodeKind.Device, device.Id, device.Name, "",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Device), parent);
    }

    private ProjectTreeNode CreateActor(ProjectTreeNode parent, IReadOnlyDictionary<string, string> values)
    {
        using var connection = _context.OpenConnection();
        var actor = _resources.ActorRepository.Create(
            connection, ProjectAncestor(parent).Id,
            Required(values, "core.name"), Required(values, "actor.shortName"),
            Required(values, "actor.defaultDeviceId"), Required(values, "actor.defaultThemeId"),
            Required(values, "actor.color.modes"), Required(values, "actor.avatarTextColor.modes"),
            Required(values, "actor.wallpaper.color"));
        return new ProjectTreeNode(ProjectTreeNodeKind.Actor, actor.Id, actor.DisplayName, actor.ShortName,
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Actor), parent);
    }

    private ProjectTreeNode CreateTheme(ProjectTreeNode parent, IReadOnlyDictionary<string, string> values)
    {
        using var connection = _context.OpenConnection();
        var projectId = ProjectAncestor(parent).Id;
        var family = Required(values, "theme.family");
        var paletteIds = _resources.PaletteRepository.QueryAll(connection)
            .Where((color) => color.ProjectId == projectId)
            .GroupBy((color) => color.Token, StringComparer.Ordinal)
            .ToDictionary((group) => group.Key, (group) => group.Single().Id, StringComparer.Ordinal);
        var created = _resources.ThemeRepository.Create(
            connection, projectId, family,
            Required(values, "theme.iconThemeId"), Required(values, "theme.statusBarId"),
            Required(values, "theme.navigationBarId"),
            SqliteResourceOwner.DefaultThemeTokensJson(
                family,
                Required(values, "theme.typography.fontFamilyId"),
                Required(values, "theme.typography.emojiFontFamilyId"),
                paletteIds),
            JsonSerializer.Serialize(new { note = $"{family} production theme." }));
        return new ProjectTreeNode(ProjectTreeNodeKind.Theme, created.Id, created.Name,
            $"{created.Family} · {SqliteResourceOwner.ThemeReferenceSummary(created)}",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Theme), parent);
    }

    private ProjectTreeNode CreateEpisode(ProjectTreeNode parent, IReadOnlyDictionary<string, string> values)
    {
        using var connection = _context.OpenConnection();
        var episode = _production.ProjectEpisodeRepository.CreateEpisode(connection, ProjectAncestor(parent).Id);
        return new ProjectTreeNode(ProjectTreeNodeKind.Episode, episode.Id, episode.Name, episode.Notes,
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Episode), parent);
    }

    private ProjectTreeNode CreateShot(ProjectTreeNode parent, IReadOnlyDictionary<string, string> values)
    {
        using var connection = _context.OpenConnection();
        var actorId = Required(values, "shot.ownerActorId");
        _production.ModuleInstanceThemeContextService.RequireEpisodeActor(connection, parent.Id, actorId);
        var shot = _production.CreateShot(
            connection, parent.Id, actorId,
            int.Parse(Required(values, "shot.creation.shotNumber")));
        return new ProjectTreeNode(ProjectTreeNodeKind.Shot, shot.Id, shot.Name, shot.Notes,
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot), parent);
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

    private static RecordCreationDefinition EmptyCreation(
        string id,
        string recordClassId,
        string title) =>
        new(id, recordClassId, title, "The complete record is created from declared defaults.", "Add", [], false);

    private static FieldValue Field(
        string id,
        string label,
        ValueKind kind,
        string value,
        NumberDefinition? number = null) =>
        new(new FieldDefinition(id, label, kind, DefaultValue: value, Number: number), value);

    private static FieldValue Field(
        RecordClassFieldDescriptor descriptor,
        string value,
        IReadOnlyList<FieldOption>? options = null) =>
        new(new FieldDefinition(
            descriptor.Id, descriptor.Label, descriptor.ValueKind, descriptor.IsEditable,
            value, Options: options ?? descriptor.Options, PairLabels: descriptor.PairLabels,
            ImagePreview: descriptor.ImagePreview, Number: descriptor.Number,
            RecordReference: descriptor.RecordReference, Unit: descriptor.Unit), value);

    private static string Required(IReadOnlyDictionary<string, string> values, string fieldId) =>
        values.TryGetValue(fieldId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Creation value '{fieldId}' is required.");

    private static void RequireParent(
        ProjectTreeNode parent,
        ProjectTreeNodeKind expected,
        string creationId)
    {
        if (parent.Kind != expected)
        {
            throw new InvalidOperationException(
                $"Record creation '{creationId}' requires parent {expected}, not {parent.Kind}.");
        }
    }

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
