using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record MotionVariantValue(
    string Transition,
    string Direction,
    string Bounds,
    bool Fade,
    bool Translate,
    bool Scale)
{
    public const string None = "none";
    public const string Slide = "slide";
    public const string Swipe = "swipe";
    public const string ScaleTransition = "scale";

    public const string Top = "top";
    public const string Bottom = "bottom";
    public const string Left = "left";
    public const string Right = "right";

    public const string Parent = "parent";
    public const string Screen = "screen";

    public static MotionVariantValue Default { get; } = new(
        Slide,
        Bottom,
        Screen,
        Fade: false,
        Translate: true,
        Scale: false);

    public static MotionVariantValue Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Motion value is empty.");
        }

        var root = JsonNode.Parse(value) as JsonObject
            ?? throw new InvalidOperationException("Motion value must be a JSON object.");

        return new MotionVariantValue(
            TransitionString(root),
            DirectionString(root),
            BoundsString(root),
            Bool(root, "fade"),
            Bool(root, "translate"),
            Bool(root, "scale"));
    }

    public string ToJsonString()
    {
        return new JsonObject
        {
            ["transition"] = Transition,
            ["direction"] = Direction,
            ["bounds"] = Bounds,
            ["fade"] = Fade,
            ["translate"] = Translate,
            ["scale"] = Scale,
        }.ToJsonString();
    }

    public string Summary()
    {
        return $"{Transition} · {Direction} · {Bounds}";
    }

    private static string String(JsonObject root, string key)
    {
        return root[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidOperationException($"Motion value is missing '{key}'.");
    }

    private static string TransitionString(JsonObject root)
    {
        var transition = String(root, "transition");
        return transition is None or Slide or Swipe or ScaleTransition
            ? transition
            : throw new InvalidOperationException($"Motion transition '{transition}' is not supported.");
    }

    private static string DirectionString(JsonObject root)
    {
        var direction = String(root, "direction");
        return direction is Top or Bottom or Left or Right
            ? direction
            : throw new InvalidOperationException($"Motion direction '{direction}' is not supported.");
    }

    private static string BoundsString(JsonObject root)
    {
        var bounds = String(root, "bounds");
        return bounds is Parent or Screen
            ? bounds
            : throw new InvalidOperationException($"Motion bounds '{bounds}' is not supported.");
    }

    private static bool Bool(JsonObject root, string key)
    {
        return root[key] is JsonValue value && value.TryGetValue<bool>(out var boolean)
            ? boolean
            : throw new InvalidOperationException($"Motion value is missing '{key}'.");
    }
}
