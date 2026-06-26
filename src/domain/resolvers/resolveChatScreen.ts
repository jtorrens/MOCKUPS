import { z } from "zod";
import {
  parseKeyboardRows,
  STANDARD_IOS_KEYBOARD_LAYOUT,
  type KeyboardMode,
} from "../keyboards/standardKeyboardLayout.js";
import type { DomainRepository } from "../repository/types.js";
import {
  ChatModuleConfigSchema,
  ChatModuleDataSchema,
  ResolvedChatScreenPropsSchema,
  type Actor,
  type App,
  type Device,
  type DeviceState,
  type ModuleInstance,
  type ResolvedChatScreenProps,
  type ScreenInstance,
  type Theme,
} from "../schemas/index.js";
import { requireRecord } from "./helpers.js";
import {
  resolveMessageBubble,
  type ResolvedChatActor,
} from "./resolveMessageBubble.js";

const CHAT_MODULE_ID = "core.chat";
const CHAT_MODULE_SCHEMA_VERSION = 1;

const DeviceMetricsSchema = z.object({
  designSpace: z
    .object({
      width: z.number().positive(),
      height: z.number().positive(),
      unit: z.string().optional(),
    })
    .optional(),
  renderSize: z
    .object({
      width: z.number().positive(),
      height: z.number().positive(),
    })
    .optional(),
  scaleToPixels: z.number().positive().optional(),
  viewport: z.object({
    x: z.number(),
    y: z.number(),
    width: z.number().positive(),
    height: z.number().positive(),
  }),
  safeArea: z.object({
    top: z.number().min(0),
    right: z.number().min(0),
    bottom: z.number().min(0),
    left: z.number().min(0),
  }),
  statusBar: z.object({ height: z.number().min(0) }),
  cornerRadius: z.number().min(0),
  pixelRatio: z.number().positive(),
  defaultScreenScale: z.number().positive(),
});

const DeviceStateValuesSchema = z.object({
  time: z.string().min(1),
  batteryLevel: z.number().min(0).max(1),
  batteryCharging: z.boolean(),
  signalBars: z.number().int().min(0),
  networkLabel: z.string(),
  wifiEnabled: z.boolean(),
  wifiIconState: z.string().min(1),
  orientation: z.enum(["portrait", "landscape"]),
  locked: z.boolean(),
});

const ChatThemeSchema = z.object({
  fonts: z.record(z.string(), z.unknown()),
  colors: z.record(z.string(), z.unknown()),
  wallpaper: z.record(z.string(), z.unknown()).optional(),
  statusBar: z.record(z.string(), z.unknown()),
  navigationBar: z.record(z.string(), z.unknown()),
  keyboard: z.record(z.string(), z.unknown()).optional(),
  layout: z.record(z.string(), z.unknown()),
  header: z.record(z.string(), z.unknown()),
  messages: z.record(z.string(), z.unknown()),
  typography: z.record(z.string(), z.unknown()).optional(),
  chatBubbles: z.record(z.string(), z.unknown()),
  avatars: z.record(z.string(), z.unknown()),
  cursor: z.record(z.string(), z.unknown()),
  shadows: z.record(z.string(), z.unknown()).optional(),
  radii: z.object({ bubble: z.number().min(0) }),
});

const ThemeEnvelopeSchema = z.object({
  defaultMode: z.enum(["light", "dark"]).optional(),
  modes: z
    .object({
      light: z.record(z.string(), z.unknown()).optional(),
      dark: z.record(z.string(), z.unknown()).optional(),
    })
    .optional(),
});

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function mergeTokenObjects(
  base: Record<string, unknown>,
  override: Record<string, unknown>,
): Record<string, unknown> {
  const merged = { ...base };
  for (const [key, value] of Object.entries(override)) {
    merged[key] =
      isObject(merged[key]) && isObject(value)
        ? mergeTokenObjects(merged[key], value)
        : value;
  }
  return merged;
}

function resolveStatusBarDefinition(
  statusBarConfig: Record<string, unknown> | undefined,
  behaviorStatusBar: unknown,
  iconTheme?: {
    asset_root: string;
    mapping_json: Record<string, unknown>;
  },
  scale = 1,
) {
  const rawLayout = isObject(statusBarConfig?.layout)
    ? statusBarConfig.layout
    : {};
  const layout = { ...rawLayout };
  for (const key of ["height", "itemSize", "gap", "sidePadding"]) {
    if (typeof layout[key] === "number") {
      layout[key] = layout[key] * scale;
    }
  }
  const rawItems = Array.isArray(statusBarConfig?.items)
    ? statusBarConfig.items
    : [];
  const behaviorRoot = isObject(behaviorStatusBar) ? behaviorStatusBar : {};
  const behaviorItems = isObject(behaviorRoot.items) ? behaviorRoot.items : {};
  const items: Record<string, unknown>[] = [];
  for (const rawItem of rawItems) {
    if (!isObject(rawItem)) continue;
    const id = stringValue(rawItem.id);
    const label = stringValue(rawItem.label, id);
    const kind = stringValue(rawItem.kind, "iconToken");
    const token = stringValue(rawItem.token);
    const zone = stringValue(rawItem.zone, "off");
    const order = numberValue(rawItem.order, 0);
    if (!id || zone === "off") continue;
    const override = isObject(behaviorItems[id]) ? behaviorItems[id] : {};
    const enabled =
      typeof override.enabled === "boolean" ? override.enabled : true;
    if (!enabled) continue;
    const iconToken =
      token && isObject(iconTheme?.mapping_json.tokens)
        ? iconTheme.mapping_json.tokens[token]
        : undefined;
    const iconFile = isObject(iconToken) ? stringValue(iconToken.file) : "";
    items.push({
      id,
      label,
      kind,
      token,
      zone,
      order,
      value:
        "value" in override
          ? override.value
          : "value" in rawItem
            ? rawItem.value
            : undefined,
      charging:
        typeof override.charging === "boolean"
          ? override.charging
          : rawItem.charging === true,
      ...(kind === "iconToken" && iconFile
        ? {
            iconUri: `${iconTheme?.asset_root.replace(/\/+$/g, "")}/${iconFile}`,
          }
        : {}),
    });
  }
  return {
    layout,
    items,
  };
}

function resolveNavigationBarDefinition(
  navigationBarConfig: Record<string, unknown> | undefined,
  scale = 1,
) {
  const rawLayout = isObject(navigationBarConfig?.layout)
    ? navigationBarConfig.layout
    : {};
  const layout = { ...rawLayout };
  for (const key of [
    "height",
    "itemSize",
    "sidePadding",
    "strokeWidth",
    "cornerRadius",
  ]) {
    if (typeof layout[key] === "number") {
      layout[key] = layout[key] * scale;
    }
  }
  const rawItems = Array.isArray(navigationBarConfig?.items)
    ? navigationBarConfig.items
    : [];
  const items: Record<string, unknown>[] = [];
  for (const rawItem of rawItems) {
    if (!isObject(rawItem)) continue;
    const id = stringValue(rawItem.id);
    const label = stringValue(rawItem.label, id);
    const kind = stringValue(rawItem.kind, "generatedHome");
    const zone = stringValue(rawItem.zone, "off");
    const order = numberValue(rawItem.order, 0);
    if (!id || zone === "off") continue;
    items.push({
      id,
      label,
      kind,
      zone,
      order,
    });
  }
  return {
    layout,
    items,
  };
}

function emojiHash(value: string) {
  let hash = 0;
  for (const character of value) {
    hash = (hash * 31 + (character.codePointAt(0) ?? 0)) >>> 0;
  }
  return hash;
}

function withExtraEmojis(
  rows: ReturnType<typeof parseKeyboardRows>,
  extraEmojis: string[],
) {
  const uniqueExtraEmojis = [...new Set(extraEmojis.map((emoji) => emoji.trim()))];
  const emojiSlots = rows.flatMap((row, rowIndex) =>
    row.flatMap((key, keyIndex) =>
      key.kind === "character" && /\p{Extended_Pictographic}/u.test(key.label)
        ? [{ rowIndex, keyIndex }]
        : [],
    ),
  );
  if (!emojiSlots.length || !uniqueExtraEmojis.length) return rows;
  const seed = emojiHash(uniqueExtraEmojis.join(""));
  const replacementBySlot = new Map<string, string>();
  for (const [extraIndex, emoji] of uniqueExtraEmojis.entries()) {
    const slot = emojiSlots[(seed + extraIndex * 7) % emojiSlots.length];
    replacementBySlot.set(`${slot.rowIndex}:${slot.keyIndex}`, emoji);
  }
  return rows.map((row, rowIndex) =>
    row.map((key, keyIndex) => {
      const emoji = replacementBySlot.get(`${rowIndex}:${keyIndex}`);
      return emoji
        ? {
            ...key,
            id: emoji,
            label: emoji,
          }
        : key;
    }),
  );
}

function keyboardModeForPressedKey(value: string): KeyboardMode {
  if (/\p{Extended_Pictographic}/u.test(value)) return "emoji";
  if (/^[A-ZÁÉÍÓÚÜÑ]$/u.test(value)) return "shift";
  if (/^[0-9]$/u.test(value)) return "numeric";
  if (/^[¿?¡!.,;:'"()[\]{}#%^*+=_/\\|~<>€£¥·-]$/u.test(value)) {
    return "symbols";
  }
  return "lowercase";
}

function resolveKeyboardDefinition(
  behaviorKeyboard: unknown,
  iconTheme?: {
    asset_root: string;
    mapping_json: Record<string, unknown>;
  },
  scale = 1,
  options: { hasTextInputBar?: boolean } = {},
) {
  const behaviorRoot = isObject(behaviorKeyboard) ? behaviorKeyboard : {};
  const pressedKey =
    typeof behaviorRoot.pressedKey === "string"
      ? behaviorRoot.pressedKey
      : undefined;
  const inferredMode = pressedKey
    ? keyboardModeForPressedKey(pressedKey)
    : undefined;
  const requestedMode =
    inferredMode ??
    stringValue(behaviorRoot.mode, STANDARD_IOS_KEYBOARD_LAYOUT.defaultMode);
  const language = stringValue(
    behaviorRoot.language,
    STANDARD_IOS_KEYBOARD_LAYOUT.defaultLanguage,
  );
  const mode = (
    requestedMode in STANDARD_IOS_KEYBOARD_LAYOUT.modes
      ? requestedMode
      : STANDARD_IOS_KEYBOARD_LAYOUT.defaultMode
  ) as KeyboardMode;
  const bottomUtilityHeight = 34;
  const designLayout = {
    topPadding: 8,
    sidePadding: 6,
    bottomPadding: 8,
    bottomUtilityHeight,
    bottomUtilitySidePadding: 24,
    bottomIconSize: 22,
    rowGap: 8,
    keyGap: 6,
    keyHeight: 42,
    keyRadius: 7,
    fontSize: 18,
    emojiFontScale: 0.6,
  };
  const rowCount = STANDARD_IOS_KEYBOARD_LAYOUT.modes[mode]?.rowsText
    .trim()
    .split("\n").length ?? 4;
  const height =
    designLayout.topPadding +
    designLayout.bottomPadding +
    rowCount * designLayout.keyHeight +
    Math.max(0, rowCount - 1) * designLayout.rowGap +
    designLayout.bottomUtilityHeight;
  const layout = Object.fromEntries(
    Object.entries({ height, ...designLayout }).map(([key, value]) => [
      key,
      value * scale,
    ]),
  );
  const rawBottomItems = Array.isArray(behaviorRoot.bottomItems)
    ? behaviorRoot.bottomItems
    : [
        {
          id: "globe",
          label: "Globe",
          kind: "iconToken",
          token: "app_language",
          zone: "left",
          order: 10,
        },
        {
          id: "dictation",
          label: "Dictation",
          kind: "iconToken",
          token: "media_mic",
          zone: "right",
          order: 10,
        },
      ];
  const bottomItems: Record<string, unknown>[] = [];
  for (const rawItem of rawBottomItems) {
    if (!isObject(rawItem)) continue;
    const id = stringValue(rawItem.id);
    const token = stringValue(rawItem.token);
    const zone = stringValue(rawItem.zone, "off");
    if (!id || !token || zone === "off") continue;
    const iconToken = isObject(iconTheme?.mapping_json.tokens)
      ? iconTheme.mapping_json.tokens[token]
      : undefined;
    const iconFile = isObject(iconToken) ? stringValue(iconToken.file) : "";
    bottomItems.push({
      id,
      label: stringValue(rawItem.label, id),
      kind: "iconToken",
      token,
      zone,
      order: numberValue(rawItem.order, 0),
      ...(iconFile
        ? {
            iconUri: `${iconTheme?.asset_root.replace(/\/+$/g, "")}/${iconFile}`,
          }
        : {}),
    });
  }
  const configuredExtraEmojis = Array.isArray(behaviorRoot.extraEmojis)
    ? behaviorRoot.extraEmojis.filter(
        (emoji): emoji is string => typeof emoji === "string" && emoji.trim() !== "",
      )
    : [];
  const pressedEmoji =
    pressedKey && /\p{Extended_Pictographic}/u.test(pressedKey)
      ? [pressedKey]
      : [];
  const extraEmojis = [...pressedEmoji, ...configuredExtraEmojis];
  const parsedRows = parseKeyboardRows(
    STANDARD_IOS_KEYBOARD_LAYOUT,
    mode,
    language,
  );
  const rows =
    mode === "emoji" && extraEmojis.length
      ? withExtraEmojis(parsedRows, extraEmojis)
      : parsedRows;
  return {
    id: STANDARD_IOS_KEYBOARD_LAYOUT.id,
    name: STANDARD_IOS_KEYBOARD_LAYOUT.name,
    language,
    mode,
    pressedKey,
    modes: Object.fromEntries(
      Object.entries(STANDARD_IOS_KEYBOARD_LAYOUT.modes).map(([key, value]) => [
        key,
        {
          id: value.id,
          label: value.label,
          rowsText: value.rowsText,
        },
      ]),
    ),
    layout,
    rows,
    bottomItems,
  };
}

function iconUriForToken(
  token: string,
  iconTheme?: {
    asset_root: string;
    mapping_json: Record<string, unknown>;
  },
) {
  if (!token || !isObject(iconTheme?.mapping_json.tokens)) return "";
  const iconToken = iconTheme.mapping_json.tokens[token];
  const iconFile = isObject(iconToken) ? stringValue(iconToken.file) : "";
  return iconFile
    ? `${iconTheme?.asset_root.replace(/\/+$/g, "")}/${iconFile}`
    : "";
}

function iconItemsForState(
  rawItems: unknown,
  fallbackItems: Record<string, unknown>[],
  iconTheme:
    | {
        asset_root: string;
        mapping_json: Record<string, unknown>;
      }
    | undefined,
) {
  const items = Array.isArray(rawItems) ? rawItems : fallbackItems;
  const resolvedItems: Record<string, unknown>[] = [];
  items.forEach((rawItem, index) => {
    const item = typeof rawItem === "string" ? { token: rawItem } : rawItem;
    if (!isObject(item)) return;
    const token = stringValue(item.token);
    if (!token) return;
    const iconUri = iconUriForToken(token, iconTheme);
    resolvedItems.push({
      id: stringValue(item.id, token),
      token,
      label: stringValue(item.label, token),
      order: numberValue(item.order, index * 10),
      color: stringValue(item.color),
      ...(iconUri ? { iconUri } : {}),
    });
  });
  return resolvedItems.sort(
    (left, right) => numberValue(left.order, 0) - numberValue(right.order, 0),
  );
}

function resolveIconItems(
  rawItems: unknown,
  iconTheme:
    | {
        asset_root: string;
        mapping_json: Record<string, unknown>;
      }
    | undefined,
) {
  if (typeof rawItems === "string") {
    return iconItemsForState(
      rawItems
        .split(",")
        .map((token) => token.trim())
        .filter(Boolean)
        .map((token, index) => ({ token, order: (index + 1) * 10 })),
      [],
      iconTheme,
    );
  }
  return iconItemsForState(rawItems, [], iconTheme);
}

function iconItemsSource(primary: unknown, fallback: unknown): unknown {
  return Array.isArray(primary) && primary.length === 0
    ? fallback
    : primary ?? fallback;
}

function resolveTextInputBarDefinition(
  behaviorTextInputBar: unknown,
  iconTheme?: {
    asset_root: string;
    mapping_json: Record<string, unknown>;
  },
  scale = 1,
  viewportWidth = 390 * scale,
) {
  const root = isObject(behaviorTextInputBar) ? behaviorTextInputBar : {};
  const text = stringValue(root.text, stringValue(root.draftText));
  const measureText = text;
  const rawState = stringValue(root.state, text ? "typing" : "idle");
  const state = rawState === "typing" ? "typing" : "idle";
  const rawIconSets = isObject(root.iconSets) ? root.iconSets : {};
  const rawLeftSets = isObject(rawIconSets.left) ? rawIconSets.left : {};
  const rawRightSets = isObject(rawIconSets.right) ? rawIconSets.right : {};
  const defaultLeftItems = {
    idle: [
      { token: "chat_emoji", order: 10 },
      { token: "chat_attach", order: 20 },
    ],
    typing: [{ token: "chat_emoji", order: 10 }],
  };
  const defaultRightItems = {
    idle: [
      { token: "media_camera", order: 10 },
      { token: "media_mic", order: 20 },
    ],
    typing: [{ token: "chat_send", order: 10, color: "#007AFF" }],
  };
  const leftItems = iconItemsForState(
    rawLeftSets[state],
    defaultLeftItems[state],
    iconTheme,
  );
  const rightItems = iconItemsForState(
    rawRightSets[state],
    defaultRightItems[state],
    iconTheme,
  );
  const baseFontSize = 17;
  const cursorWidth = 2;
  const designViewportWidth = viewportWidth / Math.max(scale, 0.0001);
  const designPaddingX = 8;
  const designGap = 8;
  const designFieldPaddingX = 14;
  const designFieldPaddingY = 6;
  const designIconSize = 20;
  const leftZoneWidth =
    leftItems.length * designIconSize +
    Math.max(0, leftItems.length - 1) * designGap;
  const rightZoneWidth =
    rightItems.length * designIconSize +
    Math.max(0, rightItems.length - 1) * designGap;
  const estimatedFieldWidth = Math.max(
    120,
    designViewportWidth -
      designPaddingX * 2 -
      leftZoneWidth -
      rightZoneWidth -
      designGap * 2,
  );
  const estimatedTextWidth = Math.max(
    64,
    estimatedFieldWidth - designFieldPaddingX * 2,
  );
  const explicitLineCount = Math.max(1, measureText.split("\n").length);
  const estimatedCharsPerLine = Math.max(
    8,
    Math.floor(estimatedTextWidth / (baseFontSize * 0.56)),
  );
  const visualLineCount = Math.max(
    explicitLineCount,
    Math.ceil(
      Math.max(1, Array.from(measureText || " ").length) / estimatedCharsPerLine,
    ),
  );
  const lineCount = Math.min(4, visualLineCount);
  const lineHeight = baseFontSize * 1.25;
  const fieldHeight =
    lineCount * lineHeight + designFieldPaddingY * 2;
  const designLayout = {
    paddingX: 8,
    paddingY: 6,
    gap: 8,
    fieldHeight,
    lineCount,
    lineHeight,
    fieldPaddingX: 14,
    fieldPaddingY: designFieldPaddingY,
    fieldRadius: 20,
    iconSize: 20,
    fontSize: baseFontSize,
    cursorWidth,
  };
  const height = designLayout.paddingY * 2 + designLayout.fieldHeight;
  const layout = Object.fromEntries(
    Object.entries({ height, ...designLayout }).map(([key, value]) => [
      key,
      value * scale,
    ]),
  );
  return {
    text,
    placeholder: stringValue(root.placeholder, "Mensaje"),
    state,
    cursorVisible: root.cursorVisible !== false,
    layout,
    leftItems,
    rightItems,
  };
}

function isActiveWriteOnMessage(
  message: {
    direction: "incoming" | "outgoing" | "system";
    text: string;
    visibleText: string;
    timing: {
      writeOnStartFrame?: number;
      writeOnDurationFrames?: number;
    };
  },
  localFrame: number,
) {
  const startFrame = message.timing.writeOnStartFrame;
  const durationFrames = message.timing.writeOnDurationFrames;
  if (
    message.direction !== "outgoing" ||
    startFrame === undefined ||
    durationFrames === undefined ||
    durationFrames <= 0
  ) {
    return false;
  }
  return localFrame >= startFrame && localFrame < startFrame + durationFrames;
}

function pressedKeyFromWriteOnState(text: string, visibleText: string) {
  const fullCharacters = Array.from(text);
  if (!fullCharacters.length) return undefined;
  const visibleCharacters = Array.from(visibleText);
  if (visibleCharacters.length > 0) {
    return normalizeKeyboardDisplayKey(visibleCharacters.at(-1) ?? "");
  }
  return normalizeKeyboardDisplayKey(fullCharacters[0] ?? "");
}

function normalizeKeyboardDisplayKey(value: string) {
  return value
    .replace(/[áàäâ]/g, "a")
    .replace(/[ÁÀÄÂ]/g, "A")
    .replace(/[éèëê]/g, "e")
    .replace(/[ÉÈËÊ]/g, "E")
    .replace(/[íìïî]/g, "i")
    .replace(/[ÍÌÏÎ]/g, "I")
    .replace(/[óòöô]/g, "o")
    .replace(/[ÓÒÖÔ]/g, "O")
    .replace(/[úùüû]/g, "u")
    .replace(/[ÚÙÜÛ]/g, "U");
}

export function resolveThemeModeTokens(
  tokens: Record<string, unknown>,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  const envelope = ThemeEnvelopeSchema.parse(tokens);
  return envelope.modes?.[themeMode] ?? {};
}

function numericToken(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function stringToken(value: unknown): string | undefined {
  return typeof value === "string" && value.trim() ? value : undefined;
}

function fontWeightToken(value: unknown): string | number | undefined {
  if (typeof value === "string" && value.trim()) return value;
  if (typeof value === "number" && Number.isFinite(value)) return value;
  return undefined;
}

export function moduleTypographyDefaultsFromFonts(
  tokens: Record<string, unknown>,
): Record<string, unknown> {
  const fonts = isObject(tokens.fonts) ? tokens.fonts : {};
  const fontFamily = stringToken(fonts.family);
  const bodySize = numericToken(fonts.bodySize);
  const bodyLineHeight = numericToken(fonts.bodyLineHeight);
  const captionSize = numericToken(fonts.captionSize);
  const weight = fontWeightToken(fonts.weight);

  if (!fontFamily && bodySize === undefined && captionSize === undefined) {
    return {};
  }

  return {
    typography: {
      message: {
        ...(fontFamily ? { fontFamily } : {}),
        ...(bodySize !== undefined ? { fontSize: bodySize } : {}),
        ...(bodyLineHeight !== undefined ? { lineHeight: bodyLineHeight } : {}),
        ...(weight !== undefined ? { fontWeight: weight } : {}),
      },
      headerTitle: {
        ...(fontFamily ? { fontFamily } : {}),
        ...(bodySize !== undefined ? { fontSize: bodySize } : {}),
        ...(bodyLineHeight !== undefined ? { lineHeight: bodyLineHeight } : {}),
        ...(weight !== undefined ? { fontWeight: weight } : {}),
      },
      headerSubtitle: {
        ...(fontFamily ? { fontFamily } : {}),
        ...(captionSize !== undefined ? { fontSize: captionSize } : {}),
        ...(captionSize !== undefined ? { lineHeight: captionSize * 1.2308 } : {}),
        ...(weight !== undefined ? { fontWeight: weight } : {}),
      },
    },
  };
}

export function resolveGlobalThemeTokens(
  theme: Theme,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  const merged = mergeTokenObjects(
    theme.tokens_json,
    resolveThemeModeTokens(theme.tokens_json, themeMode),
  );
  const cursor = isObject(merged.cursor) ? merged.cursor : {};
  return {
    ...merged,
    cursor: {
      style: "bar",
      width: 2,
      blinkFrames: 15,
      color: themeMode === "dark" ? "#0A84FF" : "#007AFF",
      ...cursor,
    },
  };
}

export function resolveModuleThemeTokens(
  tokens: Record<string, unknown>,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  return mergeTokenObjects(tokens, resolveThemeModeTokens(tokens, themeMode));
}

function normalizeChatVisualTokenGroups(
  tokens: Record<string, unknown>,
): Record<string, unknown> {
  const header = isObject(tokens.header) ? tokens.header : {};
  const chatBubbles = isObject(tokens.chatBubbles) ? tokens.chatBubbles : {};
  const chatBubbleMedia = isObject(chatBubbles.media)
    ? chatBubbles.media
    : {};
  const { shadow: _chatBubbleShadow, ...visibleChatBubbles } = chatBubbles;
  const avatars = isObject(tokens.avatars) ? tokens.avatars : {};
  const shadows = isObject(tokens.shadows) ? tokens.shadows : {};
  const headerAvatarSize = numberValue(
    header.avatarSize,
    numberValue(avatars.headerSize, 56),
  );
  const headerLeftIconTokens =
    typeof header.leftIconTokens === "string" && header.leftIconTokens.trim()
      ? header.leftIconTokens
      : "nav_chevron_left";
  const headerRightIconTokens =
    typeof header.rightIconTokens === "string" && header.rightIconTokens.trim()
      ? header.rightIconTokens
      : "media_camera, phone_call";
  const bubbleAvatarSize = numberValue(
    chatBubbles.avatarSize,
    numberValue(avatars.defaultSize, 32),
  );
  const bubbleAvatarGap = numberValue(
    chatBubbles.avatarGap,
    numberValue(avatars.gap, 8),
  );
  const systemBackground =
    typeof chatBubbles.systemBackground === "string"
      ? chatBubbles.systemBackground
      : "rgba(118, 118, 128, 0.16)";
  const systemText =
    typeof chatBubbles.systemText === "string"
      ? chatBubbles.systemText
      : typeof chatBubbles.incomingText === "string"
        ? chatBubbles.incomingText
        : "#3C3C43";
  return {
    ...tokens,
    header: {
      ...header,
      avatarSize: headerAvatarSize,
      avatarCornerRadius: numberValue(
        header.avatarCornerRadius,
        Math.round(headerAvatarSize * 0.22),
      ),
      subtitleBottomPadding: numberValue(header.subtitleBottomPadding, 10),
      elementGap: numberValue(header.elementGap, numberValue(avatars.gap, 8)),
      sidePadding: numberValue(
        header.sidePadding,
        numberValue(header.elementGap, numberValue(avatars.gap, 8)),
      ),
      leftIconTokens: headerLeftIconTokens,
      rightIconTokens: headerRightIconTokens,
    },
    chatBubbles: {
      ...visibleChatBubbles,
      avatarSize: bubbleAvatarSize,
      avatarGap: bubbleAvatarGap,
      media: {
        ...chatBubbleMedia,
        borderWidth: numberValue(chatBubbleMedia.borderWidth, 0),
        cornerRadius: numberValue(
          chatBubbleMedia.cornerRadius,
          numberValue(chatBubbles.radius, 18),
        ),
        borderColor:
          typeof chatBubbleMedia.borderColor === "string"
            ? chatBubbleMedia.borderColor
            : "transparent",
        shadowEnabled: chatBubbleMedia.shadowEnabled === true,
      },
      systemBackground,
      systemText,
    },
    avatars: {
      ...avatars,
      defaultSize: bubbleAvatarSize,
      gap: bubbleAvatarGap,
      headerSize: headerAvatarSize,
    },
    shadows: {
      ...shadows,
      avatar: isObject(shadows.avatar)
        ? shadows.avatar
        : isObject(shadows.elevated)
          ? shadows.elevated
        : isObject(shadows.notification)
          ? shadows.notification
          : {
              color: "rgba(0,0,0,0.18)",
              offsetX: 0,
              offsetY: 4,
              blur: 18,
            },
    },
  };
}

function withBubbleStatusIconUris(
  tokens: Record<string, unknown>,
  iconTheme?: {
    asset_root: string;
    mapping_json: Record<string, unknown>;
  },
) {
  const chatBubbles = isObject(tokens.chatBubbles) ? tokens.chatBubbles : {};
  const status = isObject(chatBubbles.status) ? chatBubbles.status : {};
  const tickSingleIconToken = stringValue(status.tickSingleIconToken);
  const tickDoubleIconToken = stringValue(status.tickDoubleIconToken);
  return {
    ...tokens,
    chatBubbles: {
      ...chatBubbles,
      status: {
        ...status,
        ...(tickSingleIconToken
          ? { tickSingleIconUri: iconUriForToken(tickSingleIconToken, iconTheme) }
          : {}),
        ...(tickDoubleIconToken
          ? { tickDoubleIconUri: iconUriForToken(tickDoubleIconToken, iconTheme) }
          : {}),
      },
    },
  };
}

const DESIGN_UNIT_TOKEN_PATHS = [
  ["fonts", "bodySize"],
  ["fonts", "bodyLineHeight"],
  ["fonts", "captionSize"],
  ["layout", "screenGutter"],
  ["header", "height"],
  ["header", "separatorWidth"],
  ["header", "elementGap"],
  ["header", "sidePadding"],
  ["header", "iconSize"],
  ["header", "avatarSize"],
  ["header", "avatarCornerRadius"],
  ["header", "avatarBorderWidth"],
  ["header", "subtitleBottomPadding"],
  ["messages", "spacing"],
  ["messages", "groupSpacing"],
  ["typography", "message", "fontSize"],
  ["typography", "message", "lineHeight"],
  ["typography", "headerTitle", "fontSize"],
  ["typography", "headerTitle", "lineHeight"],
  ["typography", "headerSubtitle", "fontSize"],
  ["typography", "headerSubtitle", "lineHeight"],
  ["chatBubbles", "paddingX"],
  ["chatBubbles", "paddingY"],
  ["chatBubbles", "avatarSize"],
  ["chatBubbles", "avatarGap"],
  ["chatBubbles", "media", "borderWidth"],
  ["chatBubbles", "media", "cornerRadius"],
  ["chatBubbles", "tail", "width"],
  ["chatBubbles", "tail", "height"],
  ["chatBubbles", "status", "size"],
  ["chatBubbles", "status", "gap"],
  ["chatBubbles", "status", "offsetX"],
  ["chatBubbles", "status", "offsetY"],
  ["shadows", "elevated", "offsetX"],
  ["shadows", "elevated", "offsetY"],
  ["shadows", "elevated", "blur"],
  ["shadows", "avatar", "offsetX"],
  ["shadows", "avatar", "offsetY"],
  ["shadows", "avatar", "blur"],
  ["shadows", "notification", "offsetX"],
  ["shadows", "notification", "offsetY"],
  ["shadows", "notification", "blur"],
  ["avatars", "defaultSize"],
  ["avatars", "headerSize"],
  ["avatars", "gap"],
  ["radii", "bubble"],
  ["cursor", "width"],
] as const;

function getNestedValue(
  value: Record<string, unknown>,
  path: readonly string[],
): unknown {
  return path.reduce<unknown>(
    (current, key) => (isObject(current) ? current[key] : undefined),
    value,
  );
}

function setNestedValue(
  value: Record<string, unknown>,
  path: readonly string[],
  nextValue: unknown,
): Record<string, unknown> {
  if (path.length === 0) return value;
  const [key, ...rest] = path;
  if (!key) return value;
  if (rest.length === 0) {
    return { ...value, [key]: nextValue };
  }
  const child = isObject(value[key]) ? value[key] : {};
  return {
    ...value,
    [key]: setNestedValue(child, rest, nextValue),
  };
}

function scaleDesignTokensForRender(
  tokens: Record<string, unknown>,
  scale: number,
): Record<string, unknown> {
  if (scale === 1) return tokens;
  let scaled = tokens;
  for (const path of DESIGN_UNIT_TOKEN_PATHS) {
    const value = getNestedValue(scaled, path);
    if (typeof value === "number") {
      scaled = setNestedValue(scaled, path, value * scale);
    }
  }
  return scaled;
}

function scaleChatMediaForRender(
  media: Record<string, unknown> | undefined,
  scale: number,
): Record<string, unknown> | undefined {
  if (!media) return undefined;
  const windowValue = isObject(media.window) ? media.window : undefined;
  const transformValue = isObject(media.transform) ? media.transform : undefined;
  return {
    ...media,
    ...(windowValue
      ? {
          window: {
            ...windowValue,
            width: numberValue(windowValue.width, 0) * scale,
            height: numberValue(windowValue.height, 0) * scale,
            offsetX: numberValue(windowValue.offsetX, 0) * scale,
            offsetY: numberValue(windowValue.offsetY, 0) * scale,
          },
        }
      : {}),
    ...(transformValue
      ? {
          transform: {
            ...transformValue,
            translateX: numberValue(transformValue.translateX, 0) * scale,
            translateY: numberValue(transformValue.translateY, 0) * scale,
          },
        }
      : {}),
  };
}

function renderScaleFromMetrics(
  metrics: z.infer<typeof DeviceMetricsSchema>,
): number {
  if (metrics.scaleToPixels) {
    return metrics.scaleToPixels;
  }
  if (metrics.designSpace && metrics.renderSize) {
    return metrics.renderSize.width / metrics.designSpace.width;
  }
  return metrics.pixelRatio;
}

function resolveAppTokens(
  app: App,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  const config = app.config_json ?? {};
  const tokens =
    isObject(config.tokens_json) ? config.tokens_json : config;
  return mergeTokenObjects(tokens, resolveThemeModeTokens(tokens, themeMode));
}

function chatModuleConfig(
  moduleTokens: Record<string, unknown>,
  screenInstance: ScreenInstance,
  moduleInstance: ModuleInstance,
): Record<string, unknown> {
  const screenBase =
    screenInstance.module_config_json &&
    isObject(screenInstance.module_config_json)
      ? screenInstance.module_config_json
      : {};
  const behavior =
    moduleInstance.behavior_json && isObject(moduleInstance.behavior_json)
      ? moduleInstance.behavior_json
      : {};
  const textInputBar =
    isObject(moduleTokens.textInputBar)
      ? moduleTokens.textInputBar
      : isObject(screenBase.textInputBar)
        ? screenBase.textInputBar
        : isObject(behavior.textInputBar)
          ? behavior.textInputBar
          : undefined;
  const keyboard =
    isObject(moduleTokens.keyboard)
      ? moduleTokens.keyboard
      : isObject(screenBase.keyboard)
        ? screenBase.keyboard
        : isObject(behavior.keyboard)
          ? behavior.keyboard
          : undefined;

  return {
    ...behavior,
    ...(textInputBar ? { textInputBar } : {}),
    ...(keyboard ? { keyboard } : {}),
  };
}

function stripModuleThemeSystemOwnedTokens(
  tokens: Record<string, unknown>,
): Record<string, unknown> {
  const next: Record<string, unknown> = { ...tokens };
  delete next.cursor;
  if (isObject(next.modes)) {
    const modes = { ...(next.modes as Record<string, unknown>) };
    for (const mode of ["light", "dark"] as const) {
      if (isObject(modes[mode])) {
        const modeRoot = { ...(modes[mode] as Record<string, unknown>) };
        delete modeRoot.cursor;
        modes[mode] = modeRoot;
      }
    }
    next.modes = modes;
  }
  return next;
}

function resolveChatActor(
  repository: DomainRepository,
  actorId: string,
  themeMode: "light" | "dark",
): ResolvedChatActor {
  const actor = requireRecord(
    repository.getActor(actorId),
    "Actor",
    actorId,
  );
  const directAvatarUri = actorAvatarUri(actor);
  const avatarAssetId = directAvatarUri
    ? undefined
    : actor.avatar_asset_id;
  const avatar = avatarAssetId
    ? requireRecord(
        repository.getMediaAsset(avatarAssetId),
        "MediaAsset",
        avatarAssetId,
      )
    : undefined;

  return {
    id: actor.id,
    displayName: actor.display_name,
    ...(directAvatarUri || avatar ? { avatarUri: directAvatarUri ?? avatar?.uri } : {}),
    ...(actorMetadataColor(actor, themeMode, "color") ? {
      color: actorMetadataColor(actor, themeMode, "color"),
    } : {}),
    ...(actorMetadataColor(actor, themeMode, "avatarTextColor") ? {
      avatarTextColor: actorMetadataColor(actor, themeMode, "avatarTextColor"),
    } : {}),
  };
}

function actorMetadataColor(
  actor: Actor | undefined,
  themeMode: "light" | "dark",
  field: "color" | "avatarTextColor",
) {
  const metadata = actor?.metadata_json;
  if (!isObject(metadata)) {
    return undefined;
  }
  const modes = metadata.modes;
  if (isObject(modes) && isObject(modes[themeMode])) {
    const modeValue = modes[themeMode]?.[field];
    if (typeof modeValue === "string" && modeValue.trim()) {
      return modeValue;
    }
  }
  if (field === "color") {
    return typeof metadata.color === "string" && metadata.color.trim()
      ? metadata.color
      : undefined;
  }
  const avatar = metadata.avatar;
  if (isObject(avatar) && typeof avatar.textColor === "string" && avatar.textColor.trim()) {
    return avatar.textColor;
  }
  return undefined;
}

function actorAvatarUri(actor: Actor | undefined) {
  const metadata = actor?.metadata_json;
  if (!metadata || typeof metadata !== "object" || Array.isArray(metadata)) {
    return undefined;
  }
  const avatar = metadata.avatar;
  if (!avatar || typeof avatar !== "object" || Array.isArray(avatar)) {
    return undefined;
  }
  const avatarRecord = avatar as Record<string, unknown>;
  if (avatarRecord.useInitials === true) {
    return undefined;
  }
  const filePath = avatarRecord.filePath;
  return typeof filePath === "string" && filePath.trim() ? filePath : undefined;
}

export interface ResolveChatScreenInput {
  repository: DomainRepository;
  screenInstance: ScreenInstance;
  moduleInstance: ModuleInstance;
  ownerActor: Actor;
  app: App;
  device: Device;
  deviceState: DeviceState;
  theme: Theme;
  localFrame: number;
  fps: number;
}

export function resolveChatScreen({
  repository,
  screenInstance,
  moduleInstance,
  ownerActor,
  app,
  device,
  deviceState,
  theme,
  localFrame,
  fps,
}: ResolveChatScreenInput): ResolvedChatScreenProps {
  if (moduleInstance.module_id !== CHAT_MODULE_ID) {
    throw new Error(
      `Chat module instance ${moduleInstance.id} requires module_id ${CHAT_MODULE_ID}`,
    );
  }
  if (moduleInstance.module_schema_version !== CHAT_MODULE_SCHEMA_VERSION) {
    throw new Error(
      `Chat module instance ${moduleInstance.id} requires module schema version ${CHAT_MODULE_SCHEMA_VERSION}`,
    );
  }

  const moduleData = ChatModuleDataSchema.parse(
    moduleInstance.content_json,
  );
  if (moduleData.schemaVersion !== moduleInstance.module_schema_version) {
    throw new Error(
      `Chat module data schemaVersion does not match module instance ${moduleInstance.id}`,
    );
  }

  const themeEnvelope = ThemeEnvelopeSchema.parse(theme.tokens_json);
  const themeMode =
    screenInstance.theme_mode ?? themeEnvelope.defaultMode ?? "light";
  const ownerChatActor = resolveChatActor(repository, ownerActor.id, themeMode);
  const headerActor = moduleData.header.actorId
    ? resolveChatActor(repository, moduleData.header.actorId, themeMode)
    : undefined;

  const metrics = DeviceMetricsSchema.parse(device.metrics_json);
  const state = DeviceStateValuesSchema.parse(deviceState.state_json);
  const moduleThemeConfig = repository.getModuleThemeConfig(
    theme.id,
    app.id,
    moduleInstance.module_id,
    moduleInstance.module_schema_version,
  );
  if (!moduleThemeConfig) {
    throw new Error(
      `No module theme config for theme ${theme.id}, app ${app.id}, module ${moduleInstance.module_id}, schema ${moduleInstance.module_schema_version}`,
    );
  }
  const globalThemeTokens = resolveGlobalThemeTokens(theme, themeMode);
  const appTokens = resolveAppTokens(app, themeMode);
  const moduleThemeTokens = stripModuleThemeSystemOwnedTokens(
    resolveModuleThemeTokens(moduleThemeConfig.tokens_json, themeMode),
  );
  const moduleConfig = ChatModuleConfigSchema.parse(
    chatModuleConfig(moduleThemeTokens, screenInstance, moduleInstance),
  );
  const genericTokens = mergeTokenObjects(globalThemeTokens, appTokens);
  const moduleDefaultsFromGenericTokens =
    moduleTypographyDefaultsFromFonts(genericTokens);
  const inheritedModuleTokens = mergeTokenObjects(
    mergeTokenObjects(genericTokens, moduleDefaultsFromGenericTokens),
    moduleThemeTokens,
  );
  const mergedThemeTokens = inheritedModuleTokens;
  const renderScale = renderScaleFromMetrics(metrics);
  const scaledThemeTokens = scaleDesignTokensForRender(
    mergedThemeTokens,
    renderScale,
  );
  const normalizedThemeTokens = normalizeChatVisualTokenGroups(scaledThemeTokens);
  const statusBar = theme.status_bar_id
    ? repository.getStatusBar(theme.status_bar_id)
    : undefined;
  const navigationBar = theme.navigation_bar_id
    ? repository.getNavigationBar(theme.navigation_bar_id)
    : undefined;
  const iconTheme = theme.icon_theme_id
    ? repository.getIconTheme(theme.icon_theme_id)
    : undefined;
  const themeTokens = ChatThemeSchema.parse(
    withBubbleStatusIconUris(normalizedThemeTokens, iconTheme),
  );
  const resolvedHeaderTokens = {
    ...themeTokens.header,
    leftItems: resolveIconItems(
      iconItemsSource(themeTokens.header.leftItems, themeTokens.header.leftIconTokens),
      iconTheme,
    ),
    rightItems: resolveIconItems(
      iconItemsSource(themeTokens.header.rightItems, themeTokens.header.rightIconTokens),
      iconTheme,
    ),
  };
  const effectiveStatusBar = resolveStatusBarDefinition(
    statusBar?.config_json,
    moduleConfig.statusBar,
    iconTheme,
    renderScale,
  );
  const effectiveStatusBarLayout = isObject(effectiveStatusBar.layout)
    ? effectiveStatusBar.layout
    : {};
  const statusBarHeight = numberValue(
    effectiveStatusBarLayout.height,
    metrics.statusBar.height,
  );
  const effectiveNavigationBar = resolveNavigationBarDefinition(
    navigationBar?.config_json,
    renderScale,
  );
  const resolvedMessages = moduleData.messages.map((message) => {
    const direction =
      message.direction ??
      (message.type === "system"
        ? "system"
        : message.actorId === ownerActor.id
          ? "outgoing"
          : "incoming");
    const sender =
      direction === "system"
        ? ownerChatActor
        : resolveChatActor(
            repository,
            message.actorId ?? ownerActor.id,
            themeMode,
          );
    const bubble = resolveMessageBubble({
      message,
      sender,
      direction,
      themeTokens: normalizedThemeTokens,
      localFrame,
      fps,
      viewportWidth: metrics.viewport.width,
    });
    const mediaAsset = message.mediaAssetId
      ? requireRecord(
          repository.getMediaAsset(message.mediaAssetId),
          "MediaAsset",
          message.mediaAssetId,
        )
      : undefined;
    const scaledMedia = scaleChatMediaForRender(message.media, renderScale);
    const mediaFilePath = scaledMedia
      ? stringValue(scaledMedia.filePath)
      : "";
    const mediaUri = mediaFilePath || mediaAsset?.uri;

    return {
      id: bubble.id,
      direction: bubble.direction,
      text: bubble.text,
      visibleText: bubble.visibleText,
      status: bubble.status,
      sender: {
        id: bubble.actor.id,
        displayName: bubble.actor.displayName,
      },
      ...(mediaUri
        ? {
            media: {
              ...(mediaAsset ? { assetId: mediaAsset.id } : {}),
              uri: mediaUri,
              ...(scaledMedia && stringValue(scaledMedia.type)
                ? { type: stringValue(scaledMedia.type) }
                : {}),
              ...(scaledMedia?.window
                ? { window: scaledMedia.window }
                : {}),
              ...(scaledMedia?.transform
                ? { transform: scaledMedia.transform }
                : {}),
            },
          }
        : {}),
      timing: {
        startFrame: bubble.timing.startFrame,
        enterDurationFrames: bubble.timing.enterDurationFrames,
        ...(bubble.timing.writeOnStartFrame !== null
          ? { writeOnStartFrame: bubble.timing.writeOnStartFrame }
          : {}),
        ...(bubble.timing.writeOnDurationFrames !== null
          ? { writeOnDurationFrames: bubble.timing.writeOnDurationFrames }
          : {}),
      },
      style: bubble.style,
      layout: bubble.layout,
      animation: bubble.animation,
    };
  });

  const activeComposerMessage = [...resolvedMessages]
    .reverse()
    .find((message) => isActiveWriteOnMessage(message, localFrame));

  const runtimeShowTextInputBar = moduleConfig.showTextInputBar === true;
  const runtimeShowKeyboard =
    moduleConfig.showKeyboard === true &&
    runtimeShowTextInputBar &&
    activeComposerMessage !== undefined;
  const runtimePressedKey = activeComposerMessage
    ? pressedKeyFromWriteOnState(
        activeComposerMessage.text,
        activeComposerMessage.visibleText,
      )
    : undefined;

  const effectiveKeyboard = resolveKeyboardDefinition(
    {
      ...(isObject(moduleConfig.keyboard) ? moduleConfig.keyboard : {}),
      ...(runtimePressedKey ? { pressedKey: runtimePressedKey } : {}),
    },
    iconTheme,
    renderScale,
    { hasTextInputBar: runtimeShowTextInputBar },
  );
  const effectiveTextInputBar = resolveTextInputBarDefinition(
    {
      ...(isObject(moduleConfig.textInputBar) ? moduleConfig.textInputBar : {}),
      ...(activeComposerMessage
        ? {
            text: activeComposerMessage.visibleText,
            state: "typing",
          }
        : {
            text: "",
            state: "idle",
          }),
    },
    iconTheme,
    renderScale,
    metrics.viewport.width,
  );

  const messages = resolvedMessages.map((message) =>
    activeComposerMessage &&
    runtimeShowTextInputBar &&
    message.id === activeComposerMessage.id
      ? {
          ...message,
          visibleText: "",
          animation: {
            ...(isObject(message.animation) ? message.animation : {}),
            hideUntilWriteComplete: true,
          },
        }
      : message,
  );

  const ownerAvatar = ownerChatActor.avatarUri
    ? {
        uri: ownerChatActor.avatarUri,
      }
    : undefined;
  const headerAvatar = headerActor?.avatarUri
    ? {
        uri: headerActor.avatarUri,
      }
    : undefined;
  const systemShadow = isObject(themeTokens.shadows?.elevated)
    ? themeTokens.shadows.elevated
    : isObject(themeTokens.shadows?.avatar)
      ? themeTokens.shadows.avatar
      : {};

  return ResolvedChatScreenPropsSchema.parse({
    frame: localFrame,
    fps,
    screenInstanceId: screenInstance.id,
    themeMode,
    viewport: {
      ...metrics.viewport,
      safeArea: metrics.safeArea,
    },
    theme: {
      id: theme.id,
      ...themeTokens,
      header: resolvedHeaderTokens,
      chatBubbles: {
        ...themeTokens.chatBubbles,
        radius: themeTokens.radii.bubble,
        shadow: systemShadow,
      },
    },
    device: {
      id: device.id,
      osFamily: device.os_family,
      pixelRatio: metrics.pixelRatio,
      statusBarHeight,
      cornerRadius: metrics.cornerRadius,
      defaultScreenScale: metrics.defaultScreenScale,
    },
    deviceState: state,
    statusBar: effectiveStatusBar,
    navigationBar: effectiveNavigationBar,
    keyboard: effectiveKeyboard,
    textInputBar: effectiveTextInputBar,
    ownerActor: {
      id: ownerActor.id,
      displayName: ownerActor.display_name,
      ...(ownerAvatar?.uri ? { avatar: ownerAvatar } : {}),
    },
    header: {
      title: moduleData.header.title,
      subtitle: moduleData.header.subtitle,
      ...(moduleData.header.useContactColor && headerActor?.color
        ? { backgroundColor: headerActor.color }
        : {}),
      ...(headerAvatar?.uri ? { avatar: headerAvatar } : {}),
    },
    messages,
    events: repository
      .getScreenEventsForInstance(screenInstance.id)
      .map((event) => ({
        id: event.id,
        type: event.event_type,
        startFrame: event.start_frame,
        durationFrames: event.duration_frames,
        targetId: event.target_id,
        payload: event.payload_json,
      })),
    props: {
      showHeader: moduleConfig.showHeader,
      showStatusBar: moduleConfig.showStatusBar,
      showNavigationBar: moduleConfig.showNavigationBar,
      showKeyboard: runtimeShowKeyboard,
      showTextInputBar: runtimeShowTextInputBar,
      ...(activeComposerMessage
        ? { activeComposerMessageId: activeComposerMessage.id }
        : {}),
      textInputBar: moduleConfig.textInputBar ?? {},
      keyboard: moduleConfig.keyboard ?? {},
      initialScroll: moduleConfig.initialScroll,
      messageGrouping: moduleConfig.messageGrouping,
      debugShowBounds: moduleConfig.debugShowBounds,
    },
  });
}
