import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveCursorComponentAtHeight } from "./cursorComponentResolver.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";
import type { TypographyStyleContract } from "./previewComponentContracts.js";
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
  const textBox = asRecord(config.textBox);
  const surfaceSlot = asRecord(textBox.surfaceSlot);
  const cursorSlot = asRecord(textBox.cursorSlot);
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
    componentPresetConfig(componentBaseConfigs, "surface", surfaceSlot.presetId),
    asRecord(surfaceSlot.overrides),
  );
  const embeddedCursorConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "cursor", cursorSlot.presetId),
    asRecord(cursorSlot.overrides),
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
    placeholder: optionalString(textBox, "placeholder"),
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
    typography: requiredTypographyStyle(
      textBox,
      "typography",
      "component.textBox.typography",
    ),
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
  };
}

function requiredTypographyStyle(
  value: Record<string, unknown>,
  key: string,
  path: string,
): TypographyStyleContract {
  const typography = asRecord(value[key]);
  const lineHeight = typography.lineHeight;
  if (
    typeof lineHeight !== "string" &&
    !(typeof lineHeight === "number" && Number.isFinite(lineHeight))
  ) {
    throw new Error(`Missing line height value ${path}.lineHeight`);
  }

  return {
    fontFamilyId: requiredString(
      typography,
      "fontFamilyId",
      `${path}.fontFamilyId`,
    ),
    weight: requiredString(typography, "weight", `${path}.weight`),
    style: requiredString(typography, "style", `${path}.style`),
    sizeToken: requiredString(typography, "sizeToken", `${path}.sizeToken`),
    lineHeight,
  };
}
