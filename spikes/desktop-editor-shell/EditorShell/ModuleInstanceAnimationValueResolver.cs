using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ModuleInstanceAnimationValueResolver
{
    public static string ResolveDisplayValue(
        AnimationTrackView track,
        double frame,
        JsonNode baseValue,
        ValueKind kind)
    {
        var value = ResolveValue(track, frame, baseValue);
        if (kind == ValueKind.Boolean
            && value is JsonValue boolean
            && boolean.TryGetValue<bool>(out var flag))
        {
            return flag ? "true" : "false";
        }
        if (kind == ValueKind.Integer && TryNumber(value, out var integer))
        {
            return Math.Round(integer, MidpointRounding.AwayFromZero)
                .ToString(CultureInfo.InvariantCulture);
        }
        if (kind == ValueKind.Decimal && TryNumber(value, out var number))
        {
            return number.ToString("0.################", CultureInfo.InvariantCulture);
        }
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) return text;
        return value.ToJsonString().Trim('"');
    }

    internal static JsonNode ResolveValue(
        AnimationTrackView track,
        double frame,
        JsonNode baseValue)
    {
        var keyframes = track.Keyframes
            .Where((keyframe) => keyframe.Enabled && keyframe.Value is not null)
            .OrderBy((keyframe) => keyframe.Frame)
            .ToList();
        if (keyframes.Count == 0 || frame < keyframes[0].Frame) return baseValue.DeepClone();

        var exact = keyframes.FirstOrDefault((keyframe) => Math.Abs(keyframe.Frame - frame) < 0.0000001);
        if (exact?.Value is not null) return exact.Value.DeepClone();

        var destinationIndex = keyframes.FindIndex((keyframe) => keyframe.Frame > frame);
        if (destinationIndex < 0) return keyframes[^1].Value!.DeepClone();

        var source = keyframes[destinationIndex - 1];
        var destination = keyframes[destinationIndex];
        var progress = Math.Clamp(
            (frame - source.Frame) / Math.Max(1, destination.Frame - source.Frame),
            0,
            1);
        if (destination.Interpolation == "writeOn"
            && Text(source.Value, out var sourceText)
            && Text(destination.Value, out var destinationText))
        {
            return JsonValue.Create(RewriteText(sourceText, destinationText, progress))!;
        }
        if (destination.Interpolation is "linear" or "easeInOut"
            && TryNumber(source.Value, out var sourceNumber)
            && TryNumber(destination.Value, out var destinationNumber))
        {
            var amount = destination.Interpolation == "easeInOut"
                ? progress * progress * (3 - 2 * progress)
                : progress;
            return JsonValue.Create(sourceNumber + ((destinationNumber - sourceNumber) * amount))!;
        }
        return source.Value!.DeepClone();
    }

    private static string RewriteText(string source, string destination, double progress)
    {
        var from = Graphemes(source);
        var to = Graphemes(destination);
        var common = 0;
        while (common < from.Count && common < to.Count && from[common] == to[common]) common++;
        var removals = from.Count - common;
        var additions = to.Count - common;
        var operationCount = removals + additions;
        var step = Math.Clamp((int)Math.Floor(operationCount * progress), 0, operationCount);
        var removed = Math.Min(removals, step);
        var appended = Math.Max(0, step - removals);
        return string.Concat(from.Take(from.Count - removed).Concat(to.Skip(common).Take(appended)));
    }

    private static IReadOnlyList<string> Graphemes(string value)
    {
        var indexes = StringInfo.ParseCombiningCharacters(value);
        return indexes.Select((start, index) => value.Substring(
            start,
            (index + 1 < indexes.Length ? indexes[index + 1] : value.Length) - start)).ToList();
    }

    private static bool Text(JsonNode? value, out string text)
    {
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var result))
        {
            text = result;
            return true;
        }
        text = "";
        return false;
    }

    private static bool TryNumber(JsonNode? value, out double number)
    {
        if (value is JsonValue scalar)
        {
            if (scalar.TryGetValue<double>(out number)) return true;
            if (scalar.TryGetValue<decimal>(out var decimalValue))
            {
                number = (double)decimalValue;
                return true;
            }
            if (scalar.TryGetValue<int>(out var integer))
            {
                number = integer;
                return true;
            }
        }
        number = 0;
        return false;
    }
}
