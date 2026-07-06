import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredPlacement,
  requiredRecord,
  requiredString,
  stringValue,
} from "./componentResolverCommon.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";

function labelPreview(
  preview: Record<string, unknown>,
  title: string,
  showSubtext: boolean,
): Record<string, unknown> {
  return {
    ...preview,
    sampleText: title,
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
    componentPresetConfig(componentBaseConfigs, "label", labelSlot.presetId),
    overrides,
  );
  const actor = resolveActorPreview(preview);

  return {
    id,
    actor,
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
            labelPreview(preview, actor.displayName, showSubtext),
            componentBaseConfigs,
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

function resolveActorPreview(
  preview: Record<string, unknown>,
) {
  const actor = requiredRecord(preview, "actor", "component.avatar.preview.actor");
  const avatar = requiredRecord(actor, "avatar", "component.avatar.preview.actor.avatar");
  return {
    id: requiredString(actor, "id", "component.avatar.preview.actor.id"),
    displayName: requiredString(
      actor,
      "displayName",
      "component.avatar.preview.actor.displayName",
    ),
    shortName: stringValue(actor.shortName),
    initials: requiredString(
      actor,
      "initials",
      "component.avatar.preview.actor.initials",
    ),
    avatar: {
      imageUri: stringValue(avatar.imageUri),
      backgroundColor: requiredString(
        avatar,
        "backgroundColor",
        "component.avatar.preview.actor.avatar.backgroundColor",
      ),
      textColor: requiredString(
        avatar,
        "textColor",
        "component.avatar.preview.actor.avatar.textColor",
      ),
      scale: requiredNumber(
        avatar,
        "scale",
        "component.avatar.preview.actor.avatar.scale",
      ),
      offsetX: requiredNumber(
        avatar,
        "offsetX",
        "component.avatar.preview.actor.avatar.offsetX",
      ),
      offsetY: requiredNumber(
        avatar,
        "offsetY",
        "component.avatar.preview.actor.avatar.offsetY",
      ),
      baseSize: requiredNumber(
        avatar,
        "baseSize",
        "component.avatar.preview.actor.avatar.baseSize",
      ),
    },
  };
}
