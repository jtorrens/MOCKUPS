using Mockups.DesktopEditorShell.Data;
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
    private readonly SpikeDatabase _database;

    public ProductionShotContextService(SpikeDatabase database)
    {
        _database = database;
    }

    public ProductionShotContext Resolve(string shotId)
    {
        var shot = _database.GetShotSettings(shotId);
        if (string.IsNullOrWhiteSpace(shot.OwnerActorId))
        {
            return Invalid($"Shot {shotId} has no Actor assigned.");
        }
        ActorSettings actor;
        try
        {
            actor = _database.GetActorSettings(shot.OwnerActorId);
        }
        catch (Exception)
        {
            return Invalid($"Shot {shotId} references a missing Actor.");
        }
        if (string.IsNullOrWhiteSpace(actor.DefaultDeviceId) || string.IsNullOrWhiteSpace(actor.DefaultThemeId))
        {
            return Invalid($"Actor {actor.DisplayName} must define a default Device and Theme.", actor.DisplayName);
        }
        try
        {
            var device = _database.GetDeviceSettings(actor.DefaultDeviceId).Name;
            var theme = _database.GetThemeSettings(actor.DefaultThemeId).Name;
            var mode = _database.GetThemeFieldValue(actor.DefaultThemeId, "theme.defaultMode");
            return new ProductionShotContext(true, "", actor.DisplayName, device, theme, mode);
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
