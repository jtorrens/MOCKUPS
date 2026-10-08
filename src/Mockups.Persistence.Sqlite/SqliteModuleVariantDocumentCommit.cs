using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteModuleVariantDocumentCommit
{
    internal static void Commit(
        SqliteProjectContext context,
        SqliteConnection connection,
        ModuleVariantDocumentChange change,
        SqliteProductionOwner production,
        SqliteResourceOwner resources)
    {
        lock (context.WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            var previousContract = production.ResolveModuleInstanceContract(
                connection,
                change.ModuleId,
                new JsonObject { ["moduleVariantReference"] = change.VariantReference }.ToJsonString());
            var actorIds = resources.ActorRepository.QueryAll(connection)
                .GroupBy(actor => actor.ProjectId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => (IReadOnlySet<string>)group.Select(actor => actor.Id).ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            new AppModuleRepository(context).UpdateModuleMetadata(
                connection, change.ModuleId, change.MetadataJson, transaction);
            production.ReconcileModuleVariantRuntimePayloads(
                connection, transaction, change.ModuleId, change.VariantReference, previousContract, actorIds);
            transaction.Commit();
        }
    }
}
