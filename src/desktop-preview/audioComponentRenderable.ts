import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { AudioDesignContract } from "./audioComponentContract.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { buttonIconComponentToRenderableAt } from "./buttonIconComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  boundedCenterBox,
  colorForMode,
  expandBoxXY,
  numberToken,
  placeChild,
  renderScale,
  scalePlacement,
  selectedColor,
  shadow,
  translateBox,
  unionBoxes,
  variants,
} from "./componentRenderableCommon.js";

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
  const paddingX = Math.max(0, audio.padding.x * scale);
  const paddingY = Math.max(0, audio.padding.y * scale);
  const playSize = Math.max(1, audio.playCircleSize * scale);
  const playIconPadding = Math.max(0, audio.playIconPadding * scale);
  const barCount = Math.max(4, Math.round(audio.waveformBarCount));
  const waveformGap = Math.max(0, audio.waveformGap * scale);
  const barWidth = Math.max(1, audio.waveformBarWidth * scale);
  const waveformWidth = barCount * barWidth + (barCount - 1) * waveformGap;
  const minBarHeight = Math.max(1, audio.waveformMinHeight * scale);
  const maxBarHeight = Math.max(minBarHeight, audio.waveformMaxHeight * scale);
  const knobSize = audio.progressKnobSize * scale;
  const waveformHeight = Math.max(maxBarHeight, knobSize);
  const durationText = audio.playback.durationText;
  const durationWidth = Math.ceil(durationText.length * audio.textSize * scale * 0.58);
  const textHeight = audio.textSize * scale * 1.25;
  const waveformColumnWidth = Math.max(waveformWidth, durationWidth);
  const verticalGap = Math.max(2 * scale, paddingY * 0.5);
  const playbackHeight = Math.max(playSize, waveformHeight);
  const playBoxLocal = {
    x: 0,
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
  const avatarSize = audio.avatarSlot.avatar
    ? audio.avatarSlot.avatar.size * scale
    : 0;
  const avatarBoxLocal = audio.avatarSlot.avatar
    ? placeChild(
        playbackBoxLocal,
        { width: avatarSize, height: avatarSize },
        scalePlacement(audio.avatarSlot.placement, scale),
      )
    : undefined;
  const badgeSurfaceSize = audio.badgeSlot.badge
    ? audio.badgeSlot.badge.buttonSize * scale
    : 0;
  const badgeBoxLocal = audio.badgeSlot.badge && avatarBoxLocal
    ? placeChild(
        avatarBoxLocal,
        { width: badgeSurfaceSize, height: badgeSurfaceSize },
        scalePlacement(audio.badgeSlot.placement, scale),
      )
    : undefined;
  const avatarNodeLocal = audio.avatarSlot.avatar && avatarBoxLocal
    ? avatarComponentToRenderableAt(payload, audio.avatarSlot.avatar, avatarBoxLocal)
    : undefined;
  const badgeNodeLocal = audio.badgeSlot.badge && badgeBoxLocal
    ? buttonIconComponentToRenderableAt(payload, audio.badgeSlot.badge, badgeBoxLocal)
    : undefined;
  const childBoxes: RenderableBox[] = [
    playBoxLocal,
    waveformBoxLocal,
    textBoxLocal,
  ];
  if (avatarNodeLocal?.box) childBoxes.push(avatarNodeLocal.box);
  if (badgeNodeLocal?.box) childBoxes.push(badgeNodeLocal.box);
  const childrenBounds = unionBoxes(childBoxes);
  const audioBoxLocal = expandBoxXY(childrenBounds, paddingX, paddingY);
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
  const badgeBox = badgeBoxLocal ? translateBox(badgeBoxLocal, origin) : undefined;
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
  const waveformBars = Array.from({ length: barCount }, (_, index) => {
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
      role: index < playedBars ? "played" : "unplayed",
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
    };
  });
  const badgeNode = audio.badgeSlot.badge && badgeBox
    ? buttonIconComponentToRenderableAt(payload, audio.badgeSlot.badge, badgeBox)
    : undefined;
  const audioBorderWidth = audio.surface.borderWidth * scale;
  const audioRelief = audio.surface.reliefEnabled
    ? {
        angleDeg: audio.surface.reliefAngle,
        extension: audio.surface.reliefExtent * scale,
        spread: audio.surface.reliefSpread * scale,
        upperIntensity: audio.surface.reliefTopIntensity * audio.backgroundAlpha,
        lowerIntensity: audio.surface.reliefBottomIntensity * audio.backgroundAlpha,
      }
    : undefined;
  const audioShadow = audio.surface.shadowEnabled ? shadow(payload) : undefined;

  return {
    id: audio.id,
    type: "group",
    role: "audio",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${audio.id}.surface`,
        type: "surface",
        role: "audio_surface",
        frame: 0,
        box: audioBox,
        style: {
          background: selectedColor(
            payload,
            audio.backgroundColorToken,
            audio.backgroundAlpha,
          ),
          borderRadius: numberToken(payload, audio.surface.cornerRadiusToken) * scale,
          borderWidth: audioBorderWidth,
          borderColor: selectedColor(payload, audio.surface.borderColorToken),
          shadow: audioShadow,
          surfaceRelief: audioRelief,
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: colorForMode(
                  payload,
                  audio.backgroundColorToken,
                  mode,
                  audio.backgroundAlpha,
                ),
                borderColor: colorForMode(
                  payload,
                  audio.surface.borderColorToken,
                  mode,
                ),
              },
            ]),
          ),
        },
      },
      {
        id: `${audio.id}.play`,
        type: "surface",
        role: "play_control",
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
        role: "progress_knob",
        frame: 0,
        box: knobBox,
        style: {
          background: selectedColor(payload, audio.playColorToken),
          borderRadius: knobSize / 2,
        },
      },
      {
        id: `${audio.id}.duration`,
        type: "text",
        role: "duration",
        frame: 0,
        box: textBox,
        text: durationText,
        style: {
          color: selectedColor(payload, audio.textColorToken),
          fontSize: audio.textSize * scale,
          display: "block",
          lineHeight: textBox.height,
          textAlign: "right",
          whiteSpace: "nowrap",
        },
      },
      ...(audio.avatarSlot.avatar && avatarBox
        ? [avatarComponentToRenderableAt(payload, audio.avatarSlot.avatar, avatarBox)]
        : []),
      ...(audio.badgeSlot.badge && badgeBox
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
