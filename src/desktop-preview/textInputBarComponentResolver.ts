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
  const sampleText = requiredPossiblyEmptyString(
    preview,
    "sampleText",
    "component.textInput.preview.sampleText",
  );
  const isTyping = sampleText.trim().length > 0;
  const leftIconRowInputs = asRecord(
    isTyping ? textInput.typingLeftIconRowInputs : textInput.idleLeftIconRowInputs,
  );
  const rightIconRowInputs = asRecord(
    isTyping ? textInput.typingRightIconRowInputs : textInput.idleRightIconRowInputs,
  );
  const height = requiredNumber(textInput, "height", "component.textInput.height");
  const textBoxButtonIconPresetId = requiredString(
    textBoxInputs,
    "buttonIconPresetId",
    "component.textInput.textBox.buttonIconPresetId",
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
        leftIconRowPresetId: requiredString(
          textBoxInputs,
          "leftIconRowPresetId",
          "component.textInput.textBox.leftIconRowPresetId",
        ),
        leftIconRowInputs: iconRowInputsForTextBox(
          textBoxInputs,
          leftIconRowInputs,
          "left",
          textBoxButtonIconPresetId,
        ),
        rightIconRowPresetId: requiredString(
          textBoxInputs,
          "rightIconRowPresetId",
          "component.textInput.textBox.rightIconRowPresetId",
        ),
        rightIconRowInputs: iconRowInputsForTextBox(
          textBoxInputs,
          rightIconRowInputs,
          "right",
          textBoxButtonIconPresetId,
        ),
        buttonIconPresetId: textBoxButtonIconPresetId,
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
  };
}

function iconRowInputsForTextBox(
  textBoxInputs: Record<string, unknown>,
  stateInputs: Record<string, unknown>,
  side: "left" | "right",
  buttonIconPresetId: string,
) {
  const variantIcons = requiredIconList(
    textBoxInputs,
    `${side}Icons`,
    `component.textInput.textBox.${side}Icons`,
  );
  const stateIcons = stateInputs.icons;
  return {
    ...stateInputs,
    size: requiredNumber(
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
    icons: variantIcons.length > 0 ? variantIcons : stateIcons,
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
