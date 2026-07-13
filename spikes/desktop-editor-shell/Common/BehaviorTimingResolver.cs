using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class BehaviorTimingResolver
{
    public static int ResolveFrames(
        JsonObject owner,
        JsonObject definition,
        IReadOnlyList<JsonObject> ownerFields,
        JsonObject themeTokens)
    {
        var jsonKey = Text(definition["jsonKey"]);
        if (owner[jsonKey] is not JsonObject value)
            throw new InvalidOperationException($"Behavior timing field '{Text(definition["id"])}' must be an object.");

        var mode = Text(value["mode"]);
        if (mode == "fixed") return Math.Max(0, Integer(value["fixedFrames"]));
        if (mode != "natural")
            throw new InvalidOperationException($"Behavior timing field '{Text(definition["id"])}' has invalid mode '{mode}'.");

        var natural = definition["naturalTiming"] as JsonObject
            ?? throw new InvalidOperationException($"Behavior timing field '{Text(definition["id"])}' is missing naturalTiming metadata.");
        var sourceId = Text(natural["sourceFieldId"]);
        var source = ownerFields.FirstOrDefault((field) => Text(field["id"]) == sourceId)
            ?? throw new InvalidOperationException($"Behavior timing source field '{sourceId}' does not exist.");
        var sourceText = Text(owner[Text(source["jsonKey"])]);
        return ResolveNaturalFrames(
            sourceText,
            Text(natural["unit"]),
            Number(natural["baseFramesPerUnit"]),
            Text(value["paceToken"]),
            themeTokens);
    }

    public static int ResolveNaturalFrames(
        string sourceText,
        string unit,
        double baseFramesPerUnit,
        string paceToken,
        JsonObject themeTokens)
    {
        var units = unit switch
        {
            "grapheme" => StringInfo.ParseCombiningCharacters(sourceText).Length,
            _ => throw new InvalidOperationException($"Unsupported natural timing unit '{unit}'."),
        };
        if (!ThemeNumericTokenCatalog.TryGet(paceToken, out var token)
            || !paceToken.StartsWith("theme.motion.naturalPace.", StringComparison.Ordinal))
            throw new InvalidOperationException($"Behavior timing pace token '{paceToken}' is invalid.");
        var multiplier = Number(Path(themeTokens, token.Path));
        if (multiplier <= 0) throw new InvalidOperationException($"Behavior timing pace token '{paceToken}' must be positive.");
        return Math.Max(0, (int)Math.Round(units * baseFramesPerUnit * multiplier, MidpointRounding.AwayFromZero));
    }

    private static JsonNode? Path(JsonObject root, IReadOnlyList<string> path)
    {
        JsonNode? current = root;
        foreach (var segment in path) current = (current as JsonObject)?[segment];
        return current;
    }

    private static string Text(JsonNode? value) => value is JsonValue json && json.TryGetValue<string>(out var text) ? text : "";
    private static int Integer(JsonNode? value) => value is JsonValue json && json.TryGetValue<int>(out var number) ? number : 0;
    private static double Number(JsonNode? value) => value is JsonValue json && json.TryGetValue<double>(out var number) ? number : 0;
}
