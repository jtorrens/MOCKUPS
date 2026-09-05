using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ProductionShotContext(
    bool IsValid,
    string Error,
    string Actor,
    string Device,
    string Theme,
    string ThemeMode);

internal sealed class ProductionShotContextService
{
    private readonly ProductionShotContextDataSource _dataSource;

    public ProductionShotContextService(ProductionShotContextDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public ProductionShotContext Resolve(string shotId)
    {
        var shot = _dataSource.LoadShot(shotId);
        var ownerActorId = shot.OwnerActorId;
        if (string.IsNullOrWhiteSpace(ownerActorId))
        {
            return Invalid($"Shot {shotId} has no Actor assigned.");
        }
        ActorPreviewContextSource actor;
        try
        {
            actor = _dataSource.LoadActor(ownerActorId);
        }
        catch (Exception)
        {
            return Invalid($"Shot {shotId} references a missing Actor.");
        }
        var deviceId = shot.EffectiveDeviceId(actor.DefaultDeviceId);
        var themeId = actor.DefaultThemeId;
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(themeId))
        {
            return Invalid($"Actor {actor.DisplayName} must define a default Device and Theme.", actor.DisplayName);
        }
        try
        {
            var device = _dataSource.LoadDeviceName(deviceId);
            var theme = _dataSource.LoadTheme(themeId);
            return new ProductionShotContext(true, "", actor.DisplayName, device, theme.Name, theme.DefaultMode);
        }
        catch (Exception)
        {
            return Invalid($"Actor {actor.DisplayName} references a missing Device or Theme.", actor.DisplayName);
        }
    }

    public bool CanExposeChildren(ProjectTreeNode node)
    {
        return node.Kind != ProjectTreeNodeKind.Shot || Resolve(node.Id).IsValid;
    }

    public bool IsNavigationNodeEnabled(ProjectTreeNode node)
    {
        return node.Kind != ProjectTreeNodeKind.ModuleInstance
            || node.Parent is null
            || CanExposeChildren(node.Parent);
    }

    private static ProductionShotContext Invalid(string error, string actor = "Required Actor missing") =>
        new(false, error, actor, "Unavailable", "Unavailable", "Unavailable");
}
