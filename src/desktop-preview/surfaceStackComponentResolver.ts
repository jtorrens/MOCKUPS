import { embeddedComponentConfig } from "./runtimePreviewDocumentContract.js";
import {
  parseObject,
  requiredNumberPair,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveComponentStackLayout } from "./componentStackComponentResolver.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { SurfaceStackDesignContract } from "./surfaceStackComponentContract.js";
import { resolveSurfaceComponentFromRecords } from "./surfaceComponentResolver.js";

export function resolveSurfaceStackComponent(
  payload: DesignPreviewPayload,
): SurfaceStackDesignContract {
  const config = parseObject(payload.configJson);
  const stackConfig = requiredRecord(config, "surfaceStack", "component.surfaceStack");
  const preview = parseObject(payload.designPreviewJson);
  const size = requiredNumberPair(preview, "size", "surfaceStack.runtime.size");
  const padding = requiredStringPair(stackConfig, "padding", "component.surfaceStack.padding");
  const surfaceSlot = requiredRecord(stackConfig, "surfaceSlot", "component.surfaceStack.surfaceSlot");
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return {
    id: "surfaceStack",
    width: Math.max(0, size.first),
    height: Math.max(0, size.second),
    horizontalPaddingToken: padding.first,
    verticalPaddingToken: padding.second,
    surface: resolveSurfaceComponentFromRecords(
      embeddedComponentConfig(
        componentBaseConfigs,
        surfaceSlot,
        "surface",
        "component.surfaceStack.surfaceSlot",
      ),
      { size: `${Math.max(0, size.first)}|${Math.max(0, size.second)}` },
      "component.surfaceStack.surface",
    ),
    layout: resolveComponentStackLayout(
      payload,
      preview,
      "surfaceStack.layout",
      "fill",
      requiredString(stackConfig, "startGapToken", "component.surfaceStack.startGapToken"),
      requiredString(stackConfig, "endGapToken", "component.surfaceStack.endGapToken"),
      { kind: "slot", jsonKey: "componentSlot" },
    ),
  };
}
