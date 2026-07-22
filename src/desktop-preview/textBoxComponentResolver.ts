import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentVariantConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  type TypographyStyleContract,
} from "./previewComponentContracts.js";
import {
  asRecord,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredString,
  requiredStringPair,
  requiredTypographyStyle,
} from "./componentResolverCommon.js";
import { resolveCursorComponentAtHeight } from "./cursorComponentResolver.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
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
    componentVariantConfig(componentBaseConfigs, "surface", surfaceSlot.variantReference),
    asRecord(surfaceSlot.overrides),
  );
  const embeddedCursorConfig = mergeComponentDefaults(
    componentVariantConfig(componentBaseConfigs, "cursor", cursorSlot.variantReference),
    asRecord(cursorSlot.overrides),
  );
  const leftIconRowInputs = textBoxIconRowInputs(inputs, "left");
  const rightIconRowInputs = textBoxIconRowInputs(inputs, "right");
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
      Math.floor(requiredNumber(inputs, "maxLines", "component.textBox.input.maxLines")),
    ),
    padding: { xToken: padding.first, yToken: padding.second },
    text: optionalString(inputs, "sampleText"),
    placeholder: optionalString(inputs, "placeholder"),
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
    iconGapToken: requiredString(inputs, "iconGap", "component.textBox.input.iconGap"),
    leftIconRow: resolveOptionalIconRowComponentFromRecords(
      inputs,
      leftIconRowInputs,
      "leftIconRowSlot",
      componentBaseConfigs,
      `${id}.leftIcons`,
    ),
    rightIconRow: resolveOptionalIconRowComponentFromRecords(
      inputs,
      rightIconRowInputs,
      "rightIconRowSlot",
      componentBaseConfigs,
      `${id}.rightIcons`,
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

function resolveOptionalIconRowComponentFromRecords(
  parentInputs: Record<string, unknown>,
  iconRowInputs: Record<string, unknown>,
  slotInputKey: string,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
) {
  const icons = iconRowInputs.icons;
  const hasIcons = Array.isArray(icons)
    && icons.some((entry) => typeof entry === "string" && entry.trim().length > 0);
  if (!hasIcons) {
    return {
      id,
      orientation: requiredIconRowOrientation(iconRowInputs),
      gapToken: requiredString(iconRowInputs, "gap", "component.textBox.input.iconRow.gap"),
      items: [],
    };
  }

  const iconRowSlot = componentInputSlot(
    parentInputs,
    slotInputKey,
    `component.textBox.input.${slotInputKey}`,
  );
  const iconRowConfig = componentVariantConfig(
    componentBaseConfigs,
    "iconRow",
    iconRowSlot.variantReference,
  );
  return resolveIconRowComponentFromRecords(
    mergeComponentDefaults(iconRowConfig, iconRowSlot.overrides),
    iconRowInputsFromParent(
      iconRowInputs,
      componentBaseConfigs,
    ),
    componentBaseConfigs,
    id,
  );
}

function componentInputSlot(
  inputs: Record<string, unknown>,
  slotKey: string,
  path: string,
) {
  const slot = asRecord(inputs[slotKey]);
  return {
    variantReference: requiredString(slot, "variantReference", `${path}.variantReference`),
    overrides: asRecord(slot.overrides),
  };
}

function textBoxIconRowInputs(inputs: Record<string, unknown>, side: "left" | "right") {
  const nested = asRecord(inputs[`${side}IconRowInputs`]);
  if (Object.keys(nested).length > 0) {
    return nested;
  }

  const icons = inputs[`${side}Icons`];
  return {
    size: requiredString(inputs, "iconRowSize", "component.textBox.input.iconRowSize"),
    gap: requiredString(inputs, "iconRowGap", "component.textBox.input.iconRowGap"),
    orientation: requiredString(inputs, "iconRowOrientation", "component.textBox.input.iconRowOrientation"),
    icons: Array.isArray(icons) && icons.every((entry) => typeof entry === "string")
      ? icons
      : [],
    actionIconNumber: optionalNumber(inputs, `${side}ActionIconNumber`, 0),
    actionBackgroundAlpha: optionalNumber(inputs, `${side}ActionBackgroundAlpha`, 1),
    actionBackgroundColor: optionalString(inputs, `${side}ActionBackgroundColor`),
    actionIconColor: optionalString(inputs, `${side}ActionIconColor`),
  };
}

function requiredIconRowOrientation(value: Record<string, unknown>): "horizontal" | "vertical" {
  const orientation = requiredString(value, "orientation", "component.textBox.input.iconRow.orientation");
  if (orientation === "horizontal" || orientation === "vertical") {
    return orientation;
  }

  throw new Error(`Unsupported icon row orientation ${orientation}`);
}

function iconRowInputsFromParent(
  parentInputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
) {
  const icons = Array.isArray(parentInputs.icons)
    ? parentInputs.icons.filter((icon): icon is string => typeof icon === "string" && icon.trim().length > 0)
    : [];
  const buttonVariantReference = Object.keys(asRecord(componentBaseConfigs.variants)).find((reference) =>
    reference.endsWith("_button::variant::default"),
  );
  if (!buttonVariantReference) throw new Error("Missing Button.default variant for TextBox icon rows");
  return {
    gap: parentInputs.gap,
    orientation: parentInputs.orientation,
    items: icons.map((iconToken, index) => ({
      id: `button_${String(index + 1).padStart(3, "0")}`,
      buttonVariantReference,
      contentMode: "icon",
      state: "normal",
      iconToken,
      text: "",
      pushTrigger: false,
      pushElapsedMs: 0,
      buttonOverrides: {},
    })),
  };
}
