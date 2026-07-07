import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  STANDARD_IOS_KEYBOARD_LAYOUT,
  parseKeyboardRows,
} from "../domain/keyboards/standardKeyboardLayout.js";
import type { KeyboardDesignContract, KeyboardMode } from "./keyboardComponentContract.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalString,
  parseObject,
  requiredAlpha,
  requiredBoolean,
  optionalNumber,
  requiredNumber,
  requiredString,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import type { TypographyStyleContract } from "./previewComponentContracts.js";

export function resolveKeyboardComponent(
  payload: DesignPreviewPayload,
): KeyboardDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const keyboard = asRecord(config.keyboard);
  const style = asRecord(config.style);
  const pressedEffect = requiredString(
    keyboard,
    "pressedEffect",
    "component.keyboard.pressedEffect",
  );
  if (pressedEffect !== "popup" && pressedEffect !== "scale" && pressedEffect !== "none") {
    throw new Error(`Unsupported keyboard pressed effect ${pressedEffect}`);
  }
  const language = requiredString(
    keyboard,
    "language",
    "component.keyboard.language",
  );
  const fullText = optionalString(preview, "text");
  const currentCharacterIndex = optionalNumber(preview, "currentCharacter", 1);
  const currentCharacter = characterAtPosition(fullText, currentCharacterIndex);
  const pressedKey = currentCharacter;
  const mode = keyboardMode(currentCharacter);
  const iconRowPlacement = requiredString(
    keyboard,
    "iconRowPlacement",
    "component.keyboard.iconRowPlacement",
  );
  if (iconRowPlacement !== "top" && iconRowPlacement !== "bottom") {
    throw new Error(`Unsupported keyboard icon row placement ${iconRowPlacement}`);
  }
  const iconButtonSlot = asRecord(keyboard.iconButtonSlot);
  const iconButtonPresetId = requiredString(
    iconButtonSlot,
    "presetId",
    "component.keyboard.iconButtonSlot.presetId",
  );
  const iconButtonOverrides = asRecord(iconButtonSlot.overrides);
  const parsedRows = parseKeyboardRows(STANDARD_IOS_KEYBOARD_LAYOUT, mode, language);
  const extraEmojis = keyboardExtraEmojis(fullText, currentCharacter);
  const rowsWithMode = mode === "emoji"
    ? emojiOnlyRows(parsedRows)
    : parsedRows;
  const rows = mode === "emoji" && extraEmojis.length
    ? withExtraEmojis(rowsWithMode, extraEmojis)
    : rowsWithMode;

  return {
    id: "component.keyboard",
    language,
    mode,
    pressedKey,
    backgroundColorToken: requiredString(
      keyboard,
      "backgroundColorToken",
      "component.keyboard.backgroundColorToken",
    ),
    backgroundAlpha: requiredAlpha(
      keyboard,
      "backgroundAlpha",
      "component.keyboard.backgroundAlpha",
    ),
    keyBackgroundColorToken: requiredString(
      keyboard,
      "keyBackgroundColorToken",
      "component.keyboard.keyBackgroundColorToken",
    ),
    specialKeyBackgroundColorToken: requiredString(
      keyboard,
      "specialKeyBackgroundColorToken",
      "component.keyboard.specialKeyBackgroundColorToken",
    ),
    pressedKeyBackgroundColorToken: requiredString(
      keyboard,
      "pressedKeyBackgroundColorToken",
      "component.keyboard.pressedKeyBackgroundColorToken",
    ),
    keyTextColorToken: requiredString(
      keyboard,
      "keyTextColorToken",
      "component.keyboard.keyTextColorToken",
    ),
    keyPaddingToken: requiredString(
      keyboard,
      "keyPadding",
      "component.keyboard.keyPadding",
    ),
    keyCornerRadiusToken: requiredString(
      keyboard,
      "keyCornerRadiusToken",
      "component.keyboard.keyCornerRadiusToken",
    ),
    keyBorderColorToken: requiredString(
      keyboard,
      "keyBorderColorToken",
      "component.keyboard.keyBorderColorToken",
    ),
    keyBorderWidth: requiredNumber(
      keyboard,
      "keyBorderWidth",
      "component.keyboard.keyBorderWidth",
    ),
    keyShadowEnabled: requiredBoolean(
      keyboard,
      "keyShadowEnabled",
      "component.keyboard.keyShadowEnabled",
    ),
    pressedEffect,
    specialKeyTextScale: requiredNumber(
      keyboard,
      "specialKeyTextScale",
      "component.keyboard.specialKeyTextScale",
    ),
    emojiScale: requiredNumber(
      keyboard,
      "emojiScale",
      "component.keyboard.emojiScale",
    ),
    typography: requiredTypographyStyle(
      keyboard,
      "typography",
      "component.keyboard.typography",
    ),
    iconRowPlacement,
    iconRowsHeight: requiredNumber(
      keyboard,
      "iconRowsHeight",
      "component.keyboard.iconRowsHeight",
    ),
    iconRowsEdgePaddingToken: requiredString(
      keyboard,
      "iconRowsEdgePadding",
      "component.keyboard.iconRowsEdgePadding",
    ),
    leftIconRow: resolveKeyboardIconRow(
      keyboard,
      "leftIconRowSlot",
      "leftIconRowInputs",
      optionalStringArray(preview, "leftIcons", ["app_language"]),
      iconButtonPresetId,
      iconButtonOverrides,
      componentBaseConfigs,
      "component.keyboard.leftIconRow",
    ),
    centerIconRow: resolveKeyboardIconRow(
      keyboard,
      "centerIconRowSlot",
      "centerIconRowInputs",
      optionalStringArray(preview, "centerIcons", []),
      iconButtonPresetId,
      iconButtonOverrides,
      componentBaseConfigs,
      "component.keyboard.centerIconRow",
    ),
    rightIconRow: resolveKeyboardIconRow(
      keyboard,
      "rightIconRowSlot",
      "rightIconRowInputs",
      optionalStringArray(preview, "rightIcons", ["media_mic"]),
      iconButtonPresetId,
      iconButtonOverrides,
      componentBaseConfigs,
      "component.keyboard.rightIconRow",
    ),
    rows,
    surface: resolveSurfaceStyle(style),
  };
}

function requiredTypographyStyle(
  value: Record<string, unknown>,
  key: string,
  path: string,
): TypographyStyleContract {
  const typography = asRecord(value[key]);
  const lineHeight = typography.lineHeight;
  return {
    fontFamilyId: requiredString(
      typography,
      "fontFamilyId",
      `${path}.fontFamilyId`,
    ),
    weight: requiredString(typography, "weight", `${path}.weight`),
    style: requiredString(typography, "style", `${path}.style`),
    sizeToken: requiredString(typography, "sizeToken", `${path}.sizeToken`),
    lineHeight: typeof lineHeight === "number"
      ? lineHeight
      : requiredString(typography, "lineHeight", `${path}.lineHeight`),
  };
}

function resolveKeyboardIconRow(
  keyboard: Record<string, unknown>,
  slotKey: string,
  inputsKey: string,
  icons: readonly string[],
  iconButtonPresetId: string,
  iconButtonOverrides: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
) {
  const slot = asRecord(keyboard[slotKey]);
  const inputs = {
    ...asRecord(keyboard[inputsKey]),
    buttonIconPresetId: iconButtonPresetId,
    buttonIconOverrides: iconButtonOverrides,
    icons: [...icons],
  };
  const config = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "iconRow",
      requiredString(slot, "presetId", `component.keyboard.${slotKey}.presetId`),
    ),
    asRecord(slot.overrides),
  );
  return resolveIconRowComponentFromRecords(config, inputs, componentBaseConfigs, id);
}

function keyboardMode(pressedKey: string): KeyboardMode {
  if (pressedKey) {
    if (/\p{Extended_Pictographic}/u.test(pressedKey)) return "emoji";
    if (/^[A-ZÁÉÍÓÚÜÑ]$/u.test(pressedKey)) return "shift";
    if (/^[0-9]$/u.test(pressedKey)) return "numeric";
    if (/^[¿?¡!.,;:'"()[\]{}#%^*+=_/\\|~<>€£¥·-]$/u.test(pressedKey)) {
      return "symbols";
    }
  }

  return STANDARD_IOS_KEYBOARD_LAYOUT.defaultMode;
}

function optionalStringArray(
  value: Record<string, unknown>,
  key: string,
  fallback: readonly string[],
) {
  const raw = value[key];
  if (Array.isArray(raw) && raw.every((entry) => typeof entry === "string")) {
    return raw.filter((entry) => entry.trim().length > 0);
  }

  return fallback;
}

function keyboardExtraEmojis(fullText: string, currentCharacter: string) {
  const source = `${currentCharacter}${fullText}`;
  return textCharacters(source).filter((entry) => /\p{Extended_Pictographic}/u.test(entry));
}

function characterAtPosition(fullText: string, position: number) {
  const characters = textCharacters(fullText);
  const index = Math.max(0, Math.min(characters.length - 1, Math.floor(position) - 1));
  return characters[index] ?? "";
}

function textCharacters(value: string) {
  const segmenterConstructor = (Intl as unknown as {
    Segmenter?: new (
      locale?: string,
      options?: { granularity?: "grapheme" },
    ) => { segment(input: string): Iterable<{ segment: string }> };
  }).Segmenter;

  if (segmenterConstructor) {
    return Array.from(
      new segmenterConstructor(undefined, { granularity: "grapheme" }).segment(value),
      (entry) => entry.segment,
    );
  }

  return Array.from(value);
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
  extraEmojis: readonly string[],
) {
  const defaultEmojis = new Set(
    rows
      .flat()
      .filter((key) => /\p{Extended_Pictographic}/u.test(key.label))
      .map((key) => key.label),
  );
  const uniqueExtraEmojis = [...new Set(extraEmojis.map((emoji) => emoji.trim()))]
    .filter((emoji) => emoji.length > 0 && !defaultEmojis.has(emoji));
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

function emojiOnlyRows(rows: ReturnType<typeof parseKeyboardRows>) {
  return rows
    .map((row) =>
      row.filter((key) =>
        key.kind === "character" && /\p{Extended_Pictographic}/u.test(key.label),
      ),
    )
    .filter((row) => row.length > 0);
}
