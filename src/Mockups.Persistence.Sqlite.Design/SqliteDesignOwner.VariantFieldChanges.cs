using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    internal VariantDocumentChange? PrepareVariantFieldChange(
        SqliteConnection connection, ProjectTreeNode node, string fieldId, string value)
    {
        if (node.Kind == ProjectTreeNodeKind.ComponentVariant)
        {
            if (!ComponentClassFieldCatalog.IsRuntimeOverrideField(fieldId)) return null;
            var config = ComponentVariantConfigForUpdate(connection, node, out var id, out var metadata);
            SetComponentFieldValue(config, ComponentClassFieldCatalog.Get(fieldId), value);
            return PrepareComponentVariantUpdate(connection, node, id, config, metadata);
        }
        if (node.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            if (!VariantReferenceId.TryParse(node.Id, out var id, out _))
                throw new InvalidOperationException($"Invalid Module Variant '{node.Id}'.");
            var module = _appModuleRepository.GetModule(connection, id);
            if (!GeneratedModuleScaffoldConfigRegistry.TryGetField(module.RecordClassId, fieldId, out _)
                && fieldId != "module.appearanceMode") return null;
            return PrepareModuleVariantFieldChange(connection, node, fieldId, value);
        }
        return null;
    }
}
