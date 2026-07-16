import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { AudioDesignContract } from "./audioComponentContract.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { badgeComponentToRenderableAt } from "./badgeComponentRenderable.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  boundedCenterBox,
  numberToken,
  renderScale,
  selectedColor,
  translateRenderableNode,
  translateBox,
  unionBoxes,
} from "./componentRenderableCommon.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

function hashString(value: string) {
  let hash = 2166136261;
  for (const char of value) {
    hash ^= char.charCodeAt(0);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

function deterministicWaveformValue(seed: number, index: number) {
  const value = Math.sin((seed + index * 97.13) * 0.017) * 43758.5453;
  return value - Math.floor(value);
}

export function audioComponentToRenderable(
  payload: DesignPreviewPayload,
  audio: AudioDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const paddingX = Math.max(0, numberToken(payload, audio.padding.xToken) * scale);
  const paddingY = Math.max(0, numberToken(payload, audio.padding.yToken) * scale);
  const playSize = Math.max(1, audio.playCircleSize * scale);
  const playIconPadding = Math.max(0, numberToken(payload, audio.playIconPaddingToken) * scale);
  const barCount = Math.max(4, Math.round(audio.waveformBarCount));
  const availableWidth = Math.max(1, audio.availableWidth * scale);
  const avatarSize = audio.avatarSlot.avatar
    ? audio.avatarSlot.avatar.size * scale
    : 0;
  const avatarOnLeft = avatarSize > 0 && audio.avatarSlot.placement.alignX < 0.5;
  const avatarReserve = avatarSize > 0 ? avatarSize + paddingX : 0;
  const contentLeft = paddingX + (avatarOnLeft ? avatarReserve : 0);
  const contentRight = availableWidth - paddingX - (avatarOnLeft ? 0 : avatarReserve);
  const preferredWaveformGap = Math.max(0, numberToken(payload, audio.waveformGapToken) * scale);
  const waveformWidth = Math.max(1, contentRight - contentLeft - playSize - paddingX);
  const waveformGap = Math.min(
    preferredWaveformGap,
    waveformWidth / Math.max(1, barCount * 2 - 1),
  );
  const barWidth = Math.max(1, (waveformWidth - (barCount - 1) * waveformGap) / barCount);
  const minBarHeight = Math.max(1, audio.waveformMinHeight * scale);
  const maxBarHeight = Math.max(minBarHeight, audio.waveformMaxHeight * scale);
  const knobSize = audio.progressKnobSize * scale;
  const waveformHeight = Math.max(maxBarHeight, knobSize);
  const durationSize = measureLabelComponent(audio.durationLabel, payload);
  const durationWidth = durationSize.width;
  const textHeight = durationSize.height;
  const waveformColumnWidth = Math.max(waveformWidth, durationWidth);
  const verticalGap = Math.max(2 * scale, paddingY * 0.5);
  const playbackHeight = Math.max(playSize, waveformHeight, avatarSize);
  const playBoxLocal = {
    x: contentLeft,
    y: (playbackHeight - playSize) / 2,
    width: playSize,
    height: playSize,
  };
  const waveformBoxLocal = {
    x: playBoxLocal.x + playBoxLocal.width + paddingX,
    y: (playbackHeight - waveformHeight) / 2,
    width: waveformWidth,
    height: waveformHeight,
  };
  const playbackBoxLocal = unionBoxes([playBoxLocal, waveformBoxLocal]);
  const textBoxLocal = {
    x: waveformBoxLocal.x + waveformColumnWidth - durationWidth,
    y: waveformBoxLocal.y + waveformBoxLocal.height + verticalGap,
    width: durationWidth,
    height: textHeight,
  };
  const avatarBoxLocal = audio.avatarSlot.avatar
    ? {
        x: avatarOnLeft ? paddingX : availableWidth - paddingX - avatarSize,
        y: (playbackHeight - avatarSize) / 2,
        width: avatarSize,
        height: avatarSize,
      }
    : undefined;
  const avatarNodeLocal = audio.avatarSlot.avatar && avatarBoxLocal
    ? avatarComponentToRenderableAt(payload, audio.avatarSlot.avatar, avatarBoxLocal)
    : undefined;
  const badgeNodeLocal = audio.badgeSlot.badge && avatarBoxLocal
    ? badgeComponentToRenderableAt(payload, audio.badgeSlot.badge, avatarBoxLocal)
    : undefined;
  const childBoxes: RenderableBox[] = [
    playBoxLocal,
    waveformBoxLocal,
    textBoxLocal,
  ];
  if (avatarNodeLocal?.box) childBoxes.push(avatarNodeLocal.box);
  if (badgeNodeLocal?.box) childBoxes.push(badgeNodeLocal.box);
  const childrenBounds = unionBoxes(childBoxes);
  const audioBoxLocal = {
    x: 0,
    y: childrenBounds.y - paddingY,
    width: availableWidth,
    height: childrenBounds.height + paddingY * 2,
  };
  const localBounds = audioBoxLocal;
  const groupBox = boundedCenterBox(payload, localBounds.width, localBounds.height);
  const origin = {
    x: groupBox.x - localBounds.x,
    y: groupBox.y - localBounds.y,
  };
  const audioBox = translateBox(audioBoxLocal, origin);
  const playBox = translateBox(playBoxLocal, origin);
  const waveformBox = translateBox(waveformBoxLocal, origin);
  const textBox = translateBox(textBoxLocal, origin);
  const avatarBox = avatarBoxLocal ? translateBox(avatarBoxLocal, origin) : undefined;
  const progress = audio.playback.progress;
  const actualWaveformEnd =
    waveformBox.x + (barCount - 1) * (barWidth + waveformGap) + barWidth;
  const firstBarCenter = waveformBox.x + barWidth / 2;
  const lastBarCenter = actualWaveformEnd - barWidth / 2;
  const playedBars = Math.floor(barCount * progress);
  const waveformSeed = hashString(audio.id);
  const knobBox = {
    x: firstBarCenter + (lastBarCenter - firstBarCenter) * progress - knobSize / 2,
    y: waveformBox.y + waveformBox.height / 2 - knobSize / 2,
    width: knobSize,
    height: knobSize,
  };
  const waveformBars: RenderableNode[] = Array.from({ length: barCount }, (_, index) => {
    const normalized = deterministicWaveformValue(waveformSeed, index);
    const height = minBarHeight + normalized * (maxBarHeight - minBarHeight);
    const box = {
      x: waveformBox.x + index * (barWidth + waveformGap),
      y: waveformBox.y + waveformBox.height / 2 - height / 2,
      width: barWidth,
      height,
    };
    return {
      id: `${audio.id}.waveform.${index}`,
      type: "surface",
      frame: 0,
      box,
      style: {
        background: selectedColor(
          payload,
          index < playedBars
            ? audio.waveformPlayedColorToken
            : audio.waveformColorToken,
        ),
        borderRadius: Math.max(1, barWidth / 2),
      },
    } satisfies RenderableNode;
  });
  const badgeNode = badgeNodeLocal
    ? translateRenderableNode(badgeNodeLocal, origin)
    : undefined;

  return {
    id: audio.id,
    type: "group",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        ...surfaceComponentToRenderableAt(payload, audio.surface, audioBox),
        id: `${audio.id}.surface`,
      },
      {
        id: `${audio.id}.play`,
        type: "surface",
        frame: 0,
        box: playBox,
        text: "▶",
        style: {
          alignItems: "center",
          background: selectedColor(payload, audio.playColorToken),
          borderRadius: playSize / 2,
          color: selectedColor(payload, audio.playIconColorToken),
          display: "flex",
          fontSize: Math.max(1, playSize - playIconPadding * 2),
          justifyContent: "center",
          lineHeight: playSize,
          textAlign: "center",
          paddingLeft: Math.max(0, playSize - playIconPadding * 2) * 0.08,
        },
      },
      ...waveformBars,
      {
        id: `${audio.id}.knob`,
        type: "surface",
        frame: 0,
        box: knobBox,
        style: {
          background: selectedColor(payload, audio.playColorToken),
          borderRadius: knobSize / 2,
        },
      },
      labelComponentToRenderableAt(payload, audio.durationLabel, textBox),
      ...(audio.avatarSlot.avatar && avatarBox
        ? [avatarComponentToRenderableAt(payload, audio.avatarSlot.avatar, avatarBox)]
        : []),
      ...(badgeNode
        ? [
            {
              ...badgeNode!,
              style: {
                ...badgeNode!.style,
                zIndex: 20,
              },
            },
          ]
        : []),
    ],
  };
}

export function measureAudioComponent(
  payload: DesignPreviewPayload,
  audio: AudioDesignContract,
): { width: number; height: number } {
  const node = audioComponentToRenderable(payload, audio);
  return {
    width: node.box?.width ?? 1,
    height: node.box?.height ?? 1,
  };
}

export function audioComponentToRenderableAt(
  payload: DesignPreviewPayload,
  audio: AudioDesignContract,
  box: RenderableBox,
): RenderableNode {
  const node = audioComponentToRenderable(payload, audio);
  if (!node.box) {
    return {
      ...node,
      box,
    };
  }

  return translateRenderableNode(
    node,
    {
      x: box.x - node.box.x,
      y: box.y - node.box.y,
    },
    {
      x: box.x,
      y: box.y,
      width: node.box.width,
      height: node.box.height,
    },
  );
}
