import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import type { TextInputBarDesignContract } from "./textInputBarComponentContract.js";
import {
  asRecord,
  parseObject,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveTextInputBarComponent(
  payload: DesignPreviewPayload,
): TextInputBarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const textInput = asRecord(config.textInput);
  const surfaceSlot = asRecord(textInput.surfaceSlot);
  const placeholder = requiredString(
    textInput,
    "placeholder",
    "component.textInput.placeholder",
  );
  const height = requiredNumber(textInput, "height", "component.textInput.height");
  const embeddedSurfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", surfaceSlot.presetId),
    asRecord(surfaceSlot.overrides),
  );

  return {
    id: "component.textInputBar",
    height,
    text: requiredString(preview, "sampleText", "component.textInput.preview.sampleText"),
    placeholder,
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
    surface: resolveSurfaceComponentAtSize(
      embeddedSurfaceConfig,
      { width: 520, height },
      "component.textInputBar.surface",
    ),
  };
}
