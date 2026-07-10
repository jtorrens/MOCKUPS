import { z } from "zod";
import {
  CHAT_HEADER_DEFAULTS,
  CHAT_HEADER_DESIGN_UNIT_PATHS,
  CHAT_HEADER_TOKEN_BINDINGS,
  CHAT_KEYBOARD_TOKEN_BINDINGS,
  CHAT_TEXT_INPUT_BAR_TOKEN_BINDINGS,
  CHAT_TYPOGRAPHY_TOKEN_BINDINGS,
  stripChatModuleTypographyFontIdentity,
  unscaleTextInputBarThemeScope,
} from "../fields/chatFields.js";
import { ACTOR_FIELDS } from "../fields/actorFields.js";
import {
  parseKeyboardRows,
  STANDARD_IOS_KEYBOARD_LAYOUT,
  type KeyboardMode,
} from "../../shared/keyboard/standardKeyboardLayout.js";
import {
  fontStyleForProductionStyle,
  fontWeightForProductionStyle,
} from "../fonts/productionFontNormalization.js";
import {
  resolveJsonFieldBindingGroup,
  surfaceStyleNormalize,
} from "../value-system/index.js";
import type { DomainRepository } from "../repository/types.js";
import {
  ChatModuleConfigSchema,
  ChatModuleDataSchema,
  ResolvedChatScreenPropsSchema,
  type Actor,
  type App,
  type ChatModuleMessage,
  type Device,
  type DeviceState,
  type ModuleInstance,
  type PaletteColor,
  type ResolvedChatScreenProps,
  type ScreenInstance,
  type Theme,
} from "../schemas/index.js";
import { requireRecord } from "./helpers.js";
import {
  resolveMessageBubble,
  type ResolvedChatActor,
  type TimedChatModuleMessage,
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
  systemFonts: z.record(z.string(), z.unknown()).optional(),
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
  components: z.record(z.string(), z.unknown()).optional(),
  componentOverrides: z.record(z.string(), z.unknown()).optional(),
  cursor: z.record(z.string(), z.unknown()),
  shadows: z.record(z.string(), z.unknown()).optional(),
  surfaceRelief: z.record(z.string(), z.unknown()).optional(),
  radii: z.object({
    none: z.number().min(0),
    control: z.number().min(0),
    card: z.number().min(0),
    panel: z.number().min(0),
    surface: z.number().min(0),
    pill: z.number().min(0),
    avatar: z.number().min(0),
    full: z.number().min(0),
  }),
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
  deviceState: Record<string, unknown>,
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
    const runtimeValue =
      id === "time"
        ? stringValue(deviceState.time, stringValue(rawItem.value))
        : id === "carrier"
          ? stringValue(deviceState.networkLabel, stringValue(rawItem.value))
          : id === "signal" || kind === "generatedSignal"
            ? numberValue(deviceState.signalBars, numberValue(rawItem.value, 0))
            : id === "battery" || kind === "generatedBattery"
              ? Math.round(numberValue(deviceState.batteryLevel, 0) * 100)
              : "value" in rawItem
                ? rawItem.value
                : undefined;
    const runtimeCharging =
      id === "battery" || kind === "generatedBattery"
        ? deviceState.batteryCharging === true
        : rawItem.charging === true;
    const runtimeEnabled =
      id === "wifi" && "wifiEnabled" in deviceState
        ? deviceState.wifiEnabled === true
        : true;
    const enabled =
      typeof override.enabled === "boolean" ? override.enabled : runtimeEnabled;
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
          : runtimeValue,
      charging:
        typeof override.charging === "boolean"
          ? override.charging
          : runtimeCharging,
      ...(id === "wifi" && typeof deviceState.wifiIconState === "string"
        ? { wifiIconState: deviceState.wifiIconState }
        : {}),
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

function resolveKeyboardInheritance(
  componentTokens: unknown,
  instanceTokens: unknown,
): Record<string, unknown> {
  return resolveJsonFieldBindingGroup(CHAT_KEYBOARD_TOKEN_BINDINGS, [
    isObject(instanceTokens) ? instanceTokens : {},
    isObject(componentTokens) ? componentTokens : {},
  ]);
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
  const keyPadding = numberValue(behaviorRoot.keyPadding, 6);
  const keyHeight = 42;
  const designLayout = {
    topPadding: 8,
    sidePadding: 6,
    bottomPadding: 8,
    bottomUtilityHeight,
    bottomUtilitySidePadding: 24,
    bottomIconSize: 22,
    rowGap: 8,
    keyGap: 6,
    keyHeight,
    keyPadding,
    keyRadius: numberValue(behaviorRoot.keyRadius, 7),
    fontSize: Math.max(1, keyHeight - keyPadding * 2),
    emojiFontScale: 1.2,
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
      key === "emojiFontScale" ? value : value * scale,
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
    pushDurationFrames: Math.max(
      0,
      Math.round(numberValue(behaviorRoot.pushDurationFrames, 8)),
    ),
    messageGapToTextInput:
      numberValue(behaviorRoot.messageGapToTextInput, 10) * scale,
    fontFamily: stringValue(behaviorRoot.fontFamily, "Oswald"),
    fontWeight: resolvedFontWeightToken(behaviorRoot.fontWeight) ?? 400,
    fontStyle: resolvedFontStyleToken(
      behaviorRoot.fontStyle,
      behaviorRoot.fontWeight,
    ),
    pressedEffect: stringValue(behaviorRoot.pressedEffect, "popover"),
    keyShadowEnabled: behaviorRoot.keyShadowEnabled !== false,
    surfaceReliefEnabled: behaviorRoot.surfaceReliefEnabled !== false,
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
  themeTokens: Record<string, unknown> = {},
  palette: Map<string, string> = new Map(),
) {
  const items = Array.isArray(rawItems) ? rawItems : fallbackItems;
  const resolvedItems: Record<string, unknown>[] = [];
  items.forEach((rawItem, index) => {
    const item = typeof rawItem === "string" ? { token: rawItem } : rawItem;
    if (!isObject(item)) return;
    const token = stringValue(item.token);
    if (!token) return;
    const iconUri = iconUriForToken(token, iconTheme);
    const colorToken = stringValue(item.color);
    resolvedItems.push({
      id: stringValue(item.id, token),
      token,
      label: stringValue(item.label, token),
      order: numberValue(item.order, index * 10),
      color: colorToken
        ? themeColor(themeTokens, palette, colorToken, colorToken)
        : undefined,
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
  themeTokens: Record<string, unknown> = {},
  palette: Map<string, string> = new Map(),
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
      themeTokens,
      palette,
    );
  }
  return iconItemsForState(rawItems, [], iconTheme, themeTokens, palette);
}

function iconItemsSource(primary: unknown, fallback: unknown): unknown {
  return Array.isArray(primary) && primary.length === 0
    ? fallback
    : primary ?? fallback;
}

function resolveTextInputBarInheritance(
  value: Record<string, unknown>,
  themeTokens: Record<string, unknown>,
  scale: number,
): Record<string, unknown> {
  return {
    ...value,
    ...resolveJsonFieldBindingGroup(CHAT_TEXT_INPUT_BAR_TOKEN_BINDINGS, [
      value,
      unscaleTextInputBarThemeScope(themeTokens, scale),
    ]),
  };
}

function resolveTextInputBarDefinition(
  behaviorTextInputBar: unknown,
  iconTheme?: {
    asset_root: string;
    mapping_json: Record<string, unknown>;
  },
  scale = 1,
  viewportWidth = 390 * scale,
  themeTokens: Record<string, unknown> = {},
  palette: Map<string, string> = new Map(),
) {
  const root = resolveTextInputBarInheritance(
    isObject(behaviorTextInputBar) ? behaviorTextInputBar : {},
    themeTokens,
    scale,
  );
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
    typing: [{ token: "chat_send", order: 10, color: "icons.accent" }],
  };
  const leftItems = iconItemsForState(
    rawLeftSets[state],
    defaultLeftItems[state],
    iconTheme,
    themeTokens,
    palette,
  );
  const rightItems = iconItemsForState(
    rawRightSets[state],
    defaultRightItems[state],
    iconTheme,
    themeTokens,
    palette,
  );
  const baseFontSize = 17;
  const cursorWidth = numberValue(root.cursorWidth, 2);
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
    fieldRadius: numberValue(root.fieldRadius, 20),
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
    idleTextColor: themeColor(
      themeTokens,
      palette,
      stringValue(root.idleTextColor, "icons.secondary"),
      "",
    ),
    cursorWidth: cursorWidth * scale,
    cursorBlinkFrames: Math.max(1, numberValue(root.cursorBlinkFrames, 15)),
    cursorColor: themeColor(
      themeTokens,
      palette,
      stringValue(root.cursorColor, "theme.cursor.color"),
      "",
    ),
    fieldShadowEnabled: root.fieldShadowEnabled !== false,
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

function resolvedFontWeightToken(value: unknown): string | number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    return fontWeightForProductionStyle(value);
  }
  return undefined;
}

function resolvedFontStyleToken(
  fontStyle: unknown,
  fontWeight: unknown,
): "normal" | "italic" {
  if (fontStyle === "italic" || fontStyle === "normal") return fontStyle;
  if (typeof fontWeight === "string") {
    return fontStyleForProductionStyle(fontWeight);
  }
  return "normal";
}

function clampUnit(value: number) {
  return Math.max(0, Math.min(1, value));
}

function normalizeHue(value: number) {
  return ((value % 360) + 360) % 360;
}

function hexToRgb(value: string) {
  const normalized = value.replace("#", "");
  return {
    red: Number.parseInt(normalized.slice(0, 2), 16) / 255,
    green: Number.parseInt(normalized.slice(2, 4), 16) / 255,
    blue: Number.parseInt(normalized.slice(4, 6), 16) / 255,
  };
}

function rgbToHex(red: number, green: number, blue: number) {
  const toHex = (value: number) =>
    Math.round(clampUnit(value) * 255)
      .toString(16)
      .padStart(2, "0")
      .toUpperCase();
  return `#${toHex(red)}${toHex(green)}${toHex(blue)}`;
}

function rgbToHsl({
  red,
  green,
  blue,
}: {
  red: number;
  green: number;
  blue: number;
}) {
  const max = Math.max(red, green, blue);
  const min = Math.min(red, green, blue);
  const lightness = (max + min) / 2;
  if (max === min) return { hue: 0, saturation: 0, lightness };
  const delta = max - min;
  const saturation =
    lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
  let hue =
    max === red
      ? (green - blue) / delta + (green < blue ? 6 : 0)
      : max === green
        ? (blue - red) / delta + 2
        : (red - green) / delta + 4;
  hue *= 60;
  return { hue, saturation, lightness };
}

function hslToRgb({
  hue,
  saturation,
  lightness,
}: {
  hue: number;
  saturation: number;
  lightness: number;
}) {
  const normalizedHue = normalizeHue(hue) / 360;
  if (saturation <= 0) {
    return { red: lightness, green: lightness, blue: lightness };
  }
  const hueToRgb = (p: number, q: number, t: number) => {
    let next = t;
    if (next < 0) next += 1;
    if (next > 1) next -= 1;
    if (next < 1 / 6) return p + (q - p) * 6 * next;
    if (next < 1 / 2) return q;
    if (next < 2 / 3) return p + (q - p) * (2 / 3 - next) * 6;
    return p;
  };
  const q =
    lightness < 0.5
      ? lightness * (1 + saturation)
      : lightness + saturation - lightness * saturation;
  const p = 2 * lightness - q;
  return {
    red: hueToRgb(p, q, normalizedHue + 1 / 3),
    green: hueToRgb(p, q, normalizedHue),
    blue: hueToRgb(p, q, normalizedHue - 1 / 3),
  };
}

function neutralTintFromTokens(tokens: Record<string, unknown>) {
  const tint = isObject(tokens.neutralTint) ? tokens.neutralTint : {};
  return {
    hueDeg: normalizeHue(numberValue(tint.hueDeg, 0)),
    saturation: clampUnit(numberValue(tint.saturation, 0)),
  };
}

function hasActiveNeutralTint(tokens: Record<string, unknown>) {
  const tint = isObject(tokens.neutralTint) ? tokens.neutralTint : undefined;
  if (!tint) return false;
  return clampUnit(numberValue(tint.saturation, 0)) > 0;
}

function neutralTintFromThemeAndAppTokens(
  themeTokens: Record<string, unknown>,
  appTokens: Record<string, unknown>,
) {
  return neutralTintFromTokens(
    hasActiveNeutralTint(appTokens)
      ? mergeTokenObjects(themeTokens, appTokens)
      : themeTokens,
  );
}

function tintedNeutralHex(valueHex: string, tint: { hueDeg: number; saturation: number }) {
  if (tint.saturation <= 0) return valueHex.toUpperCase();
  const hsl = rgbToHsl(hexToRgb(valueHex));
  const rgb = hslToRgb({
    hue: tint.hueDeg,
    saturation: tint.saturation,
    lightness: hsl.lightness,
  });
  return rgbToHex(rgb.red, rgb.green, rgb.blue);
}

function paletteMapForColors(
  colors: PaletteColor[],
  neutralTint: { hueDeg: number; saturation: number } = {
    hueDeg: 0,
    saturation: 0,
  },
) {
  return new Map(
    colors.map((color) => [
      color.token,
      color.is_neutral
        ? tintedNeutralHex(color.value_hex, neutralTint)
        : color.value_hex,
    ]),
  );
}

function clampAlpha(value: unknown) {
  return typeof value === "number" && Number.isFinite(value)
    ? Math.max(0, Math.min(1, value))
    : 1;
}

function rgbaFromHex(hex: string, alpha: number) {
  const normalized = hex.replace("#", "");
  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red},${green},${blue},${clampAlpha(alpha)})`;
}

function resolvePaletteTokenReferences(
  value: unknown,
  palette: Map<string, string>,
): unknown {
  if (Array.isArray(value)) {
    return value.map((entry) => resolvePaletteTokenReferences(entry, palette));
  }
  if (isObject(value)) {
    if (typeof value.color === "string" && typeof value.alpha === "number") {
      const resolvedColor = palette.get(value.color) ?? value.color;
      if (/^#[0-9a-fA-F]{6}$/.test(resolvedColor)) {
        return rgbaFromHex(resolvedColor, value.alpha);
      }
      return resolvedColor;
    }
    return Object.fromEntries(
      Object.entries(value).map(([key, entry]) => [
        key,
        resolvePaletteTokenReferences(entry, palette),
      ]),
    );
  }
  if (typeof value === "string") {
    return palette.get(value) ?? value;
  }
  return value;
}

function resolveDefaultAvatarComponent(
  repository: DomainRepository,
  productionId: string,
  palette: Map<string, string>,
  renderScale: number,
  themeTokens: Record<string, unknown>,
) {
  const component =
    repository
      .getComponentClasses(productionId, "avatar")
      .find((entry) => entry.name === "Default avatar") ??
    repository.getComponentClasses(productionId, "avatar")[0];
  const rawTokens = isObject(component?.tokens_json)
    ? component.tokens_json
    : {};
  const resolvedTokens = resolvePaletteTokenReferences(
    rawTokens,
    palette,
  ) as Record<string, unknown>;
  const shadows = isObject(themeTokens.shadows) ? themeTokens.shadows : {};
  const shadow = isObject(shadows.default) ? shadows.default : {};
  const borderColorToken = stringValue(
    rawTokens.borderColorToken,
    "borders.primary",
  );
  const borderColor = themeColor(
    themeTokens,
    palette,
    borderColorToken,
    "#D1D1D6",
  );

  return {
    id: component?.id ?? null,
    name: component?.name ?? "Default avatar",
    componentType: "avatar",
    cornerRadius: numberValue(resolvedTokens.cornerRadius, 12) * renderScale,
    borderWidth: numberValue(resolvedTokens.borderWidth, 0) * renderScale,
    borderColor,
    shadowEnabled: resolvedTokens.shadowEnabled === true,
    surfaceReliefEnabled: resolvedTokens.surfaceReliefEnabled !== false,
    shadow,
  };
}

function resolveDefaultComponentTokens(
  repository: DomainRepository,
  productionId: string,
  componentType: "textInputBar" | "keyboard",
  palette: Map<string, string>,
) {
  const component =
    repository.getComponentClasses(productionId, componentType)[0];
  const rawTokens = isObject(component?.tokens_json) ? component.tokens_json : {};
  const tokens = resolvePaletteTokenReferences(rawTokens, palette) as Record<
    string,
    unknown
  >;
  return {
    id: component?.id ?? null,
    name: component?.name ?? "",
    componentType,
    tokens,
  };
}

function resolveDefaultButtonIconComponent(
  repository: DomainRepository,
  productionId: string,
  themeMode: "light" | "dark",
  palette: Map<string, string>,
  renderScale: number,
  themeTokens: Record<string, unknown>,
) {
  const component =
    repository
      .getComponentClasses(productionId, "buttonIcon")
      .find((entry) => entry.name === "Default icon button") ??
    repository.getComponentClasses(productionId, "buttonIcon")[0];
  const rawTokens = isObject(component?.tokens_json)
    ? component.tokens_json
    : {};
  const resolvedTokens = resolvePaletteTokenReferences(
    rawTokens,
    palette,
  ) as Record<string, unknown>;
  const shadows = isObject(themeTokens.shadows) ? themeTokens.shadows : {};
  const shadow = isObject(shadows.default) ? shadows.default : {};
  const borderColorToken = stringValue(
    rawTokens.borderColorToken,
    "borders.primary",
  );
  const borderColor = themeColor(
    themeTokens,
    palette,
    borderColorToken,
    "#D1D1D6",
  );
  const labelColorToken = stringValue(
    rawTokens.labelColorToken,
    "icons.primary",
  );
  const labelColor = themeColor(
    themeTokens,
    palette,
    labelColorToken,
    "#000000",
  );

  return {
    id: component?.id ?? null,
    name: component?.name ?? "Default icon button",
    componentType: "buttonIcon",
    cornerRadius: numberValue(resolvedTokens.cornerRadius, 0) * renderScale,
    iconPadding: numberValue(resolvedTokens.iconPadding, 2) * renderScale,
    borderWidth: numberValue(resolvedTokens.borderWidth, 0) * renderScale,
    borderColor,
    shadowEnabled: resolvedTokens.shadowEnabled === true,
    shadow,
    surfaceReliefEnabled: resolvedTokens.surfaceReliefEnabled === true,
    labelEnabled: resolvedTokens.labelEnabled === true,
    labelPosition:
      stringValue(resolvedTokens.labelPosition, "bottom") === "top"
        ? "top"
        : "bottom",
    labelPadding: numberValue(resolvedTokens.labelPadding, 2) * renderScale,
    labelSize: numberValue(resolvedTokens.labelSize, 10) * renderScale,
    labelColor,
  };
}

function resolveDefaultLabelComponent(
  repository: DomainRepository,
  productionId: string,
  palette: Map<string, string>,
  renderScale: number,
  themeTokens: Record<string, unknown>,
) {
  const component =
    repository
      .getComponentClasses(productionId, "label")
      .find((entry) => entry.name === "Default label") ??
    repository.getComponentClasses(productionId, "label")[0];
  const rawTokens = isObject(component?.tokens_json) ? component.tokens_json : {};
  const componentOverrides = isObject(themeTokens.componentOverrides)
    ? themeTokens.componentOverrides
    : {};
  const labelOverrides = isObject(componentOverrides.label)
    ? componentOverrides.label
    : {};
  const tokens = {
    ...rawTokens,
    ...labelOverrides,
  };
  const shadows = isObject(themeTokens.shadows) ? themeTokens.shadows : {};
  const surfaceRelief = isObject(themeTokens.surfaceRelief)
    ? themeTokens.surfaceRelief
    : {};
  const borderColorToken = stringValue(tokens.borderColorToken, "borders.primary");
  const backgroundColorToken = stringValue(tokens.backgroundColorToken, "background");
  const textColorToken = stringValue(tokens.textColorToken, "textPrimary");
  const themeFonts = isObject(themeTokens.fonts) ? themeTokens.fonts : {};
  const hasLocalFontStyle =
    Object.hasOwn(tokens, "fontStyle") || Object.hasOwn(tokens, "fontWeight");

  return {
    id: component?.id ?? null,
    name: component?.name ?? "Default label",
    componentType: "label",
    sizingMode:
      stringValue(tokens.sizingMode, "content") === "fixed"
        ? "fixed"
        : "content",
    width: numberValue(tokens.width, 120) * renderScale,
    height: numberValue(tokens.height, 28) * renderScale,
    paddingX: numberValue(tokens.paddingX, 8) * renderScale,
    paddingY: numberValue(tokens.paddingY, 4) * renderScale,
    cornerRadius: numberValue(tokens.cornerRadius, 10) * renderScale,
    borderWidth: numberValue(tokens.borderWidth, 0) * renderScale,
    borderColor: themeColor(themeTokens, palette, borderColorToken, "#D1D1D6"),
    backgroundColor: themeColor(
      themeTokens,
      palette,
      backgroundColorToken,
      "#F2F2F7",
    ),
    textColor: themeColor(themeTokens, palette, textColorToken, "#1D1D1F"),
    fontFamily: stringValue(tokens.fontFamily, stringValue(themeFonts.family, "system-ui")),
    fontStyle: hasLocalFontStyle
      ? resolvedFontStyleToken(tokens.fontStyle, tokens.fontWeight)
      : resolvedFontStyleToken(themeFonts.fontStyle, themeFonts.fontWeight ?? themeFonts.weight),
    fontSize: numberValue(tokens.fontSize, 12) * renderScale,
    fontWeight:
      resolvedFontWeightToken(tokens.fontWeight) ??
      resolvedFontWeightToken(themeFonts.fontWeight ?? themeFonts.weight) ??
      400,
    shadowEnabled: tokens.shadowEnabled === true,
    shadow: isObject(shadows.default) ? shadows.default : {},
    surfaceReliefEnabled: tokens.surfaceReliefEnabled === true,
    surfaceRelief: isObject(surfaceRelief.default) ? surfaceRelief.default : {},
  };
}

function themeColor(
  themeTokens: Record<string, unknown>,
  palette: Map<string, string>,
  token: string,
  fallback: string,
) {
  const colors = isObject(themeTokens.colors) ? themeTokens.colors : {};
  if (typeof colors[token] === "string") {
    const colorToken = colors[token];
    return palette.get(colorToken) ?? colorToken;
  }
  const scopedValue = token.includes(".")
    ? getNestedValue(themeTokens, token.split("."))
    : undefined;
  if (typeof scopedValue === "string") return palette.get(scopedValue) ?? scopedValue;
  return palette.get(token) ?? fallback;
}

function themeRadius(
  themeTokens: Record<string, unknown>,
  token: unknown,
  fallback: number,
) {
  const radii = isObject(themeTokens.radii) ? themeTokens.radii : {};
  const key = stringValue(token, "");
  const scopedKey = key.startsWith("radii.") ? key.slice("radii.".length) : key;
  const value =
    typeof radii[scopedKey] === "number"
      ? radii[scopedKey]
      : key.includes(".")
        ? getNestedValue(themeTokens, key.split("."))
        : undefined;
  return numberValue(value, fallback);
}

function resolveSurfaceStyleToken(
  value: unknown,
  {
    themeTokens,
    palette,
    renderScale,
    fallbackCornerRadius = 0,
    fallbackBorderColor = "#D1D1D6",
  }: {
    themeTokens: Record<string, unknown>;
    palette: Map<string, string>;
    renderScale: number;
    fallbackCornerRadius?: number;
    fallbackBorderColor?: string;
  },
) {
  const style = surfaceStyleNormalize(value);
  const borderColorToken = stringValue(
    style.borderColorToken,
    "borders.primary",
  );
  return {
    shadowEnabled: style.shadowEnabled === true,
    surfaceReliefEnabled: style.surfaceReliefEnabled === true,
    borderWidth: numberValue(style.borderWidth, 0) * renderScale,
    borderColor: themeColor(themeTokens, palette, borderColorToken, fallbackBorderColor),
    cornerRadius:
      themeRadius(themeTokens, style.cornerRadiusToken, fallbackCornerRadius) *
      renderScale,
    surfaceRelief: isObject(style.surfaceRelief)
      ? style.surfaceRelief
      : {},
  };
}

function colorWithAlpha(color: string, alpha: number) {
  const safeAlpha = Math.max(0, Math.min(1, alpha));
  if (/^#[0-9a-fA-F]{6}$/.test(color)) {
    const red = parseInt(color.slice(1, 3), 16);
    const green = parseInt(color.slice(3, 5), 16);
    const blue = parseInt(color.slice(5, 7), 16);
    return `rgba(${red}, ${green}, ${blue}, ${safeAlpha})`;
  }
  return color;
}

function resolveDefaultAudioMessageComponent(
  repository: DomainRepository,
  productionId: string,
  iconTheme:
    | {
        asset_root: string;
        mapping_json: Record<string, unknown>;
      }
    | undefined,
  renderScale: number,
  themeTokens: Record<string, unknown>,
  palette: Map<string, string>,
) {
  const component =
    repository
      .getComponentClasses(productionId, "audio")
      .find((entry) => entry.name === "Default audio message") ??
    repository.getComponentClasses(productionId, "audio")[0];
  const tokens = isObject(component?.tokens_json) ? component.tokens_json : {};
  const shadows = isObject(themeTokens.shadows) ? themeTokens.shadows : {};
  const surfaceRelief = isObject(themeTokens.surfaceRelief)
    ? themeTokens.surfaceRelief
    : {};
  const playCircleColorToken = stringValue(tokens.playCircleColorToken, "icons.accent");
  const playIconColorToken = stringValue(tokens.playIconColorToken, "icons.secondary");
  const borderColorToken = stringValue(tokens.borderColorToken, "borders.primary");
  const waveformColorToken = stringValue(tokens.waveformColorToken, "icons.primary");
  const waveformPlayedColorToken = stringValue(
    tokens.waveformPlayedColorToken,
    "icons.accent",
  );
  const textColorToken = stringValue(tokens.textColorToken, "icons.secondary");
  const microphoneBadgeIconToken = stringValue(
    tokens.microphoneBadgeIconToken,
    "media_mic",
  );

  return {
    id: component?.id ?? null,
    name: component?.name ?? "Default audio message",
    componentType: "audio",
    width: numberValue(tokens.width, 260) * renderScale,
    height: numberValue(tokens.height, 58) * renderScale,
    avatarSize: numberValue(tokens.avatarSize, 38) * renderScale,
    avatarPosition:
      stringValue(tokens.avatarPosition, "left") === "right" ? "right" : "left",
    avatarGap: numberValue(tokens.avatarGap, 8) * renderScale,
    playCircleSize: numberValue(tokens.playCircleSize, 32) * renderScale,
    playCircleColor: themeColor(themeTokens, palette, playCircleColorToken, "#007AFF"),
    playIconColor: themeColor(themeTokens, palette, playIconColorToken, "#FFFFFF"),
    microphoneBadgeSize: numberValue(tokens.microphoneBadgeSize, 16) * renderScale,
    microphoneBadgeIconToken,
    microphoneBadgeIconUri: iconUriForToken(microphoneBadgeIconToken, iconTheme),
    waveformBarCount: Math.max(
      4,
      Math.round(numberValue(tokens.waveformBarCount, 28)),
    ),
    waveformGap: numberValue(tokens.waveformGap, 2) * renderScale,
    waveformMinHeight: numberValue(tokens.waveformMinHeight, 4) * renderScale,
    waveformMaxHeight: numberValue(tokens.waveformMaxHeight, 22) * renderScale,
    progressKnobSize: numberValue(tokens.progressKnobSize, 9) * renderScale,
    waveformColor: themeColor(themeTokens, palette, waveformColorToken, "#8E8E93"),
    waveformPlayedColor: themeColor(
      themeTokens,
      palette,
      waveformPlayedColorToken,
      "#007AFF",
    ),
    textSize: numberValue(tokens.textSize, 11) * renderScale,
    textColor: themeColor(themeTokens, palette, textColorToken, "#8E8E93"),
    cornerRadius: numberValue(tokens.cornerRadius, 18) * renderScale,
    borderWidth: numberValue(tokens.borderWidth, 0) * renderScale,
    borderColor: themeColor(themeTokens, palette, borderColorToken, "#D1D1D6"),
    shadowEnabled: tokens.shadowEnabled === true,
    shadow: isObject(shadows.default) ? shadows.default : {},
    surfaceReliefEnabled: tokens.surfaceReliefEnabled === true,
    surfaceRelief: isObject(surfaceRelief.default) ? surfaceRelief.default : {},
  };
}

function resolveDefaultMediaMessageComponent(
  repository: DomainRepository,
  productionId: string,
  iconTheme:
    | {
        asset_root: string;
        mapping_json: Record<string, unknown>;
      }
    | undefined,
  renderScale: number,
  themeTokens: Record<string, unknown>,
  palette: Map<string, string>,
) {
  const component =
    repository
      .getComponentClasses(productionId, "media")
      .find((entry) => entry.name === "Default media message") ??
    repository.getComponentClasses(productionId, "media")[0];
  const tokens = isObject(component?.tokens_json) ? component.tokens_json : {};
  const shadows = isObject(themeTokens.shadows) ? themeTokens.shadows : {};
  const surfaceRelief = isObject(themeTokens.surfaceRelief)
    ? themeTokens.surfaceRelief
    : {};
  const borderColorToken = stringValue(tokens.borderColorToken, "borders.primary");
  const playCircleColorToken = stringValue(tokens.playCircleColorToken, "icons.accent");
  const playIconColorToken = stringValue(tokens.playIconColorToken, "icons.secondary");
  const statusColorToken = stringValue(tokens.statusColorToken, "icons.secondary");
  const statusIconToken = stringValue(tokens.statusIconToken, "media_video");
  const playCircleColor = themeColor(
    themeTokens,
    palette,
    playCircleColorToken,
    "#000000",
  );

  return {
    id: component?.id ?? null,
    name: component?.name ?? "Default media message",
    componentType: "media",
    cornerRadius: numberValue(tokens.cornerRadius, 18) * renderScale,
    borderWidth: numberValue(tokens.borderWidth, 0) * renderScale,
    borderColor: themeColor(themeTokens, palette, borderColorToken, "#D1D1D6"),
    shadowEnabled: tokens.shadowEnabled === true,
    shadow: isObject(shadows.default) ? shadows.default : {},
    surfaceReliefEnabled: tokens.surfaceReliefEnabled === true,
    surfaceRelief: isObject(surfaceRelief.default) ? surfaceRelief.default : {},
    playOverlayEnabled: tokens.playOverlayEnabled !== false,
    playCircleSize: numberValue(tokens.playCircleSize, 44) * renderScale,
    playCircleColor: colorWithAlpha(
      playCircleColor,
      numberValue(tokens.playCircleAlpha, 0.55),
    ),
    playIconColor: themeColor(themeTokens, palette, playIconColorToken, "#FFFFFF"),
    statusVisible: tokens.statusVisible !== false,
    statusIconToken,
    statusIconUri: iconUriForToken(statusIconToken, iconTheme),
    statusSize: numberValue(tokens.statusSize, 12) * renderScale,
    statusPaddingX: numberValue(tokens.statusPaddingX, 8) * renderScale,
    statusPaddingY: numberValue(tokens.statusPaddingY, 6) * renderScale,
    statusGap: numberValue(tokens.statusGap, 4) * renderScale,
    statusColor: themeColor(themeTokens, palette, statusColorToken, "#FFFFFF"),
  };
}

export function moduleTypographyDefaultsFromFonts(
  tokens: Record<string, unknown>,
): Record<string, unknown> {
  const fonts = isObject(tokens.fonts) ? tokens.fonts : {};
  const fontFamily = stringToken(fonts.family);
  const bodySize = numericToken(fonts.bodySize);
  const bodyLineHeight = numericToken(fonts.bodyLineHeight);
  const captionSize = numericToken(fonts.captionSize);
  const weight = resolvedFontWeightToken(fonts.fontWeight ?? fonts.weight);
  const style = resolvedFontStyleToken(fonts.fontStyle, fonts.fontWeight ?? fonts.weight);

  if (
    !fontFamily &&
    bodySize === undefined &&
    captionSize === undefined &&
    weight === undefined &&
    style === undefined
  ) {
    return {};
  }

  return {
    typography: {
      message: {
        ...(fontFamily ? { fontFamily } : {}),
        ...(bodySize !== undefined ? { fontSize: bodySize } : {}),
        ...(bodyLineHeight !== undefined ? { lineHeight: bodyLineHeight } : {}),
        ...(weight !== undefined ? { fontWeight: weight } : {}),
        ...(style !== undefined ? { fontStyle: style } : {}),
      },
      headerTitle: {
        ...(fontFamily ? { fontFamily } : {}),
        ...(bodySize !== undefined ? { fontSize: bodySize } : {}),
        ...(bodyLineHeight !== undefined ? { lineHeight: bodyLineHeight } : {}),
        ...(weight !== undefined ? { fontWeight: weight } : {}),
        ...(style !== undefined ? { fontStyle: style } : {}),
      },
      headerSubtitle: {
        ...(fontFamily ? { fontFamily } : {}),
        ...(captionSize !== undefined ? { fontSize: captionSize } : {}),
        ...(captionSize !== undefined ? { lineHeight: captionSize * 1.2308 } : {}),
        ...(weight !== undefined ? { fontWeight: weight } : {}),
        ...(style !== undefined ? { fontStyle: style } : {}),
      },
    },
  };
}

function resolveChatTypographyTokens(
  genericTokens: Record<string, unknown>,
  moduleDefaultsFromGenericTokens: Record<string, unknown>,
  moduleThemeTokens: Record<string, unknown>,
): Record<string, unknown> {
  return resolveJsonFieldBindingGroup(CHAT_TYPOGRAPHY_TOKEN_BINDINGS, [
    stripChatModuleTypographyFontIdentity(moduleThemeTokens),
    moduleDefaultsFromGenericTokens,
    genericTokens,
  ]);
}

function resolveChatHeaderTokens(
  genericTokens: Record<string, unknown>,
  moduleThemeTokens: Record<string, unknown>,
): Record<string, unknown> {
  const header = resolveJsonFieldBindingGroup(CHAT_HEADER_TOKEN_BINDINGS, [
    isObject(moduleThemeTokens.header) ? moduleThemeTokens.header : {},
    isObject(genericTokens.header) ? genericTokens.header : {},
  ]);
  return {
    header,
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
  const { color: _legacyCursorColor, ...cursorTokens } = cursor;
  const colors = isObject(merged.colors) ? merged.colors : {};
  const neutralTint = isObject(merged.neutralTint) ? merged.neutralTint : {};
  return {
    ...merged,
    neutralTint: {
      hueDeg: 0,
      saturation: 0,
      ...neutralTint,
    },
    colors: {
      "icons.primary": "gray_000",
      "icons.secondary": "gray_040",
      "icons.accent": "blue",
      "borders.primary": "gray_080",
      "borders.secondary": "gray_070",
      "borders.alternate": "gray_090",
      "theme.cursor.color": "gray_000",
      ...colors,
    },
    cursor: {
      style: "bar",
      width: 2,
      blinkFrames: 15,
      ...cursorTokens,
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
  const chatBubbleStyle = surfaceStyleNormalize({
    ...(isObject(chatBubbles.style) ? chatBubbles.style : {}),
    ...(typeof chatBubbles.shadowEnabled === "boolean"
      ? { shadowEnabled: chatBubbles.shadowEnabled }
      : {}),
    ...(typeof chatBubbles.surfaceReliefEnabled === "boolean"
      ? { surfaceReliefEnabled: chatBubbles.surfaceReliefEnabled }
      : {}),
    ...(typeof chatBubbles.cornerRadiusToken === "string"
      ? { cornerRadiusToken: chatBubbles.cornerRadiusToken }
      : {}),
    ...(isObject(chatBubbles.surfaceRelief)
      ? { surfaceRelief: chatBubbles.surfaceRelief }
      : {}),
  });
  const chatBubbleMediaStyle = surfaceStyleNormalize({
    ...(isObject(chatBubbleMedia.style) ? chatBubbleMedia.style : {}),
    ...(typeof chatBubbleMedia.borderWidth === "number"
      ? { borderWidth: chatBubbleMedia.borderWidth }
      : {}),
    ...(typeof chatBubbleMedia.cornerRadiusToken === "string"
      ? { cornerRadiusToken: chatBubbleMedia.cornerRadiusToken }
      : typeof chatBubbles.cornerRadiusToken === "string"
        ? { cornerRadiusToken: chatBubbles.cornerRadiusToken }
        : {}),
    ...(typeof chatBubbleMedia.shadowEnabled === "boolean"
      ? { shadowEnabled: chatBubbleMedia.shadowEnabled }
      : {}),
    ...(typeof chatBubbleMedia.surfaceReliefEnabled === "boolean"
      ? { surfaceReliefEnabled: chatBubbleMedia.surfaceReliefEnabled }
      : {}),
  });
  const chatBubbleAvatar = isObject(chatBubbles.avatar) ? chatBubbles.avatar : {};
  const chatBubbleAvatarStyle = surfaceStyleNormalize({
    cornerRadiusToken: "radii.avatar",
    ...(isObject(chatBubbleAvatar.style) ? chatBubbleAvatar.style : {}),
  });
  const messageLabelStyle = surfaceStyleNormalize(
    isObject(chatBubbles.messageLabelStyle)
      ? chatBubbles.messageLabelStyle
      : {},
  );
  const { shadow: _chatBubbleShadow, ...visibleChatBubbles } = chatBubbles;
  const shadows = isObject(tokens.shadows) ? tokens.shadows : {};
  const headerAvatarSize = numberValue(
    header.avatarSize,
    CHAT_HEADER_DEFAULTS.avatarSize,
  );
  const headerLeftIconTokens =
    typeof header.leftIconTokens === "string" && header.leftIconTokens.trim()
      ? header.leftIconTokens
      : CHAT_HEADER_DEFAULTS.leftIconTokens;
  const headerRightIconTokens =
    typeof header.rightIconTokens === "string" && header.rightIconTokens.trim()
      ? header.rightIconTokens
      : CHAT_HEADER_DEFAULTS.rightIconTokens;
  const bubbleAvatarSize = numberValue(
    chatBubbles.avatarSize,
    32,
  );
  const bubbleAvatarGap = numberValue(
    chatBubbles.avatarGap,
    8,
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
      height: numberValue(header.height, CHAT_HEADER_DEFAULTS.height),
      separatorWidth: numberValue(
        header.separatorWidth,
        CHAT_HEADER_DEFAULTS.separatorWidth,
      ),
      avatarSize: headerAvatarSize,
      subtitleBottomPadding: numberValue(
        header.subtitleBottomPadding,
        CHAT_HEADER_DEFAULTS.subtitleBottomPadding,
      ),
      elementGap: numberValue(
        header.elementGap,
        CHAT_HEADER_DEFAULTS.elementGap,
      ),
      sidePadding: numberValue(
        header.sidePadding,
        numberValue(header.elementGap, CHAT_HEADER_DEFAULTS.sidePadding),
      ),
      iconSize: numberValue(header.iconSize, CHAT_HEADER_DEFAULTS.iconSize),
      leftIconTokens: headerLeftIconTokens,
      rightIconTokens: headerRightIconTokens,
    },
    chatBubbles: {
      ...visibleChatBubbles,
      avatarSize: bubbleAvatarSize,
      avatarGap: bubbleAvatarGap,
      style: chatBubbleStyle,
      radius: themeRadius(tokens, chatBubbleStyle.cornerRadiusToken, 18),
      shadowEnabled: chatBubbleStyle.shadowEnabled === true,
      surfaceReliefEnabled: chatBubbleStyle.surfaceReliefEnabled === true,
      contentMetaGap: numberValue(chatBubbles.contentMetaGap, 4),
      avatar: {
        ...chatBubbleAvatar,
        alignment:
          chatBubbleAvatar.alignment === "top" ||
          chatBubbleAvatar.alignment === "center"
            ? chatBubbleAvatar.alignment
            : "bottom",
        offsetX: numberValue(chatBubbleAvatar.offsetX, 0),
        offsetY: numberValue(chatBubbleAvatar.offsetY, 0),
        style: chatBubbleAvatarStyle,
      },
      messageLabelUseActorColor:
        typeof chatBubbles.messageLabelUseActorColor === "boolean"
          ? chatBubbles.messageLabelUseActorColor
          : true,
      messageLabelOffsetX: numberValue(chatBubbles.messageLabelOffsetX, 0),
      messageLabelOffsetY: numberValue(chatBubbles.messageLabelOffsetY, 0),
      messageLabelStyle,
      media: {
        ...chatBubbleMedia,
        style: chatBubbleMediaStyle,
        borderWidth: numberValue(chatBubbleMediaStyle.borderWidth, 0),
        cornerRadius: themeRadius(tokens, chatBubbleMediaStyle.cornerRadiusToken, 18),
        borderColor:
          typeof chatBubbleMedia.borderColor === "string"
            ? chatBubbleMedia.borderColor
            : "transparent",
        shadowEnabled: chatBubbleMediaStyle.shadowEnabled === true,
        surfaceReliefEnabled: chatBubbleMediaStyle.surfaceReliefEnabled === true,
      },
      systemBackground,
      systemText,
    },
    shadows: {
      ...shadows,
      default: isObject(shadows.default) ? shadows.default : {},
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
  ...CHAT_HEADER_DESIGN_UNIT_PATHS,
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
  ["chatBubbles", "contentMetaGap"],
  ["chatBubbles", "avatarSize"],
  ["chatBubbles", "avatarGap"],
  ["chatBubbles", "style", "borderWidth"],
  ["chatBubbles", "avatar", "offsetX"],
  ["chatBubbles", "avatar", "offsetY"],
  ["chatBubbles", "avatar", "style", "borderWidth"],
  ["chatBubbles", "messageLabelStyle", "borderWidth"],
  ["chatBubbles", "media", "style", "borderWidth"],
  ["chatBubbles", "tail", "width"],
  ["chatBubbles", "tail", "height"],
  ["chatBubbles", "status", "size"],
  ["chatBubbles", "status", "gap"],
  ["chatBubbles", "status", "offsetX"],
  ["chatBubbles", "status", "offsetY"],
  ["shadows", "default", "offsetX"],
  ["shadows", "default", "offsetY"],
  ["shadows", "default", "blur"],
  ["radii", "control"],
  ["radii", "card"],
  ["radii", "panel"],
  ["radii", "surface"],
  ["radii", "pill"],
  ["radii", "avatar"],
  ["radii", "full"],
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

function keyboardInstanceOverrides(value: unknown) {
  if (!isObject(value)) return {};
  const next = { ...value };
  delete next.fontFamily;
  delete next.fontWeight;
  delete next.fontStyle;
  return next;
}

function textInputBarInstanceOverrides(value: unknown) {
  if (!isObject(value)) return {};
  const next = { ...value };
  delete next.fontFamily;
  delete next.fontWeight;
  delete next.fontStyle;
  return next;
}

function textInputBarComponentBase(
  componentTokens: unknown,
) {
  return isObject(componentTokens) ? { ...componentTokens } : {};
}

function swapBoxSize<T extends { width: number; height: number }>(box: T): T {
  return {
    ...box,
    width: box.height,
    height: box.width,
  };
}

function landscapeMetrics(
  metrics: z.infer<typeof DeviceMetricsSchema>,
): z.infer<typeof DeviceMetricsSchema> {
  return {
    ...metrics,
    ...(metrics.designSpace
      ? { designSpace: swapBoxSize(metrics.designSpace) }
      : {}),
    ...(metrics.renderSize ? { renderSize: swapBoxSize(metrics.renderSize) } : {}),
    viewport: swapBoxSize(metrics.viewport),
    safeArea: {
      top: metrics.safeArea.left,
      right: metrics.safeArea.top,
      bottom: metrics.safeArea.right,
      left: metrics.safeArea.bottom,
    },
  };
}

function orientedMetrics(
  metrics: z.infer<typeof DeviceMetricsSchema>,
  orientation: "portrait" | "landscape",
): z.infer<typeof DeviceMetricsSchema> {
  if (orientation !== "landscape") return metrics;
  return landscapeMetrics(metrics);
}

function scaleNumericRecord(
  value: Record<string, unknown>,
  factor: number,
  keys: string[],
) {
  const next = { ...value };
  for (const key of keys) {
    if (typeof next[key] === "number") {
      next[key] = next[key] * factor;
    }
  }
  return next;
}

function scaledLowerChrome({
  keyboard,
  textInputBar,
  viewportHeight,
  showKeyboard,
  showTextInputBar,
}: {
  keyboard: Record<string, unknown>;
  textInputBar: Record<string, unknown>;
  viewportHeight: number;
  showKeyboard: boolean;
  showTextInputBar: boolean;
}) {
  if (!showKeyboard || !showTextInputBar) {
    return { keyboard, textInputBar };
  }
  const keyboardLayout = isObject(keyboard.layout) ? keyboard.layout : {};
  const textInputLayout = isObject(textInputBar.layout) ? textInputBar.layout : {};
  const keyboardHeight = numberValue(keyboardLayout.height, 0);
  const textInputHeight = numberValue(textInputLayout.height, 0);
  const maxCombinedHeight = viewportHeight * 0.5;
  const combinedHeight = keyboardHeight + textInputHeight;
  if (combinedHeight <= maxCombinedHeight || combinedHeight <= 0) {
    return { keyboard, textInputBar };
  }
  const factor = maxCombinedHeight / combinedHeight;
  const scaledKeyboardLayout = scaleNumericRecord(keyboardLayout, factor, [
    "height",
    "topPadding",
    "sidePadding",
    "bottomPadding",
    "bottomUtilityHeight",
    "bottomUtilitySidePadding",
    "bottomIconSize",
    "rowGap",
    "keyGap",
    "keyHeight",
    "keyPadding",
    "keyRadius",
    "fontSize",
  ]);
  const scaledTextInputLayout = scaleNumericRecord(textInputLayout, factor, [
    "height",
    "paddingX",
    "paddingY",
    "gap",
    "fieldHeight",
    "lineHeight",
    "fieldPaddingX",
    "fieldPaddingY",
    "fieldRadius",
    "iconSize",
    "fontSize",
    "cursorWidth",
  ]);
  return {
    keyboard: {
      ...keyboard,
      messageGapToTextInput:
        typeof keyboard.messageGapToTextInput === "number"
          ? keyboard.messageGapToTextInput * factor
          : keyboard.messageGapToTextInput,
      layout: scaledKeyboardLayout,
    },
    textInputBar: {
      ...textInputBar,
      cursorWidth:
        typeof textInputBar.cursorWidth === "number"
          ? textInputBar.cursorWidth * factor
          : textInputBar.cursorWidth,
      layout: scaledTextInputLayout,
    },
  };
}

type ChatAnimationInterpolation = "hold" | "linear" | "ease";
type ChatDeliveryStatus = "none" | "sent" | "delivered" | "read" | "failed";

interface ChatAnimationKeyframe {
  frame: number;
  value: unknown;
  interpolation: ChatAnimationInterpolation;
}

function animationInterpolationValue(value: unknown): ChatAnimationInterpolation {
  const interpolation = stringValue(value, "hold");
  return interpolation === "linear" || interpolation === "ease"
    ? interpolation
    : "hold";
}

function deliveryStatusValue(value: unknown): ChatDeliveryStatus {
  return value === "sent" ||
    value === "delivered" ||
    value === "read" ||
    value === "failed"
    ? value
    : "none";
}

function animationTrackKeyframesFromAnimation(
  animationValue: unknown,
  trackKey: string,
): ChatAnimationKeyframe[] {
  const animation = isObject(animationValue) ? animationValue : {};
  const tracks = isObject(animation.tracks) ? animation.tracks : {};
  const track = isObject(tracks[trackKey]) ? tracks[trackKey] : {};
  const keyframes = Array.isArray(track.keyframes) ? track.keyframes : [];
  return keyframes
    .filter(isObject)
    .map((keyframe) => {
      return {
        frame: Math.max(0, Math.round(numberValue(keyframe.frame, 0))),
        value: keyframe.value,
        interpolation: animationInterpolationValue(keyframe.interpolation),
      };
    })
    .filter((keyframe) => Number.isFinite(keyframe.frame))
    .sort((a, b) => a.frame - b.frame);
}

function animationTrackKeyframes(
  message: ChatModuleMessage,
  trackKey: string,
): ChatAnimationKeyframe[] {
  return animationTrackKeyframesFromAnimation(message.animation, trackKey);
}

function easedProgress(progress: number) {
  const safeProgress = Math.max(0, Math.min(1, progress));
  return safeProgress * safeProgress * (3 - 2 * safeProgress);
}

function interpolateTextByTail(
  fromValue: string,
  toValue: string,
  progress: number,
) {
  const fromChars = Array.from(fromValue);
  const toChars = Array.from(toValue);
  let commonPrefixLength = 0;
  while (
    commonPrefixLength < fromChars.length &&
    commonPrefixLength < toChars.length &&
    fromChars[commonPrefixLength] === toChars[commonPrefixLength]
  ) {
    commonPrefixLength += 1;
  }
  const eraseCount = fromChars.length - commonPrefixLength;
  const writeCount = toChars.length - commonPrefixLength;
  const totalEdits = eraseCount + writeCount;
  if (totalEdits <= 0) return toValue;
  const currentEdit = Math.round(totalEdits * Math.max(0, Math.min(1, progress)));
  if (currentEdit <= eraseCount) {
    return fromChars.slice(0, fromChars.length - currentEdit).join("");
  }
  return toChars
    .slice(0, commonPrefixLength + (currentEdit - eraseCount))
    .join("");
}

function animatedValueForMessageTrack(
  message: ChatModuleMessage,
  trackKey: string,
  baseValue: unknown,
  messageLocalFrame: number,
  valueType: "text" | "hold" = "text",
) {
  return animatedValueForAnimationTrack(
    message.animation,
    trackKey,
    baseValue,
    messageLocalFrame,
    valueType,
  );
}

function animatedValueForAnimationTrack(
  animationValue: unknown,
  trackKey: string,
  baseValue: unknown,
  localFrame: number,
  valueType: "text" | "hold" = "text",
) {
  const keyframes = animationTrackKeyframesFromAnimation(animationValue, trackKey);
  if (!keyframes.length) return baseValue;
  const exact = keyframes.find((keyframe) => keyframe.frame === localFrame);
  if (exact) return exact.value;
  const previous = keyframes
    .filter((keyframe) => keyframe.frame < localFrame)
    .at(-1);
  const next = keyframes.find((keyframe) => keyframe.frame > localFrame);
  if (!previous) return baseValue;
  if (valueType === "hold") return previous.value;
  if (!next || next.interpolation === "hold") return previous.value;
  if (typeof previous.value !== "string" || typeof next.value !== "string") {
    return previous.value;
  }
  const span = Math.max(1, next.frame - previous.frame);
  const rawProgress = (localFrame - previous.frame) / span;
  const progress =
    next.interpolation === "ease" ? easedProgress(rawProgress) : rawProgress;
  return interpolateTextByTail(previous.value, next.value, progress);
}

function activeTextAnimationWriteOnWindow(
  message: ChatModuleMessage,
  messageLocalFrame: number,
) {
  const keyframes = animationTrackKeyframes(message, "text");
  const previous = keyframes
    .filter((keyframe) => keyframe.frame <= messageLocalFrame)
    .at(-1);
  const next = keyframes.find((keyframe) => keyframe.frame > messageLocalFrame);
  if (!previous || !next || next.interpolation === "hold") return undefined;
  if (typeof previous.value !== "string" || typeof next.value !== "string") {
    return undefined;
  }
  const durationFrames = next.frame - previous.frame;
  if (durationFrames <= 0) return undefined;
  return {
    startFrame: previous.frame,
    durationFrames,
  };
}

function animatedTextComposerPressedKey(
  message: ChatModuleMessage,
  messageLocalFrame: number,
) {
  const currentValue = animatedValueForMessageTrack(
    message,
    "text",
    message.text,
    messageLocalFrame,
  );
  const previousValue = animatedValueForMessageTrack(
    message,
    "text",
    message.text,
    Math.max(0, messageLocalFrame - 1),
  );
  if (typeof currentValue !== "string" || typeof previousValue !== "string") {
    return "";
  }
  if (currentValue === previousValue) return "";

  const currentCharacters = Array.from(currentValue);
  const previousCharacters = Array.from(previousValue);
  if (currentCharacters.length < previousCharacters.length) {
    return "backspace";
  }
  return normalizeKeyboardDisplayKey(currentCharacters.at(-1) ?? "");
}

function animatedChatMessage(
  message: ChatModuleMessage,
  messageLocalFrame: number,
): ChatModuleMessage {
  const status = isObject(message.status) ? message.status : undefined;
  const animatedText = animatedValueForMessageTrack(
    message,
    "text",
    message.text,
    messageLocalFrame,
  );
  const animatedStatusText = animatedValueForMessageTrack(
    message,
    "status.text",
    status?.text,
    messageLocalFrame,
  );
  const animatedDeliveryStatus = animatedValueForMessageTrack(
    message,
    "status.deliveryStatus",
    status?.deliveryStatus,
    messageLocalFrame,
    "hold",
  );
  const nextStatus = {
    ...(status ?? {}),
    deliveryStatus: deliveryStatusValue(status?.deliveryStatus),
    ...(typeof animatedStatusText === "string" ? { text: animatedStatusText } : {}),
    ...(typeof animatedDeliveryStatus === "string"
      ? { deliveryStatus: deliveryStatusValue(animatedDeliveryStatus) }
      : {}),
  };
  return {
    ...message,
    ...(typeof animatedText === "string" ? { text: animatedText } : {}),
    status: nextStatus,
  };
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
  const textInputBar = {
    ...(isObject(moduleTokens.textInputBar) ? moduleTokens.textInputBar : {}),
    ...(isObject(screenBase.textInputBar) ? screenBase.textInputBar : {}),
    ...(isObject(behavior.textInputBar) ? behavior.textInputBar : {}),
  };
  const keyboard = {
    ...(isObject(moduleTokens.keyboard) ? moduleTokens.keyboard : {}),
    ...(isObject(screenBase.keyboard) ? screenBase.keyboard : {}),
    ...(isObject(behavior.keyboard) ? behavior.keyboard : {}),
  };

  return {
    ...behavior,
    ...(Object.keys(textInputBar).length ? { textInputBar } : {}),
    ...(Object.keys(keyboard).length ? { keyboard } : {}),
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
  palette: Map<string, string>,
): ResolvedChatActor {
  const actor = requireRecord(
    repository.getActor(actorId),
    "Actor",
    actorId,
  );
  const directAvatarUri = actorAvatarUri(actor, themeMode, palette);

  return {
    id: actor.id,
    displayName: actor.display_name,
    ...(directAvatarUri ? { avatarUri: directAvatarUri } : {}),
    ...actorAvatarCrop(actor),
    ...(actorMetadataColor(actor, themeMode, "color", palette) ? {
      color: actorMetadataColor(actor, themeMode, "color", palette),
    } : {}),
    ...(actorMetadataColor(actor, themeMode, "avatarTextColor", palette) ? {
      avatarTextColor: actorMetadataColor(
        actor,
        themeMode,
        "avatarTextColor",
        palette,
      ),
    } : {}),
  };
}

function actorMetadataColor(
  actor: Actor | undefined,
  themeMode: "light" | "dark",
  field: "color" | "avatarTextColor",
  palette: Map<string, string>,
) {
  const metadata = actor?.metadata_json;
  if (!isObject(metadata)) {
    return undefined;
  }
  const modes = metadata.modes;
  if (isObject(modes) && isObject(modes[themeMode])) {
    const modeValue = modes[themeMode]?.[field];
    if (typeof modeValue === "string" && modeValue.trim()) {
      return palette.get(modeValue) ?? modeValue;
    }
  }
  if (field === "color") {
    return typeof metadata.color === "string" && metadata.color.trim()
      ? palette.get(metadata.color) ?? metadata.color
      : undefined;
  }
  const avatar = metadata.avatar;
  if (isObject(avatar) && typeof avatar.textColor === "string" && avatar.textColor.trim()) {
    return palette.get(avatar.textColor) ?? avatar.textColor;
  }
  return undefined;
}

function escapeSvgText(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function actorInitials(actor: Actor | undefined) {
  const shortName = actor?.short_name?.trim() ?? "";
  const source = shortName || (actor?.display_name ?? "");
  const words = source
    .split(/\s+/)
    .map((word) => word.trim())
    .filter(Boolean);
  if (!words.length) return "?";
  return words
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? "")
    .join("");
}

function svgDataUri(svg: string) {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

function actorInitialsAvatarUri(
  actor: Actor | undefined,
  themeMode: "light" | "dark",
  palette: Map<string, string>,
) {
  const metadata = actor?.metadata_json;
  const avatar = isObject(metadata?.avatar) ? metadata.avatar : {};
  const baseSize = Math.max(1, Math.round(numberValue(avatar.baseSize, 640)));
  const padding = Math.max(
    0,
    Math.min(
      baseSize / 2 - 1,
      numberValue(
        avatar.initialsPadding,
        Number(ACTOR_FIELDS.avatarInitialsPadding.defaultValue ?? 8),
      ),
    ),
  );
  const initials = actorInitials(actor);
  const backgroundColor =
    actorMetadataColor(actor, themeMode, "color", palette) ?? "#64748b";
  const textColor =
    actorMetadataColor(actor, themeMode, "avatarTextColor", palette) ??
    "#ffffff";
  const available = Math.max(1, baseSize - padding * 2);
  const fontSize = Math.max(
    1,
    Math.min(
      available,
      initials.length <= 1 ? available : available / (initials.length * 0.62),
    ),
  );
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${baseSize}" height="${baseSize}" viewBox="0 0 ${baseSize} ${baseSize}"><rect width="${baseSize}" height="${baseSize}" fill="${escapeSvgText(backgroundColor)}"/><text x="50%" y="50%" fill="${escapeSvgText(textColor)}" font-family="Arial, Helvetica, sans-serif" font-size="${fontSize}" font-weight="700" text-anchor="middle" dominant-baseline="central">${escapeSvgText(initials)}</text></svg>`;
  return svgDataUri(svg);
}

function actorAvatarUri(
  actor: Actor | undefined,
  themeMode: "light" | "dark",
  palette: Map<string, string>,
) {
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
    return actorInitialsAvatarUri(actor, themeMode, palette);
  }
  const filePath = avatarRecord.filePath;
  return typeof filePath === "string" && filePath.trim() ? filePath : undefined;
}

function actorAvatarCrop(actor: Actor | undefined) {
  const metadata = actor?.metadata_json;
  if (!isObject(metadata) || !isObject(metadata.avatar)) {
    return {};
  }
  const avatar = metadata.avatar;
  if (avatar.useInitials === true) {
    return {};
  }
  const scale = numberValue(avatar.scale, 1);
  const offsetX = numberValue(avatar.offsetX, 0);
  const offsetY = numberValue(avatar.offsetY, 0);
  const baseSize = numberValue(avatar.baseSize, 640);
  return {
    ...(scale > 0 ? { avatarScale: scale } : {}),
    avatarOffsetX: offsetX,
    avatarOffsetY: offsetY,
    ...(baseSize > 0 ? { avatarBaseSize: baseSize } : {}),
  };
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
  const globalThemeTokens = resolveGlobalThemeTokens(theme, themeMode);
  const appTokens = resolveAppTokens(app, themeMode);
  const neutralTint = neutralTintFromThemeAndAppTokens(
    globalThemeTokens,
    appTokens,
  );
  const palette = paletteMapForColors(
    repository.getPaletteColors(theme.production_id),
    neutralTint,
  );
  const ownerChatActor = resolveChatActor(
    repository,
    ownerActor.id,
    themeMode,
    palette,
  );
  const headerActor = moduleData.header.actorId
    ? resolveChatActor(
        repository,
        moduleData.header.actorId,
        themeMode,
        palette,
      )
    : undefined;

  const state = DeviceStateValuesSchema.parse(deviceState.state_json);
  const metrics = orientedMetrics(
    DeviceMetricsSchema.parse(device.metrics_json),
    state.orientation,
  );
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
  const moduleThemeTokens = stripModuleThemeSystemOwnedTokens(
    resolveModuleThemeTokens(moduleThemeConfig.tokens_json, themeMode),
  );
  const moduleConfig = ChatModuleConfigSchema.parse(
    chatModuleConfig(moduleThemeTokens, screenInstance, moduleInstance),
  );
  const genericTokens = mergeTokenObjects(globalThemeTokens, appTokens);
  const moduleDefaultsFromGenericTokens =
    moduleTypographyDefaultsFromFonts(genericTokens);
  const typographySafeModuleThemeTokens =
    stripChatModuleTypographyFontIdentity(moduleThemeTokens);
  const baseInheritedModuleTokens = mergeTokenObjects(
    mergeTokenObjects(genericTokens, moduleDefaultsFromGenericTokens),
    typographySafeModuleThemeTokens,
  );
  const inheritedModuleTokens = mergeTokenObjects(
    mergeTokenObjects(
      baseInheritedModuleTokens,
      resolveChatHeaderTokens(genericTokens, moduleThemeTokens),
    ),
    resolveChatTypographyTokens(
      genericTokens,
      moduleDefaultsFromGenericTokens,
      typographySafeModuleThemeTokens,
    ),
  );
  const mergedThemeTokens = resolvePaletteTokenReferences(
    inheritedModuleTokens,
    palette,
  ) as Record<string, unknown>;
  const systemThemeTokens = resolvePaletteTokenReferences(
    globalThemeTokens,
    palette,
  ) as Record<string, unknown>;
  const renderScale = renderScaleFromMetrics(metrics);
  const scaledResolvableThemeTokens = scaleDesignTokensForRender(
    inheritedModuleTokens,
    renderScale,
  );
  const scaledThemeTokens = scaleDesignTokensForRender(
    mergedThemeTokens,
    renderScale,
  );
  const scaledSystemThemeTokens = scaleDesignTokensForRender(
    systemThemeTokens,
    renderScale,
  );
  const normalizedThemeTokens = {
    ...normalizeChatVisualTokenGroups(scaledThemeTokens),
    ...(isObject(scaledSystemThemeTokens.fonts)
      ? { systemFonts: scaledSystemThemeTokens.fonts }
      : {}),
  };
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
  const avatarComponent = resolveDefaultAvatarComponent(
    repository,
    theme.production_id,
    palette,
    renderScale,
    themeTokens,
  );
  const buttonIconComponent = resolveDefaultButtonIconComponent(
    repository,
    theme.production_id,
    themeMode,
    palette,
    renderScale,
    themeTokens,
  );
  const labelComponent = resolveDefaultLabelComponent(
    repository,
    theme.production_id,
    palette,
    renderScale,
    themeTokens,
  );
  const audioMessageComponent = resolveDefaultAudioMessageComponent(
    repository,
    theme.production_id,
    iconTheme,
    renderScale,
    themeTokens,
    palette,
  );
  const videoMessageComponent = resolveDefaultMediaMessageComponent(
    repository,
    theme.production_id,
    iconTheme,
    renderScale,
    themeTokens,
    palette,
  );
  const textInputBarComponent = resolveDefaultComponentTokens(
    repository,
    theme.production_id,
    "textInputBar",
    palette,
  );
  const keyboardComponent = resolveDefaultComponentTokens(
    repository,
    theme.production_id,
    "keyboard",
    palette,
  );
  const resolvedComponents = {
    ...(isObject(themeTokens.components) ? themeTokens.components : {}),
    avatar: avatarComponent,
    audioMessage: audioMessageComponent,
    buttonIcon: buttonIconComponent,
    label: labelComponent,
    videoMessage: videoMessageComponent,
    textInputBar: {
      id: textInputBarComponent.id,
      name: textInputBarComponent.name,
      componentType: textInputBarComponent.componentType,
    },
    keyboard: {
      id: keyboardComponent.id,
      name: keyboardComponent.name,
      componentType: keyboardComponent.componentType,
    },
  };
  const themeTokensWithComponents = {
    ...themeTokens,
    components: resolvedComponents,
  };
  const resolvedHeaderTokens = {
    ...themeTokens.header,
    background:
      typeof themeTokens.header.background === "string"
        ? themeColor(
            themeTokens,
            palette,
            themeTokens.header.background,
            stringValue(
              isObject(themeTokens.colors)
                ? themeTokens.colors.background
                : undefined,
              "#FFFFFF",
            ),
          )
        : stringValue(
            isObject(themeTokens.colors)
              ? themeTokens.colors.background
              : undefined,
            "#FFFFFF",
          ),
    separatorColor:
      typeof themeTokens.header.separatorColor === "string"
        ? themeColor(
            themeTokens,
            palette,
            themeTokens.header.separatorColor,
            "transparent",
          )
        : "transparent",
    leftItems: resolveIconItems(
      iconItemsSource(themeTokens.header.leftItems, themeTokens.header.leftIconTokens),
      iconTheme,
      themeTokens,
      palette,
    ),
    rightItems: resolveIconItems(
      iconItemsSource(themeTokens.header.rightItems, themeTokens.header.rightIconTokens),
      iconTheme,
      themeTokens,
      palette,
    ),
  };
  const effectiveStatusBar = resolveStatusBarDefinition(
    statusBar?.config_json,
    moduleConfig.statusBar,
    state,
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
  let previousMessageWriteOnEndFrame = 0;
  const resolvedMessages = moduleData.messages.map((message) => {
    const delayAfterPreviousFrames =
      typeof message.delayAfterPreviousFrames === "number"
        ? message.delayAfterPreviousFrames
        : 0;
    const effectiveStartFrame =
      previousMessageWriteOnEndFrame + delayAfterPreviousFrames;
    const effectiveMessage: TimedChatModuleMessage = {
      ...message,
      startFrame: effectiveStartFrame,
      ...(message.textReveal
        ? {
            textReveal: {
              ...message.textReveal,
              startFrame: effectiveStartFrame,
            },
          }
        : {}),
    };
    const messageLocalFrame = Math.max(0, localFrame - effectiveStartFrame);
    const animatedMessage: TimedChatModuleMessage = {
      ...animatedChatMessage(effectiveMessage, messageLocalFrame),
      startFrame: effectiveStartFrame,
    };
    const animatedWriteOnWindow = activeTextAnimationWriteOnWindow(
      effectiveMessage,
      messageLocalFrame,
    );
    const animatedComposerPressedKey = animatedWriteOnWindow
      ? animatedTextComposerPressedKey(effectiveMessage, messageLocalFrame)
      : undefined;
    const direction =
      animatedMessage.direction ??
      (animatedMessage.type === "system"
        ? "system"
        : animatedMessage.actorId === ownerActor.id
          ? "outgoing"
          : "incoming");
    const sender =
      direction === "system"
        ? ownerChatActor
        : resolveChatActor(
            repository,
            animatedMessage.actorId ?? ownerActor.id,
            themeMode,
            palette,
          );
    const bubble = resolveMessageBubble({
      message: animatedMessage,
      sender,
      direction,
      themeTokens: themeTokensWithComponents,
      palette,
      localFrame,
      fps,
      viewportWidth: metrics.viewport.width,
    });
    previousMessageWriteOnEndFrame =
      bubble.timing.startFrame + (bubble.timing.writeOnDurationFrames ?? 0);

    const scaledMedia = scaleChatMediaForRender(animatedMessage.media, renderScale);
    const mediaFilePath = scaledMedia
      ? stringValue(scaledMedia.filePath)
      : "";
    const mediaUri = mediaFilePath;
    const mediaType = scaledMedia ? stringValue(scaledMedia.type) : "";
    const isAudioMedia = mediaType === "audio";

    return {
      id: bubble.id,
      direction: bubble.direction,
      text: bubble.text,
      visibleText: bubble.visibleText,
      status: bubble.status,
      sender: {
        id: bubble.actor.id,
        displayName: bubble.actor.displayName,
        ...(bubble.actor.color ? { color: bubble.actor.color } : {}),
        ...(bubble.actor.avatarUri
          ? {
              avatar: {
                uri: bubble.actor.avatarUri,
                ...(bubble.actor.avatarScale !== undefined
                  ? { scale: bubble.actor.avatarScale }
                  : {}),
                ...(bubble.actor.avatarOffsetX !== undefined
                  ? { offsetX: bubble.actor.avatarOffsetX }
                  : {}),
                ...(bubble.actor.avatarOffsetY !== undefined
                  ? { offsetY: bubble.actor.avatarOffsetY }
                  : {}),
                ...(bubble.actor.avatarBaseSize !== undefined
                  ? { baseSize: bubble.actor.avatarBaseSize }
                  : {}),
              },
            }
          : {}),
      },
      ...(mediaUri || isAudioMedia
        ? {
            media: {
              ...(mediaUri ? { uri: mediaUri } : {}),
              ...(mediaType ? { type: mediaType } : {}),
              ...(typeof scaledMedia?.durationSeconds === "number"
                ? {
                    durationSeconds: numberValue(
                      scaledMedia.durationSeconds,
                      8,
                    ),
                  }
                : {}),
              ...(isAudioMedia
                ? {
                    window: {
                      width: audioMessageComponent.width,
                      height: audioMessageComponent.height,
                      offsetX: 0,
                      offsetY: 0,
                    },
                  }
                : {}),
              ...(scaledMedia && stringValue(scaledMedia.playMode)
                ? { playMode: stringValue(scaledMedia.playMode) }
                : {}),
              ...(typeof scaledMedia?.playStartFrame === "number"
                ? { playStartFrame: scaledMedia.playStartFrame }
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
        ...(animatedWriteOnWindow
          ? { writeOnStartFrame: effectiveStartFrame + animatedWriteOnWindow.startFrame }
          : bubble.timing.writeOnStartFrame !== null
          ? { writeOnStartFrame: bubble.timing.writeOnStartFrame }
          : {}),
        ...(animatedWriteOnWindow
          ? { writeOnDurationFrames: animatedWriteOnWindow.durationFrames }
          : bubble.timing.writeOnDurationFrames !== null
          ? { writeOnDurationFrames: bubble.timing.writeOnDurationFrames }
          : {}),
      },
      style: bubble.style,
      layout: bubble.layout,
      animation: {
        ...(isObject(bubble.animation) ? bubble.animation : {}),
        ...(animatedWriteOnWindow
          ? { composerPressedKey: animatedComposerPressedKey ?? "" }
          : {}),
      },
    };
  });

  const activeComposerMessage = [...resolvedMessages]
    .reverse()
    .find((message) => isActiveWriteOnMessage(message, localFrame));
  const rawKeyboardTokens = resolveKeyboardInheritance(
    keyboardComponent.tokens,
    keyboardInstanceOverrides(moduleConfig.keyboard),
  );
  const keyboardPushDurationFrames = Math.max(
    0,
    Math.round(numberValue(rawKeyboardTokens.pushDurationFrames, 8)),
  );
  const enteringComposerMessage = activeComposerMessage
    ? undefined
    : [...resolvedMessages].reverse().find((message) => {
        const writeOnStartFrame = message.timing.writeOnStartFrame;
        const writeOnDurationFrames = message.timing.writeOnDurationFrames;
        if (
          message.direction !== "outgoing" ||
          writeOnStartFrame === undefined ||
          writeOnDurationFrames === undefined ||
          writeOnDurationFrames <= 0
        ) {
          return false;
        }
        const keyboardEnterStartFrame = Math.max(
          0,
          writeOnStartFrame - keyboardPushDurationFrames,
        );
        return (
          localFrame >= keyboardEnterStartFrame &&
          localFrame < writeOnStartFrame
        );
      });
  const exitingComposerMessage = activeComposerMessage
    ? undefined
    : [...resolvedMessages].reverse().find((message) => {
        const writeOnStartFrame = message.timing.writeOnStartFrame;
        const writeOnDurationFrames = message.timing.writeOnDurationFrames;
        if (
          message.direction !== "outgoing" ||
          writeOnStartFrame === undefined ||
          writeOnDurationFrames === undefined ||
          writeOnDurationFrames <= 0
        ) {
          return false;
        }
        const writeOnEndFrame = writeOnStartFrame + writeOnDurationFrames;
        return (
          localFrame >= writeOnEndFrame &&
          localFrame < writeOnEndFrame + keyboardPushDurationFrames
        );
      });
  const keyboardTransition = activeComposerMessage
    ? {
        phase: "enter",
        startFrame: Math.max(
          0,
          (activeComposerMessage.timing.writeOnStartFrame ?? localFrame) -
            keyboardPushDurationFrames,
        ),
      }
    : enteringComposerMessage
      ? {
          phase: "enter",
          startFrame: Math.max(
            0,
            (enteringComposerMessage.timing.writeOnStartFrame ?? localFrame) -
              keyboardPushDurationFrames,
          ),
        }
    : exitingComposerMessage
      ? {
          phase: "exit",
          startFrame:
            (exitingComposerMessage.timing.writeOnStartFrame ?? localFrame) +
            (exitingComposerMessage.timing.writeOnDurationFrames ?? 0),
        }
      : undefined;

  const runtimeShowTextInputBar = moduleConfig.showTextInputBar === true;
  const runtimeShowKeyboard =
    moduleConfig.showKeyboard === true &&
    runtimeShowTextInputBar &&
    (activeComposerMessage !== undefined ||
      enteringComposerMessage !== undefined ||
      exitingComposerMessage !== undefined);
  const activeComposerAnimation = isObject(activeComposerMessage?.animation)
    ? activeComposerMessage.animation
    : {};
  const hasAnimatedComposerPressedKey =
    Object.prototype.hasOwnProperty.call(
      activeComposerAnimation,
      "composerPressedKey",
    );
  const runtimePressedKey = activeComposerMessage
    ? hasAnimatedComposerPressedKey
      ? stringValue(activeComposerAnimation.composerPressedKey) || undefined
      : pressedKeyFromWriteOnState(
          activeComposerMessage.text,
          activeComposerMessage.visibleText,
        )
    : undefined;

  const rawEffectiveKeyboard = resolveKeyboardDefinition(
    {
      ...rawKeyboardTokens,
      ...(runtimePressedKey ? { pressedKey: runtimePressedKey } : {}),
    },
    iconTheme,
    renderScale,
    { hasTextInputBar: runtimeShowTextInputBar },
  );
  const rawEffectiveTextInputBar = resolveTextInputBarDefinition(
    {
      ...textInputBarComponentBase(
        textInputBarComponent.tokens,
      ),
      ...textInputBarInstanceOverrides(moduleConfig.textInputBar),
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
    scaledResolvableThemeTokens,
    palette,
  );
  const {
    keyboard: effectiveKeyboard,
    textInputBar: effectiveTextInputBar,
  } = scaledLowerChrome({
    keyboard: rawEffectiveKeyboard,
    textInputBar: rawEffectiveTextInputBar,
    viewportHeight: metrics.viewport.height,
    showKeyboard: runtimeShowKeyboard,
    showTextInputBar: runtimeShowTextInputBar,
  });

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
        ...(ownerChatActor.avatarScale !== undefined
          ? { scale: ownerChatActor.avatarScale }
          : {}),
        ...(ownerChatActor.avatarOffsetX !== undefined
          ? { offsetX: ownerChatActor.avatarOffsetX }
          : {}),
        ...(ownerChatActor.avatarOffsetY !== undefined
          ? { offsetY: ownerChatActor.avatarOffsetY }
          : {}),
        ...(ownerChatActor.avatarBaseSize !== undefined
          ? { baseSize: ownerChatActor.avatarBaseSize }
          : {}),
      }
    : undefined;
  const headerAvatar = headerActor?.avatarUri
    ? {
        uri: headerActor.avatarUri,
        ...(headerActor.avatarScale !== undefined
          ? { scale: headerActor.avatarScale }
          : {}),
        ...(headerActor.avatarOffsetX !== undefined
          ? { offsetX: headerActor.avatarOffsetX }
          : {}),
        ...(headerActor.avatarOffsetY !== undefined
          ? { offsetY: headerActor.avatarOffsetY }
          : {}),
        ...(headerActor.avatarBaseSize !== undefined
          ? { baseSize: headerActor.avatarBaseSize }
          : {}),
      }
    : undefined;
  const animatedHeaderSubtitle = animatedValueForAnimationTrack(
    moduleData.header.animation,
    "subtitle",
    moduleData.header.subtitle,
    localFrame,
  );
  const systemShadow = isObject(themeTokens.shadows?.default)
    ? themeTokens.shadows.default
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
      components: resolvedComponents,
      header: resolvedHeaderTokens,
      chatBubbles: {
        ...themeTokens.chatBubbles,
        radius: themeRadius(
          themeTokens,
          isObject(themeTokens.chatBubbles?.style)
            ? themeTokens.chatBubbles.style.cornerRadiusToken
            : undefined,
          18,
        ),
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
      ...(typeof animatedHeaderSubtitle === "string"
        ? { subtitle: animatedHeaderSubtitle }
        : moduleData.header.subtitle
          ? { subtitle: moduleData.header.subtitle }
          : {}),
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
      showIncomingActorLabels: moduleConfig.showIncomingActorLabels,
      ...(activeComposerMessage
        ? { activeComposerMessageId: activeComposerMessage.id }
        : {}),
      ...(keyboardTransition ? { keyboardTransition } : {}),
      textInputBar: moduleConfig.textInputBar ?? {},
      keyboard: moduleConfig.keyboard ?? {},
      initialScroll: moduleConfig.initialScroll,
      messageGrouping: moduleConfig.messageGrouping,
      debugShowBounds: moduleConfig.debugShowBounds,
    },
  });
}
