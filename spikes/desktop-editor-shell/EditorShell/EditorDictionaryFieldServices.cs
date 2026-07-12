using Mockups.DesktopEditorShell.Data;
using System;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDictionaryFieldServices
{
    private readonly SpikeDatabase _database;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly Func<string?> _selectedThemeId;

    public EditorDictionaryFieldServices(
        SpikeDatabase database,
        EditorPathBrowser pathBrowser,
        EditorDomainDialogService domainDialogs,
        Func<string?> selectedThemeId)
    {
        _database = database;
        _pathBrowser = pathBrowser;
        _domainDialogs = domainDialogs;
        _selectedThemeId = selectedThemeId;
    }

    public DictionaryFieldServices ForNode(
        ProjectTreeNode node,
        Func<string, string> getFieldValue,
        Func<string, Task>? openComponentPresetReference = null,
        Func<string, Task>? openEmbeddedComponent = null,
        Func<FieldDefinition, ComponentInputBindingDefinition, Task>? openComponentInputBinding = null)
    {
        var projectId = ProjectAncestor(node).Id;
        string IconThemeId()
        {
            var effectiveThemeId = DesignPreviewPayloadFactory.ResolveThemeId(_database, node, _selectedThemeId());
            return string.IsNullOrWhiteSpace(effectiveThemeId)
                ? ""
                : _database.GetThemeSettings(effectiveThemeId).IconThemeId;
        }
        return new DictionaryFieldServices(
            _pathBrowser.BrowsePath,
            (currentValue, allowMultiple) => _domainDialogs.ShowIconTokenPicker(IconThemeId(), currentValue, allowMultiple),
            (currentValue, allowedOptions) => _domainDialogs.ShowThemeTokenPicker(projectId, currentValue, allowedOptions),
            (token) => SvgIconPreview.CreateIconTokenPreview(_database, IconThemeId(), token, 18),
            _pathBrowser.ResolveImagePath,
            getFieldValue,
            () => _database.GetPaletteColorOptions(projectId),
            (componentType) => _database.GetComponentPresetReferenceOptionsByType(projectId, componentType),
            openComponentPresetReference,
            openEmbeddedComponent,
            openComponentInputBinding);
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        }

        return current;
    }
}
