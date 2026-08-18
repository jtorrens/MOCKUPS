using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>
/// The one preparation boundary for a Runtime Preview document. Configuration
/// ownership is resolved before Runtime values are overlaid, for both Design
/// fixtures and Production Screen content.
/// </summary>
public static class RuntimePreviewDocumentContract
{
    public static JsonObject PrepareFixture(
        JsonObject previewFixture,
        JsonObject effectiveConfig)
    {
        var prepared = RuntimeInputForwardingContract.EffectivePreview(
            previewFixture,
            effectiveConfig);
        StructuredRuntimeCollectionProjection.Apply(prepared, effectiveConfig);
        RuntimeTemporalPhaseContract.Hydrate(prepared, effectiveConfig);
        return prepared;
    }

    public static JsonObject PrepareRuntime(
        JsonObject previewFixture,
        JsonObject effectiveConfig,
        JsonObject runtimeValues)
    {
        var prepared = PrepareFixture(previewFixture, effectiveConfig);
        var current = RuntimeInputDocumentContract.CreateContentForContract(
            runtimeValues,
            prepared);
        foreach (var (key, value) in current)
        {
            if (!key.Equals("schemaVersion", StringComparison.Ordinal))
            {
                prepared[key] = value?.DeepClone();
            }
        }
        return prepared;
    }
}

/// <summary>
/// Converts declarative temporal phase sources into the exact Runtime document
/// consumed by every timeline. The authored contract keeps a stable config
/// path; Preview and Production never resolve that path independently.
/// </summary>
internal static class RuntimeTemporalPhaseContract
{
    public static void Hydrate(JsonObject runtimeContract, JsonObject effectiveConfig)
    {
        HydrateTimeline(
            JsonPath.OptionalObject(runtimeContract, "animationTimeline", "Runtime Preview document"),
            effectiveConfig,
            "Runtime Preview document animation timeline");
        foreach (var collection in JsonPath.OptionalObjectArray(
                     runtimeContract,
                     "collections",
                     "Runtime Preview document"))
        {
            HydrateTimeline(
                JsonPath.OptionalObject(collection, "animationTimeline", "Runtime Preview collection"),
                effectiveConfig,
                "Runtime Preview collection animation timeline");
        }
    }

    private static void HydrateTimeline(JsonObject? timeline, JsonObject effectiveConfig, string owner)
    {
        if (timeline is null || !timeline.TryGetPropertyValue("ownerPhase", out var phaseNode)) return;
        var phase = phaseNode as JsonObject
            ?? throw new InvalidOperationException($"{owner} ownerPhase must be an object.");
        var kind = JsonPath.RequiredString(phase, "kind", $"{owner} ownerPhase");
        if (kind.Equals("resolvedMotion", StringComparison.Ordinal)
            || kind.Equals("itemMotion", StringComparison.Ordinal))
        {
            return;
        }
        if (!kind.Equals("configMotion", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{owner} has unknown ownerPhase kind '{kind}'.");
        }
        var path = JsonPath.OptionalStringArray(phase, "configPath", $"{owner} configMotion");
        if (path.Count == 0)
        {
            throw new InvalidOperationException($"{owner} configMotion requires a non-empty configPath.");
        }
        var motion = JsonPath.Get(effectiveConfig, path.ToArray()) as JsonObject
            ?? throw new InvalidOperationException(
                $"{owner} configMotion path '{string.Join('.', path)}' must resolve to a Motion object.");
        phase.Clear();
        phase["kind"] = "resolvedMotion";
        phase["motion"] = motion.DeepClone();
    }
}
