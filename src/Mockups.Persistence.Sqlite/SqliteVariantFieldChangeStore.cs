using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteVariantFieldChangeStore(
    SqliteProjectContext context, SqliteDesignOwner design, SqliteProductionOwner production) : IVariantFieldChangeStore
{
    public VariantFieldChangePreparation? Prepare(ProjectTreeNode node, string fieldId, string value)
    {
        lock (context.WriteGate)
        {
            using var connection = context.OpenConnection();
            var change = design.PrepareVariantFieldChange(connection, node, fieldId, value);
            return change is null ? null : SqliteVariantDocumentCommit.Execute(
                context, connection, [change], design, production, false, null, null);
        }
    }

    public void Commit(ProjectTreeNode node, string fieldId, string value, string revision,
        IReadOnlyDictionary<string, RecordCreationDraft> screenInputs)
    {
        lock (context.WriteGate)
        {
            using var connection = context.OpenConnection();
            var change = design.PrepareVariantFieldChange(connection, node, fieldId, value)
                ?? throw new InvalidOperationException("The field does not declare a Variant configuration change.");
            SqliteVariantDocumentCommit.Execute(context, connection, [change], design, production, true, revision, screenInputs);
        }
    }
}
