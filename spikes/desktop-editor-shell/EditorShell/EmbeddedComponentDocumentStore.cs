using Mockups.DesktopEditorShell.Data;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EmbeddedComponentDocumentStore
{
    private readonly SpikeDatabase _database;

    public EmbeddedComponentDocumentStore(SpikeDatabase database)
    {
        _database = database;
    }

    public string ActivePresetName(EditorEmbeddedContext context)
    {
        return context.RuntimeSource is null
            ? _database.GetEmbeddedComponentPresetName(context.OwnerNode, context.Slots)
            : _database.GetRuntimeComponentPresetName(
                context.RuntimeSource.PresetReference,
                context.RuntimeSource.Overrides,
                context.Slots);
    }

    public FieldValue CreateFieldValue(EditorEmbeddedContext context, string fieldId)
    {
        return context.RuntimeSource is null
            ? _database.CreateEmbeddedComponentFieldValue(
                context.OwnerNode,
                context.Slots,
                fieldId)
            : _database.CreateRuntimeComponentOverrideFieldValue(
                context.RuntimeSource.ProjectId,
                context.RuntimeSource.BaseConfigJson,
                context.RuntimeSource.Overrides,
                context.Slots,
                fieldId);
    }

    public void CommitFieldValue(EditorEmbeddedContext context, string fieldId, string value)
    {
        if (context.RuntimeSource is null)
        {
            _database.UpdateEmbeddedComponentField(
                context.OwnerNode,
                context.Slots,
                fieldId,
                value);
            return;
        }

        _database.UpdateRuntimeComponentOverride(
            context.RuntimeSource.Overrides,
            context.Slots,
            fieldId,
            value);
        context.RuntimeSource.OverridesChanged(context.RuntimeSource.Overrides);
    }
}
