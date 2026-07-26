using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class ModuleRuntimeDocumentContracts
{
    public const string ConversationRecordClassId = "module.core.chat";

    public static void ValidateCurrent(
        string recordClassId,
        string owner,
        JsonObject content,
        IReadOnlySet<string> projectActorIds)
    {
        if (recordClassId.Equals(ConversationRecordClassId, StringComparison.Ordinal))
        {
            ConversationMessageActorContract.ValidateCurrent(owner, content, projectActorIds);
        }
    }

    public static void PrepareProduction(
        string recordClassId,
        string owner,
        JsonObject content,
        string shotOwnerActorId)
    {
        if (recordClassId.Equals(ConversationRecordClassId, StringComparison.Ordinal))
        {
            ConversationMessageActorContract.PrepareProduction(owner, content, shotOwnerActorId);
        }
    }
}

internal static class ConversationMessageActorContract
{
    public static void ValidateCurrent(
        string owner,
        JsonObject content,
        IReadOnlySet<string> projectActorIds)
    {
        var messages = content["messages"] as JsonArray
            ?? throw new InvalidOperationException($"{owner} requires a current messages array.");
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index] as JsonObject
                ?? throw new InvalidOperationException($"{owner} message at index {index} must be an object.");
            var context = MessageContext(owner, message, index);
            var direction = RequiredString(message, "direction", context);
            var actorId = RequiredString(message, "actorId", context, allowEmpty: true);
            switch (direction)
            {
                case "incoming":
                    RequireProjectActor(actorId, projectActorIds, context, optional: false);
                    break;
                case "outgoing":
                    if (!string.IsNullOrWhiteSpace(actorId))
                    {
                        throw new InvalidOperationException(
                            $"{context} is outgoing and must not persist an Actor reference.");
                    }
                    break;
                case "system":
                    RequireProjectActor(actorId, projectActorIds, context, optional: true);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{context} has unsupported direction '{direction}'.");
            }
        }
    }

    public static void PrepareProduction(
        string owner,
        JsonObject content,
        string shotOwnerActorId)
    {
        if (string.IsNullOrWhiteSpace(shotOwnerActorId))
        {
            throw new InvalidOperationException($"{owner} has no exact Shot owner Actor.");
        }
        var messages = content["messages"] as JsonArray
            ?? throw new InvalidOperationException($"{owner} requires a current messages array.");
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index] as JsonObject
                ?? throw new InvalidOperationException($"{owner} message at index {index} must be an object.");
            var context = MessageContext(owner, message, index);
            var direction = RequiredString(message, "direction", context);
            _ = RequiredString(message, "actorId", context, allowEmpty: true);
            switch (direction)
            {
                case "incoming":
                case "system":
                    break;
                case "outgoing":
                    message["actorId"] = shotOwnerActorId;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{context} has unsupported direction '{direction}'.");
            }
        }
    }

    private static void RequireProjectActor(
        string actorId,
        IReadOnlySet<string> projectActorIds,
        string context,
        bool optional)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            if (optional) return;
            throw new InvalidOperationException($"{context} requires an explicit incoming Actor.");
        }
        if (!projectActorIds.Contains(actorId))
        {
            throw new InvalidOperationException(
                $"{context} references missing or cross-Project Actor '{actorId}'.");
        }
    }

    private static string RequiredString(
        JsonObject value,
        string key,
        string context,
        bool allowEmpty = false)
    {
        if (value[key] is not JsonValue jsonValue
            || !jsonValue.TryGetValue<string>(out var text)
            || (!allowEmpty && string.IsNullOrWhiteSpace(text)))
        {
            var qualifier = allowEmpty ? "a string" : "a non-empty string";
            throw new InvalidOperationException($"{context} requires {qualifier} '{key}'.");
        }
        return text;
    }

    private static string MessageContext(string owner, JsonObject message, int index)
    {
        var id = message["id"] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : "";
        return string.IsNullOrWhiteSpace(id)
            ? $"{owner} message at index {index}"
            : $"{owner} message '{id}'";
    }
}
