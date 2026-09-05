using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteEditorNodeCommandStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteProductionOwner _production;
    private readonly SqliteResourceOwner _resources;
    private readonly ReferenceUsageService _referenceUsages;
    private readonly SqliteCoreFieldStore _coreFields;

    internal SqliteEditorNodeCommandStore(
        SqliteProjectContext context,
        SqliteDesignOwner design,
        SqliteProductionOwner production,
        SqliteResourceOwner resources,
        ReferenceUsageService referenceUsages,
        SqliteCoreFieldStore coreFields)
    {
        _context = context;
        _design = design;
        _production = production;
        _resources = resources;
        _referenceUsages = referenceUsages;
        _coreFields = coreFields;
    }

    internal void Delete(ProjectTreeNode node)
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

        using var connection = _context.OpenConnection();
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
            throw new InvalidOperationException(
                $"Cannot delete {node.Kind}.");
        }

        var usages = GetReferenceUsages(
            connection,
            node.Kind,
            node.Id);
        if (usages.Count > 0)
        {
            throw new InvalidOperationException(
                $"This {node.Kind} is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
        }

        switch (node.Kind)
        {
            case ProjectTreeNodeKind.ProductionFont:
                _resources.DeleteProductionFontFiles(
                    connection,
                    node.Id);
                _resources.ProductionFontRepository.Delete(
                    connection,
                    node.Id);
                return;
            case ProjectTreeNodeKind.IconTheme:
                DeleteIconTheme(connection, node.Id);
                return;
            case ProjectTreeNodeKind.Episode:
                _production.ProjectEpisodeRepository.DeleteEpisode(
                    connection,
                    node.Id);
                return;
            case ProjectTreeNodeKind.PaletteColor:
                _resources.PaletteRepository.Delete(
                    connection,
                    node.Id);
                return;
            case ProjectTreeNodeKind.Device:
                _resources.DeviceRepository.Delete(
                    connection,
                    node.Id);
                return;
            case ProjectTreeNodeKind.Actor:
                _resources.ActorRepository.Delete(
                    connection,
                    node.Id);
                return;
            case ProjectTreeNodeKind.Theme:
                _resources.ThemeRepository.Delete(
                    connection,
                    node.Id);
                return;
            case ProjectTreeNodeKind.ModuleInstance:
                _production.ModuleInstanceRepository.Delete(
                    connection,
                    node.Id);
                _production.SynchronizeTimelineDurations(connection);
                return;
            case ProjectTreeNodeKind.Shot:
                _production.ShotRepository.Delete(
                    connection,
                    node.Id);
                return;
        }
    }

    internal ProjectTreeNode Duplicate(ProjectTreeNode node)
    {
        using var connection = _context.OpenConnection();
        switch (node.Kind)
        {
            case ProjectTreeNodeKind.Episode:
            {
                var copy = _production.ProjectEpisodeRepository
                    .DuplicateEpisode(
                        connection,
                        node.Id,
                        $"{node.Name} copy");
                return new ProjectTreeNode(
                    ProjectTreeNodeKind.Episode,
                    copy.Id,
                    copy.Name,
                    copy.Notes,
                    node.RecordClassId,
                    node.Parent);
            }
            case ProjectTreeNodeKind.Shot:
            {
                var source = _production.ShotRepository.Get(
                    connection,
                    node.Id);
                var copy = _production.DuplicateShot(
                    connection,
                    node.Id,
                    $"shot_{Guid.NewGuid():N}",
                    $"{node.Name} copy",
                    source.OwnerActorId,
                    _production.ShotRepository.SuggestShotNumber(
                        connection,
                        source.EpisodeId));
                return new ProjectTreeNode(
                    ProjectTreeNodeKind.Shot,
                    copy.Id,
                    copy.Name,
                    copy.Notes,
                    node.RecordClassId,
                    node.Parent);
            }
            case ProjectTreeNodeKind.PaletteColor:
            {
                var copy = _resources.PaletteRepository.Duplicate(
                    connection,
                    node.Id);
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
            case ProjectTreeNodeKind.Device:
            {
                var copy = _resources.DeviceRepository.Duplicate(
                    connection,
                    node.Id,
                    $"{node.Name} copy");
                return new ProjectTreeNode(
                    ProjectTreeNodeKind.Device,
                    copy.Id,
                    copy.Name,
                    node.Notes,
                    node.RecordClassId,
                    node.Parent);
            }
            case ProjectTreeNodeKind.Actor:
            {
                var copy = _resources.ActorRepository.Duplicate(
                    connection,
                    node.Id,
                    $"{node.Name} copy");
                return new ProjectTreeNode(
                    ProjectTreeNodeKind.Actor,
                    copy.Id,
                    copy.DisplayName,
                    node.Notes,
                    node.RecordClassId,
                    node.Parent);
            }
            case ProjectTreeNodeKind.Theme:
            {
                var copy = _resources.ThemeRepository.Duplicate(
                    connection,
                    node.Id,
                    $"{node.Name} copy");
                return new ProjectTreeNode(
                    ProjectTreeNodeKind.Theme,
                    copy.Id,
                    copy.Name,
                    node.Notes,
                    node.RecordClassId,
                    node.Parent);
            }
            case ProjectTreeNodeKind.IconTheme:
                return DuplicateIconTheme(connection, node);
            case ProjectTreeNodeKind.ComponentVariant:
                return _design.DuplicateComponentVariant(node);
            case ProjectTreeNodeKind.ModuleVariant:
                return _design.SaveModuleVariant(
                    node,
                    $"{node.Name} copy");
            default:
                throw new InvalidOperationException(
                    $"Cannot duplicate {node.Kind}.");
        }
    }

    internal ProjectTreeNode DuplicateShot(
        ProjectTreeNode shot,
        int shotNumber)
    {
        if (shot.Kind != ProjectTreeNodeKind.Shot
            || shot.Parent?.Kind != ProjectTreeNodeKind.Episode)
        {
            throw new InvalidOperationException(
                "Only a concrete Shot inside an Episode can be duplicated.");
        }

        using var connection = _context.OpenConnection();
        var source = _production.ShotRepository.Get(
            connection,
            shot.Id);
        var duplicate = _production.DuplicateShot(
            connection,
            shot.Id,
            $"shot_{Guid.NewGuid():N}",
            $"{shot.Name} copy",
            source.OwnerActorId,
            shotNumber);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.Shot,
            duplicate.Id,
            duplicate.Name,
            duplicate.Notes,
            shot.RecordClassId,
            shot.Parent);
    }

    internal ProjectTreeNode RenameDirectNode(
        ProjectTreeNode node,
        string name) =>
        _coreFields.RenameDirectNode(node, name);

    internal void ReplaceComponentVariantConfig(
        ProjectTreeNode node,
        string configJson) =>
        _design.ReplaceComponentVariantConfig(node, configJson);

    internal void ReplaceModuleVariantConfig(
        ProjectTreeNode node,
        string configJson) =>
        _design.ReplaceModuleVariantConfig(node, configJson);

    internal ProjectTreeNode SaveComponentVariant(
        ProjectTreeNode sourceNode,
        string name) =>
        _design.SaveComponentVariant(sourceNode, name);

    internal ProjectTreeNode SaveModuleVariant(
        ProjectTreeNode sourceNode,
        string name) =>
        _design.SaveModuleVariant(sourceNode, name);

    internal ProjectTreeNode ToggleComponentVariantLock(
        ProjectTreeNode node) =>
        _design.ToggleComponentVariantLock(node);

    internal ProjectTreeNode ToggleModuleVariantLock(
        ProjectTreeNode node) =>
        _design.ToggleModuleVariantLock(node);

    private void DeleteComponentVariant(ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out _,
                out _))
        {
            throw new InvalidOperationException(
                $"Invalid component variant node id '{node.Id}'.");
        }

        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            _design.RequireComponentVariantDeleteAllowed(
                connection,
                node);
            var usages = GetReferenceUsages(
                connection,
                node.Kind,
                node.Id);
            if (usages.Count > 0)
            {
                throw new InvalidOperationException(
                    $"This component variant is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
            }

            _design.DeleteComponentVariant(connection, node);
        }
    }

    private void DeleteModuleVariant(ProjectTreeNode node)
    {
        if (!VariantReferenceId.TryParse(
                node.Id,
                out var moduleId,
                out _))
        {
            throw new InvalidOperationException(
                $"Invalid module variant '{node.Id}'.");
        }

        lock (_context.WriteGate)
        {
            using var connection = _context.OpenConnection();
            _design.RequireModuleVariantDeleteAllowed(
                connection,
                node);
            if (_production.ModuleInstanceRepository
                    .CountVariantReferences(
                        connection,
                        moduleId,
                        node.Id) > 0)
            {
                throw new InvalidOperationException(
                    "This module variant is still used and cannot be deleted.");
            }

            _design.DeleteModuleVariant(connection, node);
        }
    }

    private ProjectTreeNode DuplicateIconTheme(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        ProjectTreeNode node)
    {
        var source = _resources.IconThemeRepository.Get(
            connection,
            node.Id);
        var id = $"icon_theme_{Guid.NewGuid():N}";
        var duplicatedAssets = _resources.DuplicateIconThemeAssets(
            connection,
            source,
            $"{node.Name} copy");
        var metadata = SqliteResourceOwner.IconThemeMetadata(
            _resources.IconThemeAssetDirectory(
                connection,
                source.ProjectId,
                duplicatedAssets.AssetRoot),
            duplicatedAssets.Name);
        try
        {
            _resources.IconThemeRepository.CreateDuplicate(
                connection,
                node.Id,
                id,
                duplicatedAssets.Name,
                duplicatedAssets.AssetRoot,
                metadata.ToJsonString());
        }
        catch
        {
            _resources.DeleteIconThemeAssetDirectory(
                connection,
                source.ProjectId,
                duplicatedAssets.AssetRoot);
            throw;
        }

        return new ProjectTreeNode(
            ProjectTreeNodeKind.IconTheme,
            id,
            duplicatedAssets.Name,
            node.Notes,
            node.RecordClassId,
            node.Parent);
    }

    private void DeleteIconTheme(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string iconThemeId)
    {
        var iconTheme = _resources.IconThemeRepository.Get(
            connection,
            iconThemeId);
        _resources.DeleteIconThemeAssetDirectory(
            connection,
            iconTheme.ProjectId,
            iconTheme.AssetRoot);
        _resources.IconThemeRepository.Delete(
            connection,
            iconThemeId);
    }

    private IReadOnlyList<string> GetReferenceUsages(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        ProjectTreeNodeKind kind,
        string nodeId) =>
        _referenceUsages.GetUsages(connection, kind, nodeId)
            .Select(UsageSummary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                (usage) => usage,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string UsageSummary(ReferenceUsageRecord usage) =>
        $"{usage.SourceTypeLabel}: {usage.SourceName}{(string.IsNullOrWhiteSpace(usage.FieldLabel) ? "" : $" · {usage.FieldLabel}")}";
}
