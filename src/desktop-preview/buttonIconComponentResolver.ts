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
  requiredPlacement,
  requiredString,
} from "./componentResolverCommon.js";
import type { ButtonIconDesignContract } from "./buttonIconComponentContract.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

function labelPreview(
  preview: Record<string, unknown>,
  showSubtext: boolean,
): Record<string, unknown> {
  return {
    ...preview,
    sampleSubtext: showSubtext ? preview.sampleSubtext : "",
  };
}

function optionalInputStringOrConfiguredValue(
  value: Record<string, unknown>,
  key: string,
  configuredValue: string,
) {
  const raw = value[key];
  return typeof raw === "string" && raw.trim() ? raw : configuredValue;
}

export function resolveButtonIconComponent(
  payload: DesignPreviewPayload,
): ButtonIconDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveButtonIconComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.buttonIcon",
  );
}

export function resolveButtonIconComponentFromRecords(
  config: Record<string, unknown>,
  preview: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): ButtonIconDesignContract {
  const buttonIcon = asRecord(config.buttonIcon);
  const labelSlot = asRecord(buttonIcon.labelSlot);
  const surfaceSlot = asRecord(buttonIcon.surfaceSlot);
  const showLabel = requiredBoolean(
    labelSlot,
    "showLabel",
    "component.buttonIcon.label.showLabel",
  );
  const showSubtext = requiredBoolean(
    labelSlot,
    "showSubtext",
    "component.buttonIcon.label.showSubtext",
  );
  const overrides = asRecord(labelSlot.overrides);
  const embeddedLabelConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "label", labelSlot.presetId),
    overrides,
  );
  const sizeMode = requiredString(
    buttonIcon,
    "sizeMode",
    "component.buttonIcon.sizeMode",
  );
  if (sizeMode !== "fixed" && sizeMode !== "iconSize") {
    throw new Error(`Unsupported button icon size mode ${sizeMode}`);
  }
  const buttonSize = requiredNumber(buttonIcon, "size", "component.buttonIcon.size");
  const iconSizeToken = requiredString(
    buttonIcon,
    "iconSizeToken",
    "component.buttonIcon.iconSizeToken",
  );
  const iconPaddingToken = requiredString(
    buttonIcon,
    "iconPadding",
    "component.buttonIcon.iconPadding",
  );
  const embeddedSurfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", surfaceSlot.presetId),
    asRecord(surfaceSlot.overrides),
  );

  return {
    id,
    sizeMode,
    buttonSize,
    iconSizeToken,
    iconPaddingToken,
    iconToken: optionalInputStringOrConfiguredValue(
      preview,
      "iconToken",
      requiredString(buttonIcon, "iconToken", "component.buttonIcon.iconToken"),
    ),
    iconColorToken: requiredString(
      buttonIcon,
      "iconColorToken",
      "component.buttonIcon.iconColorToken",
    ),
    backgroundPaletteColor: optionalString(buttonIcon, "backgroundPaletteColor") || undefined,
    iconPaletteColor: optionalString(buttonIcon, "iconPaletteColor") || undefined,
    labelSlot: {
      showLabel,
      showSubtext,
      placement: requiredPlacement(
        labelSlot,
        "placement",
        "component.buttonIcon.label.placement",
      ),
      label: showLabel
        ? resolveLabelComponentFromRecords(
            embeddedLabelConfig,
            labelPreview(preview, showSubtext),
            componentBaseConfigs,
            `${id}.label`,
          )
        : undefined,
    },
    surface: resolveSurfaceComponentAtSize(
      embeddedSurfaceConfig,
      { width: buttonSize, height: buttonSize },
      `${id}.surface`,
    ),
  };
}
