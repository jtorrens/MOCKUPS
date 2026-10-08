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
        SqliteProductionOwner production)
    {
        lock (context.WriteGate)
        {
            using var transaction = connection.BeginTransaction();
            var previousContract = production.ResolveModuleInstanceContract(
                connection,
                change.ModuleId,
                new JsonObject { ["moduleVariantReference"] = change.VariantReference }.ToJsonString());
            new AppModuleRepository(context).UpdateModuleMetadata(
                connection, change.ModuleId, change.MetadataJson, transaction);
            production.ReconcileModuleVariantRuntimePayloads(
                connection, transaction, change.ModuleId, change.VariantReference, previousContract);
            transaction.Commit();
        }
    }
}
