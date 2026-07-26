namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record DeviceImportDraft(
    string Name,
    string Manufacturer,
    string Model,
    string OsFamily,
    string MetricsJson);
