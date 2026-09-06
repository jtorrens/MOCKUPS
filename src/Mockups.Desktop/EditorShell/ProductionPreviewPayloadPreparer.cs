using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
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

    public DesignPreviewPayload? PrepareRender(
        ProjectTreeNode shot,
        string themeStrategy,
        string themeId,
        string deviceId,
        string themeMode,
        int shotFrame)
    {
        var payload = DesignPreviewPayloadFactory.CreateProductionRender(
            _payloads,
            shot,
            themeStrategy,
            themeId,
            deviceId,
            themeMode,
            shotFrame);
        return payload is null
            ? null
            : _runtime.Resolve(payload, themeMode);
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
        return node.Kind switch
        {
            ProjectTreeNodeKind.ModuleInstance =>
                PrepareModuleInstanceFrames(
                    node,
                    themeId,
                    themeMode,
                    startFrame,
                    lastFrame,
                    cancellationToken),
            ProjectTreeNodeKind.Shot =>
                PrepareShotFrames(
                    node,
                    themeId,
                    themeMode,
                    startFrame,
                    lastFrame,
                    cancellationToken),
            _ => PrepareIndependentFrames(
                node,
                themeId,
                themeMode,
                startFrame,
                lastFrame,
                cancellationToken),
        };
    }

    private IReadOnlyList<DesignPreviewPayload>
        PrepareModuleInstanceFrames(
            ProjectTreeNode node,
            string? themeId,
            string themeMode,
            int startFrame,
            int lastFrame,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();
        var template =
            Prepare(
                node,
                themeId,
                themeMode,
                startFrame,
                cancellationToken)
            ?? throw MissingPayload(
                node,
                startFrame);
        var firstLocalFrame =
            template.ScreenTiming?.ScreenFrame
            ?? template.LocalFrame;
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
                AtLocalFrame(
                    template,
                    firstLocalFrame
                    + frame - startFrame));
        }

        return frames;
    }

    private IReadOnlyList<DesignPreviewPayload>
        PrepareShotFrames(
            ProjectTreeNode node,
            string? themeId,
            string themeMode,
            int startFrame,
            int lastFrame,
            CancellationToken cancellationToken)
    {
        var slots =
            _payloads.LoadShotSlots(
                node.Id);
        if (slots.Count == 0)
        {
            throw MissingPayload(
                node,
                startFrame);
        }

        var frames = new List<DesignPreviewPayload>(
            lastFrame - startFrame + 1);
        for (var frame = startFrame;
             frame <= lastFrame;
             frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(
                Prepare(
                    node,
                    themeId,
                    themeMode,
                    frame,
                    cancellationToken)
                ?? throw MissingPayload(node, frame));
        }

        return frames;
    }

    private IReadOnlyList<DesignPreviewPayload>
        PrepareIndependentFrames(
            ProjectTreeNode node,
            string? themeId,
            string themeMode,
            int startFrame,
            int lastFrame,
            CancellationToken cancellationToken)
    {
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
                ?? throw MissingPayload(
                    node,
                    frame));
        }

        return frames;
    }

    private static DesignPreviewPayload AtLocalFrame(
        DesignPreviewPayload template,
        int localFrame)
    {
        var frame =
            Math.Max(
                0,
                localFrame);
        if (template.ScreenTiming
            is { } timing)
        {
            var transition =
                template.ScreenTransition;
            var baseIncoming =
                (transition?.Incoming
                    ?? template)
                with
                {
                    ScreenTiming = null,
                    ScreenTransition = null,
                };
            var actionFrame =
                Math.Clamp(
                    frame
                    - timing.ActionStartFrame,
                    0,
                    timing.ActionDurationFrames - 1);
            var incoming =
                AtLocalFrame(
                    baseIncoming,
                    actionFrame);
            if (transition is null
                || frame
                    >= transition.DurationFrames)
            {
                return incoming with
                {
                    Name = template.Name,
                    OwnerId = template.OwnerId,
                    ScreenTiming =
                        timing with
                        {
                            ScreenFrame = frame,
                        },
                };
            }

            var outgoing =
                AtLocalFrame(
                    transition.Outgoing,
                    transition.Outgoing.LocalFrame);
            return incoming with
            {
                Kind = "screenTransition",
                Name = template.Name,
                OwnerId = template.OwnerId,
                ScreenTiming =
                    timing with
                    {
                        ScreenFrame = frame,
                    },
                ScreenTransition =
                    transition with
                    {
                        Outgoing = outgoing,
                        Incoming =
                            incoming with
                            {
                                ScreenTiming = null,
                            },
                        ElapsedMilliseconds =
                            frame
                            * 1000.0
                            / Math.Max(
                                1,
                                incoming.FrameRate),
                    },
            };
        }

        var preview =
            WithTimelineFrame(
                template.DesignPreviewJson,
                frame,
                "Production Preview frame");
        var runtimeContract =
            WithTimelineFrame(
                template.RuntimeContractJson,
                frame,
                "Production Runtime contract frame");
        var instance =
            JsonPath.ParseRequiredObject(
                template.InstanceJson,
                "Production Preview instance");
        var context =
            JsonPath.RequiredObject(
                instance,
                "context",
                "Production Preview instance");
        context["screenFrame"] =
            frame;
        return template with
        {
            DesignPreviewJson =
                preview.ToJsonString(),
            RuntimeContractJson =
                runtimeContract.ToJsonString(),
            InstanceJson =
                instance.ToJsonString(),
            LocalFrame =
                frame,
        };
    }

    private static JsonObject WithTimelineFrame(
        string json,
        int frame,
        string owner)
    {
        var document =
            JsonPath.ParseRequiredObject(
                json,
                owner);
        if (document["timelineFrameJsonKey"]
                ?.GetValue<string>()
            is { Length: > 0 } key)
        {
            document[key] =
                frame;
        }

        return document;
    }

    private static InvalidOperationException MissingPayload(
        ProjectTreeNode node,
        int frame) =>
        new(
            $"Production Preview frame {frame} for '{node.Id}' has no complete payload.");
}

internal sealed record PreparedProductionPlayback(
    string RequestSignature,
    ProjectTreeNodeKind NodeKind,
    string NodeId,
    int StartFrame,
    IReadOnlyList<DesignPreviewPayload> Frames)
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
