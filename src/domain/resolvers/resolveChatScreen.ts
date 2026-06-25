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
  type ChatParticipant,
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
  type ResolvedChatParticipant,
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
  const designLayout = {
    height: 336,
    topPadding: 8,
    sidePadding: 6,
    bottomPadding: 8,
    bottomUtilityHeight: 34,
    bottomUtilitySidePadding: 24,
    bottomIconSize: 22,
    rowGap: 8,
    keyGap: 6,
    keyHeight: 42,
    keyRadius: 7,
    fontSize: 18,
  };
  const layout = Object.fromEntries(
    Object.entries(designLayout).map(([key, value]) => [key, value * scale]),
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
  return mergeTokenObjects(
    theme.tokens_json,
    resolveThemeModeTokens(theme.tokens_json, themeMode),
  );
}

export function resolveModuleThemeTokens(
  tokens: Record<string, unknown>,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  return mergeTokenObjects(tokens, resolveThemeModeTokens(tokens, themeMode));
}

const DESIGN_UNIT_TOKEN_PATHS = [
  ["fonts", "bodySize"],
  ["fonts", "bodyLineHeight"],
  ["fonts", "captionSize"],
  ["layout", "screenGutter"],
  ["header", "height"],
  ["header", "separatorWidth"],
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
  ["chatBubbles", "tail", "width"],
  ["chatBubbles", "tail", "height"],
  ["chatBubbles", "shadow", "offsetX"],
  ["chatBubbles", "shadow", "offsetY"],
  ["chatBubbles", "shadow", "blur"],
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

function resolveParticipant(
  repository: DomainRepository,
  participant: ChatParticipant,
): ResolvedChatParticipant {
  const actor = participant.actorId
    ? requireRecord(
        repository.getActor(participant.actorId),
        "Actor",
        participant.actorId,
      )
    : undefined;
  const directAvatarUri = actorAvatarUri(actor);
  const avatarAssetId = directAvatarUri
    ? undefined
    : participant.avatarAssetId ?? actor?.avatar_asset_id;
  const avatar = avatarAssetId
    ? requireRecord(
        repository.getMediaAsset(avatarAssetId),
        "MediaAsset",
        avatarAssetId,
      )
    : undefined;
  const displayName = participant.displayName ?? actor?.display_name;
  if (!displayName) {
    throw new Error(`Chat participant ${participant.id} has no display name`);
  }

  return {
    participantId: participant.id,
    ...(actor ? { actorId: actor.id } : {}),
    displayName,
    ...(directAvatarUri || avatar ? { avatarUri: directAvatarUri ?? avatar?.uri } : {}),
    role: participant.role,
  };
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
  const moduleConfig = ChatModuleConfigSchema.parse(
    moduleInstance.behavior_json,
  );
  if (moduleData.schemaVersion !== moduleInstance.module_schema_version) {
    throw new Error(
      `Chat module data schemaVersion does not match module instance ${moduleInstance.id}`,
    );
  }

  const participants = new Map(
    moduleData.participants.map((participant) => {
      const resolved = resolveParticipant(repository, participant);
      return [resolved.participantId, resolved] as const;
    }),
  );
  const ownerParticipant = [...participants.values()].find(
    (participant) => participant.role === "owner",
  );
  if (!ownerParticipant) {
    throw new Error(`Chat screen instance ${screenInstance.id} has no owner`);
  }
  if (
    ownerParticipant.actorId &&
    ownerParticipant.actorId !== ownerActor.id
  ) {
    throw new Error(
      `Chat owner participant does not match screen owner ${ownerActor.id}`,
    );
  }

  const headerAvatarParticipant = moduleData.header.avatarParticipantId
    ? participants.get(moduleData.header.avatarParticipantId)
    : undefined;
  if (
    moduleData.header.avatarParticipantId &&
    !headerAvatarParticipant
  ) {
    throw new Error(
      `Chat header references missing participant ${moduleData.header.avatarParticipantId}`,
    );
  }

  const metrics = DeviceMetricsSchema.parse(device.metrics_json);
  const state = DeviceStateValuesSchema.parse(deviceState.state_json);
  const themeEnvelope = ThemeEnvelopeSchema.parse(theme.tokens_json);
  const themeMode =
    screenInstance.theme_mode ?? themeEnvelope.defaultMode ?? "light";
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
  const moduleThemeTokens = resolveModuleThemeTokens(
    moduleThemeConfig.tokens_json,
    themeMode,
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
  const themeTokens = ChatThemeSchema.parse(scaledThemeTokens);
  const statusBar = theme.status_bar_id
    ? repository.getStatusBar(theme.status_bar_id)
    : undefined;
  const navigationBar = theme.navigation_bar_id
    ? repository.getNavigationBar(theme.navigation_bar_id)
    : undefined;
  const iconTheme = theme.icon_theme_id
    ? repository.getIconTheme(theme.icon_theme_id)
    : undefined;
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
  const effectiveKeyboard = resolveKeyboardDefinition(
    moduleConfig.keyboard,
    iconTheme,
    renderScale,
  );

  const messages = moduleData.messages.map((message) => {
    const sender = participants.get(message.senderParticipantId);
    if (!sender) {
      throw new Error(
        `Chat message ${message.id} references missing participant ${message.senderParticipantId}`,
      );
    }
    const direction =
      message.direction ??
      (message.type === "system"
        ? "system"
        : sender.participantId === ownerParticipant.participantId
          ? "outgoing"
          : "incoming");
    const bubble = resolveMessageBubble({
      message,
      sender,
      direction,
      themeTokens: scaledThemeTokens,
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
    const mediaUri = message.media?.filePath ?? mediaAsset?.uri;

    return {
      id: bubble.id,
      direction: bubble.direction,
      text: bubble.text,
      visibleText: bubble.visibleText,
      sender: {
        id: bubble.actor.id,
        participantId: sender.participantId,
        displayName: bubble.actor.displayName,
      },
      ...(mediaUri
        ? {
            media: {
              ...(mediaAsset ? { assetId: mediaAsset.id } : {}),
              uri: mediaUri,
              ...(message.media?.type ? { type: message.media.type } : {}),
              ...(message.media?.window
                ? { window: message.media.window }
                : {}),
              ...(message.media?.transform
                ? { transform: message.media.transform }
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

  const ownerAvatar = ownerParticipant.avatarUri
    ? {
        uri: ownerParticipant.avatarUri,
      }
    : undefined;
  const headerAvatar = headerAvatarParticipant?.avatarUri
    ? {
        uri: headerAvatarParticipant.avatarUri,
      }
    : undefined;

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
      chatBubbles: {
        ...themeTokens.chatBubbles,
        radius: themeTokens.radii.bubble,
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
    ownerActor: {
      id: ownerActor.id,
      displayName: ownerActor.display_name,
      ...(ownerAvatar?.uri ? { avatar: ownerAvatar } : {}),
    },
    header: {
      title: moduleData.header.title,
      subtitle: moduleData.header.subtitle,
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
      showKeyboard: moduleConfig.showKeyboard,
      keyboard: moduleConfig.keyboard ?? {},
      initialScroll: moduleConfig.initialScroll,
      messageGrouping: moduleConfig.messageGrouping,
      debugShowBounds: moduleConfig.debugShowBounds,
    },
  });
}
