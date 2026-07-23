import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveComponentCollectionItems } from "./componentCollectionResolverCommon.js";
import { optionalBoolean, optionalNumber, optionalString, parseObject, requiredNumber, requiredRecord, requiredString } from "./componentResolverCommon.js";
import type {
  CollectionStackDesignContract,
  CollectionStackDirection,
  CollectionStackDistributionMode,
  CollectionStackItemSizingMode,
} from "./collectionStackComponentContract.js";
import type { ComponentCollectionSizingMode } from "./componentCollectionContract.js";
import { easingProgress } from "./previewMotionHelpers.js";

export function resolveCollectionStackComponent(payload: DesignPreviewPayload): CollectionStackDesignContract {
  const preview = parseObject(payload.designPreviewJson);
  const distributionMode = requiredString(preview, "distributionMode", "collectionStack.runtime.distributionMode");
  if (distributionMode !== "flow" && distributionMode !== "stacked") {
    throw new Error(`Unsupported collection stack distribution mode ${distributionMode}`);
  }
  const sizingMode = requiredString(preview, "sizingMode", "collectionStack.runtime.sizingMode");
  if (sizingMode !== "fill" && sizingMode !== "content") {
    throw new Error(`Unsupported collection stack sizing mode ${sizingMode}`);
  }
  const stackDirection = requiredString(preview, "stackDirection", "collectionStack.runtime.stackDirection");
  if (stackDirection !== "down" && stackDirection !== "up") {
    throw new Error(`Unsupported collection stack direction ${stackDirection}`);
  }
  const itemSizingMode = requiredString(preview, "itemSizingMode", "collectionStack.runtime.itemSizingMode");
  if (itemSizingMode !== "intrinsic" && itemSizingMode !== "largest") {
    throw new Error(`Unsupported collection stack item sizing mode ${itemSizingMode}`);
  }
  const scaleRatio = requiredUnitRatio(preview, "scaleRatio", "collectionStack.runtime.scaleRatio", false);
  const opacityRatio = requiredUnitRatio(preview, "opacityRatio", "collectionStack.runtime.opacityRatio", true);
  const allItems = resolveComponentCollectionItems(payload, preview, "collectionStack");
  const items = allItems.filter((item) => item.present || item.exitFrame !== undefined || item.presenceTransition);
  const reflow = resolveDistributionReflow(payload, preview, allItems, items)
    ?? resolveReflow(payload, allItems);
  return {
    id: "collectionStack",
    distributionMode: distributionMode as CollectionStackDistributionMode,
    sizingMode: distributionMode === "stacked" ? "content" : sizingMode as ComponentCollectionSizingMode,
    startGapToken: requiredString(preview, "startGapToken", "collectionStack.runtime.startGapToken"),
    endGapToken: requiredString(preview, "endGapToken", "collectionStack.runtime.endGapToken"),
    stackDirection: stackDirection as CollectionStackDirection,
    stackOffsetToken: requiredString(preview, "stackOffsetToken", "collectionStack.runtime.stackOffsetToken"),
    itemSizingMode: itemSizingMode as CollectionStackItemSizingMode,
    scaleRatio,
    opacityRatio,
    items,
    reflow,
  };
}

function resolveDistributionReflow(
  payload: DesignPreviewPayload,
  preview: Record<string, unknown>,
  allItems: CollectionStackDesignContract["items"],
  items: CollectionStackDesignContract["items"],
) {
  if (!optionalBoolean(preview, "distributionTransition")) return undefined;
  const fromDistributionMode = optionalString(preview, "distributionFrom");
  if (fromDistributionMode !== "flow" && fromDistributionMode !== "stacked") return undefined;
  const root = parseObject(payload.themeTokensJson);
  const motion = requiredRecord(root, "motion", "theme.motion");
  const durationMs = requiredNumber(motion, "reflowDurationMs", "theme.motion.reflowDurationMs");
  const easing = requiredString(motion, "reflowEasing", "theme.motion.reflowEasing");
  const intensity = optionalNumber(motion, "reflowIntensity", 1);
  const elapsedMs = Math.max(0, optionalNumber(preview, "distributionElapsedMs", 0));
  return {
    progress: easingProgress(easing, durationMs <= 0 ? 1 : elapsedMs / durationMs, intensity),
    fromItems: allItems.length > 0 ? allItems : items,
    fromDistributionMode,
  };
}

function resolveReflow(
  payload: DesignPreviewPayload,
  allItems: CollectionStackDesignContract["items"],
) {
  const root = parseObject(payload.themeTokensJson);
  const motion = requiredRecord(root, "motion", "theme.motion");
  const durationMs = requiredNumber(motion, "reflowDurationMs", "theme.motion.reflowDurationMs");
  const easing = requiredString(motion, "reflowEasing", "theme.motion.reflowEasing");
  const durationFrames = Math.max(0, durationMs / 1000 * Math.max(1, payload.frameRate));
  if (durationFrames <= 0) return undefined;
  const frame = Math.max(0, payload.localFrame);
  const starts = allItems
    .flatMap((item) => item.reflowStartFrame === undefined ? [] : [item.reflowStartFrame])
    .filter((start) => start <= frame && frame < start + durationFrames)
    .sort((a, b) => b - a);
  const start = starts[0];
  if (start === undefined) return undefined;
  const fromItems = allItems.filter((item) =>
    item.present || item.exitFrame !== undefined || item.reflowStartFrame === start)
    .map((item) => item.reflowStartFrame === start && item.reflowFromInputs
      ? { ...item, inputs: item.reflowFromInputs }
      : item);
  return {
    progress: easingProgress(easing, (frame - start) / durationFrames, 1),
    fromItems,
  };
}

function requiredUnitRatio(
  value: Record<string, unknown>,
  key: string,
  path: string,
  allowZero: boolean,
) {
  const ratio = requiredNumber(value, key, path);
  if (ratio > 1 || ratio < (allowZero ? 0 : 0.01)) {
    throw new Error(`${path} must be between ${allowZero ? 0 : 0.01} and 1`);
  }
  return ratio;
}
