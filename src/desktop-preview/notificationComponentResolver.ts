import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, parseObject, requiredNumber, requiredNumberPair, requiredPlacement, requiredRecord, requiredString, requiredStringPair } from "./componentResolverCommon.js";
import { screenPercentToDesignWidth } from "./previewGeometryHelpers.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import type { NotificationDesignContract } from "./notificationComponentContract.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveNotificationComponent(payload: DesignPreviewPayload): NotificationDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const notification = asRecord(config.notification);
  const dimensionMode = requiredString(notification, "dimensionMode", "component.notification.dimensionMode");
  if (dimensionMode !== "fixed" && dimensionMode !== "content") {
    throw new Error(`Unsupported Notification dimension mode ${dimensionMode}`);
  }
  const rawSize = requiredNumberPair(notification, "size", "component.notification.size");
  const rawPadding = requiredStringPair(notification, "padding", "component.notification.padding");
  const avatarInputs = asRecord(notification.avatarInputs);
  const avatarConfig = embeddedConfig(asRecord(notification.avatarSlot), "avatar", bases, "component.notification.avatarSlot");
  const displayMode = requiredString(preview, "displayMode", "component.notification.runtime.displayMode");
  if (displayMode !== "summary" && displayMode !== "detail") {
    throw new Error(`Unsupported Notification display mode ${displayMode}`);
  }
  const labelSlotKey = displayMode === "summary" ? "summaryLabelSlot" : "detailLabelSlot";
  const textKey = displayMode === "summary" ? "summaryText" : "detailText";
  const subtextKey = displayMode === "summary" ? "summarySubtext" : "detailSubtext";
  const labelConfig = embeddedConfig(asRecord(notification[labelSlotKey]), "label", bases, `component.notification.${labelSlotKey}`);
  const surfaceConfig = embeddedConfig(asRecord(notification.surfaceSlot), "surface", bases, "component.notification.surfaceSlot");
  const maxWidthPercent = Math.min(
    100,
    Math.max(1, requiredNumber(preview, "maxWidth", "component.notification.runtime.maxWidth")),
  );
  return {
    id: "component.notification",
    maxWidth: screenPercentToDesignWidth(payload, maxWidthPercent),
    dimensionMode,
    size: { width: rawSize.first, height: rawSize.second },
    padding: { xToken: rawPadding.first, yToken: rawPadding.second },
    gapToken: requiredString(notification, "gapToken", "component.notification.gapToken"),
    avatarPlacement: requiredPlacement(notification, "avatarPlacement", "component.notification.avatarPlacement"),
    labelPlacement: requiredPlacement(notification, "labelPlacement", "component.notification.labelPlacement"),
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      { width: 0, height: 0 },
      "component.notification.surface",
    ),
    avatar: resolveAvatarComponentFromRecords(
      avatarConfig,
      {
        ...avatarInputs,
        actor: requiredRecord(preview, "actor", "component.notification.runtime.actor"),
        sampleSubtext: requiredString(preview, subtextKey, `component.notification.runtime.${subtextKey}`),
      },
      bases,
      "component.notification.avatar",
    ),
    label: resolveLabelComponentFromRecords(
      labelConfig,
      literalLabelPreview(
        requiredString(preview, textKey, `component.notification.runtime.${textKey}`),
        requiredString(preview, subtextKey, `component.notification.runtime.${subtextKey}`),
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
