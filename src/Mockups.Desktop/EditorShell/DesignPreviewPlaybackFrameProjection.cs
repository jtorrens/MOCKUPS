using Mockups.DesktopEditorShell.Common;
using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DesignPreviewPlaybackFrameProjection
{
    public static DesignPreviewPayload Apply(
        DesignPreviewPayload payload,
        ComponentPreviewActionDefinition action,
        int localFrame)
    {
        if (!action.DefinesModuleDuration)
        {
            return payload;
        }
        if (action.IsCollectionItemAction)
        {
            throw new InvalidOperationException(
                $"Design Preview action '{action.Id}' cannot define the root Module frame from a collection item.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(localFrame);
        var instance = JsonPath.ParseRequiredObject(
            payload.InstanceJson,
            "Preview instance envelope");
        instance["designPlaybackTimeKey"] = action.TimeJsonKey;
        if (instance.TryGetPropertyValue("context", out var contextNode))
        {
            var context = contextNode as JsonObject
                ?? throw new InvalidOperationException(
                    "Preview instance context must be an object.");
            context["screenFrame"] = localFrame;
        }

        return payload with
        {
            LocalFrame = localFrame,
            InstanceJson = instance.ToJsonString(),
        };
    }
}
