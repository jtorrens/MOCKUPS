import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { mergeComponentDefaults } from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredAlpha,
  requiredBoolean,
  requiredNumber,
  requiredPlacement,
  requiredRecord,
  requiredString,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";
import type { ButtonIconDesignContract } from "./buttonIconComponentContract.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";

function labelPreview(
  preview: Record<string, unknown>,
  showSubtext: boolean,
): Record<string, unknown> {
  return {
    ...preview,
    sampleSubtext: showSubtext ? preview.sampleSubtext : "",
  };
}

function inputString(
  value: Record<string, unknown>,
  key: string,
  fallback: string,
) {
  const raw = value[key];
  return typeof raw === "string" && raw.trim() ? raw : fallback;
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
  const style = asRecord(config.style);
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
    requiredRecord(componentBaseConfigs, "label", "componentBaseConfigs.label"),
    overrides,
  );

  return {
    id,
    buttonSize: requiredNumber(buttonIcon, "size", "component.buttonIcon.size"),
    iconSize: Math.max(
      1,
      requiredNumber(buttonIcon, "size", "component.buttonIcon.size") -
        requiredNumber(buttonIcon, "iconPadding", "component.buttonIcon.iconPadding") *
          2,
    ),
    iconPadding: requiredNumber(
      buttonIcon,
      "iconPadding",
      "component.buttonIcon.iconPadding",
    ),
    iconToken: inputString(
      preview,
      "iconToken",
      requiredString(buttonIcon, "iconToken", "component.buttonIcon.iconToken"),
    ),
    backgroundColorToken: requiredString(
      buttonIcon,
      "backgroundColorToken",
      "component.buttonIcon.backgroundColorToken",
    ),
    backgroundAlpha: requiredAlpha(
      buttonIcon,
      "backgroundAlpha",
      "component.buttonIcon.backgroundAlpha",
    ),
    iconColorToken: requiredString(
      buttonIcon,
      "iconColorToken",
      "component.buttonIcon.iconColorToken",
    ),
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
            `${id}.label`,
          )
        : undefined,
    },
    surface: resolveSurfaceStyle(style),
  };
}
