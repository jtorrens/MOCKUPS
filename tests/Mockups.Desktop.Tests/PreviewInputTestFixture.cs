using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;

// Test composition only: the production session has no persistence capability.
internal static class PreviewInputTestFixture
{
    private sealed class Binding(DesignPreviewInputPreparer preparer)
    {
        public DesignPreviewInputPreparer Preparer { get; } = preparer;
        public DesignPreviewPayload? Payload { get; set; }
    }
    private static readonly ConditionalWeakTable<ComponentPreviewInputSession, Binding> Bindings = new();

    public static ComponentPreviewInputSession Create(
        IComponentPreviewInputRepository componentPreview,
        IDictionaryFieldContextRepository dictionary,
        IActorPreviewRepository actors,
        IProjectPathResolver projectPaths,
        Action refreshPreview,
        Func<ComponentPreviewActionDefinition, Task<bool>>? preparePlaybackFrames = null)
    {
        var session = new ComponentPreviewInputSession(new PreviewPlaybackState(), refreshPreview, refreshPreview, preparePlaybackFrames);
        Bind(session, componentPreview, dictionary, actors, projectPaths);
        return session;
    }

    public static void Bind(ComponentPreviewInputSession session,
        IComponentPreviewInputRepository componentPreview, IDictionaryFieldContextRepository dictionary,
        IActorPreviewRepository actors, IProjectPathResolver projectPaths) =>
        Bindings.Add(session, new(new(componentPreview, dictionary, actors, projectPaths)));

    private static Binding Required(ComponentPreviewInputSession session) =>
        Bindings.TryGetValue(session, out var binding) ? binding
            : throw new InvalidOperationException("The test must explicitly compose the Preview preparer.");

    public static void UpdateForPayload(this ComponentPreviewInputSession session, DesignPreviewPayload? payload, string? projectId)
    {
        if (payload is null) { session.ClearPreparedContext(); return; }
        session.ApplyInputs(payload, payload.ThemeMode, projectId);
    }

    public static DesignPreviewPayload ApplyInputs(this ComponentPreviewInputSession session,
        DesignPreviewPayload payload, string themeMode, string? projectId)
    {
        var binding = Required(session);
        binding.Payload = payload;
        var prepared = binding.Preparer.Prepare(payload, session.CapturePreparation(payload), themeMode,
            projectId ?? throw new InvalidOperationException("Missing test Project."));
        session.ApplyPrepared(prepared);
        return prepared.Payload;
    }

    public static JsonObject ApplyTransientTestValues(this ComponentPreviewInputSession session,
        JsonObject preview, DesignPreviewPayload payload) =>
        Required(session).Preparer.ApplyTransient(preview, JsonNode.Parse(payload.ConfigJson)!.AsObject(),
            session.CaptureTransientState(payload));

    public static void SetExternalCollectionItemValues(this ComponentPreviewInputSession session,
        StructuredCollectionAddress address, string itemId, IReadOnlyDictionary<string, JsonNode?> values)
    {
        var binding = Required(session);
        var payload = binding.Payload ?? throw new InvalidOperationException("Missing prepared test owner.");
        var result = binding.Preparer.UpdateCollection(payload, session.CaptureTransientState(payload), address, itemId, values);
        session.SetExternalCollectionItems(payload, address.RootStorageJsonKey, result.Select(item => item!.AsObject()).ToArray());
    }
}
