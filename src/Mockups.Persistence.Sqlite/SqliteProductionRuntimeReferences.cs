using Microsoft.Data.Sqlite;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteProductionRuntimeReferences(
    IActorRepository actors) : IProductionRuntimeReferences
{
    public IReadOnlySet<string> ActorIds(SqliteConnection connection, string projectId) =>
        actors.QueryAll(connection)
            .Where(actor => actor.ProjectId == projectId)
            .Select(actor => actor.Id)
            .ToHashSet(StringComparer.Ordinal);
}
