using Mockups.DesktopEditorShell.Data;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDictionaryFieldServices
{
    private readonly SpikeDatabase _database;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;

    public EditorDictionaryFieldServices(
        SpikeDatabase database,
        EditorPathBrowser pathBrowser,
        EditorDomainDialogService domainDialogs)
    {
        _database = database;
        _pathBrowser = pathBrowser;
        _domainDialogs = domainDialogs;
    }

    public DictionaryFieldServices ForNode(ProjectTreeNode node, Func<string, string> getFieldValue)
    {
        var projectId = ProjectAncestor(node).Id;
        return new DictionaryFieldServices(
            _pathBrowser.BrowsePath,
            (currentValue, allowMultiple) => _domainDialogs.ShowIconTokenPicker(projectId, currentValue, allowMultiple),
            (currentValue, allowedOptions) => _domainDialogs.ShowThemeTokenPicker(projectId, currentValue, allowedOptions),
            (token) => SvgIconPreview.CreateProjectIconTokenPreview(_database, projectId, token, 18),
            _pathBrowser.ResolveImagePath,
            getFieldValue);
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

