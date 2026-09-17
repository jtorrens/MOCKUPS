using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteRuntimeContractUsageStore(
    SqliteProjectContext context,
    SqliteProductionOwner production,
    SqliteResourceOwner resources) : IRuntimeContractUsageStore
{
    public void ResetRuntimePayloads(ProjectTreeNode owner)
    {
        if (owner.Kind != ProjectTreeNodeKind.ModuleVariant
            || !VariantReferenceId.TryParse(
                owner.Id,
                out var moduleId,
                out _))
        {
            throw new InvalidOperationException(
                $"Runtime payload reset requires a Module Variant, not '{owner.Kind}'.");
        }

        lock (context.WriteGate)
        {
            using var connection = context.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var projectActorIds = resources.ActorRepository.QueryAll(connection)
                .GroupBy((actor) => actor.ProjectId, StringComparer.Ordinal)
                .ToDictionary(
                    (group) => group.Key,
                    (group) => (IReadOnlySet<string>)group
                        .Select((actor) => actor.Id)
                        .ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            production.ResetModuleVariantRuntimePayloads(
                connection,
                transaction,
                moduleId,
                owner.Id,
                projectActorIds);
            transaction.Commit();
        }
    }
}
