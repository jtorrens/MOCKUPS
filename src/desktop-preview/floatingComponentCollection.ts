import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ComponentCollectionChildRenderer } from "./componentCollectionContract.js";
import type { AlignmentPlacementContract, ComponentMotionContract, ComponentMotionFrameContract } from "./previewComponentContracts.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import { parseObject, requiredRecord, requiredString, requiredPlacement, requiredComponentVariantSlot } from "./componentResolverCommon.js";
import { optionalObject, requiredObjectArray } from "./previewJsonHelpers.js";
import { prepareComponentConfiguration } from "./runtimePreviewDocumentContract.js";
import { resolveComponentCollectionRuntimeInputs } from "./componentCollectionResolverCommon.js";
import { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";
import { rootScreenFrame } from "./previewFrameContext.js";
import { requiredMotionContract, wrapMotionFrame, wrapExitMotionFrame } from "./previewMotionHelpers.js";
import { resolveRuntimePresenceInterval } from "./runtimeCollectionPresence.js";
import { embeddedVariantComponentPayload, previewScreenBox, placeChild, scalePlacement, renderScale, translateRenderableNode } from "./componentRenderableCommon.js";

export interface FloatingComponentCollection {
  id: string;
  motion: ComponentMotionContract;
  items: Array<{
    id: string;
    componentType: string;
    variantReference: string;
    config: Record<string, unknown>;
    inputs: Record<string, unknown>;
    placement: AlignmentPlacementContract;
    localFrame: number;
    presenceMotionKind?: "enter" | "exit";
    presenceMotionFrame?: ComponentMotionFrameContract;
  }>;
}

/** Variant structure + prepared Screen-owned values. No lifecycle or source repair. */
export function resolveFloatingComponentCollection(
  payload: DesignPreviewPayload,
  config: Record<string, unknown>,
  runtimeKey: string,
  id: string,
): FloatingComponentCollection {
  const source = requiredObjectArray(config, "items", id);
  const runtime = parseObject(payload.designPreviewJson);
  const values = requiredObjectArray(runtime, runtimeKey, id);
  if (values.length !== source.length || values.some((value, index) => value.id !== source[index].id))
    throw new Error(`${id} requires exact prepared Runtime membership and order`);
  const motion = requiredMotionContract(config, "motion", id);
  const envelope = parseObject(payload.runtimeContractJson);
  const animation = optionalObject(parseObject(payload.instanceJson), "animation", id);
  const theme = parseObject(payload.themeTokensJson);
  const timeline = new RuntimeOwnerTimeline(envelope, envelope, animation, theme, 0, payload.frameRate);
  const frame = rootScreenFrame(payload);
  const end = Math.max(1, payload.screenTiming?.actionDurationFrames ?? timeline.durationFrames);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const types = requiredRecord(bases, "variantTypes", id);
  const ids = new Set<string>();
  const items: FloatingComponentCollection["items"] = [];
  for (const [index, item] of source.entries()) {
    const itemId = requiredString(item, "id", id);
    if (ids.has(itemId)) throw new Error(`${id} has duplicate item '${itemId}'`);
    ids.add(itemId);
    const value = values[index];
    if (item.componentSlot === null) continue;
    const slot = requiredComponentVariantSlot(item, "componentSlot", id);
    const componentType = requiredString(types, slot.variantReference, id);
    const presenceEnd = timeline.itemPresenceEndFrame(itemId, end);
    const explicitEnd = timeline.itemHasExplicitPresenceEnd(itemId);
    const presence = resolveRuntimePresenceInterval(payload, frame, timeline.itemStartFrame(itemId), presenceEnd, explicitEnd, end, motion);
    if (!presence.visible) continue;
    const inputs = resolveComponentCollectionRuntimeInputs(timeline, animation,
      requiredRecord(value, "inputs", id), itemId, frame, theme, payload.frameRate,
      explicitEnd ? presenceEnd : undefined).values;
    items.push({ id: itemId, componentType, variantReference: slot.variantReference,
      config: prepareComponentConfiguration(bases, componentType, slot.variantReference, slot.overrides),
      inputs, placement: requiredPlacement(item, "placement", id),
      localFrame: Math.max(0, timeline.temporalOwnerFrame(itemId, frame, explicitEnd ? presenceEnd : undefined)),
      ...presence,
    });
  }
  return { id, motion, items };
}

export function floatingComponentCollectionToRenderable(
  payload: DesignPreviewPayload,
  collection: FloatingComponentCollection,
  renderChild: ComponentCollectionChildRenderer,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const children = collection.items.map(item => {
    const child = renderChild(embeddedVariantComponentPayload({ ...payload, localFrame: item.localFrame },
      item.componentType, item.variantReference, item.config, item.inputs));
    if (!child.box) throw new Error(`${collection.id} child '${item.id}' has no measured box`);
    const box = placeChild(screen, child.box, scalePlacement(item.placement, renderScale(payload)));
    const placed = translateRenderableNode(child, { x: box.x - child.box.x, y: box.y - child.box.y });
    if (!item.presenceMotionFrame) return placed;
    return (item.presenceMotionKind === "exit" ? wrapExitMotionFrame : wrapMotionFrame)(
      payload, placed, collection.motion, item.presenceMotionFrame, placed.box!, screen);
  });
  return { id: collection.id, type: "group", frame: 0, box: screen,
    style: { overflow: "visible", zIndex: 100 }, children };
}
