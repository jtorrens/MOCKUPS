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
        return
            Prepare(
                node,
                themeId,
                themeMode,
                shotFrame,
                CancellationToken.None)
            ?? throw new InvalidOperationException(
                $"Production Preview frame {shotFrame} for '{node.Id}' has no complete payload.");
    }

    public DesignPreviewPayload? Prepare(
        ProjectTreeNode node,
        string? themeId,
        string themeMode,
        int shotFrame,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        var payload =
            DesignPreviewPayloadFactory.Create(
                _payloads,
                node,
                themeId,
                themeMode,
                shotFrame);
        cancellationToken
            .ThrowIfCancellationRequested();
        if (payload is null)
        {
            return null;
        }

        var resolved =
            _runtime.Resolve(
                payload,
                themeMode);
        cancellationToken
            .ThrowIfCancellationRequested();
        return resolved;
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
                Prepare(
                    node,
                    themeId,
                    themeMode,
                    frame,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Production Preview frame {frame} for '{node.Id}' has no complete payload."));
        }

        return frames;
    }
}
