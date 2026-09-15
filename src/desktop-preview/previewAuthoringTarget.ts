import type { RenderableNode } from "../visual/renderable/types.js";
import type {
  DesignPreviewPayload,
  PreviewAuthoringTargetPayload,
} from "./designPreviewPayload.js";

function authoringTarget(
  payload: DesignPreviewPayload,
): PreviewAuthoringTargetPayload | undefined {
  if (!payload.authoringOwnerId) return undefined;
  return {
    ...(payload.authoringFocusFieldId
      ? { focusFieldId: payload.authoringFocusFieldId }
      : {}),
    ...(payload.authoringFocusItemId
      ? { focusItemId: payload.authoringFocusItemId }
      : {}),
    ownerId: payload.authoringOwnerId,
    ...(payload.authoringRuntimeComponentSlot
      ? { runtimeComponentSlot: payload.authoringRuntimeComponentSlot }
      : {}),
    slotFieldIds: [...(payload.authoringSlotFieldIds ?? [])],
  };
}

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

export function authoringRuntimeComponentSlotPayload(
  payload: DesignPreviewPayload,
  ownerRecordClassId: string,
  collectionFieldId: string,
  itemId: string,
  slotFieldId: string,
  childRecordClassId: string,
  childFocusFieldId: string,
): DesignPreviewPayload {
  if (!payload.authoringOwnerId
    || payload.authoringRecordClassId !== ownerRecordClassId) {
    return payload;
  }
  for (const [label, value] of Object.entries({
    collectionFieldId,
    itemId,
    slotFieldId,
    childRecordClassId,
  })) {
    if (!value.trim()) {
      throw new Error(`Runtime Component authoring target requires ${label}.`);
    }
  }
  return {
    ...payload,
    authoringFocusFieldId: childFocusFieldId,
    authoringFocusItemId: undefined,
    authoringRecordClassId: childRecordClassId,
    authoringRuntimeComponentSlot: {
      collectionFieldId,
      itemId,
      slotFieldId,
      recordClassId: childRecordClassId,
    },
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
    authoringRuntimeComponentSlot: undefined,
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
      authoringTarget: authoringTarget(payload),
    },
  };
}

export function assignAuthoringTargetToInput(
  payload: DesignPreviewPayload,
  inputFieldId: string,
  sourcePayload: DesignPreviewPayload,
): DesignPreviewPayload {
  const target = authoringTarget(sourcePayload);
  if (!target) return payload;
  if (!inputFieldId.trim()) {
    throw new Error("Authoring input target requires a stable input field id.");
  }
  return {
    ...payload,
    authoringInputTargets: {
      ...(payload.authoringInputTargets ?? {}),
      [inputFieldId]: target,
    },
  };
}

export function forwardAuthoringInputTarget(
  payload: DesignPreviewPayload,
  sourceInputFieldId: string,
  targetInputFieldId: string,
): DesignPreviewPayload {
  const target = payload.authoringInputTargets?.[sourceInputFieldId];
  if (!target) return payload;
  if (!targetInputFieldId.trim()) {
    throw new Error("Forwarded authoring input target requires a stable target field id.");
  }
  return {
    ...payload,
    authoringInputTargets: {
      ...(payload.authoringInputTargets ?? {}),
      [targetInputFieldId]: target,
    },
  };
}

export function withAuthoringInputTarget(
  payload: DesignPreviewPayload,
  inputFieldId: string,
  node: RenderableNode,
): RenderableNode {
  const target = payload.authoringInputTargets?.[inputFieldId];
  if (!target) return node;
  return {
    ...node,
    metadata: {
      ...node.metadata,
      authoringTarget: target,
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

export function renderAuthoringRuntimeComponentSlot(
  payload: DesignPreviewPayload,
  ownerRecordClassId: string,
  collectionFieldId: string,
  itemId: string,
  slotFieldId: string,
  childRecordClassId: string,
  childFocusFieldId: string,
  render: (slotPayload: DesignPreviewPayload) => RenderableNode,
): RenderableNode {
  const slotPayload = authoringRuntimeComponentSlotPayload(
    payload,
    ownerRecordClassId,
    collectionFieldId,
    itemId,
    slotFieldId,
    childRecordClassId,
    childFocusFieldId,
  );
  return withAuthoringTarget(slotPayload, render(slotPayload));
}
