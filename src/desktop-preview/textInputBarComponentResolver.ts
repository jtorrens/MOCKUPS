import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { TextInputBarDesignContract } from "./textInputBarComponentContract.js";
import {
  asRecord,
  parseObject,
  requiredAlpha,
  requiredNumber,
  requiredString,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";

export function resolveTextInputBarComponent(
  payload: DesignPreviewPayload,
): TextInputBarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const textInput = asRecord(config.textInput);
  const style = asRecord(config.style);
  const placeholder = requiredString(
    textInput,
    "placeholder",
    "component.textInput.placeholder",
  );

  return {
    id: "component.textInputBar",
    height: requiredNumber(textInput, "height", "component.textInput.height"),
    text: requiredString(preview, "sampleText", "component.textInput.preview.sampleText"),
    placeholder,
    backgroundColorToken: requiredString(
      textInput,
      "backgroundColorToken",
      "component.textInput.backgroundColorToken",
    ),
    backgroundAlpha: requiredAlpha(
      textInput,
      "backgroundAlpha",
      "component.textInput.backgroundAlpha",
    ),
    idleTextColorToken: requiredString(
      textInput,
      "idleTextColorToken",
      "component.textInput.idleTextColorToken",
    ),
    textSizeToken: requiredString(
      textInput,
      "textSizeToken",
      "component.textInput.textSizeToken",
    ),
    cursorColorToken: requiredString(
      textInput,
      "cursorColorToken",
      "component.textInput.cursorColorToken",
    ),
    cursorWidth: requiredNumber(
      textInput,
      "cursorWidth",
      "component.textInput.cursorWidth",
    ),
    cursorBlinkFrames: requiredNumber(
      textInput,
      "cursorBlinkFrames",
      "component.textInput.cursorBlinkFrames",
    ),
    surface: resolveSurfaceStyle(style),
  };
}
