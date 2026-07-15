import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveComponentCollectionItems } from "./componentCollectionResolverCommon.js";
import { parseObject, requiredString } from "./componentResolverCommon.js";
import type {
  ComponentStackDesignContract,
  ComponentStackSizingMode,
} from "./componentStackComponentContract.js";

export function resolveComponentStackComponent(payload: DesignPreviewPayload): ComponentStackDesignContract {
  const preview = parseObject(payload.designPreviewJson);
  const sizingMode = requiredString(preview, "sizingMode", "componentStack.runtime.sizingMode");
  if (sizingMode !== "fill" && sizingMode !== "content") {
    throw new Error(`Unsupported component stack sizing mode ${sizingMode}`);
  }
  return {
    id: "componentStack",
    sizingMode: sizingMode as ComponentStackSizingMode,
    startGapToken: requiredString(preview, "startGapToken", "componentStack.runtime.startGapToken"),
    endGapToken: requiredString(preview, "endGapToken", "componentStack.runtime.endGapToken"),
    items: resolveComponentCollectionItems(payload, preview, "componentStack"),
  };
}
