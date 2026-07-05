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
  type AlignmentPlacementContract,
} from "./componentResolverCommon.js";
import type { LabelDesignContract } from "./labelComponentResolver.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";

export interface ButtonIconDesignContract {
  id: "component.buttonIcon";
  iconSize: number;
  iconPadding: number;
  backgroundColorToken: string;
  backgroundAlpha: number;
  iconColorToken: string;
  labelSlot: {
    showLabel: boolean;
    showSubtext: boolean;
    placement: AlignmentPlacementContract;
    label?: LabelDesignContract;
  };
  surface: {
    shadowEnabled: boolean;
    reliefEnabled: boolean;
    borderWidth: number;
    borderColorToken: string;
    cornerRadiusToken: string;
    reliefAngle: number;
    reliefExtent: number;
    reliefSpread: number;
    reliefTopIntensity: number;
    reliefBottomIntensity: number;
  };
}

function labelPreview(
  preview: Record<string, unknown>,
  showSubtext: boolean,
): Record<string, unknown> {
  return {
    ...preview,
    sampleSubtext: showSubtext ? preview.sampleSubtext : "",
  };
}

export function resolveButtonIconComponent(
  payload: DesignPreviewPayload,
): ButtonIconDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
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
    id: "component.buttonIcon",
    iconSize: requiredNumber(
      preview,
      "sampleSize",
      "component.buttonIcon.preview.sampleSize",
    ),
    iconPadding: requiredNumber(
      buttonIcon,
      "iconPadding",
      "component.buttonIcon.iconPadding",
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
            "component.buttonIcon.label",
          )
        : undefined,
    },
    surface: {
      shadowEnabled: requiredBoolean(
        style,
        "shadowEnabled",
        "component.style.shadowEnabled",
      ),
      reliefEnabled: requiredBoolean(
        style,
        "reliefEnabled",
        "component.style.reliefEnabled",
      ),
      borderWidth: requiredNumber(style, "borderWidth", "component.style.borderWidth"),
      borderColorToken: requiredString(
        style,
        "borderColorToken",
        "component.style.borderColorToken",
      ),
      cornerRadiusToken: requiredString(
        style,
        "cornerRadiusToken",
        "component.style.cornerRadiusToken",
      ),
      reliefAngle: requiredNumber(style, "reliefAngle", "component.style.reliefAngle"),
      reliefExtent: requiredNumber(style, "reliefExtent", "component.style.reliefExtent"),
      reliefSpread: requiredNumber(style, "reliefSpread", "component.style.reliefSpread"),
      reliefTopIntensity: requiredNumber(
        style,
        "reliefTopIntensity",
        "component.style.reliefTopIntensity",
      ),
      reliefBottomIntensity: requiredNumber(
        style,
        "reliefBottomIntensity",
        "component.style.reliefBottomIntensity",
      ),
    },
  };
}
