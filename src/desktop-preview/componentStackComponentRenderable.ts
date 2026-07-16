import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { ComponentCollectionLayoutItem } from "./componentCollectionContract.js";
import {
  embeddedComponentPayload,
  placeChild,
  previewScreenBox,
  renderScale,
  scalePlacement,
  translateRenderableNode,
  unionBoxes,
} from "./componentRenderableCommon.js";
import type {
  ComponentStackAlternativeContract,
  ComponentStackChildRenderer,
  ComponentStackDesignContract,
  ComponentStackSlotContract,
} from "./componentStackComponentContract.js";
import { renderComponentCollectionFlowResolved } from "./componentCollectionRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { wrapExitMotionFrame, wrapMotionFrame } from "./previewMotionHelpers.js";

export function componentStackComponentToRenderable(
  payload: DesignPreviewPayload,
  stack: ComponentStackDesignContract,
  renderChild: ComponentStackChildRenderer,
): RenderableNode {
  const slots = stack.slots.map(slotLayoutItem);
  const byId = new Map(stack.slots.map((slot) => [slot.id, slot]));
  return renderComponentCollectionFlowResolved(payload, slots, (item, assignedBox) => {
    const slot = byId.get(item.id);
    if (!slot) throw new Error(`Missing resolved Component Stack slot ${item.id}`);
    return renderSlot(payload, slot, renderChild, assignedBox);
  }, {
    id: stack.id,
    sizingMode: stack.sizingMode,
    startGapToken: stack.startGapToken,
    endGapToken: stack.endGapToken,
  });
}

function renderSlot(
  payload: DesignPreviewPayload,
  slot: ComponentStackSlotContract,
  renderChild: ComponentStackChildRenderer,
  assignedBox?: RenderableBox,
): RenderableNode {
  const visibleAlternatives = slot.alternatives.filter((alternative) => alternative.component !== undefined);
  const intrinsicNodes = visibleAlternatives
    .map((alternative) => renderAlternative(payload, alternative, renderChild));
  if (intrinsicNodes.length === 0) {
    const screen = previewScreenBox(payload);
    const emptyBox = assignedBox
      ?? { x: screen.x + screen.width / 2, y: screen.y + screen.height / 2, width: 0, height: 0 };
    return group(slot.id, emptyBox, []);
  }
  const boxes = intrinsicNodes.flatMap((node) => node.box ? [node.box] : []);
  if (boxes.length !== intrinsicNodes.length) throw new Error(`Component Stack slot ${slot.id} contains an unresolved box`);
  if (!assignedBox) return group(slot.id, unionBoxes(boxes), intrinsicNodes);
  const nodes = visibleAlternatives.map((alternative) => {
    const node = renderAlternative(payload, alternative, renderChild, assignedBox);
    const box = node.box!;
    if (sameBox(box, assignedBox)) return node;
    const placed = placeChild(
      assignedBox,
      { width: box.width, height: box.height },
      scalePlacement(alternative.placement, renderScale(payload)),
    );
    return translateRenderableNode(node, { x: placed.x - box.x, y: placed.y - box.y });
  });
  return group(slot.id, assignedBox, nodes);
}

function renderAlternative(
  payload: DesignPreviewPayload,
  alternative: ComponentStackAlternativeContract,
  renderChild: ComponentStackChildRenderer,
  assignedBox?: RenderableBox,
): RenderableNode {
  const component = alternative.component!;
  const activationFrame = alternative.activationFrame ?? 0;
  const localFrame = alternative.exitFrame === undefined
    ? Math.max(0, payload.localFrame - activationFrame)
    : Math.max(0, alternative.exitFrame - activationFrame);
  const childPayload = embeddedComponentPayload(
    { ...payload, localFrame },
    component.componentType,
    component.config,
    component.inputs,
  );
  const node = renderChild(childPayload, assignedBox);
  if (!node.box) throw new Error(`Component Stack state ${alternative.id} has no resolved box`);
  if (alternative.exitFrame !== undefined) {
    const exitElapsedMs = alternative.exitElapsedMs
      ?? Math.max(0, payload.localFrame - alternative.exitFrame) / Math.max(1, payload.frameRate) * 1000;
    const wrapped = wrapExitMotionFrame(
      payload,
      node,
      alternative.exitMotion,
      { trigger: true, elapsedMs: exitElapsedMs },
      node.box,
      previewScreenBox(payload),
    );
    return { ...wrapped, box: node.box };
  }
  if (alternative.activationFrame === undefined && alternative.enterElapsedMs === undefined) return node;
  const enterElapsedMs = alternative.enterElapsedMs
    ?? localFrame / Math.max(1, payload.frameRate) * 1000;
  const wrapped = wrapMotionFrame(
    payload,
    node,
    alternative.enterMotion,
    { trigger: true, elapsedMs: enterElapsedMs },
    node.box,
    previewScreenBox(payload),
  );
  return { ...wrapped, box: node.box };
}

function sameBox(first: RenderableBox, second: RenderableBox) {
  const epsilon = 0.001;
  return Math.abs(first.x - second.x) <= epsilon
    && Math.abs(first.y - second.y) <= epsilon
    && Math.abs(first.width - second.width) <= epsilon
    && Math.abs(first.height - second.height) <= epsilon;
}

function slotLayoutItem(slot: ComponentStackSlotContract): ComponentCollectionLayoutItem {
  return {
    id: slot.id,
    alignment: "center",
    gapBeforeMode: slot.gapBeforeMode,
    gapBeforeToken: slot.gapBeforeToken,
    gapBeforeWeight: slot.gapBeforeWeight,
  };
}

function group(id: string, box: RenderableBox, children: RenderableNode[]): RenderableNode {
  return {
    id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children,
  };
}
