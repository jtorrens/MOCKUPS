using Mockups.DesktopEditorShell.Data;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorPreparedHeader(
    string OwnerId,
    ProductionScreenPresentationSource? Screen,
    string ActiveVariantName,
    string RootVariantName)
{
    public static EditorPreparedHeader Loading(string ownerId) =>
        new(ownerId, null, "", "");
}

internal sealed class EditorHeaderPreparationService
{
    private readonly EmbeddedComponentDocumentStore
        _embeddedDocuments;
    private readonly ProductionScreenPresentationDataSource
        _screenPresentation;

    public EditorHeaderPreparationService(
        IComponentDocumentStore components,
        IPreviewInputRepository preview,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes)
    {
        _embeddedDocuments =
            new EmbeddedComponentDocumentStore(components);
        _screenPresentation =
            new ProductionScreenPresentationDataSource(
                preview,
                timeline,
                moduleInstanceThemes);
    }

    public EditorPreparedHeader PrepareRoot(
        ProjectTreeNode node,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var screen = node.Kind == ProjectTreeNodeKind.ModuleInstance
            ? _screenPresentation.Load(node.Id)
            : null;
        cancellationToken.ThrowIfCancellationRequested();
        return new EditorPreparedHeader(
            node.Id,
            screen,
            "",
            "");
    }

    public EditorPreparedHeader PrepareEmbedded(
        EditorEmbeddedContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.IsRecordReferenceOverride)
        {
            return new EditorPreparedHeader(
                context.OwnerNode.Id,
                null,
                "",
                "");
        }
        var activeVariantName =
            _embeddedDocuments.ActiveVariantName(context);
        cancellationToken.ThrowIfCancellationRequested();
        var rootVariantName = context.RuntimeSource is null
            ? ""
            : _embeddedDocuments.ActiveVariantName(
                context.Ancestor(0));
        cancellationToken.ThrowIfCancellationRequested();
        return new EditorPreparedHeader(
            context.OwnerNode.Id,
            null,
            activeVariantName,
            rootVariantName);
    }
}
