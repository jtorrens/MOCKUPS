using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public sealed record EventBurst(int Count, int IntervalFrames)
{
    public const int MaximumCount = 1000;
    public const int MaximumInterval = 10000;
    public int LastOffset => (Count - 1) * IntervalFrames;

    public static EventBurst Read(JsonObject burst)
    {
        var count = JsonPath.RequiredInteger(burst, "count", "Event burst");
        var interval = JsonPath.RequiredInteger(burst, "intervalFrames", "Event burst");
        if (count is < 1 or > MaximumCount || interval is < 0 or > MaximumInterval)
            throw new InvalidOperationException("Event burst count or interval is out of range.");
        return new(count, interval);
    }

    public JsonObject ToJson() => new() { ["count"] = Count, ["intervalFrames"] = IntervalFrames };
}
