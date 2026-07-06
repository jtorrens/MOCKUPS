import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { mergeComponentDefaults } from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredPlacement,
  requiredRecord,
  requiredString,
  type AlignmentPlacementContract,
} from "./componentResolverCommon.js";
import type { LabelDesignContract } from "./labelComponentResolver.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";

export interface AvatarDesignContract {
  id: string;
  size: number;
  cornerRadiusToken: string;
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

export function resolveAvatarComponent(
  payload: DesignPreviewPayload,
): AvatarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveAvatarComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.avatar",
  );
}

export function resolveAvatarComponentFromRecords(
  config: Record<string, unknown>,
  preview: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): AvatarDesignContract {
  const avatar = asRecord(config.avatar);
  const labelSlot = asRecord(avatar.labelSlot);
  const style = asRecord(config.style);
  const showLabel = requiredBoolean(
    labelSlot,
    "showLabel",
    "component.avatar.label.showLabel",
  );
  const showSubtext = requiredBoolean(
    labelSlot,
    "showSubtext",
    "component.avatar.label.showSubtext",
  );
  const overrides = asRecord(labelSlot.overrides);
  const embeddedLabelConfig = mergeComponentDefaults(
    requiredRecord(componentBaseConfigs, "label", "componentBaseConfigs.label"),
    overrides,
  );

  return {
    id,
    size: requiredNumber(avatar, "defaultSize", "component.avatar.defaultSize"),
    cornerRadiusToken: requiredString(
      avatar,
      "cornerRadiusToken",
      "component.avatar.cornerRadiusToken",
    ),
    labelSlot: {
      showLabel,
      showSubtext,
      placement: requiredPlacement(
        labelSlot,
        "placement",
        "component.avatar.label.placement",
      ),
      label: showLabel
        ? resolveLabelComponentFromRecords(
            embeddedLabelConfig,
            labelPreview(preview, showSubtext),
            `${id}.label`,
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
