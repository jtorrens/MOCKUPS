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
  requiredPossiblyEmptyString,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveIconBarComponentFromRecords } from "./iconBarComponentResolver.js";
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
  const iconBarSlot = asRecord(textInput.iconBarSlot);
  const textBoxInputs = asRecord(textInput.textBoxInputs);
  const leftTextBoxIconRowSlot = componentInputSlot(
    textBoxInputs,
    "leftIconRowSlot",
    "component.textInput.textBox.leftIconRowSlot",
  );
  const rightTextBoxIconRowSlot = componentInputSlot(
    textBoxInputs,
    "rightIconRowSlot",
    "component.textInput.textBox.rightIconRowSlot",
  );
  const sampleText = requiredPossiblyEmptyString(
    textBoxInputs,
    "sampleText",
    "component.textInput.textBox.sampleText",
  );
  const availableWidth = Math.max(
    1,
    requiredNumber(preview, "availableWidth", "component.textInputBar.input.availableWidth"),
  );
  const isTyping = sampleText.trim().length > 0;
  const height = requiredNumber(textInput, "height", "component.textInput.height");
  const embeddedBarSurfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", barSurfaceSlot.presetId),
    asRecord(barSurfaceSlot.overrides),
  );
  const embeddedTextBoxConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "textBox", textBoxSlot.presetId),
    asRecord(textBoxSlot.overrides),
  );
  const embeddedIconBarConfig = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "iconBar",
      requiredString(iconBarSlot, "presetId", "component.textInput.iconBarSlot.presetId"),
    ),
    asRecord(iconBarSlot.overrides),
  );

  const resolvedTextBox = resolveTextBoxComponentFromRecords(
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
      ),
      rightIconRowSlot: rightTextBoxIconRowSlot,
      rightIconRowInputs: iconRowInputsForTextBox(
        textBoxInputs,
        "right",
      ),
      iconGap: requiredString(
        textBoxInputs,
        "iconGap",
        "component.textInput.textBox.iconGap",
      ),
      size: `${availableWidth}|${height}`,
      maxWidth: availableWidth,
    },
    componentBaseConfigs,
    "component.textInputBar.textBox",
  );

  return {
    id: "component.textInputBar",
    availableWidth,
    height,
    barPadding: toSpacingPair(requiredStringPair(
      textInput,
      "barPadding",
      "component.textInput.barPadding",
    )),
    barSurface: resolveSurfaceComponentAtSize(
      embeddedBarSurfaceConfig,
      { width: availableWidth, height },
      "component.textInputBar.barSurface",
    ),
    iconGapToken: requiredString(textInput, "iconGap", "component.textInput.iconGap"),
    iconBar: resolveIconBarComponentFromRecords(
      embeddedIconBarConfig,
      {
        state: isTyping ? "active" : "idle",
        size: `${availableWidth}|${height}`,
      },
      componentBaseConfigs,
      "component.textInputBar.iconBar",
    ),
    textBox: {
      ...resolvedTextBox,
      typography: {
        ...resolvedTextBox.typography,
        fontFamilyId: "theme.system",
      },
    },
  };
}

function componentInputSlot(
  inputs: Record<string, unknown>,
  slotKey: string,
  path: string,
) {
  const slot = asRecord(inputs[slotKey]);
  return {
    presetId: requiredString(slot, "presetId", `${path}.presetId`),
    overrides: asRecord(slot.overrides),
  };
}

function iconRowInputsForTextBox(
  textBoxInputs: Record<string, unknown>,
  side: "left" | "right",
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
  };
}

function toSpacingPair(pair: { first: string; second: string }) {
  return { xToken: pair.first, yToken: pair.second };
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
