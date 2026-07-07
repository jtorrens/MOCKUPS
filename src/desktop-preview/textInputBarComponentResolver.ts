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
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";

export function resolveTextInputBarComponent(
  payload: DesignPreviewPayload,
): TextInputBarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const textInput = asRecord(config.textInput);
  const surfaceSlot = asRecord(textInput.surfaceSlot);
  const leftIconRowSlot = asRecord(textInput.leftIconRowSlot);
  const rightIconRowSlot = asRecord(textInput.rightIconRowSlot);
  const leftIconRowInputs = asRecord(textInput.leftIconRowInputs);
  const rightIconRowInputs = asRecord(textInput.rightIconRowInputs);
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
  const embeddedLeftIconRowConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "iconRow", leftIconRowSlot.presetId),
    asRecord(leftIconRowSlot.overrides),
  );
  const embeddedRightIconRowConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "iconRow", rightIconRowSlot.presetId),
    asRecord(rightIconRowSlot.overrides),
  );

  return {
    id: "component.textInputBar",
    height,
    barPadding: toSpacingPair(requiredStringPair(
      textInput,
      "barPadding",
      "component.textInput.barPadding",
    )),
    textPadding: toSpacingPair(requiredStringPair(
      textInput,
      "textPadding",
      "component.textInput.textPadding",
    )),
    iconGapToken: requiredString(textInput, "iconGap", "component.textInput.iconGap"),
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
    leftIconRow: resolveIconRowComponentFromRecords(
      embeddedLeftIconRowConfig,
      {
        size: requiredNumber(leftIconRowInputs, "size", "component.textInput.leftIconRow.size"),
        gap: requiredString(leftIconRowInputs, "gap", "component.textInput.leftIconRow.gap"),
        orientation: requiredString(
          leftIconRowInputs,
          "orientation",
          "component.textInput.leftIconRow.orientation",
        ),
        buttonIconPresetId: requiredString(
          leftIconRowInputs,
          "buttonIconPresetId",
          "component.textInput.leftIconRow.buttonIconPresetId",
        ),
        icons: requiredStringArray(preview, "leftIcons", "component.textInput.input.leftIcons"),
      },
      componentBaseConfigs,
      "component.textInputBar.leftIcons",
    ),
    rightIconRow: resolveIconRowComponentFromRecords(
      embeddedRightIconRowConfig,
      {
        size: requiredNumber(rightIconRowInputs, "size", "component.textInput.rightIconRow.size"),
        gap: requiredString(rightIconRowInputs, "gap", "component.textInput.rightIconRow.gap"),
        orientation: requiredString(
          rightIconRowInputs,
          "orientation",
          "component.textInput.rightIconRow.orientation",
        ),
        buttonIconPresetId: requiredString(
          rightIconRowInputs,
          "buttonIconPresetId",
          "component.textInput.rightIconRow.buttonIconPresetId",
        ),
        icons: requiredStringArray(preview, "rightIcons", "component.textInput.input.rightIcons"),
      },
      componentBaseConfigs,
      "component.textInputBar.rightIcons",
    ),
  };
}

function toSpacingPair(pair: { first: string; second: string }) {
  return { xToken: pair.first, yToken: pair.second };
}

function requiredStringArray(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (Array.isArray(raw) && raw.every((entry) => typeof entry === "string")) {
    return raw;
  }

  throw new Error(`Missing string array value ${path}`);
}
