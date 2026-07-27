using System;
using System.Collections.Generic;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ProductionPreviewPayloadPreparer
{
    private readonly DesignPreviewPayloadDataSource
        _payloads;
    private readonly ProductionPreviewRuntimeResolver
        _runtime;

    public ProductionPreviewPayloadPreparer(
        DesignPreviewPayloadDataSource payloads,
        ProductionPreviewRuntimeResolver runtime)
    {
        _payloads = payloads;
        _runtime = runtime;
    }

    public DesignPreviewPayload PrepareRequired(
        ProjectTreeNode node,
        string? themeId,
        string themeMode,
        int shotFrame)
    {
        var payload =
            DesignPreviewPayloadFactory.Create(
                _payloads,
                node,
                themeId,
                themeMode,
                shotFrame)
            ?? throw new InvalidOperationException(
                $"Production Preview frame {shotFrame} for '{node.Id}' has no complete payload.");
        return _runtime.Resolve(
            payload,
            themeMode);
    }

    public IReadOnlyList<DesignPreviewPayload>
        PrepareFrames(
            ProjectTreeNode node,
            string? themeId,
            string themeMode,
            int startFrame,
            int endFrame,
            CancellationToken cancellationToken)
    {
        var lastFrame =
            Math.Max(startFrame, endFrame);
        var frames =
            new List<DesignPreviewPayload>(
                lastFrame - startFrame + 1);
        for (var frame = startFrame;
             frame <= lastFrame;
             frame++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();
            frames.Add(
                PrepareRequired(
                    node,
                    themeId,
                    themeMode,
                    frame));
        }

        return frames;
    }
}
