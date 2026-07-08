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
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
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
  const leftTextBoxIconRowSlot = componentInputSlot(
    textBoxInputs,
    "leftIconRowSlot",
    "leftIconRowPresetId",
    "component.textInput.textBox.leftIconRowSlot",
  );
  const rightTextBoxIconRowSlot = componentInputSlot(
    textBoxInputs,
    "rightIconRowSlot",
    "rightIconRowPresetId",
    "component.textInput.textBox.rightIconRowSlot",
  );
  const textBoxButtonIconSlot = componentInputSlot(
    textBoxInputs,
    "buttonIconSlot",
    "buttonIconPresetId",
    "component.textInput.textBox.buttonIconSlot",
  );
  const sampleText = requiredPossiblyEmptyString(
    preview,
    "sampleText",
    "component.textInput.preview.sampleText",
  );
  const isTyping = sampleText.trim().length > 0;
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

  return {
    id: "component.textInputBar",
    height,
    barPadding: toSpacingPair(requiredStringPair(
      textInput,
      "barPadding",
      "component.textInput.barPadding",
    )),
    barSurface: resolveSurfaceComponentAtSize(
      embeddedBarSurfaceConfig,
      { width: 520, height },
      "component.textInputBar.barSurface",
    ),
    iconGapToken: requiredString(textInput, "iconGap", "component.textInput.iconGap"),
    leftIconRow: resolveTextInputIconRow(
      textInput,
      isTyping ? "typingLeftIconRowSlot" : "idleLeftIconRowSlot",
      isTyping ? "typingLeftIconRowInputs" : "idleLeftIconRowInputs",
      iconButtonPresetId,
      componentBaseConfigs,
      "component.textInputBar.leftIcons",
    ),
    textBox: resolveTextBoxComponentFromRecords(
      embeddedTextBoxConfig,
      {
        sampleText,
        placeholder: requiredString(
          textBoxInputs,
          "placeholder",
          "component.textInput.textBox.placeholder",
        ),
        maxLines: requiredNumber(
          textBoxInputs,
          "maxLines",
          "component.textInput.textBox.maxLines",
        ),
        leftIconRowSlot: leftTextBoxIconRowSlot,
        leftIconRowInputs: iconRowInputsForTextBox(
          textBoxInputs,
          "left",
          textBoxButtonIconSlot.presetId,
        ),
        rightIconRowSlot: rightTextBoxIconRowSlot,
        rightIconRowInputs: iconRowInputsForTextBox(
          textBoxInputs,
          "right",
          textBoxButtonIconSlot.presetId,
        ),
        buttonIconSlot: textBoxButtonIconSlot,
        iconGap: requiredString(
          textBoxInputs,
          "iconGap",
          "component.textInput.textBox.iconGap",
        ),
        size: `520|${height}`,
        maxWidth: 520,
      },
      componentBaseConfigs,
      "component.textInputBar.textBox",
    ),
    rightIconRow: resolveTextInputIconRow(
      textInput,
      isTyping ? "typingRightIconRowSlot" : "idleRightIconRowSlot",
      isTyping ? "typingRightIconRowInputs" : "idleRightIconRowInputs",
      iconButtonPresetId,
      componentBaseConfigs,
      "component.textInputBar.rightIcons",
    ),
  };
}

function resolveTextInputIconRow(
  textInput: Record<string, unknown>,
  slotKey: string,
  inputsKey: string,
  iconButtonPresetId: string,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
) {
  const slot = asRecord(textInput[slotKey]);
  const inputs = {
    ...asRecord(textInput[inputsKey]),
    buttonIconPresetId: iconButtonPresetId,
  };
  const config = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "iconRow",
      requiredString(slot, "presetId", `component.textInput.${slotKey}.presetId`),
    ),
    asRecord(slot.overrides),
  );
  return resolveIconRowComponentFromRecords(
    config,
    inputs,
    componentBaseConfigs,
    id,
  );
}

function componentInputSlot(
  inputs: Record<string, unknown>,
  slotKey: string,
  legacyPresetKey: string,
  path: string,
) {
  const slot = asRecord(inputs[slotKey]);
  const presetId = typeof slot.presetId === "string"
    ? slot.presetId
    : requiredString(inputs, legacyPresetKey, `${path}.presetId`);
  return {
    presetId,
    overrides: asRecord(slot.overrides),
  };
}

function iconRowInputsForTextBox(
  textBoxInputs: Record<string, unknown>,
  side: "left" | "right",
  buttonIconPresetId: string,
) {
  const variantIcons = requiredIconList(
    textBoxInputs,
    `${side}Icons`,
    `component.textInput.textBox.${side}Icons`,
  );
  return {
    size: requiredString(
      textBoxInputs,
      "iconRowSize",
      "component.textInput.textBox.iconRowSize",
    ),
    gap: requiredString(
      textBoxInputs,
      "iconRowGap",
      "component.textInput.textBox.iconRowGap",
    ),
    orientation: requiredString(
      textBoxInputs,
      "iconRowOrientation",
      "component.textInput.textBox.iconRowOrientation",
    ),
    icons: variantIcons,
    buttonIconPresetId,
  };
}

function toSpacingPair(pair: { first: string; second: string }) {
  return { xToken: pair.first, yToken: pair.second };
}

function requiredPossiblyEmptyString(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string") return raw;
  throw new Error(`Missing string value ${path}`);
}

function requiredIconList(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (Array.isArray(raw) && raw.every((entry) => typeof entry === "string")) {
    return raw;
  }

  throw new Error(`Missing icon list value ${path}`);
}
