using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public sealed record AppModuleCreationCapability(string TemplateModuleId);

public static class AppModuleCreationContract
{
    public const string AddOperationId = "module.create";

    public static AppModuleCreationCapability? Read(
        JsonObject metadata,
        string owner)
    {
        if (!metadata.TryGetPropertyValue("moduleCreation", out var node))
        {
            return null;
        }

        if (node is not JsonObject creation)
        {
            throw new InvalidOperationException(
                $"{owner} 'moduleCreation' must be an object.");
        }

        var templateModuleId = JsonPath.String(
            creation,
            "templateModuleId",
            "").Trim();
        if (templateModuleId.Length == 0)
        {
            throw new InvalidOperationException(
                $"{owner} 'moduleCreation.templateModuleId' is required.");
        }

        return new AppModuleCreationCapability(templateModuleId);
    }
}
