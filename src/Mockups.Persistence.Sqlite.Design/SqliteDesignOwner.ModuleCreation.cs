using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    internal ProjectTreeNode CreateModuleFromDeclaredTemplate(
        ProjectTreeNode appNode,
        string name)
    {
        if (appNode.Kind != ProjectTreeNodeKind.App)
        {
            throw new InvalidOperationException(
                "Modules can only be created under an App.");
        }

        var moduleName = name.Trim();
        if (moduleName.Length == 0)
        {
            throw new InvalidOperationException(
                "Module name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var app = _appModuleRepository.GetApp(connection, appNode.Id);
            var capability = AppModuleCreationContract.Read(
                    ParseJsonObject(app.MetadataJson),
                    $"App '{app.Id}' metadata_json")
                ?? throw new InvalidOperationException(
                    $"App '{app.Name}' does not declare Module creation.");
            var template = _appModuleRepository.GetModule(
                connection,
                capability.TemplateModuleId);
            if (!template.AppId.Equals(app.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Module creation template '{template.Id}' does not belong to App '{app.Id}'.");
            }

            var templateMetadata = ParseJsonObject(template.MetadataJson);
            var defaultConfig = VariantEnvelopeContract.Read(
                    templateMetadata,
                    "variants",
                    $"Module '{template.Id}'")
                .Single((variant) => variant.Id == VariantEnvelopeContract.DefaultId)
                .Config.DeepClone().AsObject();
            var metadata = new JsonObject
            {
                ["variants"] = new JsonArray
                {
                    VariantEnvelopeContract.CreateSource(
                        VariantEnvelopeContract.DefaultId,
                        "Default",
                        defaultConfig.DeepClone().AsObject(),
                        isProtected: true,
                        isLocked: true),
                },
            };
            var sortOrder = _appModuleRepository.QueryModules(connection)
                .Where((candidate) => candidate.AppId == app.Id)
                .Select((candidate) => candidate.SortOrder)
                .DefaultIfEmpty(-1)
                .Max() + 1;
            var module = new ModuleDefinitionRecord(
                $"module_{Guid.NewGuid():N}",
                app.Id,
                template.RecordClassId,
                moduleName,
                $"{moduleName} system module.",
                sortOrder,
                defaultConfig.ToJsonString(),
                template.DesignPreviewJson,
                metadata.ToJsonString());
            _appModuleRepository.CreateModule(connection, module);
            return new ProjectTreeNode(
                ProjectTreeNodeKind.Module,
                module.Id,
                module.Name,
                module.Notes,
                module.RecordClassId,
                appNode);
        }
    }
}
