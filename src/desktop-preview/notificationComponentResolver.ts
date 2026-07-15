import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, parseObject, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import type { NotificationDesignContract } from "./notificationComponentContract.js";

export function resolveNotificationComponent(payload: DesignPreviewPayload): NotificationDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const notification = asRecord(config.notification);
  const avatarPosition = requiredString(notification, "avatarPosition", "component.notification.avatarPosition");
  if (avatarPosition !== "start" && avatarPosition !== "end") {
    throw new Error(`Unsupported Notification avatar position ${avatarPosition}`);
  }
  const avatarConfig = embeddedConfig(asRecord(notification.avatarSlot), "avatar", bases, "component.notification.avatarSlot");
  const labelConfig = embeddedConfig(asRecord(notification.labelSlot), "label", bases, "component.notification.labelSlot");
  return {
    id: "component.notification",
    avatarPosition,
    gapToken: requiredString(notification, "gapToken", "component.notification.gapToken"),
    avatar: resolveAvatarComponentFromRecords(avatarConfig, preview, bases, "component.notification.avatar"),
    label: resolveLabelComponentFromRecords(
      labelConfig,
      literalLabelPreview(
        requiredString(preview, "sampleText", "component.notification.runtime.sampleText"),
        requiredString(preview, "sampleSubtext", "component.notification.runtime.sampleSubtext"),
      ),
      bases,
      "component.notification.label",
      staticLabelFrameContext,
    ),
  };
}

function embeddedConfig(
  slot: Record<string, unknown>,
  componentType: string,
  bases: Record<string, unknown>,
  path: string,
) {
  return mergeComponentDefaults(
    componentPresetConfig(bases, componentType, requiredString(slot, "presetId", `${path}.presetId`)),
    asRecord(slot.overrides),
  );
}
