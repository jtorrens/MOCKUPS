import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function authoringSlotPayload(
  payload: DesignPreviewPayload,
  ownerRecordClassId: string,
  fieldId: string,
  childRecordClassId: string,
): DesignPreviewPayload {
  if (!payload.authoringOwnerId
    || payload.authoringRecordClassId !== ownerRecordClassId) {
    return payload;
  }
  return {
    ...payload,
    authoringRecordClassId: childRecordClassId,
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
  ownerRecordClassId: string,
  fieldId: string,
  childRecordClassId: string,
  render: (slotPayload: DesignPreviewPayload) => RenderableNode,
): RenderableNode {
  if (!payload.authoringOwnerId
    || payload.authoringRecordClassId !== ownerRecordClassId) {
    return render(payload);
  }
  const slotPayload = authoringSlotPayload(
    payload,
    ownerRecordClassId,
    fieldId,
    childRecordClassId,
  );
  return withAuthoringTarget(slotPayload, render(slotPayload));
}
