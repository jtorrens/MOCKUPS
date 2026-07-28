import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function authoringSlotPayload(
  payload: DesignPreviewPayload,
  ownerRecordClassId: string,
  fieldId: string,
  childRecordClassId: string,
  childFocusFieldId: string,
): DesignPreviewPayload {
  if (!payload.authoringOwnerId
    || payload.authoringRecordClassId !== ownerRecordClassId) {
    return payload;
  }
  return {
    ...payload,
    authoringFocusFieldId: childFocusFieldId,
    authoringFocusItemId: undefined,
    authoringRecordClassId: childRecordClassId,
    authoringSlotFieldIds: [...(payload.authoringSlotFieldIds ?? []), fieldId],
  };
}

export function authoringVariantPayload(
  payload: DesignPreviewPayload,
  variantReference: string,
  childRecordClassId: string,
): DesignPreviewPayload {
  if (!payload.authoringOwnerId) return payload;
  if (!/^[A-Za-z0-9_.-]+::variant::[A-Za-z0-9_.-]+$/.test(variantReference)) {
    throw new Error(
      `Authoring Component boundary requires a full Variant reference '${variantReference}'.`,
    );
  }
  return {
    ...payload,
    authoringOwnerId: variantReference,
    authoringFocusFieldId: undefined,
    authoringFocusItemId: undefined,
    authoringRecordClassId: childRecordClassId,
    authoringSlotFieldIds: [],
  };
}

export function authoringCollectionItemPayload(
  payload: DesignPreviewPayload,
  ownerRecordClassId: string,
  fieldId: string,
  itemId: string,
): DesignPreviewPayload {
  if (!payload.authoringOwnerId
    || payload.authoringRecordClassId !== ownerRecordClassId) {
    return payload;
  }
  if (!itemId.trim()) {
    throw new Error(`Authoring collection field '${fieldId}' requires a stable item id.`);
  }
  return {
    ...payload,
    authoringFocusFieldId: fieldId,
    authoringFocusItemId: itemId,
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
        ...(payload.authoringFocusFieldId
          ? { focusFieldId: payload.authoringFocusFieldId }
          : {}),
        ...(payload.authoringFocusItemId
          ? { focusItemId: payload.authoringFocusItemId }
          : {}),
        ownerId: payload.authoringOwnerId,
        slotFieldIds: [...(payload.authoringSlotFieldIds ?? [])],
      },
    },
  };
}

export function renderAuthoringCollectionItem(
  payload: DesignPreviewPayload,
  ownerRecordClassId: string,
  fieldId: string,
  itemId: string,
  render: (itemPayload: DesignPreviewPayload) => RenderableNode,
): RenderableNode {
  const itemPayload = authoringCollectionItemPayload(
    payload,
    ownerRecordClassId,
    fieldId,
    itemId,
  );
  return withAuthoringTarget(itemPayload, render(itemPayload));
}

export function renderAuthoringSlot(
  payload: DesignPreviewPayload,
  ownerRecordClassId: string,
  fieldId: string,
  childRecordClassId: string,
  childFocusFieldId: string,
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
    childFocusFieldId,
  );
  return withAuthoringTarget(slotPayload, render(slotPayload));
}
