import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { AudioDesignContract } from "./audioComponentResolver.js";
import type { AvatarDesignContract } from "./avatarComponentResolver.js";
import type { ButtonIconDesignContract } from "./buttonIconComponentResolver.js";
import type { AlignmentPlacementContract } from "./componentResolverCommon.js";
import type { LabelDesignContract } from "./labelComponentResolver.js";
import type {
  NavigationBarDesignContract,
  StatusBarDesignContract,
  SystemBarItemContract,
} from "./systemBarPreviewResolver.js";

type JsonRecord = Record<string, unknown>;

function asRecord(value: unknown): JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as JsonRecord)
    : {};
}

function parseObject(json: string | undefined) {
  return asRecord(JSON.parse(json || "{}"));
}

function parsePreviewText(payload: DesignPreviewPayload) {
  const preview = parseObject(payload.designPreviewJson);
  const sampleText = preview.sampleText;
  if (typeof sampleText === "string") return sampleText;

  throw new Error("Missing design preview sampleText");
}

function renderScale(payload: DesignPreviewPayload) {
  const scale = payload.device.scaleToPixels;
  return typeof scale === "number" && Number.isFinite(scale) && scale > 0
    ? scale
    : 1;
}

function variants(payload: DesignPreviewPayload) {
  const tokens = parseObject(payload.themeTokensJson);
  const modes = asRecord(tokens.modes);
  const names = Object.keys(modes);
  return names.length > 0 ? names : [payload.themeMode || "light"];
}

function tokenValueForMode(
  payload: DesignPreviewPayload,
  token: string,
  mode: string,
) {
  if (!token.startsWith("theme.")) {
    throw new Error(`Unsupported theme token ${token}`);
  }

  const root = parseObject(payload.themeTokensJson);
  const modeRoot = asRecord(asRecord(root.modes)[mode]);
  const semanticKey = token.replace(/^theme\./, "");
  const parts = semanticKey.split(".");
  for (const source of [modeRoot, root]) {
    let current: unknown = source;
    for (const part of parts) {
      current = asRecord(current)[part];
    }
    if (current !== undefined) return current;

    const colors = asRecord(asRecord(source).colors);
    if (colors[semanticKey] !== undefined) return colors[semanticKey];
    if (colors[token] !== undefined) return colors[token];
  }

  throw new Error(`Missing theme token ${token} for mode ${mode}`);
}

function resolvePaletteColor(payload: DesignPreviewPayload, value: unknown) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error("Missing palette color value");
  }
  if (/^#|^rgb|^hsl|^transparent$/i.test(value)) return value;
  const resolved = payload.paletteColors?.[value];
  if (!resolved) throw new Error(`Missing palette color ${value}`);
  return payload.paletteNeutralColors?.[value]
    ? applyNeutralTint(payload, resolved)
    : resolved;
}

function colorForMode(
  payload: DesignPreviewPayload,
  token: string,
  mode: string,
  alpha = 1,
) {
  const raw = tokenValueForMode(payload, token, mode);
  const color =
    typeof raw === "object" && raw !== null && !Array.isArray(raw)
      ? resolvePaletteColor(payload, asRecord(raw).color)
      : resolvePaletteColor(payload, raw);
  const rawAlpha =
    typeof raw === "object" && raw !== null && !Array.isArray(raw)
      ? numberValue(asRecord(raw).alpha, 1)
      : 1;
  return cssColorWithAlpha(color, rawAlpha * alpha);
}

function selectedColor(payload: DesignPreviewPayload, token: string, alpha = 1) {
  return colorForMode(payload, token, payload.themeMode || "light", alpha);
}

function selectedPaletteColor(payload: DesignPreviewPayload, token: string, alpha = 1) {
  return cssColorWithAlpha(resolvePaletteColor(payload, token), alpha);
}

function buttonBackgroundColor(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  alpha = buttonIcon.backgroundAlpha,
) {
  return buttonIcon.backgroundPaletteColor
    ? selectedPaletteColor(payload, buttonIcon.backgroundPaletteColor, alpha)
    : selectedColor(payload, buttonIcon.backgroundColorToken, alpha);
}

function buttonIconColor(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
) {
  return buttonIcon.iconPaletteColor
    ? selectedPaletteColor(payload, buttonIcon.iconPaletteColor)
    : selectedColor(payload, buttonIcon.iconColorToken);
}

function buttonBackgroundColorForMode(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  mode: string,
) {
  return buttonIcon.backgroundPaletteColor
    ? selectedPaletteColor(
        payload,
        buttonIcon.backgroundPaletteColor,
        buttonIcon.backgroundAlpha,
      )
    : colorForMode(
        payload,
        buttonIcon.backgroundColorToken,
        mode,
        buttonIcon.backgroundAlpha,
      );
}

function buttonIconColorForMode(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  mode: string,
) {
  return buttonIcon.iconPaletteColor
    ? selectedPaletteColor(payload, buttonIcon.iconPaletteColor)
    : colorForMode(payload, buttonIcon.iconColorToken, mode);
}

function numberToken(payload: DesignPreviewPayload, token: string) {
  const raw = tokenValueForMode(payload, token, payload.themeMode || "light");
  const value = numberValue(raw, NaN);
  if (Number.isFinite(value)) return value;
  throw new Error(`Theme token ${token} is not numeric`);
}

function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function requiredNumberValue(value: unknown, path: string) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  throw new Error(`Missing numeric theme value ${path}`);
}

function shadow(payload: DesignPreviewPayload) {
  const root = parseObject(payload.themeTokensJson);
  const shadowRoot = asRecord(asRecord(root.shadows).default);
  const color = asRecord(shadowRoot.color);
  const colorToken = color.color;
  if (typeof colorToken !== "string" || !colorToken.trim()) {
    throw new Error("Missing theme.shadows.default.color.color");
  }

  const scale = renderScale(payload);
  return {
    offsetX:
      requiredNumberValue(shadowRoot.offsetX, "theme.shadows.default.offsetX") *
      scale,
    offsetY:
      requiredNumberValue(shadowRoot.offsetY, "theme.shadows.default.offsetY") *
      scale,
    blur:
      requiredNumberValue(shadowRoot.blur, "theme.shadows.default.blur") *
      scale,
    color: cssColorWithAlpha(
      resolvePaletteColor(payload, colorToken),
      requiredNumberValue(color.alpha, "theme.shadows.default.color.alpha"),
    ),
  };
}

function centerBox(payload: DesignPreviewPayload, width: number, height: number) {
  const { device } = payload;
  return {
    x: device.screenX + (device.screenWidth - width) / 2,
    y: device.screenY + (device.screenHeight - height) / 2,
    width,
    height,
  };
}

function labelTextWidth(text: string, fontSize: number) {
  return text.length * fontSize * 0.58;
}

export function measureLabelComponent(
  label: LabelDesignContract,
  payload: DesignPreviewPayload,
) {
  const scale = renderScale(payload);
  const fontSize = numberToken(payload, label.textSizeToken) * scale;
  const subtextFontSize = numberToken(payload, label.subtextSizeToken) * scale;
  return labelSize(label, fontSize, subtextFontSize, scale);
}

function labelSize(
  label: LabelDesignContract,
  fontSize: number,
  subtextFontSize: number,
  scale: number,
) {
  const paddingX = label.padding.x * scale;
  const paddingY = label.padding.y * scale;
  const hasSubtext = label.subtext.trim().length > 0;
  const lineHeight = Math.max(fontSize * 1.2, fontSize);
  const subtextLineHeight = Math.max(subtextFontSize * 1.2, subtextFontSize);
  const textGap = hasSubtext ? label.textGap * scale : 0;
  const contentWidth = Math.max(
    labelTextWidth(label.text, fontSize),
    hasSubtext ? labelTextWidth(label.subtext, subtextFontSize) : 0,
  );
  const contentHeight =
    lineHeight + (hasSubtext ? textGap + subtextLineHeight : 0);
  if (label.dimensionMode === "fixed") {
    return {
      width: label.size.width * scale,
      height: label.size.height * scale,
      lineHeight,
      subtextLineHeight,
      hasSubtext,
    };
  }

  return {
    width: Math.max(1, contentWidth + paddingX * 2),
    height: Math.max(1, contentHeight + paddingY * 2),
    lineHeight,
    subtextLineHeight,
    hasSubtext,
  };
}

export function labelComponentToRenderable(
  payload: DesignPreviewPayload,
  label: LabelDesignContract,
): RenderableNode {
  const size = measureLabelComponent(label, payload);
  return labelComponentToRenderableAt(
    payload,
    label,
    centerBox(payload, size.width, size.height),
  );
}

export function labelComponentToRenderableAt(
  payload: DesignPreviewPayload,
  label: LabelDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const fontSize = numberToken(payload, label.textSizeToken) * scale;
  const subtextFontSize = numberToken(payload, label.subtextSizeToken) * scale;
  const size = labelSize(label, fontSize, subtextFontSize, scale);
  const borderWidth = label.surface.borderWidth * scale;
  const background = selectedColor(
    payload,
    label.backgroundColorToken,
    label.surfaceAlpha,
  );
  const borderColor = selectedColor(
    payload,
    label.surface.borderColorToken,
    label.surfaceAlpha,
  );
  const cornerRadius = numberToken(payload, label.surface.cornerRadiusToken) * scale;
  const surfaceRelief = label.surface.reliefEnabled
    ? {
        angleDeg: label.surface.reliefAngle,
        extension: label.surface.reliefExtent * scale,
        spread: label.surface.reliefSpread * scale,
        upperIntensity: label.surface.reliefTopIntensity * label.surfaceAlpha,
        lowerIntensity: label.surface.reliefBottomIntensity * label.surfaceAlpha,
      }
    : undefined;

  return {
    id: label.id,
    type: "component_label",
    frame: 0,
    box,
    style: {
      background,
      borderWidth,
      borderColor,
      borderRadius: cornerRadius,
      shadow: label.surface.shadowEnabled ? shadow(payload) : undefined,
      surfaceRelief,
      paddingX: label.padding.x * scale,
      paddingY: label.padding.y * scale,
      whiteSpace: "nowrap",
      colorModes: Object.fromEntries(
        variants(payload).map((mode) => [
          mode,
          {
            background: colorForMode(
              payload,
              label.backgroundColorToken,
              mode,
              label.surfaceAlpha,
            ),
            textColor: colorForMode(payload, label.textColorToken, mode),
            subtextColor: colorForMode(payload, label.subtextColorToken, mode),
            borderColor: colorForMode(
              payload,
              label.surface.borderColorToken,
              mode,
              label.surfaceAlpha,
            ),
          },
        ]),
      ),
    },
    children: [
      {
        id: `${label.id}.text`,
        type: "component_label_text",
        frame: 0,
        text: label.text,
        style: {
          textColor: selectedColor(payload, label.textColorToken),
          fontSize,
          lineHeight: size.lineHeight,
          textAlign: label.textAlign,
          fontStyle: label.textStyle === "italic" ? "italic" : undefined,
          whiteSpace: "nowrap",
        },
      },
      ...(size.hasSubtext
        ? [
            {
              id: `${label.id}.subtext`,
              type: "component_label_subtext",
              frame: 0,
              text: label.subtext,
              style: {
                textColor: selectedColor(payload, label.subtextColorToken),
                fontSize: subtextFontSize,
                lineHeight: size.subtextLineHeight,
                marginTop: label.textGap * scale,
                textAlign: label.textAlign,
                fontStyle:
                  label.subtextStyle === "italic" ? "italic" : undefined,
                whiteSpace: "nowrap",
              },
            },
          ]
        : []),
    ],
    metadata: {
      route: "component-resolver.web-bridge",
      componentType: "label",
    },
  };
}

export function avatarComponentToRenderable(
  payload: DesignPreviewPayload,
  avatar: AvatarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const avatarSize = avatar.size * scale;
  const avatarShadow = avatar.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = avatar.labelSlot.label
    ? measureLabelComponent(avatar.labelSlot.label, payload)
    : undefined;
  const avatarLocalBox = { x: 0, y: 0, width: avatarSize, height: avatarSize };
  const labelLocalBox = labelSize
    ? placeChild(
        avatarLocalBox,
        labelSize,
        scalePlacement(avatar.labelSlot.placement, scale),
      )
    : undefined;
  const contentBounds = unionBoxes([
    avatarLocalBox,
    ...(labelLocalBox ? [labelLocalBox] : []),
  ]);
  const borderWidth = avatar.surface.borderWidth * scale;
  const surfaceRelief = avatar.surface.reliefEnabled
    ? {
        angleDeg: avatar.surface.reliefAngle,
        extension: avatar.surface.reliefExtent * scale,
        spread: avatar.surface.reliefSpread * scale,
        upperIntensity: avatar.surface.reliefTopIntensity,
        lowerIntensity: avatar.surface.reliefBottomIntensity,
      }
    : undefined;
  const visualPadding = avatarVisualPadding(borderWidth, avatarShadow, surfaceRelief);
  const groupBox = boundedCenterBox(
    payload,
    contentBounds.width + visualPadding * 2,
    contentBounds.height + visualPadding * 2,
  );
  const contentOrigin = {
    x: groupBox.x + visualPadding - contentBounds.x,
    y: groupBox.y + visualPadding - contentBounds.y,
  };
  const avatarBox = translateBox(avatarLocalBox, contentOrigin);
  const labelBox = labelLocalBox ? translateBox(labelLocalBox, contentOrigin) : undefined;

  return {
    id: avatar.id,
    type: "component_avatar",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${avatar.id}.placeholder`,
        type: "avatar",
        frame: 0,
        box: avatarBox,
        style: {
          borderRadius: numberToken(payload, avatar.cornerRadiusToken) * scale,
          borderWidth,
          borderColor: selectedColor(payload, avatar.surface.borderColorToken),
          shadow: avatarShadow,
          surfaceRelief,
        },
        asset: {
          type: "image",
          uri: sampleAvatarUri(),
        },
        metadata: {
          label: "Avatar preview",
          imageBaseSize: 256,
        },
      },
      ...(avatar.labelSlot.label && labelBox
        ? [
            labelComponentToRenderableAt(
              payload,
              avatar.labelSlot.label,
              labelBox,
            ),
          ]
        : []),
    ],
    metadata: {
      route: "component-resolver.web-bridge",
      componentType: "avatar",
    },
  };
}

export function audioComponentToRenderable(
  payload: DesignPreviewPayload,
  audio: AudioDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const audioBoxLocal = {
    x: 0,
    y: 0,
    width: audio.size.width * scale,
    height: audio.size.height * scale,
  };
  const avatarSize = audio.avatarSlot.avatar
    ? audio.avatarSlot.avatar.size * scale
    : 0;
  const avatarBoxLocal = audio.avatarSlot.avatar
    ? placeChild(
        audioBoxLocal,
        { width: avatarSize, height: avatarSize },
        scalePlacement(audio.avatarSlot.placement, scale),
      )
    : undefined;
  const badgeSurfaceSize = audio.badgeSlot.badge
    ? (audio.badgeSlot.badge.iconSize + audio.badgeSlot.badge.iconPadding * 2) * scale
    : 0;
  const badgeBoxLocal = audio.badgeSlot.badge && avatarBoxLocal
    ? placeChild(
        avatarBoxLocal,
        { width: badgeSurfaceSize, height: badgeSurfaceSize },
        scalePlacement(audio.badgeSlot.placement, scale),
      )
    : undefined;
  const localBounds = unionBoxes([
    audioBoxLocal,
    ...(avatarBoxLocal ? [avatarBoxLocal] : []),
    ...(badgeBoxLocal ? [badgeBoxLocal] : []),
  ]);
  const groupBox = boundedCenterBox(payload, localBounds.width, localBounds.height);
  const origin = {
    x: groupBox.x - localBounds.x,
    y: groupBox.y - localBounds.y,
  };
  const audioBox = translateBox(audioBoxLocal, origin);
  const avatarBox = avatarBoxLocal ? translateBox(avatarBoxLocal, origin) : undefined;
  const badgeBox = badgeBoxLocal ? translateBox(badgeBoxLocal, origin) : undefined;
  const playSize = Math.min(audioBox.height, audio.playCircleSize * scale);
  const playBox = {
    x: audioBox.x + audioBox.height * 0.22,
    y: audioBox.y + (audioBox.height - playSize) / 2,
    width: playSize,
    height: playSize,
  };
  const textBox = {
    x: audioBox.x + audioBox.width - audioBox.height * 1.12,
    y: audioBox.y + audioBox.height * 0.18,
    width: audioBox.height,
    height: audioBox.height * 0.32,
  };
  const waveformBox = {
    x: playBox.x + playBox.width + audioBox.height * 0.18,
    y: audioBox.y + audioBox.height * 0.36,
    width: Math.max(1, textBox.x - playBox.x - playBox.width - audioBox.height * 0.36),
    height: audioBox.height * 0.28,
  };
  const knobSize = audio.progressKnobSize * scale;
  const progress = 0.42;
  const barCount = Math.max(4, Math.round(audio.waveformBarCount));
  const waveformGap = Math.max(0, audio.waveformGap * scale);
  const barWidth = Math.max(
    1,
    Math.floor((waveformBox.width - waveformGap * (barCount - 1)) / barCount),
  );
  const actualWaveformEnd =
    waveformBox.x + (barCount - 1) * (barWidth + waveformGap) + barWidth;
  const firstBarCenter = waveformBox.x + barWidth / 2;
  const lastBarCenter = actualWaveformEnd - barWidth / 2;
  const minBarHeight = Math.max(1, audio.waveformMinHeight * scale);
  const maxBarHeight = Math.max(minBarHeight, audio.waveformMaxHeight * scale);
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
      y: audioBox.y + audioBox.height / 2 - height / 2,
      width: barWidth,
      height,
    };
    return {
      id: `${audio.id}.waveform.${index}`,
      type: "component_audio_waveform_bar",
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

  return {
    id: audio.id,
    type: "component_audio",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${audio.id}.surface`,
        type: "component_audio_surface",
        frame: 0,
        box: audioBox,
        style: {
          background: selectedColor(
            payload,
            audio.backgroundColorToken,
            audio.backgroundAlpha,
          ),
          borderRadius: audioBox.height / 2,
        },
      },
      {
        id: `${audio.id}.play`,
        type: "component_audio_play",
        frame: 0,
        box: playBox,
        text: "▶",
        style: {
          alignItems: "center",
          background: selectedColor(payload, audio.playColorToken),
          borderRadius: playSize / 2,
          color: selectedColor(payload, audio.playIconColorToken),
          display: "flex",
          fontSize: playSize * 0.44,
          justifyContent: "center",
          lineHeight: playSize,
          textAlign: "center",
          paddingLeft: playSize * 0.07,
        },
      },
      ...waveformBars,
      {
        id: `${audio.id}.knob`,
        type: "component_audio_knob",
        frame: 0,
        box: knobBox,
        style: {
          background: selectedColor(payload, audio.playColorToken),
          borderRadius: knobSize / 2,
        },
      },
      {
        id: `${audio.id}.duration`,
        type: "component_audio_duration",
        frame: 0,
        box: textBox,
        text: parsePreviewText(payload),
        style: {
          color: selectedColor(payload, audio.textColorToken),
          fontSize: audio.textSize * scale,
          lineHeight: textBox.height,
          textAlign: "center",
          whiteSpace: "nowrap",
        },
      },
      ...(audio.avatarSlot.avatar && avatarBox
        ? [avatarComponentToRenderableAt(payload, audio.avatarSlot.avatar, avatarBox)]
        : []),
      ...(audio.badgeSlot.badge && badgeBox
        ? [buttonIconComponentToRenderableAt(payload, audio.badgeSlot.badge, badgeBox)]
        : []),
    ],
    metadata: {
      route: "component-resolver.web-bridge",
      componentType: "audio",
    },
  };
}

function avatarComponentToRenderableAt(
  payload: DesignPreviewPayload,
  avatar: AvatarDesignContract,
  avatarBox: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const avatarShadow = avatar.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = avatar.labelSlot.label
    ? measureLabelComponent(avatar.labelSlot.label, payload)
    : undefined;
  const labelBox = labelSize
    ? placeChild(
        avatarBox,
        labelSize,
        scalePlacement(avatar.labelSlot.placement, scale),
      )
    : undefined;
  const borderWidth = avatar.surface.borderWidth * scale;
  const surfaceRelief = avatar.surface.reliefEnabled
    ? {
        angleDeg: avatar.surface.reliefAngle,
        extension: avatar.surface.reliefExtent * scale,
        spread: avatar.surface.reliefSpread * scale,
        upperIntensity: avatar.surface.reliefTopIntensity,
        lowerIntensity: avatar.surface.reliefBottomIntensity,
      }
    : undefined;
  const visualPadding = avatarVisualPadding(borderWidth, avatarShadow, surfaceRelief);
  const contentBounds = unionBoxes([
    avatarBox,
    ...(labelBox ? [labelBox] : []),
  ]);
  const groupBox = expandBox(contentBounds, visualPadding);

  return {
    id: avatar.id,
    type: "component_avatar",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${avatar.id}.placeholder`,
        type: "avatar",
        frame: 0,
        box: avatarBox,
        style: {
          borderRadius: numberToken(payload, avatar.cornerRadiusToken) * scale,
          borderWidth,
          borderColor: selectedColor(payload, avatar.surface.borderColorToken),
          shadow: avatarShadow,
          surfaceRelief,
        },
        asset: {
          type: "image",
          uri: sampleAvatarUri(),
        },
        metadata: {
          label: "Avatar preview",
          imageBaseSize: 256,
        },
      },
      ...(avatar.labelSlot.label && labelBox
        ? [
            labelComponentToRenderableAt(
              payload,
              avatar.labelSlot.label,
              labelBox,
            ),
          ]
        : []),
    ],
    metadata: {
      route: "component-resolver.web-bridge",
      componentType: "avatar",
    },
  };
}

export function buttonIconComponentToRenderable(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const iconSize = buttonIcon.iconSize * scale;
  const iconPadding = buttonIcon.iconPadding * scale;
  const surfaceSize = iconSize + iconPadding * 2;
  const iconShadow = buttonIcon.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = buttonIcon.labelSlot.label
    ? measureLabelComponent(buttonIcon.labelSlot.label, payload)
    : undefined;
  const buttonLocalBox = { x: 0, y: 0, width: surfaceSize, height: surfaceSize };
  const labelLocalBox = labelSize
    ? placeChild(
        buttonLocalBox,
        labelSize,
        scalePlacement(buttonIcon.labelSlot.placement, scale),
      )
    : undefined;
  const contentBounds = unionBoxes([
    buttonLocalBox,
    ...(labelLocalBox ? [labelLocalBox] : []),
  ]);
  const borderWidth = buttonIcon.surface.borderWidth * scale;
  const surfaceRelief = buttonIcon.surface.reliefEnabled
    ? {
        angleDeg: buttonIcon.surface.reliefAngle,
        extension: buttonIcon.surface.reliefExtent * scale,
        spread: buttonIcon.surface.reliefSpread * scale,
        upperIntensity: buttonIcon.surface.reliefTopIntensity * buttonIcon.backgroundAlpha,
        lowerIntensity: buttonIcon.surface.reliefBottomIntensity * buttonIcon.backgroundAlpha,
      }
    : undefined;
  const visualPadding = avatarVisualPadding(borderWidth, iconShadow, surfaceRelief);
  const groupBox = boundedCenterBox(
    payload,
    contentBounds.width + visualPadding * 2,
    contentBounds.height + visualPadding * 2,
  );
  const contentOrigin = {
    x: groupBox.x + visualPadding - contentBounds.x,
    y: groupBox.y + visualPadding - contentBounds.y,
  };
  const buttonBox = translateBox(buttonLocalBox, contentOrigin);
  const labelBox = labelLocalBox ? translateBox(labelLocalBox, contentOrigin) : undefined;
  const iconBox = {
    x: buttonBox.x + iconPadding,
    y: buttonBox.y + iconPadding,
    width: iconSize,
    height: iconSize,
  };

  return {
    id: buttonIcon.id,
    type: "component_button_icon",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${buttonIcon.id}.surface`,
        type: "component_button_icon_surface",
        frame: 0,
        box: buttonBox,
        style: {
          background: buttonBackgroundColor(payload, buttonIcon),
          borderRadius: numberToken(payload, buttonIcon.surface.cornerRadiusToken) * scale,
          borderWidth,
          borderColor: selectedColor(
            payload,
            buttonIcon.surface.borderColorToken,
            buttonIcon.backgroundAlpha,
          ),
          shadow: iconShadow,
          surfaceRelief,
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: buttonBackgroundColorForMode(payload, buttonIcon, mode),
                color: buttonIconColorForMode(payload, buttonIcon, mode),
                borderColor: colorForMode(
                  payload,
                  buttonIcon.surface.borderColorToken,
                  mode,
                  buttonIcon.backgroundAlpha,
                ),
              },
            ]),
          ),
        },
      },
      {
        id: `${buttonIcon.id}.glyph`,
        type: "component_button_icon_glyph",
        frame: 0,
        box: iconBox,
        style: {
          color: buttonIconColor(payload, buttonIcon),
        },
      },
      ...(buttonIcon.labelSlot.label && labelBox
        ? [
            labelComponentToRenderableAt(
              payload,
              buttonIcon.labelSlot.label,
              labelBox,
            ),
          ]
        : []),
    ],
    metadata: {
      route: "component-resolver.web-bridge",
      componentType: "buttonIcon",
    },
  };
}

function buttonIconComponentToRenderableAt(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  buttonBox: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const iconPadding = buttonIcon.iconPadding * scale;
  const iconSize = Math.max(1, buttonBox.width - iconPadding * 2);
  const iconShadow = buttonIcon.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = buttonIcon.labelSlot.label
    ? measureLabelComponent(buttonIcon.labelSlot.label, payload)
    : undefined;
  const labelBox = labelSize
    ? placeChild(
        buttonBox,
        labelSize,
        scalePlacement(buttonIcon.labelSlot.placement, scale),
      )
    : undefined;
  const borderWidth = buttonIcon.surface.borderWidth * scale;
  const surfaceRelief = buttonIcon.surface.reliefEnabled
    ? {
        angleDeg: buttonIcon.surface.reliefAngle,
        extension: buttonIcon.surface.reliefExtent * scale,
        spread: buttonIcon.surface.reliefSpread * scale,
        upperIntensity: buttonIcon.surface.reliefTopIntensity * buttonIcon.backgroundAlpha,
        lowerIntensity: buttonIcon.surface.reliefBottomIntensity * buttonIcon.backgroundAlpha,
      }
    : undefined;
  const visualPadding = avatarVisualPadding(borderWidth, iconShadow, surfaceRelief);
  const contentBounds = unionBoxes([
    buttonBox,
    ...(labelBox ? [labelBox] : []),
  ]);
  const groupBox = expandBox(contentBounds, visualPadding);
  const iconBox = {
    x: buttonBox.x + iconPadding,
    y: buttonBox.y + iconPadding,
    width: iconSize,
    height: iconSize,
  };

  return {
    id: buttonIcon.id,
    type: "component_button_icon",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${buttonIcon.id}.surface`,
        type: "component_button_icon_surface",
        frame: 0,
        box: buttonBox,
        style: {
          background: buttonBackgroundColor(payload, buttonIcon),
          borderRadius: numberToken(payload, buttonIcon.surface.cornerRadiusToken) * scale,
          borderWidth,
          borderColor: selectedColor(
            payload,
            buttonIcon.surface.borderColorToken,
            buttonIcon.backgroundAlpha,
          ),
          shadow: iconShadow,
          surfaceRelief,
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: buttonBackgroundColorForMode(payload, buttonIcon, mode),
                color: buttonIconColorForMode(payload, buttonIcon, mode),
                borderColor: colorForMode(
                  payload,
                  buttonIcon.surface.borderColorToken,
                  mode,
                  buttonIcon.backgroundAlpha,
                ),
              },
            ]),
          ),
        },
      },
      {
        id: `${buttonIcon.id}.glyph`,
        type: "component_button_icon_glyph",
        frame: 0,
        box: iconBox,
        style: {
          color: buttonIconColor(payload, buttonIcon),
        },
      },
      ...(buttonIcon.labelSlot.label && labelBox
        ? [
            labelComponentToRenderableAt(
              payload,
              buttonIcon.labelSlot.label,
              labelBox,
            ),
          ]
        : []),
    ],
    metadata: {
      route: "component-resolver.web-bridge",
      componentType: "buttonIcon",
    },
  };
}

function avatarVisualPadding(
  borderWidth: number,
  shadowValue: Record<string, unknown> | undefined,
  surfaceRelief: Record<string, unknown> | undefined,
) {
  const shadowPadding = shadowValue
    ? Math.max(
        Math.abs(typeof shadowValue.offsetX === "number" ? shadowValue.offsetX : 0),
        Math.abs(typeof shadowValue.offsetY === "number" ? shadowValue.offsetY : 0),
      ) + (typeof shadowValue.blur === "number" ? shadowValue.blur * 2 : 0)
    : 0;
  const reliefPadding = surfaceRelief
    ? Math.max(
        typeof surfaceRelief.extension === "number" ? surfaceRelief.extension : 0,
        typeof surfaceRelief.spread === "number" ? surfaceRelief.spread : 0,
      )
    : 0;
  return Math.ceil(Math.max(borderWidth, shadowPadding, reliefPadding, 0));
}

function boundedCenterBox(
  payload: DesignPreviewPayload,
  width: number,
  height: number,
) {
  const centered = centerBox(payload, width, height);
  const minX = payload.device.screenX;
  const minY = payload.device.screenY;
  const maxX = payload.device.screenX + payload.device.screenWidth - width;
  const maxY = payload.device.screenY + payload.device.screenHeight - height;
  return {
    x: maxX >= minX ? Math.min(Math.max(centered.x, minX), maxX) : minX,
    y: maxY >= minY ? Math.min(Math.max(centered.y, minY), maxY) : minY,
    width,
    height,
  };
}

function scalePlacement(
  placement: AlignmentPlacementContract,
  scale: number,
): AlignmentPlacementContract {
  return {
    ...placement,
    offsetX: placement.offsetX * scale,
    offsetY: placement.offsetY * scale,
  };
}

function placeChild(
  parent: RenderableBox,
  childSize: { width: number; height: number },
  placement: AlignmentPlacementContract,
): RenderableBox {
  return {
    x: placeAxis(parent.x, parent.width, childSize.width, placement.alignX, placement.offsetX, placement.mode),
    y: placeAxis(parent.y, parent.height, childSize.height, placement.alignY, placement.offsetY, placement.mode),
    width: childSize.width,
    height: childSize.height,
  };
}

function placeAxis(
  parentStart: number,
  parentSize: number,
  childSize: number,
  align: number,
  offset: number,
  mode: "center" | "edge",
) {
  const clamped = Math.max(0, Math.min(1, align));
  if (mode === "center") {
    return parentStart + parentSize * clamped - childSize / 2 + offset;
  }

  const center = parentStart + parentSize / 2 - childSize / 2;
  if (clamped <= 0.5) {
    const outsideStart = parentStart - childSize;
    return lerp(outsideStart, center, clamped / 0.5) + offset;
  }

  const outsideEnd = parentStart + parentSize;
  return lerp(center, outsideEnd, (clamped - 0.5) / 0.5) + offset;
}

function lerp(start: number, end: number, amount: number) {
  return start + (end - start) * amount;
}

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

function unionBoxes(boxes: RenderableBox[]): RenderableBox {
  const minX = Math.min(...boxes.map((box) => box.x));
  const minY = Math.min(...boxes.map((box) => box.y));
  const maxX = Math.max(...boxes.map((box) => box.x + box.width));
  const maxY = Math.max(...boxes.map((box) => box.y + box.height));
  return {
    x: minX,
    y: minY,
    width: maxX - minX,
    height: maxY - minY,
  };
}

function expandBox(box: RenderableBox, padding: number): RenderableBox {
  return {
    x: box.x - padding,
    y: box.y - padding,
    width: box.width + padding * 2,
    height: box.height + padding * 2,
  };
}

function translateBox(box: RenderableBox, origin: { x: number; y: number }): RenderableBox {
  return {
    x: box.x + origin.x,
    y: box.y + origin.y,
    width: box.width,
    height: box.height,
  };
}

function sampleAvatarUri() {
  const svg = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
  <defs>
    <linearGradient id="bg" x1="40" y1="24" x2="216" y2="232" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#DCE6F3"/>
      <stop offset="0.52" stop-color="#AEBBD0"/>
      <stop offset="1" stop-color="#63738E"/>
    </linearGradient>
    <linearGradient id="skin" x1="86" y1="54" x2="170" y2="152" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#F4C8AA"/>
      <stop offset="1" stop-color="#C98970"/>
    </linearGradient>
  </defs>
  <rect width="256" height="256" fill="url(#bg)"/>
  <circle cx="128" cy="99" r="50" fill="url(#skin)"/>
  <path d="M46 246c10-58 45-86 82-86s72 28 82 86H46z" fill="#26354F"/>
  <path d="M78 92c9-40 35-60 63-52 24 7 39 27 41 54-24-10-47-23-64-44-8 21-20 35-40 42z" fill="#3A2B26"/>
  <circle cx="109" cy="104" r="5" fill="#332A2A"/>
  <circle cx="148" cy="104" r="5" fill="#332A2A"/>
  <path d="M112 128c12 10 25 10 37 0" fill="none" stroke="#7E4D43" stroke-width="6" stroke-linecap="round"/>
</svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

export function statusBarToRenderable(
  payload: DesignPreviewPayload,
  statusBar: StatusBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const tokens = systemBarTokens(payload, "statusBar");
  const layout = {
    height: statusBar.layout.height * scale,
    itemSize: statusBar.layout.itemSize * scale,
    gap: statusBar.layout.gap * scale,
    sidePadding: statusBar.layout.sidePadding * scale,
  };
  const viewport = designViewport(payload);
  const statusBarHeight = layout.height;
  return {
    id: "status_bar",
    type: "status_bar",
    role: "device_status",
    frame: 0,
    box: {
      x: viewport.x,
      y: viewport.y,
      width: viewport.width,
      height: statusBarHeight,
    },
    style: {
      foreground: tokens.foreground,
      background: tokens.background,
      fontSize: layout.itemSize,
      lineHeight: layout.itemSize,
      gap: layout.gap,
      paddingX: layout.sidePadding,
    },
    children: boxedStatusItems(payload, statusBar, layout, statusBarHeight),
    metadata: {
      route: "system-bar-resolver.web-bridge",
      systemBarType: "statusBar",
    },
  };
}

export function navigationBarToRenderable(
  payload: DesignPreviewPayload,
  navigationBar: NavigationBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const tokens = systemBarTokens(payload, "navigationBar");
  const layout = {
    height: navigationBar.layout.height * scale,
    itemSize: navigationBar.layout.itemSize * scale,
    sidePadding: navigationBar.layout.sidePadding * scale,
    strokeWidth: navigationBar.layout.strokeWidth * scale,
    cornerRadius: navigationBar.layout.cornerRadius * scale,
    filled: navigationBar.layout.filled,
  };
  const gesture = {
    width: navigationBar.gesture.width * scale,
    height: navigationBar.gesture.height * scale,
    cornerRadius: navigationBar.gesture.cornerRadius * scale,
  };
  const viewport = designViewport(payload);
  const box = {
    x: viewport.x,
    y: viewport.y + viewport.height - layout.height,
    width: viewport.width,
    height: layout.height,
  };
  if (navigationBar.type === "gestureBar") {
    return {
      id: "navigation_bar",
      type: "navigation_bar",
      role: "device_navigation",
      frame: 0,
      box,
      style: {
        background: tokens.background,
      },
      children: [
        {
          id: "navigation_bar:gesture",
          type: "navigation_bar_gesture",
          role: "gesture_bar",
          frame: 0,
          box: {
            x: viewport.x + (viewport.width - gesture.width) / 2,
            y: box.y + (layout.height - gesture.height) / 2,
            width: gesture.width,
            height: gesture.height,
          },
          style: {
            background: tokens.foreground,
            cornerRadius: gesture.cornerRadius,
          },
        },
      ],
      metadata: {
        route: "system-bar-resolver.web-bridge",
        systemBarType: "navigationBar",
      },
    };
  }

  return {
    id: "navigation_bar",
    type: "navigation_bar",
    role: "device_navigation",
    frame: 0,
    box,
    style: {
      foreground: tokens.foreground,
      background: tokens.background,
      fontSize: layout.itemSize,
      lineHeight: layout.itemSize,
      paddingX: layout.sidePadding,
    },
    children: [
      navigationZoneNode(navigationBar, layout, tokens.foreground, "left"),
      navigationZoneNode(navigationBar, layout, tokens.foreground, "center"),
      navigationZoneNode(navigationBar, layout, tokens.foreground, "right"),
    ],
    metadata: {
      route: "system-bar-resolver.web-bridge",
      systemBarType: "navigationBar",
    },
  };
}

export function iconUriForToken(payload: DesignPreviewPayload, token: string) {
  const mapping = parseObject(payload.iconMappingJson ?? "{}");
  const tokens = asRecord(mapping.tokens);
  const iconToken = asRecord(tokens[token]);
  const file = typeof iconToken.file === "string" ? iconToken.file : "";
  const assetRoot = payload.iconAssetRoot?.replace(/\/+$/g, "") ?? "";
  if (!file || !assetRoot) return "";

  const candidates = [
    path.resolve(payload.projectMediaRoot ?? "", assetRoot, file),
    path.resolve("assets/FOQN_S2", assetRoot, file),
    path.resolve("assets", assetRoot, file),
    path.resolve(assetRoot, file),
  ];
  const fullPath = candidates.find((candidate) => existsSync(candidate));
  if (!fullPath) return "";

  const svg = readFileSync(fullPath);
  return `data:image/svg+xml;base64,${svg.toString("base64")}`;
}

function designViewport(payload: DesignPreviewPayload) {
  return {
    x: payload.device.screenX,
    y: payload.device.screenY,
    width: payload.device.screenWidth,
    height: payload.device.screenHeight,
    safeArea: { top: 0, right: 0, bottom: 0, left: 0 },
  };
}

function systemBarTokens(
  payload: DesignPreviewPayload,
  key: "statusBar" | "navigationBar",
) {
  const prefix = key === "statusBar" ? "theme.statusBar" : "theme.navigationBar";
  return {
    foreground: selectedColor(payload, `${prefix}.foreground`),
    background: selectedColor(payload, `${prefix}.background`),
  };
}

function statusBarItemForRender(
  payload: DesignPreviewPayload,
  item: SystemBarItemContract,
): Record<string, unknown> {
  const iconUri =
    item.kind === "iconToken" && item.token ? iconUriForToken(payload, item.token) : "";
  return iconUri ? { ...item, iconUri } : { ...item };
}

function boxedStatusItems(
  payload: DesignPreviewPayload,
  statusBar: StatusBarDesignContract,
  layout: { itemSize: number; gap: number; sidePadding: number },
  statusBarHeight: number,
) {
  const { itemSize, gap, sidePadding } = layout;
  const foreground = systemBarTokens(payload, "statusBar").foreground;
  const y = payload.device.screenY + (statusBarHeight - itemSize) / 2;

  return (["left", "right"] as const).flatMap((zone) => {
    const zoneItems = statusBar.zones[zone].map((item) => statusBarItemForRender(payload, item));
    const widths = zoneItems.map((item) => statusItemWidth(item, itemSize));
    const totalWidth = widths.reduce((sum, width) => sum + width, 0)
      + Math.max(0, widths.length - 1) * gap;
    let x = zone === "left"
      ? payload.device.screenX + sidePadding
      : payload.device.screenX + payload.device.screenWidth - sidePadding - totalWidth;

    return zoneItems.map((item, index) => {
      const width = widths[index] ?? itemSize;
      const kind = stringValue(item.kind, "text");
      const id = stringValue(item.id, stringValue(item.label, `item_${index}`));
      const iconUri = stringValue(item.iconUri);
      const node = {
        id: `status_bar:${zone}:${id}`,
        type: "status_bar_item",
        role: kind,
        frame: 0,
        text: kind === "text"
          ? stringValue(item.value)
          : stringValue(item.token, stringValue(item.label)),
        box: { x, y, width, height: itemSize },
        style: {
          color: foreground,
          fontSize: itemSize,
          lineHeight: itemSize,
          ...(iconUri
            ? {
                maskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
                WebkitMaskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
              }
            : {}),
        },
        metadata: { ...item },
      };
      x += width + gap;
      return node;
    });
  });
}

function navigationZoneNode(
  navigationBar: NavigationBarDesignContract,
  layout: {
    itemSize: number;
    strokeWidth: number;
    cornerRadius: number;
    filled: boolean;
  },
  foreground: string,
  zone: "left" | "center" | "right",
): RenderableNode {
  return {
    id: `navigation_bar:${zone}`,
    type: "navigation_bar_zone",
    role: zone,
    frame: 0,
    style: {
      color: foreground,
      itemSize: layout.itemSize,
      justifyContent:
        zone === "left"
          ? "flex-start"
          : zone === "right"
            ? "flex-end"
            : "center",
    },
    children: navigationBar.zones[zone].map((item) => ({
      id: `navigation_bar:${zone}:${item.id || item.label || "item"}`,
      type: "navigation_bar_item",
      role: item.kind || "generatedHome",
      frame: 0,
      style: {
        color: foreground,
        fontSize: layout.itemSize,
        lineHeight: layout.itemSize,
      },
      metadata: {
        ...item,
        filled: layout.filled,
        strokeWidth: layout.strokeWidth,
        cornerRadius: layout.cornerRadius,
      },
    })),
  };
}

function statusItemWidth(item: Record<string, unknown>, itemSize: number) {
  const kind = stringValue(item.kind, "text");
  if (kind === "generatedBattery") return itemSize * 1.55;
  if (kind === "generatedSignal") return itemSize * 1.08;
  if (kind === "iconToken") return itemSize;
  return Math.max(itemSize, stringValue(item.value).length * itemSize * 0.58);
}

function cssColorWithAlpha(color: string, alpha: number) {
  if (color === "transparent") return color;
  const clamped = Math.max(0, Math.min(1, alpha));
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(color.trim());
  if (!match) return color;
  const sourceAlpha = match[2]
    ? Number.parseInt(match[2], 16) / 255
    : 1;
  const resolvedAlpha = Math.max(0, Math.min(1, clamped * sourceAlpha));
  const hex = match[1];
  if (resolvedAlpha >= 1) return `#${hex}`;
  const red = Number.parseInt(hex.slice(0, 2), 16);
  const green = Number.parseInt(hex.slice(2, 4), 16);
  const blue = Number.parseInt(hex.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${formatAlpha(resolvedAlpha)})`;
}

function formatAlpha(value: number) {
  return Number(value.toFixed(3)).toString();
}

function applyNeutralTint(payload: DesignPreviewPayload, color: string) {
  const root = parseObject(payload.themeTokensJson);
  const tint = asRecord(root.neutralTint);
  const hueDeg = numberValue(tint.hueDeg, 0);
  const saturation = Math.max(0, Math.min(1, numberValue(tint.saturation, 0)));
  if (saturation <= 0) return color;
  const parsed = parseHex(color);
  if (!parsed) return color;
  const [, , lightness] = rgbToHsl(parsed.red / 255, parsed.green / 255, parsed.blue / 255);
  const [red, green, blue] = hslToRgb(normalizeHue(hueDeg) / 360, saturation, lightness);
  const rgb = `#${byteHex(red * 255)}${byteHex(green * 255)}${byteHex(blue * 255)}`;
  return parsed.alpha === 255 ? rgb : `${rgb}${byteHex(parsed.alpha)}`;
}

function parseHex(color: string) {
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(color.trim());
  if (!match) return null;
  const hex = match[1];
  return {
    red: Number.parseInt(hex.slice(0, 2), 16),
    green: Number.parseInt(hex.slice(2, 4), 16),
    blue: Number.parseInt(hex.slice(4, 6), 16),
    alpha: match[2] ? Number.parseInt(match[2], 16) : 255,
  };
}

function byteHex(value: number) {
  return Math.max(0, Math.min(255, Math.round(value)))
    .toString(16)
    .padStart(2, "0")
    .toUpperCase();
}

function normalizeHue(value: number) {
  const normalized = value % 360;
  return normalized < 0 ? normalized + 360 : normalized;
}

function rgbToHsl(red: number, green: number, blue: number) {
  const max = Math.max(red, green, blue);
  const min = Math.min(red, green, blue);
  const lightness = (max + min) / 2;
  if (Math.abs(max - min) < 0.000001) return [0, 0, lightness] as const;
  const delta = max - min;
  const saturation =
    lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
  const hue =
    max === red
      ? (green - blue) / delta + (green < blue ? 6 : 0)
      : max === green
        ? (blue - red) / delta + 2
        : (red - green) / delta + 4;
  return [hue / 6, saturation, lightness] as const;
}

function hslToRgb(hue: number, saturation: number, lightness: number) {
  if (saturation <= 0) return [lightness, lightness, lightness] as const;
  const q =
    lightness < 0.5
      ? lightness * (1 + saturation)
      : lightness + saturation - lightness * saturation;
  const p = 2 * lightness - q;
  return [
    hueToRgb(p, q, hue + 1 / 3),
    hueToRgb(p, q, hue),
    hueToRgb(p, q, hue - 1 / 3),
  ] as const;
}

function hueToRgb(p: number, q: number, t: number) {
  let value = t;
  if (value < 0) value += 1;
  if (value > 1) value -= 1;
  if (value < 1 / 6) return p + (q - p) * 6 * value;
  if (value < 1 / 2) return q;
  if (value < 2 / 3) return p + (q - p) * (2 / 3 - value) * 6;
  return p;
}
