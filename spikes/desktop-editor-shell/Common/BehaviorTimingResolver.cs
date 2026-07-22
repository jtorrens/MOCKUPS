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
        var definitionContext = "Behavior timing definition";
        var fieldId = JsonPath.RequiredString(definition, "id", definitionContext);
        var jsonKey = JsonPath.RequiredString(definition, "jsonKey", definitionContext);
        var value = owner[jsonKey] as JsonObject
            ?? throw new InvalidOperationException(
                $"Behavior timing field '{fieldId}' must be an object.");
        var timing = BehaviorTimingValue.Parse(value, $"Behavior timing field '{fieldId}'");
        if (timing.Mode == "fixed") return timing.FixedFrames;

        var natural = JsonPath.RequiredObject(
            definition,
            "naturalTiming",
            $"Behavior timing field '{fieldId}'");
        var sourceId = JsonPath.RequiredString(
            natural,
            "sourceFieldId",
            $"Behavior timing field '{fieldId}' naturalTiming");
        var source = ownerFields.FirstOrDefault((field) =>
                JsonString(field, "id", "Behavior timing source definition") == sourceId)
            ?? throw new InvalidOperationException($"Behavior timing source field '{sourceId}' does not exist.");
        var sourceJsonKey = JsonPath.RequiredString(
            source,
            "jsonKey",
            $"Behavior timing source field '{sourceId}'");
        var sourceText = JsonPath.RequiredString(
            owner,
            sourceJsonKey,
            $"Behavior timing source field '{sourceId}' value",
            allowEmpty: true);
        return ResolveNaturalFrames(
            sourceText,
            JsonPath.RequiredString(
                natural,
                "unit",
                $"Behavior timing field '{fieldId}' naturalTiming"),
            JsonPath.RequiredNumber(
                natural,
                "baseFramesPerUnit",
                $"Behavior timing field '{fieldId}' naturalTiming"),
            timing.PaceToken,
            themeTokens);
    }

    public static int ResolveNaturalFrames(
        string sourceText,
        string unit,
        double baseFramesPerUnit,
        string paceToken,
        JsonObject themeTokens)
    {
        if (!double.IsFinite(baseFramesPerUnit) || baseFramesPerUnit <= 0)
        {
            throw new InvalidOperationException(
                "Behavior timing baseFramesPerUnit must be a positive finite number.");
        }
        var units = unit switch
        {
            "grapheme" => StringInfo.ParseCombiningCharacters(sourceText).Length,
            _ => throw new InvalidOperationException($"Unsupported natural timing unit '{unit}'."),
        };
        if (!ThemeNumericTokenCatalog.TryGet(paceToken, out var token)
            || !paceToken.StartsWith("theme.motion.naturalPace.", StringComparison.Ordinal))
            throw new InvalidOperationException($"Behavior timing pace token '{paceToken}' is invalid.");
        var multiplier = RequiredNumber(
            Path(themeTokens, token.Path),
            $"Behavior timing pace token '{paceToken}'");
        if (multiplier <= 0) throw new InvalidOperationException($"Behavior timing pace token '{paceToken}' must be positive.");
        return Math.Max(0, (int)Math.Round(units * baseFramesPerUnit * multiplier, MidpointRounding.AwayFromZero));
    }

    private static JsonNode? Path(JsonObject root, IReadOnlyList<string> path)
    {
        JsonNode? current = root;
        foreach (var segment in path) current = (current as JsonObject)?[segment];
        return current;
    }

    private static string JsonString(JsonObject owner, string key, string context) =>
        JsonPath.RequiredString(owner, key, context);

    private static double RequiredNumber(JsonNode? value, string context)
    {
        if (value is not JsonValue scalar
            || !scalar.TryGetValue<double>(out var number)
            || !double.IsFinite(number))
        {
            throw new InvalidOperationException($"{context} must resolve to a finite number.");
        }
        return number;
    }
}
