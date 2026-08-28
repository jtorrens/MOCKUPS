using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteModuleInstanceCollectionStore
{
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _design;
    private readonly SqliteProductionOwner _production;
    private readonly SqliteResourceOwner _resources;
    private readonly ReferenceUsageService _referenceUsages;

    internal SqliteModuleInstanceCollectionStore(
        SqliteProjectContext context,
        SqliteDesignOwner design,
        SqliteProductionOwner production,
        SqliteResourceOwner resources,
        ReferenceUsageService referenceUsages)
    {
        _context = context;
        _design = design;
        _production = production;
        _resources = resources;
        _referenceUsages = referenceUsages;
    }

    internal IReadOnlyList<ShotModuleChoice> GetAvailableShotModules(
        string shotId)
    {
        using var connection = _context.OpenConnection();
        var shot = _production.ShotRepository.Get(connection, shotId);
        var apps = _design.AppModuleRepository
            .QueryApps(connection)
            .Where((app) => app.ProjectId == shot.ProjectId)
            .OrderBy((app) => app.SortOrder)
            .ThenBy((app) => app.Name)
            .ToDictionary((app) => app.Id, StringComparer.Ordinal);
        return _design.AppModuleRepository
            .QueryModules(connection)
            .Where((module) => apps.ContainsKey(module.AppId))
            .OrderBy((module) => apps[module.AppId].SortOrder)
            .ThenBy((module) => apps[module.AppId].Name)
            .ThenBy((module) => module.SortOrder)
            .ThenBy((module) => module.Name)
            .Select((module) => new ShotModuleChoice(
                module.Id,
                module.Name,
                apps[module.AppId].Name,
                module.AppId,
                module.RecordClassId))
            .ToList();
    }

    internal ProjectTreeNode AddModuleInstance(
        ProjectTreeNode shot,
        ShotModuleInstanceDraft draft)
    {
        using var connection = _context.OpenConnection();
        var module = _design.GetModuleSettings(draft.Module.Id);
        return _production.AddModuleInstance(
            connection,
            shot,
            draft,
            ProjectActorIds(connection, module.ProjectId));
    }

    internal void Delete(ProjectTreeNode node)
    {
        RequireModuleInstance(node);
        using var connection = _context.OpenConnection();
        var usages = _referenceUsages
            .GetUsages(connection, node.Kind, node.Id)
            .Select(UsageSummary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                (usage) => usage,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (usages.Count > 0)
        {
            throw new InvalidOperationException(
                $"This {node.Kind} is still used and cannot be deleted.\n\n{string.Join(Environment.NewLine, usages.Take(12))}");
        }

        _production.ModuleInstanceRepository.Delete(
            connection,
            node.Id);
        _production.SynchronizeTimelineDurations(connection);
    }

    internal ProjectTreeNode Duplicate(ProjectTreeNode node)
    {
        RequireModuleInstance(node);
        using var connection = _context.OpenConnection();
        var settings = _production.GetModuleInstanceSettings(node.Id);
        var id = $"module_instance_{Guid.NewGuid():N}";
        var sortOrder = _production.ModuleInstanceRepository
            .NextSortOrder(connection, settings.ShotId);
        var copyName = _production.ModuleInstanceRepository
            .UniqueName(
                connection,
                settings.ShotId,
                $"{node.Name} copy");
        _production.ModuleInstanceRepository.Duplicate(
            connection,
            node.Id,
            id,
            settings.ShotId,
            copyName,
            sortOrder);
        _production.SynchronizeTimelineDurations(connection);

        return new ProjectTreeNode(
            ProjectTreeNodeKind.ModuleInstance,
            id,
            copyName,
            node.Notes,
            node.RecordClassId,
            node.Parent);
    }

    internal void MoveModuleInstance(
        string moduleInstanceId,
        int offset) =>
        _production.MoveModuleInstance(moduleInstanceId, offset);

    internal IReadOnlyList<FieldOption> GetModuleVariantOptions(
        string moduleId) =>
        _design.GetModuleVariantOptions(moduleId);

    private IReadOnlySet<string> ProjectActorIds(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string projectId) =>
        _resources.ActorRepository.QueryAll(connection)
            .Where((actor) => actor.ProjectId.Equals(
                projectId,
                StringComparison.Ordinal))
            .Select((actor) => actor.Id)
            .ToHashSet(StringComparer.Ordinal);

    private static void RequireModuleInstance(ProjectTreeNode node)
    {
        if (node.Kind != ProjectTreeNodeKind.ModuleInstance)
        {
            throw new InvalidOperationException(
                $"Module Instance collection cannot change {node.Kind}.");
        }
    }

    private static string UsageSummary(ReferenceUsageRecord usage) =>
        $"{usage.SourceTypeLabel}: {usage.SourceName}{(string.IsNullOrWhiteSpace(usage.FieldLabel) ? "" : $" · {usage.FieldLabel}")}";
}
