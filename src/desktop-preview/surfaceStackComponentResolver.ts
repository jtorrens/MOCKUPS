import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
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
  const stackRuntime = normalizedSurfaceStackRuntime(preview);
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
      stackRuntime,
      "surfaceStack.layout",
      "fill",
      requiredString(stackConfig, "startGapToken", "component.surfaceStack.startGapToken"),
      requiredString(stackConfig, "endGapToken", "component.surfaceStack.endGapToken"),
    ),
  };
}

function normalizedSurfaceStackRuntime(
  preview: Record<string, unknown>,
): Record<string, unknown> {
  const items = Array.isArray(preview.items)
    ? preview.items.map((item) => normalizeSlot(item))
    : preview.items;
  return { ...preview, items };
}

function normalizeSlot(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const slot = value as Record<string, unknown>;
  const alternatives = Array.isArray(slot.alternatives)
    ? slot.alternatives.map((alternative) => normalizeAlternative(alternative))
    : slot.alternatives;
  return { ...slot, alternatives };
}

function normalizeAlternative(value: unknown): unknown {
  if (!value || typeof value !== "object" || Array.isArray(value)) return value;
  const alternative = value as Record<string, unknown>;
  const componentSlot = alternative.componentSlot;
  if (!componentSlot || typeof componentSlot !== "object" || Array.isArray(componentSlot)) {
    return alternative;
  }
  const boundary = componentSlot as Record<string, unknown>;
  return {
    ...alternative,
    variantReference: boundary.variantReference,
    overrides: boundary.overrides,
  };
}
