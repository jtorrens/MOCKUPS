using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace Mockups.DesktopEditorShell.EditorShell;

// A request-scoped value document. Frame evaluation has no persistence capability.
internal sealed class PreparedProductionPreview
{
    public string OwnerId { get; }
    public bool IsScreen { get; }
    public int ShotDurationFrames { get; }
    public IReadOnlyList<DesignPreviewPayload> Screens { get; }

    public PreparedProductionPreview(string ownerId, bool isScreen, int shotDurationFrames,
        IEnumerable<DesignPreviewPayload> screens)
    {
        OwnerId = ownerId;
        IsScreen = isScreen;
        ShotDurationFrames = shotDurationFrames;
        Screens = Array.AsReadOnly(screens.ToArray());
        if (IsScreen && Screens.Count != 1)
            throw new InvalidOperationException("Screen preparation requires its exact owner.");
        foreach (var screen in Screens)
        {
            if (screen.ScreenTiming is null || screen.ScreenTransition is not null)
                throw new InvalidOperationException("Production preparation requires complete uncomposed Screen owners.");
        }
    }

    public PreparedProductionPreview MapScreens(Func<DesignPreviewPayload, DesignPreviewPayload> prepare) =>
        new(OwnerId, IsScreen, ShotDurationFrames, Screens.Select(prepare));

    public string ContentSignature() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));

    public DesignPreviewPayload? AtFrame(int shotFrame)
    {
        if (!IsScreen && (shotFrame < 0 || shotFrame >= ShotDurationFrames)) return null;
        var layers = Screens.Where(screen => IsScreen || Contains(screen.ScreenTiming!, shotFrame))
            .Reverse().Select(screen => LayerAtFrame(screen, shotFrame, IsScreen)).ToArray();
        if (layers.Length == 0) return null;
        var primary = layers[^1].Owner;
        return primary with
        {
            OwnerId = OwnerId,
            Kind = layers.Length == 1 && layers[0].Phase == "content" ? primary.Kind : "screenTransition",
            ScreenTransition = layers.Length == 1 && layers[0].Phase == "content" ? null
                : new ScreenTransitionPayload(layers, primary.ScreenTiming!.TransitionFrameCount),
        };
    }

    public IReadOnlyList<DesignPreviewPayload?> Frames(int startFrame, int endFrame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new FrameInterval(this, startFrame, checked(Math.Max(startFrame, endFrame) - startFrame + 1), cancellationToken);
    }

    // The interval owns no duplicate frame documents. Opening a Screen evaluates
    // only its requested frame; Play/export enumerate through the same projection.
    private sealed class FrameInterval(PreparedProductionPreview sequence, int start, int count,
        CancellationToken cancellationToken) : IReadOnlyList<DesignPreviewPayload?>
    {
        public int Count => count;
        public DesignPreviewPayload? this[int index]
        {
            get
            {
                if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
                cancellationToken.ThrowIfCancellationRequested();
                return sequence.AtFrame(checked(start + index));
            }
        }
        public IEnumerator<DesignPreviewPayload?> GetEnumerator()
        {
            for (var index = 0; index < count; index++) yield return this[index];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static bool Contains(ScreenTimingPayload timing, int frame) =>
        frame >= timing.ScreenStartFrame && frame < timing.ScreenStartFrame + ScreenTimelineTiming.EffectiveDurationFrames(
            timing.ActionDurationFrames, timing.TransitionFrameCount, timing.ActionDelayFrames);

    private static ScreenTransitionLayerPayload LayerAtFrame(DesignPreviewPayload template, int shotFrame, bool clamp)
    {
        var timing = template.ScreenTiming!;
        var state = ScreenTimelineTiming.ResolveFrame(shotFrame, timing.ShotDurationFrames,
            timing.ScreenStartFrame, timing.ActionDurationFrames, timing.TransitionFrameCount, timing.ActionDelayFrames, clamp);
        var instance = JsonPath.ParseRequiredObject(template.InstanceJson, "Production Preview instance");
        JsonPath.RequiredObject(instance, "context", "Production Preview instance")["screenFrame"] = state.ActionFrame;
        var owner = template with
        {
            LocalFrame = state.ActionFrame,
            ScreenTiming = timing with { ScreenFrame = state.ScreenFrame },
            InstanceJson = instance.ToJsonString(),
            DesignPreviewJson = WithFrame(template.DesignPreviewJson, state.ActionFrame),
            RuntimeContractJson = WithFrame(template.RuntimeContractJson, state.ActionFrame),
        };
        return new ScreenTransitionLayerPayload(owner, timing.TransitionMotionJson, state.Phase,
            state.PhaseElapsedFrames * 1000.0 / Math.Max(1, owner.FrameRate));
    }

    private static string WithFrame(string json, int frame)
    {
        var document = JsonPath.ParseRequiredObject(json, "Production frame document");
        if (document["timelineFrameJsonKey"]?.GetValue<string>() is { Length: > 0 } key)
            document[key] = frame;
        return document.ToJsonString();
    }
}
