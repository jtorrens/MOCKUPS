using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ActorAvatarPreviewController : IEditorInlinePreviewController
{
    private readonly ActorAvatarPreviewFactory _previewFactory;
    private readonly Func<bool> _isDark;
    private readonly List<ContentControl> _previewHosts = [];

    public ActorAvatarPreviewController(ActorPreviewDataSource dataSource, Func<bool> isDark)
    {
        _previewFactory = new ActorAvatarPreviewFactory(dataSource);
        _isDark = isDark;
    }

    public void Reset()
    {
        _previewHosts.Clear();
    }

    public void AddIfNeeded(ProjectTreeNode node, EditorLayoutCard layoutCard, Panel groupPanel)
    {
        if (node.Kind != ProjectTreeNodeKind.Actor || layoutCard.Id != "avatar")
        {
            return;
        }

        var previewHost = new ContentControl
        {
            Content = _previewFactory.Create(node.Id, _isDark()),
        };
        _previewHosts.Add(previewHost);
        groupPanel.Children.Add(previewHost);
    }

    public void Refresh(ProjectTreeNode node, IReadOnlyDictionary<string, DictionaryFieldControl> controlsByFieldId)
    {
        if (node.Kind != ProjectTreeNodeKind.Actor || _previewHosts.Count == 0)
        {
            return;
        }

        var draftValues = controlsByFieldId.ToDictionary(
            (pair) => pair.Key,
            (pair) => pair.Value.Value);
        foreach (var previewHost in _previewHosts)
        {
            previewHost.Content = _previewFactory.Create(node.Id, _isDark(), draftValues);
        }
    }

    public string ToStoragePath(ProjectTreeNode node, string fieldId, string path)
    {
        if (node.Kind != ProjectTreeNodeKind.Actor || fieldId != "actor.avatar.filePath")
        {
            return path;
        }

        return _previewFactory.RelativeActorMediaPath(node.Id, path) ?? path;
    }
}
