import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { ComponentCollectionLayoutItem } from "./componentCollectionContract.js";
import {
  embeddedComponentPayload,
  previewScreenBox,
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
  return renderComponentCollectionFlowResolved(payload, slots, (item) => {
    const slot = byId.get(item.id);
    if (!slot) throw new Error(`Missing resolved Component Stack slot ${item.id}`);
    return renderSlot(payload, slot, renderChild);
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
): RenderableNode {
  const nodes = slot.alternatives
    .filter((alternative) => alternative.component !== undefined)
    .map((alternative) => renderAlternative(payload, alternative, renderChild));
  if (nodes.length === 0) {
    const screen = previewScreenBox(payload);
    return group(slot.id, { x: screen.x + screen.width / 2, y: screen.y + screen.height / 2, width: 0, height: 0 }, []);
  }
  const boxes = nodes.flatMap((node) => node.box ? [node.box] : []);
  if (boxes.length !== nodes.length) throw new Error(`Component Stack slot ${slot.id} contains an unresolved box`);
  return group(slot.id, unionBoxes(boxes), nodes);
}

function renderAlternative(
  payload: DesignPreviewPayload,
  alternative: ComponentStackAlternativeContract,
  renderChild: ComponentStackChildRenderer,
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
  const node = renderChild(childPayload);
  if (!node.box) throw new Error(`Component Stack state ${alternative.id} has no resolved box`);
  if (alternative.exitFrame !== undefined) {
    const exitElapsedFrames = Math.max(0, payload.localFrame - alternative.exitFrame);
    const wrapped = wrapExitMotionFrame(
      payload,
      node,
      alternative.exitMotion,
      { trigger: true, elapsedMs: exitElapsedFrames / Math.max(1, payload.frameRate) * 1000 },
      node.box,
      previewScreenBox(payload),
    );
    return { ...wrapped, box: node.box };
  }
  if (alternative.isDefault || alternative.activationFrame === undefined) return node;
  const wrapped = wrapMotionFrame(
    payload,
    node,
    alternative.enterMotion,
    { trigger: true, elapsedMs: localFrame / Math.max(1, payload.frameRate) * 1000 },
    node.box,
    previewScreenBox(payload),
  );
  return { ...wrapped, box: node.box };
}

function slotLayoutItem(slot: ComponentStackSlotContract): ComponentCollectionLayoutItem {
  return {
    id: slot.id,
    alignment: slot.alignment,
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
