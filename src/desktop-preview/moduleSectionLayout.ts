import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { AlignmentPlacementContract } from "./previewComponentContracts.js";
import { requiredPlacement, requiredRecord, requiredString, requiredStringPair } from "./componentResolverCommon.js";
import { numberToken, renderScale, scalePlacement } from "./componentRenderableCommon.js";
import { placeRenderableNode } from "./previewGeometryHelpers.js";

export interface ModuleSectionLayout {
  mode: "stack" | "float";
  padding: { xToken: string; yToken: string };
  placement: AlignmentPlacementContract;
}

export function requiredModuleSectionLayout(owner: Record<string, unknown>, key: string, context: string): ModuleSectionLayout {
  const value = requiredRecord(owner, key, context);
  if (Object.keys(value).sort().join(",") !== "mode,padding,placement") throw new Error(`${context}.${key} requires exactly mode, padding and placement`);
  const mode = requiredString(value, "mode", context);
  if (mode !== "stack" && mode !== "float") throw new Error(`${context}.${key}.mode must be stack or float`);
  const { first: xToken, second: yToken } = requiredStringPair(value, "padding", context);
  if (![xToken, yToken].every((token) => /^theme\.spacing\.[a-zA-Z0-9_]+$/.test(token))) throw new Error(`${context}.${key}.padding requires spacing tokens`);
  return { mode, padding: { xToken, yToken }, placement: requiredPlacement(value, "placement", context) };
}

export function sectionContentBox(payload: DesignPreviewPayload, layout: ModuleSectionLayout, bounds: RenderableBox): RenderableBox {
  if (layout.mode === "stack") return bounds;
  const scale = renderScale(payload);
  const x = numberToken(payload, layout.padding.xToken) * scale;
  const y = numberToken(payload, layout.padding.yToken) * scale;
  return { x: bounds.x + x, y: bounds.y + y, width: Math.max(1, bounds.width - 2 * x), height: Math.max(1, bounds.height - 2 * y) };
}

export function placeModuleSection(payload: DesignPreviewPayload, layout: ModuleSectionLayout, bounds: RenderableBox, node: RenderableNode): RenderableNode {
  if (layout.mode === "stack") return node;
  return placeRenderableNode(node, sectionContentBox(payload, layout, bounds), scalePlacement(layout.placement, renderScale(payload)));
}

export function sectionFlowEdge(layout: ModuleSectionLayout, node: RenderableNode | undefined, edge: "top" | "bottom", unchanged: number): number {
  if (layout.mode === "float" || !node?.box) return unchanged;
  return edge === "top" ? node.box.y + node.box.height : node.box.y;
}
