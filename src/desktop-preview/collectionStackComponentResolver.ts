import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveComponentCollectionItems } from "./componentCollectionResolverCommon.js";
import { parseObject, requiredString } from "./componentResolverCommon.js";
import type {
  CollectionStackDesignContract,
  CollectionStackDirection,
  CollectionStackDistributionMode,
} from "./collectionStackComponentContract.js";
import type { ComponentCollectionSizingMode } from "./componentCollectionContract.js";

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
  return {
    id: "collectionStack",
    distributionMode: distributionMode as CollectionStackDistributionMode,
    sizingMode: distributionMode === "stacked" ? "content" : sizingMode as ComponentCollectionSizingMode,
    startGapToken: requiredString(preview, "startGapToken", "collectionStack.runtime.startGapToken"),
    endGapToken: requiredString(preview, "endGapToken", "collectionStack.runtime.endGapToken"),
    stackDirection: stackDirection as CollectionStackDirection,
    stackOffsetToken: requiredString(preview, "stackOffsetToken", "collectionStack.runtime.stackOffsetToken"),
    items: resolveComponentCollectionItems(payload, preview, "collectionStack"),
  };
}
