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
  const leftIconRowSlot = asRecord(
    isTyping ? textInput.typingLeftIconRowSlot : textInput.idleLeftIconRowSlot,
  );
  const rightIconRowSlot = asRecord(
    isTyping ? textInput.typingRightIconRowSlot : textInput.idleRightIconRowSlot,
  );
  const leftIconRowInputs = asRecord(
    isTyping ? textInput.typingLeftIconRowInputs : textInput.idleLeftIconRowInputs,
  );
  const rightIconRowInputs = asRecord(
    isTyping ? textInput.typingRightIconRowInputs : textInput.idleRightIconRowInputs,
  );
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
        leftIconRowPresetId: leftIconRowSlot.presetId,
        leftIconRowInputs: iconRowInputsFromParent(
          leftIconRowInputs,
          iconButtonPresetId,
        ),
        rightIconRowPresetId: rightIconRowSlot.presetId,
        rightIconRowInputs: iconRowInputsFromParent(
          rightIconRowInputs,
          iconButtonPresetId,
        ),
        buttonIconPresetId: iconButtonPresetId,
        iconGap: requiredString(textInput, "iconGap", "component.textInput.iconGap"),
        size: `520|${height}`,
        maxWidth: 520,
      },
      componentBaseConfigs,
      "component.textInputBar.textBox",
    ),
  };
}

function iconRowInputsFromParent(
  parentInputs: Record<string, unknown>,
  buttonIconPresetId: string,
) {
  return {
    ...parentInputs,
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
