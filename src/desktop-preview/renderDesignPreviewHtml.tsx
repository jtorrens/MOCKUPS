import { existsSync, readFileSync } from "node:fs";
import { readFile } from "node:fs/promises";
import path from "node:path";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { AvatarModule } from "../visual/modules/atomic/AvatarModule.js";
import { KeyboardModule } from "../visual/modules/atomic/KeyboardModule.js";
import { NavigationBarModule } from "../visual/modules/atomic/NavigationBarModule.js";
import { StatusBarModule } from "../visual/modules/atomic/StatusBarModule.js";
import { TextInputBarModule } from "../visual/modules/atomic/TextInputBarModule.js";
import { RenderableReactAdapter } from "../visual/adapters/react/RenderableReactAdapter.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";

interface DevicePayload {
  canvasWidth: number;
  canvasHeight: number;
  screenX: number;
  screenY: number;
  screenWidth: number;
  screenHeight: number;
  statusBarHeight?: number;
  safeAreaBottom?: number;
  scaleToPixels?: number;
}

interface DesignPreviewPayload {
  kind: "statusBar" | "navigationBar" | "componentClass";
  componentType?: string;
  configJson: string;
  designPreviewJson?: string;
  device: DevicePayload;
  iconAssetRoot?: string;
  iconMappingJson?: string;
  paletteColors?: Record<string, string>;
  projectMediaRoot?: string;
  themeMode: "light" | "dark";
  themeTokensJson: string;
}

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

function scaleLayout(
  layout: Record<string, unknown>,
  scale: number,
  keys: string[],
) {
  return {
    ...layout,
    ...Object.fromEntries(
      keys
        .filter((key) => typeof layout[key] === "number")
        .map((key) => [key, (layout[key] as number) * scale]),
    ),
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
  let value: unknown = modeTokens(payload);
  for (const part of parts) {
    value = asRecord(value)[part];
  }
  return resolvePaletteValue(payload, value);
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
    shadow:
      style.shadowEnabled === true
        ? {
            offsetX: 0,
            offsetY: 4 * renderScale(payload),
            blur: 16 * renderScale(payload),
            color: "rgba(0,0,0,0.22)",
          }
        : undefined,
    surfaceRelief: reliefEnabled
      ? {
          angleDeg: readNumber(style, "reliefAngle", -45),
          extension: readNumber(style, "reliefExtent", 1) * renderScale(payload),
          spread: readNumber(style, "reliefSpread", 0) * renderScale(payload),
          upperIntensity: readNumber(style, "reliefTopIntensity", 12) / 100,
          lowerIntensity:
            readNumber(style, "reliefBottomIntensity", -10) / 100,
        }
      : undefined,
    borderWidth,
    borderColor,
    borderRadius,
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

  if (componentType === "label") {
    const label = asRecord(config.label);
    const pair = String(label.size ?? "120|32").split("|");
    const width = (Number(pair[0]) || 120) * scale;
    const height = (Number(pair[1]) || 32) * scale;
    return {
      id: "design:label",
      type: "component_label_preview",
      frame: 0,
      box: centerBox(payload, width, height),
      text: sampleText,
      style: {
        backgroundColor:
          label.backgroundVisible === false
            ? "transparent"
            : themeTokenColor(payload, label.backgroundColorToken, "#FFFFFF"),
        textColor: themeTokenColor(payload, label.textColorToken, "#111827"),
        fontSize: readNumber(label, "textSize", 12) * scale,
        lineHeight: height,
        textAlign: "center",
        fontStyle: readString(label, "textStyle", "normal"),
        ...surface,
      },
    };
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

function statusBarTokens(payload: DesignPreviewPayload) {
  const statusBar = asRecord(modeTokens(payload).statusBar);
  return {
    foreground:
      typeof statusBar.foreground === "string" ? statusBar.foreground : "#111827",
    background:
      typeof statusBar.background === "object" && statusBar.background !== null
        ? (asRecord(statusBar.background).color as string | undefined) ?? "transparent"
        : typeof statusBar.background === "string"
          ? statusBar.background
          : "transparent",
  };
}

function navigationBarTokens(payload: DesignPreviewPayload) {
  const navigationBar = asRecord(modeTokens(payload).navigationBar);
  return {
    foreground:
      typeof navigationBar.foreground === "string"
        ? navigationBar.foreground
        : "#111827",
    background:
      typeof navigationBar.background === "object" && navigationBar.background !== null
        ? (asRecord(navigationBar.background).color as string | undefined) ?? "transparent"
        : typeof navigationBar.background === "string"
          ? navigationBar.background
          : "transparent",
  };
}

function iconUriForToken(payload: DesignPreviewPayload, token: string) {
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

function resolveStatusBarItems(
  payload: DesignPreviewPayload,
  config: Record<string, unknown>,
) {
  const items = Array.isArray(config.items) ? config.items : [];
  return items.map((item) => {
    const row = asRecord(item);
    const kind = typeof row.kind === "string" ? row.kind : "";
    const token = typeof row.token === "string" ? row.token : "";
    const iconUri = kind === "iconToken" && token ? iconUriForToken(payload, token) : "";
    return iconUri ? { ...row, iconUri } : row;
  });
}

function statusItemWidth(item: Record<string, unknown>, itemSize: number) {
  const kind = readString(item, "kind", "text");
  if (kind === "generatedBattery") return itemSize * 1.55;
  if (kind === "generatedSignal") return itemSize * 1.08;
  if (kind === "iconToken") return itemSize;
  return Math.max(itemSize, readString(item, "value", "").length * itemSize * 0.58);
}

function boxedStatusItems(
  payload: DesignPreviewPayload,
  config: Record<string, unknown>,
  statusBarHeight: number,
) {
  const layout = asRecord(config.layout);
  const itemSize = readNumber(layout, "itemSize", 18);
  const gap = readNumber(layout, "gap", 6);
  const sidePadding = readNumber(layout, "sidePadding", 24);
  const foreground = statusBarTokens(payload).foreground;
  const y = payload.device.screenY + (statusBarHeight - itemSize) / 2;
  const items = resolveStatusBarItems(payload, config)
    .map((item) => asRecord(item))
    .filter((item) => ["left", "right"].includes(readString(item, "zone", "off")))
    .filter((item) => readString(item, "kind", "text") !== "text" || readString(item, "value", "").trim())
    .sort((left, right) => readNumber(left, "order", 0) - readNumber(right, "order", 0));

  return (["left", "right"] as const).flatMap((zone) => {
    const zoneItems = items.filter((item) => readString(item, "zone", "off") === zone);
    const widths = zoneItems.map((item) => statusItemWidth(item, itemSize));
    const totalWidth = widths.reduce((sum, width) => sum + width, 0)
      + Math.max(0, widths.length - 1) * gap;
    let x = zone === "left"
      ? payload.device.screenX + sidePadding
      : payload.device.screenX + payload.device.screenWidth - sidePadding - totalWidth;

    return zoneItems.map((item, index) => {
      const width = widths[index] ?? itemSize;
      const kind = readString(item, "kind", "text");
      const id = readString(item, "id", readString(item, "label", `item_${index}`));
      const iconUri = readString(item, "iconUri", "");
      const node = {
        id: `status_bar:${zone}:${id}`,
        type: "status_bar_item",
        role: kind,
        frame: 0,
        text: kind === "text" ? readString(item, "value", "") : readString(item, "token", readString(item, "label", "")),
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

  const config = parseObject(payload.configJson);
  const viewport = {
    x: payload.device.screenX,
    y: payload.device.screenY,
    width: payload.device.screenWidth,
    height: payload.device.screenHeight,
    safeArea: { top: 0, right: 0, bottom: 0, left: 0 },
  };
  const scale = renderScale(payload);
  const rawLayout = asRecord(config.layout);
  const configForRender =
    payload.kind === "statusBar"
      ? {
          ...config,
          layout: scaleLayout(rawLayout, scale, [
            "height",
            "itemSize",
            "gap",
            "sidePadding",
          ]),
          items: resolveStatusBarItems(payload, config),
        }
      : {
          ...config,
          layout: scaleLayout(rawLayout, scale, [
            "height",
            "itemSize",
            "sidePadding",
            "strokeWidth",
            "cornerRadius",
          ]),
          gesture: scaleLayout(asRecord(config.gesture), scale, [
            "width",
            "height",
            "cornerRadius",
          ]),
        };
  const layout = asRecord(configForRender.layout);
  const statusBarHeight =
    readNumber(
      layout,
      "height",
      typeof payload.device.statusBarHeight === "number" &&
        Number.isFinite(payload.device.statusBarHeight) &&
        payload.device.statusBarHeight > 0
        ? payload.device.statusBarHeight
        : 54,
    );
  const navigationBarHeight = readNumber(layout, "height", 0);
  const child =
    payload.kind === "statusBar"
      ? {
          ...StatusBarModule.render({
            frame: 0,
            viewport,
            statusBarHeight,
            statusBar: configForRender,
            tokens: statusBarTokens(payload),
          }),
          box: {
            x: viewport.x,
            y: viewport.y,
            width: viewport.width,
            height: statusBarHeight,
          },
          children: boxedStatusItems(payload, configForRender, statusBarHeight),
        }
      : {
          ...NavigationBarModule.render({
            frame: 0,
            viewport,
            navigationBar: configForRender,
            tokens: navigationBarTokens(payload),
          }),
          box: {
            x: viewport.x,
            y: viewport.y + viewport.height - navigationBarHeight,
            width: viewport.width,
            height: navigationBarHeight,
          },
        };

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
    React.createElement(RenderableReactAdapter, { tree: renderable }),
  );
  process.stdout.write(markup);
}

await main();
