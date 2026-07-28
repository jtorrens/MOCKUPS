import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function authoringSlotPayload(
  payload: DesignPreviewPayload,
  fieldId: string,
): DesignPreviewPayload {
  if (!payload.authoringOwnerId) return payload;
  return {
    ...payload,
    authoringSlotFieldIds: [...(payload.authoringSlotFieldIds ?? []), fieldId],
  };
}

export function withAuthoringTarget(
  payload: DesignPreviewPayload,
  node: RenderableNode,
): RenderableNode {
  if (!payload.authoringOwnerId) return node;
  return {
    ...node,
    metadata: {
      ...node.metadata,
      authoringTarget: {
        ownerId: payload.authoringOwnerId,
        slotFieldIds: [...(payload.authoringSlotFieldIds ?? [])],
      },
    },
  };
}

export function renderAuthoringSlot(
  payload: DesignPreviewPayload,
  fieldId: string,
  render: (slotPayload: DesignPreviewPayload) => RenderableNode,
): RenderableNode {
  const slotPayload = authoringSlotPayload(payload, fieldId);
  return withAuthoringTarget(slotPayload, render(slotPayload));
}
