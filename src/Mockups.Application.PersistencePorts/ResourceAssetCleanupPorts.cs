namespace Mockups.DesktopEditorShell.Data;

public interface IResourceAssetCleanupStore
{
    IReadOnlyList<ResourceAssetCleanupItem> GetPending();
    void Retry(string id);
}
