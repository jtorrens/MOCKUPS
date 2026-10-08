namespace Mockups.DesktopEditorShell.Data;

public interface IResourceAssetCleanupStore
{
    event Action<ResourceAssetCleanupItem>? RecoveryPending;
    IReadOnlyList<ResourceAssetCleanupItem> GetPending();
    void Retry(string id);
}
