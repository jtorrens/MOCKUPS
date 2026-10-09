using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ProductionPreviewPayloadPreparer
{
    private readonly DesignPreviewPayloadDataSource _payloads;
    private readonly ProductionPreviewRuntimeResolver _runtime;

    public ProductionPreviewPayloadPreparer(DesignPreviewPayloadDataSource payloads, ProductionPreviewRuntimeResolver runtime)
    {
        _payloads = payloads;
        _runtime = runtime;
    }

    public DesignPreviewPayload PrepareRequired(ProjectTreeNode node, string? themeId, string themeMode, int shotFrame) =>
        Prepare(node, themeId, themeMode, shotFrame, CancellationToken.None)
        ?? throw new InvalidOperationException($"Production Preview frame {shotFrame} for '{node.Id}' has no complete payload.");

    public DesignPreviewPayload? Prepare(ProjectTreeNode node, string? themeId, string themeMode,
        int shotFrame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (node.Kind is ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance)
            return PrepareSequence(node, themeId, themeMode, shotFrame, shotFrame, cancellationToken).AtFrame(shotFrame);
        var payload = DesignPreviewPayloadFactory.Create(_payloads, node, themeId, themeMode, shotFrame);
        cancellationToken.ThrowIfCancellationRequested();
        return payload is null ? null : _runtime.Resolve(payload, themeMode);
    }

    public PreparedProductionPreview PrepareSequence(ProjectTreeNode node, string? themeId, string themeMode,
        int startFrame, int endFrame, CancellationToken cancellationToken) =>
        ResolveScreens(DesignPreviewPayloadFactory.PrepareProduction(_payloads, node, themeId, themeMode,
            startFrame, endFrame, cancellationToken), themeMode, cancellationToken);

    public PreparedProductionPreview PrepareRenderSequence(ProjectTreeNode shot, string themeStrategy, string themeId,
        string deviceId, string themeMode, int startFrame, int endFrame, CancellationToken cancellationToken) =>
        ResolveScreens(DesignPreviewPayloadFactory.PrepareProductionRender(_payloads, shot, themeStrategy, themeId,
            deviceId, themeMode, startFrame, endFrame, cancellationToken), themeMode, cancellationToken);

    private PreparedProductionPreview ResolveScreens(PreparedProductionPreview sequence, string themeMode,
        CancellationToken cancellationToken) =>
        sequence.MapScreens(screen =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = _runtime.Resolve(screen, themeMode);
            cancellationToken.ThrowIfCancellationRequested();
            return resolved;
        });

    public IReadOnlyList<DesignPreviewPayload?> PrepareFrames(ProjectTreeNode node, string? themeId,
        string themeMode, int startFrame, int endFrame, CancellationToken cancellationToken) =>
        PrepareSequence(node, themeId, themeMode, startFrame, endFrame, cancellationToken)
            .Frames(startFrame, endFrame, cancellationToken);
}

internal sealed record PreparedProductionPlayback(
    string RequestSignature,
    ProjectTreeNodeKind NodeKind,
    string NodeId,
    int StartFrame,
    IReadOnlyList<DesignPreviewPayload?> Frames)
{
    public bool Covers(
        ProjectTreeNode node,
        int startFrame,
        int endFrame)
    {
        return node.Kind == NodeKind
            && node.Id.Equals(
                NodeId,
                StringComparison.Ordinal)
            && startFrame >= StartFrame
            && endFrame
                < StartFrame + Frames.Count;
    }

    public bool TryGetFrame(
        ProjectTreeNode node,
        int frame,
        out DesignPreviewPayload? payload)
    {
        var frameIndex = frame - StartFrame;
        if (node.Kind != NodeKind
            || !node.Id.Equals(NodeId, StringComparison.Ordinal)
            || frameIndex < 0
            || frameIndex >= Frames.Count)
        {
            payload = null;
            return false;
        }

        payload = Frames[frameIndex];
        return true;
    }
}
