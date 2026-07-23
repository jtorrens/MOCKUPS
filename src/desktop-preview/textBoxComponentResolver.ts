import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentVariantConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  type TypographyStyleContract,
} from "./previewComponentContracts.js";
import {
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredComponentVariantSlot,
  requiredNumber,
  requiredNumberPair,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
  requiredStringPair,
  requiredTypographyStyle,
} from "./componentResolverCommon.js";
import { resolveCursorComponentAtHeight } from "./cursorComponentResolver.js";
import { resolveConfiguredIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveTextBoxComponent(
  payload: DesignPreviewPayload,
): TextBoxDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveTextBoxComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.textBox",
  );
}

export function resolveTextBoxComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): TextBoxDesignContract {
  rejectVariantOwnedTextBoxInputs(inputs);
  const textBox = requiredRecord(config, "textBox", "component.textBox");
  const surfaceSlot = requiredRecord(textBox, "surfaceSlot", "component.textBox.surfaceSlot");
  const cursorSlot = requiredRecord(textBox, "cursorSlot", "component.textBox.cursorSlot");
  const leftIconRowSlot = requiredComponentVariantSlot(
    textBox,
    "leftIconRowSlot",
    "component.textBox.leftIconRowSlot",
  );
  const rightIconRowSlot = requiredComponentVariantSlot(
    textBox,
    "rightIconRowSlot",
    "component.textBox.rightIconRowSlot",
  );
  const dimensionMode = requiredString(
    textBox,
    "dimensionMode",
    "component.textBox.dimensionMode",
  );
  if (
    dimensionMode !== "fixed" &&
    dimensionMode !== "content" &&
    dimensionMode !== "growVertical"
  ) {
    throw new Error(`Unsupported text box dimension mode ${dimensionMode}`);
  }
  const size = dimensionMode === "content"
    ? {
        first: requiredNumber(inputs, "maxWidth", "component.textBox.input.maxWidth"),
        second: 0,
      }
    : requiredNumberPair(inputs, "size", "component.textBox.input.size");

  const textAlign = requiredString(textBox, "textAlign", "component.textBox.textAlign");
  if (textAlign !== "left" && textAlign !== "center" && textAlign !== "right") {
    throw new Error(`Unsupported text box text align ${textAlign}`);
  }

  const overflowMode = requiredString(
    textBox,
    "overflowMode",
    "component.textBox.overflowMode",
  );
  if (overflowMode !== "clip" && overflowMode !== "scroll") {
    throw new Error(`Unsupported text box overflow mode ${overflowMode}`);
  }

  const padding = requiredStringPair(textBox, "padding", "component.textBox.padding");
  const embeddedSurfaceConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "surface",
      requiredString(
        surfaceSlot,
        "variantReference",
        "component.textBox.surfaceSlot.variantReference",
      ),
    ),
    requiredRecord(surfaceSlot, "overrides", "component.textBox.surfaceSlot.overrides"),
  );
  const embeddedCursorConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "cursor",
      requiredString(
        cursorSlot,
        "variantReference",
        "component.textBox.cursorSlot.variantReference",
      ),
    ),
    requiredRecord(cursorSlot, "overrides", "component.textBox.cursorSlot.overrides"),
  );
  const typography = typographyWithInputSizeOverride(
    requiredTypographyStyle(
      textBox,
      "typography",
      "component.textBox.typography",
    ),
    inputs,
  );

  return {
    id,
    dimensionMode,
    size: { width: size.first, height: size.second },
    maxLines: Math.max(
      1,
      Math.floor(requiredNumber(textBox, "maxLines", "component.textBox.maxLines")),
    ),
    padding: { xToken: padding.first, yToken: padding.second },
    text: optionalString(inputs, "sampleText"),
    placeholder: requiredPossiblyEmptyString(
      textBox,
      "placeholder",
      "component.textBox.placeholder",
    ),
    textColorToken: requiredString(
      textBox,
      "textColorToken",
      "component.textBox.textColorToken",
    ),
    placeholderColorToken: requiredString(
      textBox,
      "placeholderColorToken",
      "component.textBox.placeholderColorToken",
    ),
    typography,
    textAlign,
    overflowMode,
    cursorVisible: requiredBoolean(
      cursorSlot,
      "showCursor",
      "component.textBox.cursor.showCursor",
    ),
    surface: resolveSurfaceComponentAtSize(
      embeddedSurfaceConfig,
      { width: size.first, height: size.second },
      `${id}.surface`,
    ),
    cursor: resolveCursorComponentAtHeight(embeddedCursorConfig, 1, `${id}.cursor`),
    iconGapToken: requiredString(textBox, "iconGap", "component.textBox.iconGap"),
    leftIconRow: resolveTextBoxIconRowComponentFromRecords(
      leftIconRowSlot,
      componentBaseConfigs,
      `${id}.leftIconRow`,
    ),
    rightIconRow: resolveTextBoxIconRowComponentFromRecords(
      rightIconRowSlot,
      componentBaseConfigs,
      `${id}.rightIconRow`,
    ),
    textAnimation: {
      mode: textAnimationMode(optionalString(inputs, "textAnimationMode")),
      elapsedMs: Math.max(0, optionalNumber(inputs, "textAnimationElapsedMs", 0)),
      minimumOpacity: 0.35,
    },
  };
}

function typographyWithInputSizeOverride(
  typography: TypographyStyleContract,
  inputs: Record<string, unknown>,
): TypographyStyleContract {
  const sizeToken = optionalString(inputs, "textSizeToken");
  return sizeToken ? { ...typography, sizeToken } : typography;
}

function textAnimationMode(value: string | undefined): TextBoxDesignContract["textAnimation"]["mode"] {
  return value === "pulsating" || value === "wave" ? value : "none";
}

function resolveTextBoxIconRowComponentFromRecords(
  iconRowSlot: { variantReference: string; overrides: Record<string, unknown> },
  componentBaseConfigs: Record<string, unknown>,
  id: string,
) {
  const iconRowConfig = componentVariantConfig(
    componentBaseConfigs,
    "iconRow",
    iconRowSlot.variantReference,
  );
  return resolveConfiguredIconRowComponentFromRecords(
    mergeComponentDefaults(
      iconRowConfig,
      iconRowSlot.overrides,
    ),
    componentBaseConfigs,
    id,
  );
}

function rejectVariantOwnedTextBoxInputs(inputs: Record<string, unknown>) {
  for (const key of [
    "placeholder",
    "maxLines",
    "leftIconRowSlot",
    "leftIconRowItems",
    "leftIconRowGap",
    "leftIconRowOrientation",
    "rightIconRowSlot",
    "rightIconRowItems",
    "rightIconRowGap",
    "rightIconRowOrientation",
    "iconGap",
    "leftIcons",
    "rightIcons",
    "leftIconRowInputs",
    "rightIconRowInputs",
    "iconRowSize",
    "iconRowGap",
    "iconRowOrientation",
  ]) {
    if (Object.hasOwn(inputs, key)) {
      throw new Error(`Text Box Variant-owned input '${key}' is not supported at Runtime`);
    }
  }
}
