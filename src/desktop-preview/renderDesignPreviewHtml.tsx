import { readFile } from "node:fs/promises";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { AvatarModule } from "../visual/modules/atomic/AvatarModule.js";
import { KeyboardModule } from "../visual/modules/atomic/KeyboardModule.js";
import { TextInputBarModule } from "../visual/modules/atomic/TextInputBarModule.js";
import { RenderableReactAdapter } from "../visual/adapters/react/RenderableReactAdapter.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveLabelComponent } from "./labelComponentResolver.js";
import {
  iconUriForToken,
  labelComponentToRenderable,
  navigationBarToRenderable,
  statusBarToRenderable,
} from "./webPreviewBridge.js";
import {
  resolveNavigationBar,
  resolveStatusBar,
} from "./systemBarPreviewResolver.js";

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function readNumber(
  value: Record<string, unknown>,
  key: string,
  fallback: number,
) {
  const raw = value[key];
  return typeof raw === "number" && Number.isFinite(raw) ? raw : fallback;
}

function readString(
  value: Record<string, unknown>,
  key: string,
  fallback = "",
) {
  const raw = value[key];
  return typeof raw === "string" ? raw : fallback;
}

function readPair(
  value: unknown,
  fallbackFirst: number,
  fallbackSecond: number,
) {
  const parts = String(value ?? "").split("|");
  const first = Number(parts[0]);
  const second = Number(parts[1]);
  return {
    first: Number.isFinite(first) ? first : fallbackFirst,
    second: Number.isFinite(second) ? second : fallbackSecond,
  };
}

function requiredNumber(value: Record<string, unknown>, key: string, path: string) {
  const raw = value[key];
  if (typeof raw === "number" && Number.isFinite(raw)) return raw;
  throw new Error(`Missing numeric theme value ${path}`);
}

function requiredAlpha(value: Record<string, unknown>, key: string, path: string) {
  return Math.max(0, Math.min(1, requiredNumber(value, key, path)));
}

function colorWithAlpha(color: string, alpha: number) {
  const clamped = Math.max(0, Math.min(1, alpha));
  if (clamped >= 1 || color === "transparent") return color;
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(color.trim());
  if (!match) return color;
  const hex = match[1];
  const sourceAlpha = match[2]
    ? Number.parseInt(match[2], 16) / 255
    : 1;
  const resolvedAlpha = Math.max(0, Math.min(1, clamped * sourceAlpha));
  const red = Number.parseInt(hex.slice(0, 2), 16);
  const green = Number.parseInt(hex.slice(2, 4), 16);
  const blue = Number.parseInt(hex.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${resolvedAlpha})`;
}

function renderScale(payload: DesignPreviewPayload) {
  const scale = payload.device.scaleToPixels;
  return typeof scale === "number" && Number.isFinite(scale) && scale > 0
    ? scale
    : 1;
}

function resolvePaletteValue(payload: DesignPreviewPayload, value: unknown) {
  if (typeof value !== "string") return value;
  if (/^#|^rgb|^hsl|^transparent$/i.test(value)) return value;
  return payload.paletteColors?.[value] ?? value;
}

function resolvePaletteObject(
  payload: DesignPreviewPayload,
  value: Record<string, unknown>,
): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(value).map(([key, item]) => [
      key,
      typeof item === "object" && item !== null && !Array.isArray(item)
        ? resolvePaletteObject(payload, asRecord(item))
        : resolvePaletteValue(payload, item),
    ]),
  );
}

function safeAreaViewport(box: {
  x: number;
  y: number;
  width: number;
  height: number;
}) {
  return {
    ...box,
    safeArea: { top: 0, right: 0, bottom: 0, left: 0 },
  };
}

function parseObject(json: string, fallback: Record<string, unknown> = {}) {
  try {
    return asRecord(JSON.parse(json || "{}"));
  } catch {
    return fallback;
  }
}

function modeTokens(payload: DesignPreviewPayload) {
  const tokens = parseObject(payload.themeTokensJson);
  const modes = asRecord(tokens.modes);
  return resolvePaletteObject(payload, asRecord(modes[payload.themeMode]));
}

function themeBackground(payload: DesignPreviewPayload) {
  const colors = asRecord(modeTokens(payload).colors);
  const value = resolvePaletteValue(payload, colors.background);
  return typeof value === "string" && value.trim()
    ? value
    : payload.themeMode === "dark"
      ? "#101827"
      : "#F7F9FC";
}

function themeTokenValue(payload: DesignPreviewPayload, token: unknown) {
  if (typeof token !== "string" || !token.startsWith("theme.")) return token;
  const parts = token.replace(/^theme\./, "").split(".");
  const sources = [modeTokens(payload), parseObject(payload.themeTokensJson)];
  for (const source of sources) {
    let value: unknown = source;
    for (const part of parts) {
      value = asRecord(value)[part];
    }
    if (value !== undefined) {
      return resolvePaletteValue(payload, value);
    }
  }

  return undefined;
}

function themeTokenNumber(
  payload: DesignPreviewPayload,
  token: unknown,
  fallback: number,
) {
  const value = themeTokenValue(payload, token);
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function themeTokenColor(
  payload: DesignPreviewPayload,
  token: unknown,
  fallback: string,
) {
  const value = themeTokenValue(payload, token);
  return typeof value === "string" && value.trim() ? value : fallback;
}

function componentSurfaceStyle(
  payload: DesignPreviewPayload,
  config: Record<string, unknown>,
) {
  const style = asRecord(config.style);
  const borderWidth = readNumber(style, "borderWidth", 0) * renderScale(payload);
  const borderColor = themeTokenColor(
    payload,
    style.borderColorToken,
    "transparent",
  );
  const borderRadius =
    themeTokenNumber(payload, style.cornerRadiusToken, 0) * renderScale(payload);
  const reliefEnabled = style.reliefEnabled === true;
  return {
    shadow: style.shadowEnabled === true ? themeShadow(payload) : undefined,
    surfaceRelief: reliefEnabled
      ? {
          angleDeg: readNumber(style, "reliefAngle", -45),
          extension: readNumber(style, "reliefExtent", 1) * renderScale(payload),
          spread: readNumber(style, "reliefSpread", 0) * renderScale(payload),
          upperIntensity: readNumber(style, "reliefTopIntensity", 0.12),
          lowerIntensity: readNumber(style, "reliefBottomIntensity", -0.1),
        }
      : undefined,
    borderWidth,
    borderColor,
    borderRadius,
  };
}

function themeShadow(payload: DesignPreviewPayload) {
  const tokens = parseObject(payload.themeTokensJson);
  const shadow = asRecord(asRecord(tokens.shadows).default);
  const color = asRecord(shadow.color);
  const colorToken = color.color;
  const resolvedColor = resolvePaletteValue(payload, colorToken);
  if (typeof resolvedColor !== "string" || !resolvedColor.trim()) {
    throw new Error("Missing theme.shadows.default.color.color");
  }

  return {
    offsetX:
      requiredNumber(shadow, "offsetX", "theme.shadows.default.offsetX") *
      renderScale(payload),
    offsetY:
      requiredNumber(shadow, "offsetY", "theme.shadows.default.offsetY") *
      renderScale(payload),
    blur:
      requiredNumber(shadow, "blur", "theme.shadows.default.blur") *
      renderScale(payload),
    color: colorWithAlpha(
      resolvedColor,
      requiredAlpha(color, "alpha", "theme.shadows.default.color.alpha"),
    ),
  };
}

function centerBox(
  payload: DesignPreviewPayload,
  width: number,
  height: number,
  yBias = 0,
) {
  const { device } = payload;
  return {
    x: device.screenX + (device.screenWidth - width) / 2,
    y: device.screenY + (device.screenHeight - height) / 2 + yBias,
    width,
    height,
  };
}

function genericAvatarUri() {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 640"><rect width="640" height="640" fill="#9fb7d7"/><circle cx="320" cy="250" r="118" fill="#f4d1b5"/><path d="M123 640c28-138 120-214 197-214s169 76 197 214z" fill="#31445f"/><path d="M190 230c20-105 89-164 168-144 62 16 104 72 101 157-76-53-155-63-269-13z" fill="#3b2b22"/></svg>`;
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}`;
}

function iconUriListForSlots(payload: DesignPreviewPayload, slots: unknown) {
  const record = asRecord(slots);
  return ["left", "center", "right"]
    .flatMap((zone) => (Array.isArray(record[zone]) ? record[zone] : []))
    .filter((item): item is string => typeof item === "string")
    .map((token) => ({ token, uri: iconUriForToken(payload, token) }));
}

function componentRenderableForPayload(
  payload: DesignPreviewPayload,
): RenderableNode {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson ?? "{}");
  const componentType =
    payload.componentType || readString(preview, "componentType", "component");
  if (componentType === "label") {
    return labelComponentToRenderable(payload, resolveLabelComponent(payload));
  }

  const scale = renderScale(payload);
  const surface = componentSurfaceStyle(payload, config);
  const tokens = modeTokens(payload);
  const sampleText = readString(preview, "sampleText", "Sample");

  if (componentType === "avatar") {
    const avatar = asRecord(config.avatar);
    const size = readNumber(avatar, "defaultSize", 48) * scale;
    const node = AvatarModule.render({
      id: "design:avatar",
      uri: genericAvatarUri(),
      size,
      label: "Avatar",
      frame: 0,
      cornerRadius:
        themeTokenNumber(
          payload,
          avatar.cornerRadiusToken ?? asRecord(config.style).cornerRadiusToken,
          999,
        ) * scale,
      borderWidth: surface.borderWidth,
      borderColor: surface.borderColor,
      shadow: surface.shadow,
      surfaceRelief: surface.surfaceRelief,
    });
    return { ...node, box: centerBox(payload, size, size) };
  }

  if (componentType === "textInputBar") {
    const textInput = asRecord(config.textInput);
    const height = readNumber(textInput, "height", 44) * scale;
    const width = Math.min(payload.device.screenWidth * 0.86, 360 * scale);
    const box = centerBox(payload, width, height);
    return TextInputBarModule.render({
      frame: 0,
      viewport: safeAreaViewport(box),
      tokens: tokens as never,
      textInputBar: {
        layout: {
          height,
          paddingX: 0,
          paddingY: 0,
          fieldHeight: height,
          fieldPaddingX: 14 * scale,
          fieldPaddingY: 6 * scale,
          fieldRadius: Math.min(height / 2, 22 * scale),
          iconSize: 20 * scale,
          fontSize: 17 * scale,
          lineHeight: 22 * scale,
        },
        text: sampleText,
        cursorWidth: readNumber(textInput, "cursorWidth", 2) * scale,
        cursorBlinkFrames: readNumber(textInput, "cursorBlinkFrames", 18),
        idleTextColor: themeTokenColor(
          payload,
          textInput.idleTextColorToken,
          "#6B7280",
        ),
        cursorColor: themeTokenColor(
          payload,
          textInput.cursorColorToken,
          "#007AFF",
        ),
        leftItems: [],
        rightItems: [],
      } as never,
    });
  }

  if (componentType === "keyboard") {
    const keyboard = asRecord(config.keyboard);
    const height = Math.min(payload.device.screenHeight * 0.42, 300 * scale);
    const keyHeight = Math.max(28 * scale, (height - 56 * scale) / 4.6);
    return KeyboardModule.render({
      frame: 0,
      viewport: {
        x: payload.device.screenX,
        y: payload.device.screenY,
        width: payload.device.screenWidth,
        height: payload.device.screenHeight,
        safeArea: { top: 0, right: 0, bottom: 0, left: 0 },
      },
      tokens: tokens as never,
      keyboard: {
        layout: {
          height,
          topPadding: 8 * scale,
          sidePadding: 6 * scale,
          bottomPadding: 8 * scale,
          bottomUtilityHeight: 42 * scale,
          rowGap: 8 * scale,
          keyGap: 6 * scale,
          keyHeight,
          keyPadding: readNumber(keyboard, "keyPadding", 4) * scale,
          keyRadius: readNumber(keyboard, "keyCornerRadius", 6) * scale,
          fontSize: 18 * scale,
          bottomIconSize: 22 * scale,
        },
        rows: [
          ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p"].map((label) => ({
            id: label,
            label,
          })),
          ["a", "s", "d", "f", "g", "h", "j", "k", "l"].map((label) => ({
            id: label,
            label,
          })),
          ["shift", "z", "x", "c", "v", "b", "n", "m", "backspace"].map(
            (label) => ({ id: label, label }),
          ),
        ],
        bottomItems: iconUriListForSlots(payload, keyboard.bottomIconSlots).map(
          (item, index) => ({
            id: item.token,
            token: item.token,
            iconUri: item.uri,
            order: index,
            zone: index % 2 === 0 ? "left" : "right",
          }),
        ),
        keyShadowEnabled: keyboard.keyShadowEnabled,
        pressedEffect: keyboard.pressedEffect,
      } as never,
    });
  }

  if (componentType === "buttonIcon") {
    const buttonIcon = asRecord(config.buttonIcon);
    const iconSize = 28 * scale;
    const padding = readNumber(buttonIcon, "iconPadding", 6) * scale;
    const size = iconSize + padding * 2;
    return {
      id: "design:buttonIcon",
      type: "component_button_icon_preview",
      frame: 0,
      box: centerBox(payload, size, size),
      text: "✦",
      style: {
        fontSize: iconSize,
        lineHeight: size,
        textAlign: "center",
        textColor: themeTokenColor(payload, "theme.icons.primary", "#111827"),
        backgroundColor: "transparent",
        ...surface,
      },
    };
  }

  if (componentType === "audio") {
    const audio = asRecord(config.audio);
    const pair = String(audio.size ?? "230|54").split("|");
    const width = (Number(pair[0]) || 230) * scale;
    const height = (Number(pair[1]) || 54) * scale;
    const box = centerBox(payload, width, height);
    const playColor = themeTokenColor(payload, audio.playColorToken, "#007AFF");
    const waveformColor = themeTokenColor(
      payload,
      audio.waveformColorToken,
      "#111827",
    );
    return {
      id: "design:audio",
      type: "component_audio_preview",
      frame: 0,
      box,
      style: {
        backgroundColor: themeTokenColor(payload, "theme.colors.surface", "#FFFFFF"),
        ...surface,
      },
      children: [
        {
          id: "design:audio:play",
          type: "message_bubble_audio_play",
          frame: 0,
          box: {
            x: box.x + 12 * scale,
            y: box.y + (height - 28 * scale) / 2,
            width: 28 * scale,
            height: 28 * scale,
          },
          text: "▶",
          style: {
            backgroundColor: playColor,
            textColor: "#FFFFFF",
            borderRadius: 999,
            fontSize: 13 * scale,
            lineHeight: 28 * scale,
            textAlign: "center",
          },
        },
        {
          id: "design:audio:waveform",
          type: "text",
          frame: 0,
          box: {
            x: box.x + 52 * scale,
            y: box.y,
            width: width - 100 * scale,
            height,
          },
          text: "▂▆▃▇▄▅▂▆▇▃▅",
          style: {
            textColor: waveformColor,
            fontSize: 20 * scale,
            lineHeight: height,
          },
        },
        {
          id: "design:audio:duration",
          type: "text",
          frame: 0,
          box: {
            x: box.x + width - 48 * scale,
            y: box.y,
            width: 42 * scale,
            height,
          },
          text: sampleText,
          style: {
            textColor: themeTokenColor(payload, "theme.colors.textPrimary", "#111827"),
            fontSize: readNumber(audio, "textSize", 13) * scale,
            lineHeight: height,
          },
        },
      ],
    };
  }

  if (componentType === "video") {
    const width = Math.min(payload.device.screenWidth * 0.72, 260 * scale);
    const height = width * 0.62;
    const box = centerBox(payload, width, height);
    const video = asRecord(config.video);
    const playColor = themeTokenColor(payload, video.playColorToken, "#007AFF");
    return {
      id: "design:video",
      type: "component_video_preview",
      frame: 0,
      box,
      style: {
        backgroundColor: "#8796A8",
        backgroundImage:
          "linear-gradient(135deg, rgba(255,255,255,.25), rgba(0,0,0,.18))",
        overflow: "hidden",
        ...surface,
      },
      children: [
        {
          id: "design:video:play",
          type: "message_bubble_video_play_overlay",
          frame: 0,
          box: {
            x: box.x + width / 2 - 24 * scale,
            y: box.y + height / 2 - 24 * scale,
            width: 48 * scale,
            height: 48 * scale,
          },
          text: "▶",
          style: {
            backgroundColor: playColor,
            textColor: "#FFFFFF",
            borderRadius: 999,
            fontSize: 22 * scale,
            lineHeight: 48 * scale,
            textAlign: "center",
          },
        },
      ],
    };
  }

  const box = centerBox(payload, 180 * scale, 96 * scale);
  return {
    id: "design:component",
    type: "component_preview",
    frame: 0,
    box,
    text: componentType,
    style: {
      backgroundColor: themeTokenColor(payload, "theme.colors.surface", "#FFFFFF"),
      textColor: themeTokenColor(payload, "theme.colors.textPrimary", "#111827"),
      fontSize: 16 * scale,
      lineHeight: box.height,
      textAlign: "center",
      ...surface,
    },
  };
}

function renderableForPayload(payload: DesignPreviewPayload): RenderableNode {
  if (payload.kind === "componentClass") {
    const component = componentRenderableForPayload(payload);
    return RenderableNodeSchema.parse({
      id: "design_preview_surface",
      type: "design_preview_surface",
      frame: 0,
      box: {
        x: 0,
        y: 0,
        width: payload.device.canvasWidth,
        height: payload.device.canvasHeight,
      },
      style: {
        backgroundColor: themeBackground(payload),
      },
      children: [component],
    });
  }

  const child = payload.kind === "statusBar"
    ? statusBarToRenderable(payload, resolveStatusBar(payload))
    : navigationBarToRenderable(payload, resolveNavigationBar(payload));

  return RenderableNodeSchema.parse({
    id: "design_preview_surface",
    type: "design_preview_surface",
    frame: 0,
    box: {
      x: 0,
      y: 0,
      width: payload.device.canvasWidth,
      height: payload.device.canvasHeight,
    },
    style: {
      backgroundColor: themeBackground(payload),
    },
    children: [child],
  });
}

async function main() {
  const inputPath = process.argv[2];
  if (!inputPath) {
    throw new Error("Missing design preview payload path.");
  }

  const payload = JSON.parse(
    await readFile(inputPath, "utf8"),
  ) as DesignPreviewPayload;
  const renderable = renderableForPayload(payload);
  const markup = renderToStaticMarkup(
    React.createElement(RenderableReactAdapter, {
      tree: renderable,
      showBounds: payload.showMarks === true,
    }),
  );
  process.stdout.write(markup);
}

await main();
