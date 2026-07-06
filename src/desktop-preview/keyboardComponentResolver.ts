import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { KeyboardDesignContract } from "./keyboardComponentContract.js";
import {
  asRecord,
  parseObject,
  requiredAlpha,
  requiredBoolean,
  requiredIconSlots,
  requiredNumber,
  requiredString,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";

export function resolveKeyboardComponent(
  payload: DesignPreviewPayload,
): KeyboardDesignContract {
  const config = parseObject(payload.configJson);
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

  return {
    id: "component.keyboard",
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
    keyTextColorToken: requiredString(
      keyboard,
      "keyTextColorToken",
      "component.keyboard.keyTextColorToken",
    ),
    bottomIconColorToken: requiredString(
      keyboard,
      "bottomIconColorToken",
      "component.keyboard.bottomIconColorToken",
    ),
    keyPaddingToken: requiredString(
      keyboard,
      "keyPadding",
      "component.keyboard.keyPadding",
    ),
    keyCornerRadius: requiredNumber(
      keyboard,
      "keyCornerRadius",
      "component.keyboard.keyCornerRadius",
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
    bottomIconSlots: requiredIconSlots(
      keyboard,
      "bottomIconSlots",
      "component.keyboard.bottomIconSlots",
    ),
    surface: resolveSurfaceStyle(style),
  };
}
