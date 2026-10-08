namespace Mockups.DesktopEditorShell.Data;

public sealed record ResourceAssetCleanupItem(string Id, string Label, string Path, string Error);

public sealed record ResourceAssetDeletionResult(int PendingCleanupCount);
