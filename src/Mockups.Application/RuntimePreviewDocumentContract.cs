using System.Text.Json.Nodes;

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
