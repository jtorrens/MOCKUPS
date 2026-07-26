namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    private bool IsVariantLockedForEditing(
        string ownerId,
        string variantId,
        bool persistedLocked) =>
        _designOwner.IsVariantLockedForEditing(
            ownerId,
            variantId,
            persistedLocked);

    private bool ToggleDefaultVariantSessionLock(
        string ownerId,
        string variantId) =>
        _designOwner.ToggleDefaultVariantSessionLock(
            ownerId,
            variantId);
}
