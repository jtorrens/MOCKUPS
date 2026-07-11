import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  STANDARD_IOS_KEYBOARD_LAYOUT,
  parseKeyboardRows,
} from "../shared/keyboard/standardKeyboardLayout.js";
import type { KeyboardDesignContract, KeyboardMode } from "./keyboardComponentContract.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalBoolean,
  optionalString,
  parseObject,
  requiredAlpha,
  requiredBoolean,
  optionalNumber,
  requiredNumber,
  requiredString,
  requiredTypographyStyle,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";
import { resolveIconBarComponentFromRecords } from "./iconBarComponentResolver.js";
import { requiredMotionContract } from "./previewMotionHelpers.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";

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
  const iconBarSlot = asRecord(keyboard.iconBarSlot);
  const embeddedIconBarConfig = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "iconBar",
      requiredString(iconBarSlot, "presetId", "component.keyboard.iconBarSlot.presetId"),
    ),
    asRecord(iconBarSlot.overrides),
  );
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
    heightToken: requiredString(
      keyboard,
      "heightToken",
      "component.keyboard.heightToken",
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
    iconEdgePaddingToken: requiredString(
      keyboard,
      "iconEdgePadding",
      "component.keyboard.iconEdgePadding",
    ),
    iconBar: resolveIconBarComponentFromRecords(
      embeddedIconBarConfig,
      {
        state: "idle",
        size: `${payload.previewFrame.screenWidth}|${requiredNumber(
          keyboard,
          "iconRowsHeight",
          "component.keyboard.iconRowsHeight",
        )}`,
      },
      componentBaseConfigs,
      "component.keyboard.iconBar",
    ),
    rows,
    surface: resolveSurfaceStyle(style),
    motion: requiredMotionContract(
      keyboard,
      "motion",
      "component.keyboard.motion",
    ),
    motionFrame: {
      trigger: optionalBoolean(preview, "trigger"),
      timeSeconds: optionalNumber(preview, "motionTimeSeconds", 0),
    },
  };
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

function keyboardExtraEmojis(fullText: string, currentCharacter: string) {
  const source = `${currentCharacter}${fullText}`;
  return textGraphemes(source).filter((entry) => /\p{Extended_Pictographic}/u.test(entry));
}

function characterAtPosition(fullText: string, position: number) {
  const characters = textGraphemes(fullText);
  const index = Math.max(0, Math.min(characters.length - 1, Math.floor(position) - 1));
  return characters[index] ?? "";
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
