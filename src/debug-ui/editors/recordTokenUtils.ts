import { CHAT_HEADER_DEFAULTS } from "../../domain/fields/chatFields.js";
import { surfaceStyleNormalize } from "../../domain/value-system/index.js";
import {
  cloneJson,
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

function defaultTokenGroupValue(groupKey: string): JsonValue {
  return groupKey === "messages" ? [] : {};
}

function normalizeSurfaceStyleForContext(
  value: Record<string, unknown>,
  defaults: Record<string, unknown>,
) {
  return surfaceStyleNormalize({
    ...defaults,
    ...value,
  });
}

export function tokenEditorGroups(
  root: Record<string, unknown>,
  inheritedRoot?: unknown,
) {
  const inherited = isJsonObject(inheritedRoot as JsonValue)
    ? (inheritedRoot as Record<string, unknown>)
    : {};
  return Array.from(
    new Set([...Object.keys(root), ...Object.keys(inherited)]),
  ).filter((group) => {
    const value = root[group] ?? inherited[group];
    return (
      group !== "modes" &&
      group !== "colors" &&
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value)
    );
  });
}

export function stripAppStatusAndNavigationTokens(
  value: unknown,
): Record<string, JsonValue> {
  const source = isJsonObject(value as JsonValue)
    ? cloneJson(value as JsonValue)
    : ({} as JsonValue);
  const root = isJsonObject(source) ? source : {};
  delete root.statusBar;
  delete root.navigationBar;
  delete root.keyboard;
  delete root.cursor;
  delete root.shadows;
  if (isJsonObject(root.notifications)) {
    delete root.notifications.background;
    delete root.notifications.titleColor;
    delete root.notifications.bodyColor;
  }
  const modes = isJsonObject(root.modes) ? root.modes : {};
  for (const mode of ["light", "dark"] as const) {
    const modeRoot = isJsonObject(modes[mode]) ? modes[mode] : undefined;
    if (!modeRoot) continue;
    delete modeRoot.statusBar;
    delete modeRoot.navigationBar;
    delete modeRoot.keyboard;
    delete modeRoot.cursor;
    const colors = isJsonObject(modeRoot.colors) ? modeRoot.colors : undefined;
    if (colors) {
      delete colors.navigationBackground;
    }
  }
  return root;
}

export function stripModuleSystemOwnedTokens(
  value: unknown,
): Record<string, JsonValue> {
  const source = isJsonObject(value as JsonValue)
    ? cloneJson(value as JsonValue)
    : ({} as JsonValue);
  const root = isJsonObject(source) ? source : {};
  delete root.cursor;
  const modes = isJsonObject(root.modes) ? root.modes : {};
  for (const mode of ["light", "dark"] as const) {
    const modeRoot = isJsonObject(modes[mode]) ? modes[mode] : undefined;
    if (!modeRoot) continue;
    delete modeRoot.cursor;
  }
  return root;
}

function numberValue(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function modeRoot(root: Record<string, JsonValue>, mode: "light" | "dark") {
  const modes = isJsonObject(root.modes) ? root.modes : {};
  const currentMode = isJsonObject(modes[mode]) ? modes[mode] : {};
  const header = isJsonObject(currentMode.header) ? currentMode.header : {};
  const chatBubbles = isJsonObject(currentMode.chatBubbles)
    ? currentMode.chatBubbles
    : {};
  return { modes, currentMode, header, chatBubbles };
}

export function normalizeCoreChatModuleTokensForEditor(
  value: unknown,
): Record<string, JsonValue> {
  const source = isJsonObject(value as JsonValue)
    ? cloneJson(value as JsonValue)
    : ({} as JsonValue);
  const root = isJsonObject(source) ? source : {};
  const header = isJsonObject(root.header) ? root.header : {};
  const chatBubbles = isJsonObject(root.chatBubbles) ? root.chatBubbles : {};
  const chatBubbleMedia = isJsonObject(chatBubbles.media)
    ? chatBubbles.media
    : {};
  const chatBubbleAvatar = isJsonObject(chatBubbles.avatar)
    ? chatBubbles.avatar
    : {};
  const hasBubbleStyle =
    isJsonObject(chatBubbles.style) ||
    typeof chatBubbles.shadowEnabled === "boolean" ||
    typeof chatBubbles.surfaceReliefEnabled === "boolean" ||
    typeof chatBubbles.borderWidth === "number" ||
    typeof chatBubbles.borderColorToken === "string" ||
    typeof chatBubbles.cornerRadiusToken === "string" ||
    isJsonObject(chatBubbles.surfaceRelief);
  const bubbleStyle = hasBubbleStyle
    ? surfaceStyleNormalize({
    ...(isJsonObject(chatBubbles.style) ? chatBubbles.style : {}),
    ...(typeof chatBubbles.shadowEnabled === "boolean"
      ? { shadowEnabled: chatBubbles.shadowEnabled }
      : {}),
    ...(typeof chatBubbles.surfaceReliefEnabled === "boolean"
      ? { surfaceReliefEnabled: chatBubbles.surfaceReliefEnabled }
      : {}),
    ...(typeof chatBubbles.borderWidth === "number"
      ? { borderWidth: chatBubbles.borderWidth }
      : {}),
    ...(typeof chatBubbles.borderColorToken === "string"
      ? { borderColorToken: chatBubbles.borderColorToken }
      : {}),
    ...(typeof chatBubbles.cornerRadiusToken === "string"
      ? { cornerRadiusToken: chatBubbles.cornerRadiusToken }
      : {}),
    ...(isJsonObject(chatBubbles.surfaceRelief)
      ? { surfaceRelief: chatBubbles.surfaceRelief }
      : {}),
      })
    : undefined;
  const avatarStyle = normalizeSurfaceStyleForContext({
    ...(isJsonObject(chatBubbleAvatar.style) ? chatBubbleAvatar.style : {}),
    ...(typeof chatBubbleAvatar.shadowEnabled === "boolean"
      ? { shadowEnabled: chatBubbleAvatar.shadowEnabled }
      : {}),
    ...(typeof chatBubbleAvatar.surfaceReliefEnabled === "boolean"
      ? { surfaceReliefEnabled: chatBubbleAvatar.surfaceReliefEnabled }
      : {}),
    ...(typeof chatBubbleAvatar.borderWidth === "number"
      ? { borderWidth: chatBubbleAvatar.borderWidth }
      : {}),
    ...(typeof chatBubbleAvatar.borderColorToken === "string"
      ? { borderColorToken: chatBubbleAvatar.borderColorToken }
      : {}),
    ...(typeof chatBubbleAvatar.cornerRadiusToken === "string"
      ? { cornerRadiusToken: chatBubbleAvatar.cornerRadiusToken }
      : {}),
  }, { cornerRadiusToken: "radii.avatar" });
  const messageLabelStyle = surfaceStyleNormalize({
    ...(isJsonObject(chatBubbles.messageLabelStyle)
      ? chatBubbles.messageLabelStyle
      : {}),
    ...(typeof chatBubbles.messageLabelShadowEnabled === "boolean"
      ? { shadowEnabled: chatBubbles.messageLabelShadowEnabled }
      : {}),
    ...(typeof chatBubbles.messageLabelSurfaceReliefEnabled === "boolean"
      ? { surfaceReliefEnabled: chatBubbles.messageLabelSurfaceReliefEnabled }
      : {}),
    ...(typeof chatBubbles.messageLabelBorderWidth === "number"
      ? { borderWidth: chatBubbles.messageLabelBorderWidth }
      : {}),
    ...(typeof chatBubbles.messageLabelBorderColorToken === "string"
      ? { borderColorToken: chatBubbles.messageLabelBorderColorToken }
      : {}),
    ...(typeof chatBubbles.messageLabelCornerRadiusToken === "string"
      ? { cornerRadiusToken: chatBubbles.messageLabelCornerRadiusToken }
      : {}),
  });
  const mediaStyle = surfaceStyleNormalize({
    ...(isJsonObject(chatBubbleMedia.style) ? chatBubbleMedia.style : {}),
    ...(typeof chatBubbleMedia.shadowEnabled === "boolean"
      ? { shadowEnabled: chatBubbleMedia.shadowEnabled }
      : {}),
    ...(typeof chatBubbleMedia.surfaceReliefEnabled === "boolean"
      ? { surfaceReliefEnabled: chatBubbleMedia.surfaceReliefEnabled }
      : {}),
    ...(typeof chatBubbleMedia.borderWidth === "number"
      ? { borderWidth: chatBubbleMedia.borderWidth }
      : {}),
    ...(typeof chatBubbleMedia.borderColorToken === "string"
      ? { borderColorToken: chatBubbleMedia.borderColorToken }
      : {}),
    ...(typeof chatBubbleMedia.cornerRadiusToken === "string"
      ? { cornerRadiusToken: chatBubbleMedia.cornerRadiusToken }
      : typeof chatBubbles.cornerRadiusToken === "string"
        ? { cornerRadiusToken: chatBubbles.cornerRadiusToken }
        : {}),
  });
  const {
    shadowEnabled: _legacyBubbleShadowEnabled,
    surfaceReliefEnabled: _legacyBubbleSurfaceReliefEnabled,
    borderWidth: _legacyBubbleBorderWidth,
    borderColorToken: _legacyBubbleBorderColorToken,
    radius: _legacyBubbleRadius,
    cornerRadiusToken: _legacyBubbleCornerRadiusToken,
    surfaceRelief: _legacyBubbleSurfaceRelief,
    messageLabelShadowEnabled: _legacyLabelShadowEnabled,
    messageLabelSurfaceReliefEnabled: _legacyLabelSurfaceReliefEnabled,
    messageLabelBorderWidth: _legacyLabelBorderWidth,
    messageLabelBorderColorToken: _legacyLabelBorderColorToken,
    messageLabelCornerRadius: _legacyLabelCornerRadius,
    messageLabelCornerRadiusToken: _legacyLabelCornerRadiusToken,
    ...visibleChatBubbles
  } = chatBubbles;
  const {
    shadowEnabled: _legacyAvatarShadowEnabled,
    surfaceReliefEnabled: _legacyAvatarSurfaceReliefEnabled,
    borderWidth: _legacyAvatarBorderWidth,
    borderColorToken: _legacyAvatarBorderColorToken,
    cornerRadius: _legacyAvatarCornerRadius,
    cornerRadiusToken: _legacyAvatarCornerRadiusToken,
    ...visibleChatBubbleAvatar
  } = chatBubbleAvatar;
  const {
    shadowEnabled: _legacyMediaShadowEnabled,
    surfaceReliefEnabled: _legacyMediaSurfaceReliefEnabled,
    borderWidth: _legacyMediaBorderWidth,
    borderColorToken: _legacyMediaBorderColorToken,
    cornerRadius: _legacyMediaCornerRadius,
    cornerRadiusToken: _legacyMediaCornerRadiusToken,
    ...visibleChatBubbleMedia
  } = chatBubbleMedia;
  const light = modeRoot(root, "light");
  const dark = modeRoot(root, "dark");
  const lightMedia = isJsonObject(light.chatBubbles.media)
    ? light.chatBubbles.media
    : {};
  const darkMedia = isJsonObject(dark.chatBubbles.media)
    ? dark.chatBubbles.media
    : {};
  root.modes = {
      ...light.modes,
      light: {
        ...light.currentMode,
      chatBubbles: {
        ...light.chatBubbles,
        systemBackground:
          typeof light.chatBubbles.systemBackground === "string"
            ? light.chatBubbles.systemBackground
            : "rgba(118, 118, 128, 0.16)",
        systemText:
          typeof light.chatBubbles.systemText === "string"
            ? light.chatBubbles.systemText
            : "#3C3C43",
        media: {
          ...lightMedia,
          borderColor:
            typeof lightMedia.borderColor === "string"
              ? lightMedia.borderColor
              : "#FFFFFF",
        },
      },
    },
    dark: {
      ...dark.currentMode,
      chatBubbles: {
        ...dark.chatBubbles,
        systemBackground:
          typeof dark.chatBubbles.systemBackground === "string"
            ? dark.chatBubbles.systemBackground
            : "rgba(118, 118, 128, 0.24)",
        systemText:
          typeof dark.chatBubbles.systemText === "string"
            ? dark.chatBubbles.systemText
            : "#F2F2F7",
        media: {
          ...darkMedia,
          borderColor:
            typeof darkMedia.borderColor === "string"
              ? darkMedia.borderColor
              : "#1C1C1E",
        },
      },
    },
  };
  root.header = {
    ...header,
    background:
      typeof header.background === "string"
        ? header.background
        : CHAT_HEADER_DEFAULTS.background,
    separatorColor:
      typeof header.separatorColor === "string"
        ? header.separatorColor
        : CHAT_HEADER_DEFAULTS.separatorColor,
    avatarSize: numberValue(header.avatarSize, CHAT_HEADER_DEFAULTS.avatarSize),
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
    leftIconTokens:
      typeof header.leftIconTokens === "string"
        ? header.leftIconTokens
        : CHAT_HEADER_DEFAULTS.leftIconTokens,
    rightIconTokens:
      typeof header.rightIconTokens === "string"
        ? header.rightIconTokens
        : CHAT_HEADER_DEFAULTS.rightIconTokens,
  };
  const chatBubbleTail = isJsonObject(chatBubbles.tail) ? chatBubbles.tail : {};
  const { scale: _legacyTailScale, ...visibleChatBubbleTail } = chatBubbleTail;
  root.chatBubbles = {
    ...visibleChatBubbles,
    avatarSize: numberValue(chatBubbles.avatarSize, 32),
    avatarGap: numberValue(chatBubbles.avatarGap, 8),
    ...(bubbleStyle ? { style: bubbleStyle as JsonValue } : {}),
    contentMetaGap: numberValue(chatBubbles.contentMetaGap, 4),
    avatar: {
      ...visibleChatBubbleAvatar,
      alignment:
        chatBubbleAvatar.alignment === "top" || chatBubbleAvatar.alignment === "bottom"
          ? chatBubbleAvatar.alignment
          : "bottom",
      offsetX: numberValue(chatBubbleAvatar.offsetX, 0),
      offsetY: numberValue(chatBubbleAvatar.offsetY, 0),
      style: avatarStyle as JsonValue,
    },
    messageLabelUseActorColor:
      typeof chatBubbles.messageLabelUseActorColor === "boolean"
        ? chatBubbles.messageLabelUseActorColor
        : true,
    messageLabelOffsetX: numberValue(chatBubbles.messageLabelOffsetX, 0),
    messageLabelOffsetY: numberValue(chatBubbles.messageLabelOffsetY, 0),
    messageLabelStyle: messageLabelStyle as JsonValue,
    media: {
      ...visibleChatBubbleMedia,
      style: mediaStyle as JsonValue,
    },
    tail: {
      ...visibleChatBubbleTail,
      style:
        isJsonObject(chatBubbles.tail) && typeof chatBubbles.tail.style === "string"
          ? chatBubbles.tail.style
          : "rounded_wedge",
      verticalPosition:
        isJsonObject(chatBubbles.tail) &&
        chatBubbles.tail.verticalPosition === "top"
          ? "top"
          : "bottom",
      width: numberValue(
        isJsonObject(chatBubbles.tail) ? chatBubbles.tail.width : undefined,
        8,
      ),
      height: numberValue(
        isJsonObject(chatBubbles.tail) ? chatBubbles.tail.height : undefined,
        12,
      ),
    },
  };
  delete root.avatars;
  return root;
}

export function editorValueForThemeTokenGroup(
  themeTokenRoot: Record<string, JsonValue>,
  groupKey: string,
): JsonValue {
  const value = themeTokenRoot[groupKey] ?? defaultTokenGroupValue(groupKey);
  if (!isJsonObject(value)) return value;
  if (groupKey === "fonts") {
    const {
      source: _source,
      productionFontId: _productionFontId,
      weight: _weight,
      ...visibleValue
    } = value;
    return visibleValue;
  }
  if (groupKey === "notifications") {
    const {
      background: _background,
      titleColor: _titleColor,
      bodyColor: _bodyColor,
      ...visibleValue
    } = value;
    return visibleValue;
  }
  return value;
}

export function visibleTokenGroupValue(
  value: unknown,
  groupKey: string,
): JsonValue {
  if (!isJsonObject(value as JsonValue)) {
    return (value ?? defaultTokenGroupValue(groupKey)) as JsonValue;
  }
  const root = value as Record<string, JsonValue>;
  if (groupKey === "notifications") {
    const {
      background: _background,
      titleColor: _titleColor,
      bodyColor: _bodyColor,
      ...visibleValue
    } = root;
    return visibleValue;
  }
  const {
    source: _source,
    productionFontId: _productionFontId,
    ...visibleValue
  } = root;
  return visibleValue;
}

export function editorValueForTokenGroup(
  tokenRoot: Record<string, unknown>,
  groupKey: string,
): JsonValue {
  const value = tokenRoot[groupKey] ?? defaultTokenGroupValue(groupKey);
  return visibleTokenGroupValue(value, groupKey);
}

export function inheritedValueForTokenGroup(
  tokenRoot: unknown,
  groupKey: string,
): Record<string, unknown> | null {
  if (!isJsonObject(tokenRoot as JsonValue)) return null;
  const value = (tokenRoot as Record<string, JsonValue>)[groupKey];
  if (!isJsonObject(value)) return null;
  const visibleValue = visibleTokenGroupValue(value, groupKey);
  return isJsonObject(visibleValue) ? visibleValue : null;
}

export function mergeTokenGroupWithInternalFields(
  originalValue: unknown,
  nextVisibleValue: JsonValue,
): JsonValue {
  const original = isJsonObject(originalValue as JsonValue)
    ? (originalValue as Record<string, JsonValue>)
    : {};
  const nextVisible = isJsonObject(nextVisibleValue)
    ? (nextVisibleValue as Record<string, JsonValue>)
    : {};
  const internalFields: Record<string, JsonValue> = {};
  if (Object.hasOwn(original, "source")) {
    internalFields.source = original.source;
  }
  if (Object.hasOwn(original, "productionFontId")) {
    internalFields.productionFontId = original.productionFontId;
  }
  return {
    ...internalFields,
    ...nextVisible,
  };
}

export function nextThemeTokenGroupValue({
  themeTokenRoot,
  groupKey,
  parsedValue,
}: {
  themeTokenRoot: Record<string, JsonValue>;
  groupKey: string;
  parsedValue: JsonValue;
}) {
  const originalValue = themeTokenRoot[groupKey];
  if (groupKey !== "fonts" || !isJsonObject(parsedValue)) {
    return parsedValue;
  }
  const nextValue: Record<string, JsonValue> = {
    ...(isJsonObject(originalValue) ? originalValue : {}),
    ...parsedValue,
    source:
      isJsonObject(originalValue) && typeof originalValue.source === "string"
        ? originalValue.source
        : "production_font_family",
  };
  if (
    isJsonObject(originalValue) &&
    typeof originalValue.productionFontId === "string"
  ) {
    nextValue.productionFontId = originalValue.productionFontId;
  }
  delete nextValue.weight;
  return nextValue;
}
