using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal sealed record AlignmentPlacementValue(
    string Mode,
    double AlignX,
    double AlignY,
    int OffsetX,
    int OffsetY)
{
    public const string CenterMode = "center";
    public const string InsideEdgeMode = "insideEdge";
    public const string OutsideEdgeMode = "outsideEdge";

    public static AlignmentPlacementValue Default { get; } = new(OutsideEdgeMode, 1, 0.5, 4, 0);

    public static AlignmentPlacementValue Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Alignment placement value is empty.");
        }

        var node = JsonNode.Parse(value) as JsonObject
            ?? throw new InvalidOperationException("Alignment placement value must be a JSON object.");

        return new AlignmentPlacementValue(
            RequiredMode(node),
            Clamp01(RequiredNumber(node, "alignX")),
            Clamp01(RequiredNumber(node, "alignY")),
            RequiredInteger(node, "offsetX"),
            RequiredInteger(node, "offsetY"));
    }

    public static AlignmentPlacementValue FromDirectionalOutsideEdge(string position, double gap)
    {
        return position switch
        {
            "top" => new AlignmentPlacementValue(OutsideEdgeMode, 0.5, 0, 0, -(int)Math.Round(gap)),
            "left" => new AlignmentPlacementValue(OutsideEdgeMode, 0, 0.5, -(int)Math.Round(gap), 0),
            "right" => new AlignmentPlacementValue(OutsideEdgeMode, 1, 0.5, (int)Math.Round(gap), 0),
            _ => new AlignmentPlacementValue(OutsideEdgeMode, 0.5, 1, 0, (int)Math.Round(gap)),
        };
    }

    public static string Format(AlignmentPlacementValue value)
    {
        return new JsonObject
        {
            ["mode"] = NormalizeMode(value.Mode),
            ["alignX"] = Math.Round(Clamp01(value.AlignX), 3),
            ["alignY"] = Math.Round(Clamp01(value.AlignY), 3),
            ["offsetX"] = value.OffsetX,
            ["offsetY"] = value.OffsetY,
        }.ToJsonString();
    }

    public string ToJsonString()
    {
        return Format(this);
    }

    private static string NormalizeMode(string value)
    {
        return value switch
        {
            CenterMode => CenterMode,
            InsideEdgeMode => InsideEdgeMode,
            OutsideEdgeMode => OutsideEdgeMode,
            _ => throw new InvalidOperationException($"Alignment placement mode '{value}' is not supported."),
        };
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    private static string RequiredString(JsonObject node, string key)
    {
        return node[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidOperationException($"Alignment placement is missing '{key}'.");
    }

    private static string RequiredMode(JsonObject node)
    {
        var mode = RequiredString(node, "mode");
        return mode is CenterMode or InsideEdgeMode or OutsideEdgeMode
            ? mode
            : throw new InvalidOperationException($"Alignment placement mode '{mode}' is not supported.");
    }

    private static double RequiredNumber(JsonObject root, string key)
    {
        var node = root[key];
        if (node is JsonValue value)
        {
            if (value.TryGetValue<double>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<string>(out var text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException($"Alignment placement is missing numeric '{key}'.");
    }

    private static int RequiredInteger(JsonObject root, string key)
    {
        return (int)Math.Round(RequiredNumber(root, key));
    }
}
