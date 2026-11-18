namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record VariantScreenInputRequest(string ScreenId, RecordCreationDefinition Definition);
public sealed record VariantFieldChangePreparation(string Revision, IReadOnlyList<VariantScreenInputRequest> Screens);
