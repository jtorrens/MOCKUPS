import {
  cloneJson,
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

function defaultTokenGroupValue(groupKey: string): JsonValue {
  return groupKey === "messages" ? [] : {};
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
  if (isJsonObject(root.chatBubbles)) {
    delete root.chatBubbles.shadow;
  }
  const modes = isJsonObject(root.modes) ? root.modes : {};
  for (const mode of ["light", "dark"] as const) {
    const modeRoot = isJsonObject(modes[mode]) ? modes[mode] : undefined;
    if (!modeRoot) continue;
    delete modeRoot.cursor;
    if (isJsonObject(modeRoot.chatBubbles)) {
      delete modeRoot.chatBubbles.shadow;
    }
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
  const avatars = isJsonObject(root.avatars) ? root.avatars : {};
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
      header: {
        ...light.header,
        avatarBorderColor:
          typeof light.header.avatarBorderColor === "string"
            ? light.header.avatarBorderColor
            : "#ffffff",
      },
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
      header: {
        ...dark.header,
        avatarBorderColor:
          typeof dark.header.avatarBorderColor === "string"
            ? dark.header.avatarBorderColor
            : "#1c1c1e",
      },
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
    avatarSize: numberValue(
      header.avatarSize,
      numberValue(avatars.headerSize, 56),
    ),
    avatarCornerRadius: numberValue(
      header.avatarCornerRadius,
      Math.round(
        numberValue(header.avatarSize, numberValue(avatars.headerSize, 56)) *
          0.22,
      ),
    ),
    avatarBorderWidth: numberValue(header.avatarBorderWidth, 0),
    avatarShadow: header.avatarShadow === true,
    subtitleBottomPadding: numberValue(header.subtitleBottomPadding, 10),
    elementGap: numberValue(
      header.elementGap,
      numberValue(avatars.gap, 8),
    ),
    sidePadding: numberValue(
      header.sidePadding,
      numberValue(header.elementGap, numberValue(avatars.gap, 8)),
    ),
    iconSize: numberValue(header.iconSize, 24),
    leftIconTokens:
      typeof header.leftIconTokens === "string"
        ? header.leftIconTokens
        : "nav_chevron_left",
    rightIconTokens:
      typeof header.rightIconTokens === "string"
        ? header.rightIconTokens
        : "media_camera, phone_call",
  };
  root.chatBubbles = {
    ...chatBubbles,
    avatarSize: numberValue(
      chatBubbles.avatarSize,
      numberValue(avatars.defaultSize, 32),
    ),
    avatarGap: numberValue(
      chatBubbles.avatarGap,
      numberValue(avatars.gap, 8),
    ),
    shadowEnabled: chatBubbles.shadowEnabled === true,
    media: {
      ...chatBubbleMedia,
      borderWidth: numberValue(chatBubbleMedia.borderWidth, 0),
      cornerRadius: numberValue(
        chatBubbleMedia.cornerRadius,
        numberValue(chatBubbles.radius, 18),
      ),
      shadowEnabled: chatBubbleMedia.shadowEnabled === true,
    },
    tail: {
      ...(isJsonObject(chatBubbles.tail) ? chatBubbles.tail : {}),
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
      scale: numberValue(
        isJsonObject(chatBubbles.tail) ? chatBubbles.tail.scale : undefined,
        1,
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
  return nextValue;
}
