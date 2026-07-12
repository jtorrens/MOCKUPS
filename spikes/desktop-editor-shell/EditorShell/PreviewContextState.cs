namespace Mockups.DesktopEditorShell.EditorShell;

internal enum PreviewContextStateKind
{
    Renderable,
    NonRenderable,
    Loading,
    Error,
}

internal sealed record PreviewContextState(
    PreviewContextStateKind Kind,
    string Title,
    string Message,
    string ActionLabel = "",
    string ActionTargetId = "")
{
    public static PreviewContextState Renderable { get; } = new(
        PreviewContextStateKind.Renderable,
        "",
        "");

    public bool HasAction =>
        !string.IsNullOrWhiteSpace(ActionLabel)
        && !string.IsNullOrWhiteSpace(ActionTargetId);
}
