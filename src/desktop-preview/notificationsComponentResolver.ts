import { resolveCollectionStackComponent } from "./collectionStackComponentResolver.js";
import { componentVariantConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { parseObject, requiredBoolean, requiredNumber, requiredRecord, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { NotificationsDesignContract } from "./notificationsComponentContract.js";
import { resolveBadgeComponentFromRecords } from "./badgeComponentResolver.js";
import { requiredMotionContract } from "./previewMotionHelpers.js";
import { motionTotalDurationMs } from "./previewMotionHelpers.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { optionalObject, requiredObjectArray } from "./previewJsonHelpers.js";
import type { CollectionStackDistributionMode } from "./collectionStackComponentContract.js";
import { optionalRuntimeTransition } from "./runtimeTransitionDocument.js";

export function resolveNotificationsComponent(payload: DesignPreviewPayload): NotificationsDesignContract {
  const config = parseObject(payload.configJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const notifications = requiredRecord(config, "notifications", "component.notifications");
  const slot = requiredRecord(notifications, "collectionStackSlot", "component.notifications.collectionStackSlot");
  const notificationSlot = requiredRecord(notifications, "notificationSlot", "component.notifications.notificationSlot");
  const notificationInputs = requiredRecord(notifications, "notificationInputs", "component.notifications.notificationInputs");
  const badgeSlot = requiredRecord(notifications, "badgeSlot", "component.notifications.badgeSlot");
  const badgeInputs = requiredRecord(notifications, "badgeInputs", "component.notifications.badgeInputs");
  const preview = parseObject(payload.designPreviewJson);
  const distributionMotion = requiredMotionContract(notifications, "distributionMotion", "component.notifications.distributionMotion");
  const distribution = resolveDistribution(payload, preview, distributionMotion);
  const stackConfig = mergeComponentDefaults(
    componentVariantConfig(bases, "collectionStack", requiredString(slot, "variantReference", "component.notifications.collectionStackSlot.variantReference")),
    requiredRecord(slot, "overrides", "component.notifications.collectionStackSlot.overrides"),
  );
  const notificationVariantReference = requiredString(
    notificationSlot,
    "variantReference",
    "component.notifications.notificationSlot.variantReference",
  );
  const notificationConfig = mergeComponentDefaults(
    componentVariantConfig(bases, "notification", notificationVariantReference),
    requiredRecord(notificationSlot, "overrides", "component.notifications.notificationSlot.overrides"),
  );
  const stackItems = requiredObjectArray(preview, "items", "component.notifications runtime").map((rawItem, index) => notificationStackItem(
    rawItem,
    index,
    notificationVariantReference,
    notificationConfig,
    notificationInputs,
    notifications,
  ));
  const stackPreview = {
    ...preview,
    distributionMode: distribution.mode,
    sizingMode: requiredString(notifications, "sizingMode", "component.notifications.sizingMode"),
    startGapToken: requiredString(notifications, "startGapToken", "component.notifications.startGapToken"),
    endGapToken: requiredString(notifications, "endGapToken", "component.notifications.endGapToken"),
    stackDirection: requiredString(notifications, "stackDirection", "component.notifications.stackDirection"),
    stackOffsetToken: requiredString(notifications, "stackOffsetToken", "component.notifications.stackOffsetToken"),
    itemSizingMode: requiredString(notifications, "itemSizingMode", "component.notifications.itemSizingMode"),
    scaleRatio: requiredNumber(notifications, "scaleRatio", "component.notifications.scaleRatio"),
    opacityRatio: requiredNumber(notifications, "opacityRatio", "component.notifications.opacityRatio"),
    items: stackItems,
  };
  const stack = resolveCollectionStackComponent({
    ...payload,
    componentType: "collectionStack",
    configJson: JSON.stringify(stackConfig),
    designPreviewJson: JSON.stringify(stackPreview),
  });
  const closedItemLimit = Math.max(1, Math.floor(requiredNumber(notifications, "closedItemLimit", "component.notifications.closedItemLimit")));
  const presentCount = stack.items.filter((item) => item.present).length;
  const visibleStack = stack.distributionMode === "stacked"
    ? {
        ...stack,
        items: stack.items.slice(0, closedItemLimit),
        reflow: stack.reflow
          ? { ...stack.reflow, fromItems: stack.reflow.fromItems.slice(0, closedItemLimit) }
          : undefined,
      }
    : stack;
  const distributionTransition = distribution.transition
    ? {
        ...distribution.transition,
        fromStack: {
          ...stack,
          distributionMode: distribution.transition.fromMode,
          sizingMode: distribution.transition.fromMode === "stacked"
            ? "content" as const
            : requiredString(notifications, "sizingMode", "component.notifications.sizingMode") as "fill" | "content",
          items: distribution.transition.fromMode === "stacked"
            ? stack.items.slice(0, closedItemLimit)
            : stack.items,
          reflow: undefined,
        },
      }
    : undefined;
  const showBadge = requiredBoolean(notifications, "showBadge", "component.notifications.showBadge");
  const badgeConfig = mergeComponentDefaults(
    componentVariantConfig(bases, "badge", requiredString(badgeSlot, "variantReference", "component.notifications.badgeSlot.variantReference")),
    requiredRecord(badgeSlot, "overrides", "component.notifications.badgeSlot.overrides"),
  );
  return {
    id: "component.notifications",
    stack: visibleStack,
    closedItemLimit,
    distributionMotion,
    distributionTransition,
    badge: showBadge ? resolveBadgeComponentFromRecords(
      badgeConfig,
      { ...badgeInputs, iconToken: "", text: String(presentCount), contentMode: "text" },
      "component.notifications.badge",
    ) : undefined,
  };
}

const notificationItemKeys = new Set([
  "id",
  "actorId",
  "actor",
  "displayMode",
  "summaryText",
  "summarySubtext",
  "detailText",
  "detailSubtext",
  "present",
  "presenceTransition",
  "presenceElapsedMs",
  "displayModeTransition",
  "displayModeElapsedMs",
  "displayModeFrom",
]);

function notificationStackItem(
  item: Record<string, unknown>,
  index: number,
  variantReference: string,
  notificationConfig: Record<string, unknown>,
  baseInputs: Record<string, unknown>,
  notificationsConfig: Record<string, unknown>,
) {
  const path = `component.notifications.items[${index}]`;
  const unknown = Object.keys(item).filter((key) => !notificationItemKeys.has(key));
  if (unknown.length > 0) {
    throw new Error(`${path} contains undeclared fields: ${unknown.join(", ")}`);
  }
  return {
    id: requiredString(item, "id", `${path}.id`),
    variantReference: variantReference,
    overrides: notificationConfig,
    inputs: {
      ...baseInputs,
      actorId: requiredString(item, "actorId", `${path}.actorId`),
      actor: requiredRecord(item, "actor", `${path}.actor`),
      displayMode: requiredString(item, "displayMode", `${path}.displayMode`),
      summaryText: requiredString(item, "summaryText", `${path}.summaryText`),
      summarySubtext: requiredString(item, "summarySubtext", `${path}.summarySubtext`),
      detailText: requiredString(item, "detailText", `${path}.detailText`),
      detailSubtext: requiredString(item, "detailSubtext", `${path}.detailSubtext`),
      ...(item.displayModeTransition === undefined ? {} : { displayModeTransition: item.displayModeTransition }),
      ...(item.displayModeElapsedMs === undefined ? {} : { displayModeElapsedMs: item.displayModeElapsedMs }),
      ...(item.displayModeFrom === undefined ? {} : { displayModeFrom: item.displayModeFrom }),
    },
    present: item.present,
    presenceMotion: requiredRecord(notificationsConfig, "itemPresenceMotion", "component.notifications.itemPresenceMotion"),
    ...(item.presenceTransition === undefined ? {} : { presenceTransition: item.presenceTransition }),
    ...(item.presenceElapsedMs === undefined ? {} : { presenceElapsedMs: item.presenceElapsedMs }),
    alignment: requiredString(notificationsConfig, "itemAlignment", "component.notifications.itemAlignment"),
    gapBeforeMode: requiredString(notificationsConfig, "itemGapBeforeMode", "component.notifications.itemGapBeforeMode"),
    gapBeforeToken: requiredString(notificationsConfig, "itemGapBeforeToken", "component.notifications.itemGapBeforeToken"),
    gapBeforeWeight: requiredNumber(notificationsConfig, "itemGapBeforeWeight", "component.notifications.itemGapBeforeWeight"),
  };
}

function resolveDistribution(
  payload: DesignPreviewPayload,
  preview: Record<string, unknown>,
  motion: NotificationsDesignContract["distributionMotion"],
) {
  const base = requiredString(preview, "distributionMode", "component.notifications.runtime.distributionMode");
  if (base !== "flow" && base !== "stacked") throw new Error(`Unsupported Notifications distribution ${base}`);
  const instance = parseObject(payload.instanceJson);
  const animation = optionalObject(instance, "animation", "Preview instance envelope");
  const frame = Math.max(0, payload.localFrame);
  const resolved = resolveParameterAnimation(animation, "distributionMode", "", frame, base);
  const mode = resolved.value;
  if (mode !== "flow" && mode !== "stacked") throw new Error(`Unsupported Notifications distribution ${String(mode)}`);
  const forwardedTransitions = optionalObject(
    preview,
    "__runtimeTransitions",
    "component.notifications runtime transitions",
  );
  const forwardedTransition = optionalRuntimeTransition(
    forwardedTransitions,
    "distributionMode",
    "component.notifications runtime transitions",
  );
  const source = resolved.sourceKeyframeFrame
    ?? forwardedTransition?.sourceFrame;
  if (source === undefined || source <= 0) return { mode: mode as CollectionStackDistributionMode, transition: undefined };
  const durationFrames = Math.ceil(motionTotalDurationMs(payload, motion) / 1000 * Math.max(1, payload.frameRate));
  if (durationFrames <= 0 || frame - source >= durationFrames) {
    return { mode: mode as CollectionStackDistributionMode, transition: undefined };
  }
  const previous = forwardedTransition?.previousValue
    ?? resolveParameterAnimation(animation, "distributionMode", "", Math.max(0, source - 1), base).value;
  if (previous !== "flow" && previous !== "stacked") throw new Error(`Unsupported previous Notifications distribution ${String(previous)}`);
  return {
    mode: mode as CollectionStackDistributionMode,
    transition: { fromMode: previous as CollectionStackDistributionMode, elapsedFrames: frame - source },
  };
}
