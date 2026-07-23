import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { optionalBoolean, optionalNumber, optionalString, parseObject, requiredNumber, requiredNumberPair, requiredPlacement, requiredRecord, requiredString, requiredStringPair } from "./componentResolverCommon.js";
import { screenPercentToDesignWidth } from "./previewGeometryHelpers.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import type { NotificationDesignContract } from "./notificationComponentContract.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { easingProgress } from "./previewMotionHelpers.js";

export function resolveNotificationComponent(payload: DesignPreviewPayload): NotificationDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const notification = requiredRecord(config, "notification", "component.notification");
  const dimensionMode = requiredString(notification, "dimensionMode", "component.notification.dimensionMode");
  if (dimensionMode !== "fixed" && dimensionMode !== "content") {
    throw new Error(`Unsupported Notification dimension mode ${dimensionMode}`);
  }
  const rawSize = requiredNumberPair(notification, "size", "component.notification.size");
  const rawPadding = requiredStringPair(notification, "padding", "component.notification.padding");
  const avatarInputs = requiredRecord(notification, "avatarInputs", "component.notification.avatarInputs");
  const avatarSlot = requiredRecord(notification, "avatarSlot", "component.notification.avatarSlot");
  const surfaceSlot = requiredRecord(notification, "surfaceSlot", "component.notification.surfaceSlot");
  const summaryLabelSlot = requiredRecord(notification, "summaryLabelSlot", "component.notification.summaryLabelSlot");
  const detailLabelSlot = requiredRecord(notification, "detailLabelSlot", "component.notification.detailLabelSlot");
  const avatarConfig = embeddedComponentConfig(bases, avatarSlot, "avatar", "component.notification.avatarSlot");
  const displayMode = requiredString(preview, "displayMode", "component.notification.runtime.displayMode");
  if (displayMode !== "summary" && displayMode !== "detail") {
    throw new Error(`Unsupported Notification display mode ${displayMode}`);
  }
  const surfaceConfig = embeddedComponentConfig(bases, surfaceSlot, "surface", "component.notification.surfaceSlot");
  const maxWidthPercent = Math.min(
    100,
    Math.max(1, requiredNumber(preview, "maxWidth", "component.notification.runtime.maxWidth")),
  );
  const resolveDisplayLabel = (mode: "summary" | "detail") => {
    const modeSlotKey = mode === "summary" ? "summaryLabelSlot" : "detailLabelSlot";
    const modeTextKey = mode === "summary" ? "summaryText" : "detailText";
    const modeSubtextKey = mode === "summary" ? "summarySubtext" : "detailSubtext";
    const modeSlot = mode === "summary" ? summaryLabelSlot : detailLabelSlot;
    return resolveLabelComponentFromRecords(
      embeddedComponentConfig(bases, modeSlot, "label", `component.notification.${modeSlotKey}`),
      literalLabelPreview(
        requiredString(preview, modeTextKey, `component.notification.runtime.${modeTextKey}`),
        requiredString(preview, modeSubtextKey, `component.notification.runtime.${modeSubtextKey}`),
      ),
      bases,
      "component.notification.label",
      staticLabelFrameContext,
    );
  };
  const label = resolveDisplayLabel(displayMode);
  const fromMode = optionalString(preview, "displayModeFrom");
  const transitionActive = optionalBoolean(preview, "displayModeTransition")
    && (fromMode === "summary" || fromMode === "detail")
    && fromMode !== displayMode;
  const motion = requiredRecord(parseObject(payload.themeTokensJson), "motion", "theme.motion");
  const durationMs = requiredNumber(motion, "reflowDurationMs", "theme.motion.reflowDurationMs");
  const reflow = transitionActive
    ? {
        progress: easingProgress(
          requiredString(motion, "reflowEasing", "theme.motion.reflowEasing"),
          durationMs <= 0 ? 1 : Math.max(0, optionalNumber(preview, "displayModeElapsedMs", 0)) / durationMs,
          optionalNumber(motion, "reflowIntensity", 1),
        ),
        fromLabel: resolveDisplayLabel(fromMode as "summary" | "detail"),
      }
    : undefined;
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
        sampleSubtext: requiredString(
          preview,
          displayMode === "summary" ? "summarySubtext" : "detailSubtext",
          `component.notification.runtime.${displayMode === "summary" ? "summarySubtext" : "detailSubtext"}`,
        ),
      },
      bases,
      "component.notification.avatar",
    ),
    label,
    reflow,
  };
}
