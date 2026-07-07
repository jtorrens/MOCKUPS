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
import { resolveTextBoxComponentFromRecords } from "./textBoxComponentResolver.js";

export function resolveTextInputBarComponent(
  payload: DesignPreviewPayload,
): TextInputBarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const textInput = asRecord(config.textInput);
  const barSurfaceSlot = asRecord(textInput.barSurfaceSlot);
  const textBoxSlot = asRecord(textInput.textBoxSlot);
  const textBoxInputs = asRecord(textInput.textBoxInputs);
  const leftIconRowSlot = asRecord(textInput.leftIconRowSlot);
  const rightIconRowSlot = asRecord(textInput.rightIconRowSlot);
  const leftIconRowInputs = asRecord(textInput.leftIconRowInputs);
  const rightIconRowInputs = asRecord(textInput.rightIconRowInputs);
  const height = requiredNumber(textInput, "height", "component.textInput.height");
  const iconButtonPresetId = requiredString(
    textInput,
    "iconButtonPresetId",
    "component.textInput.iconButtonPresetId",
  );
  const embeddedBarSurfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", barSurfaceSlot.presetId),
    asRecord(barSurfaceSlot.overrides),
  );
  const embeddedTextBoxConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "textBox", textBoxSlot.presetId),
    asRecord(textBoxSlot.overrides),
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
    iconGapToken: requiredString(textInput, "iconGap", "component.textInput.iconGap"),
    barSurface: resolveSurfaceComponentAtSize(
      embeddedBarSurfaceConfig,
      { width: 520, height },
      "component.textInputBar.barSurface",
    ),
    textBox: resolveTextBoxComponentFromRecords(
      embeddedTextBoxConfig,
      {
        sampleText: requiredString(preview, "sampleText", "component.textInput.preview.sampleText"),
        placeholder: requiredString(
          textBoxInputs,
          "placeholder",
          "component.textInput.textBox.placeholder",
        ),
        size: `520|${height}`,
        maxWidth: 520,
      },
      componentBaseConfigs,
      "component.textInputBar.textBox",
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
        buttonIconPresetId: iconButtonPresetId,
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
        buttonIconPresetId: iconButtonPresetId,
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
