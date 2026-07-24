import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveComponentCollectionItem } from "./componentCollectionResolverCommon.js";
import {
  optionalString,
  parseObject,
  requiredNumber,
  requiredPlacement,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { optionalObject, optionalObjectArray, requiredObjectArray } from "./previewJsonHelpers.js";
import { validateTransientAnimationDocument } from "./transientAnimationDocument.js";
import { requiredNumberValue } from "./previewValueHelpers.js";
import { motionTotalDurationMs, requiredMotionContract } from "./previewMotionHelpers.js";
import { rootScreenFrame } from "./previewFrameContext.js";
import type {
  ComponentStackAlternativeContract,
  ComponentStackDesignContract,
  ComponentStackGapMode,
  ComponentStackSizingMode,
  ComponentStackSlotContract,
} from "./componentStackComponentContract.js";
import { optionalComponentBoundaryMotion } from "./componentBoundaryMotion.js";

export function resolveComponentStackComponent(payload: DesignPreviewPayload): ComponentStackDesignContract {
  const preview = parseObject(payload.designPreviewJson);
  const sizingMode = requiredString(preview, "sizingMode", "componentStack.runtime.sizingMode");
  if (sizingMode !== "fill" && sizingMode !== "content") {
    throw new Error(`Unsupported component stack sizing mode ${sizingMode}`);
  }
  return {
    id: "componentStack",
    sizingMode: sizingMode as ComponentStackSizingMode,
    startGapToken: requiredString(preview, "startGapToken", "componentStack.runtime.startGapToken"),
    endGapToken: requiredString(preview, "endGapToken", "componentStack.runtime.endGapToken"),
    slots: requiredObjectArray(preview, "items", "componentStack runtime")
      .map((slot, index) => resolveSlot(payload, slot, index)),
  };
}

function resolveSlot(
  payload: DesignPreviewPayload,
  slot: Record<string, unknown>,
  index: number,
): ComponentStackSlotContract {
  const path = `componentStack.items[${index}]`;
  const slotId = requiredString(slot, "id", `${path}.id`);
  const authoredSizeMode = optionalString(slot, "sizeMode");
  const sizeMode = authoredSizeMode || "content";
  if (sizeMode !== "content" && sizeMode !== "fill") {
    throw new Error(`Unsupported componentStack slot size mode ${sizeMode}`);
  }
  const gapBeforeMode = requiredString(slot, "gapBeforeMode", `${path}.gapBeforeMode`);
  if (gapBeforeMode !== "fixed" && gapBeforeMode !== "reflow") {
    throw new Error(`Unsupported componentStack gap-before mode ${gapBeforeMode}`);
  }
  const rawAlternatives = requiredObjectArray(slot, "alternatives", `${path} states`);
  const alternatives = rawAlternatives.map((rawAlternative, alternativeIndex) => resolveAlternative(
    payload,
    rawAlternative,
    alternativeIndex,
    path,
    gapBeforeMode as ComponentStackGapMode,
    requiredString(slot, "gapBeforeToken", `${path}.gapBeforeToken`),
    Math.max(0, requiredNumber(slot, "gapBeforeWeight", `${path}.gapBeforeWeight`)),
  ));
  const instance = parseObject(payload.instanceJson);
  const frame = rootScreenFrame(payload);
  const authoredRuntimeStateId = optionalString(slot, "runtimeStateId");
  const baseStateId = authoredRuntimeStateId || alternatives[0]?.id || "";
  const animatedState = resolveParameterAnimation(
    optionalObject(instance, "animation", "Preview instance envelope"),
    "runtimeStateId",
    slotId,
    frame,
    baseStateId,
  );
  const runtimeStateId = typeof animatedState.value === "string" ? animatedState.value : baseStateId;
  const animatedTransition = animatedState.sourceKeyframeFrame !== undefined
    && typeof animatedState.previousValue === "string"
    && animatedState.previousValue !== runtimeStateId
    ? {
        fromId: animatedState.previousValue,
        elapsedMs: Math.max(0, frame - animatedState.sourceKeyframeFrame) / Math.max(1, payload.frameRate) * 1000,
      }
    : undefined;
  return {
    id: slotId,
    sizeMode,
    gapBeforeMode: gapBeforeMode as ComponentStackGapMode,
    gapBeforeToken: requiredString(slot, "gapBeforeToken", `${path}.gapBeforeToken`),
    gapBeforeWeight: Math.max(0, requiredNumber(slot, "gapBeforeWeight", `${path}.gapBeforeWeight`)),
    alternatives: (authoredRuntimeStateId || animatedState.animated) && runtimeStateId
      ? runtimeSelectedAlternatives(payload, slot, runtimeStateId, alternatives, path, animatedTransition)
      : visibleAlternativesWithExits(payload, rawAlternatives, alternatives),
  };
}

type RuntimeStateTransitionFrame = {
  fromId: string;
  elapsedMs: number;
};

function runtimeSelectedAlternatives(
  payload: DesignPreviewPayload,
  slot: Record<string, unknown>,
  stateId: string,
  alternatives: ComponentStackAlternativeContract[],
  path: string,
  animatedTransition?: RuntimeStateTransitionFrame,
) {
  const target = alternatives.find((alternative) => alternative.id === stateId);
  if (!target) throw new Error(`Unknown Component Stack runtime state ${stateId} at ${path}`);
  const desired = target.behavior === "replace"
    ? [target]
    : [alternatives[0], target].filter((item, index, items) => item && items.indexOf(item) === index);
  const transition = slot.runtimeStateTransition === true
    ? {
        fromId: optionalString(slot, "runtimeStateFromId"),
        elapsedMs: Math.max(0, Number(slot.runtimeStateElapsedMs) || 0),
      }
    : animatedTransition;
  if (!transition) return desired.map((item) => ({ ...item, active: true }));

  const frame = rootScreenFrame(payload);
  const elapsedMs = transition.elapsedMs;
  const eventFrame = Math.max(0, frame - Math.floor(elapsedMs / 1000 * Math.max(1, payload.frameRate)));
  const outgoing = alternatives.find((alternative) => alternative.id === transition.fromId);
  const exitDurationMs = outgoing && !desired.some((item) => item.id === outgoing.id)
    ? motionTotalDurationMs(payload, outgoing.exitMotion)
    : 0;
  const enterDurationMs = Math.max(0, ...desired.map((item) => motionTotalDurationMs(payload, item.enterMotion)));
  if (elapsedMs >= Math.max(exitDurationMs, enterDurationMs)) {
    return desired.map((item) => ({ ...item, active: true }));
  }
  const entering = desired.map((item) => ({
    ...item,
    active: true,
    activationFrame: eventFrame,
    enterElapsedMs: elapsedMs,
  }));
  if (!outgoing || entering.some((item) => item.id === outgoing.id) || elapsedMs >= exitDurationMs) return entering;
  return [...entering, {
    ...outgoing,
    active: false,
    exitFrame: eventFrame,
    exitElapsedMs: elapsedMs,
  }];
}

function resolveAlternative(
  payload: DesignPreviewPayload,
  alternative: Record<string, unknown>,
  index: number,
  slotPath: string,
  gapBeforeMode: ComponentStackGapMode,
  gapBeforeToken: string,
  gapBeforeWeight: number,
): ComponentStackAlternativeContract {
  const path = `${slotPath}.alternatives[${index}]`;
  const behavior = index === 0 ? "replace" : requiredString(alternative, "behavior", `${path}.behavior`);
  if (behavior !== "replace" && behavior !== "overlay") {
    throw new Error(`Unsupported Component Stack state behavior ${behavior}`);
  }
  const instance = parseObject(payload.instanceJson);
  const frame = rootScreenFrame(payload);
  const animation = optionalObject(instance, "animation", "Preview instance envelope");
  const id = requiredString(alternative, "id", `${path}.id`);
  const resolvedActive = index === 0
    ? { value: true, sourceKeyframeFrame: undefined }
    : resolveParameterAnimation(animation, "active", id, frame, alternative.active === true);
  const variantReference = optionalString(alternative, "variantReference");
  const component = variantReference
    ? resolveComponentCollectionItem(payload, {
        ...alternative,
        alignment: "center",
        gapBeforeMode,
        gapBeforeToken,
        gapBeforeWeight,
      }, path, undefined, false)
    : undefined;
  const boundaryMotion = component
    ? optionalComponentBoundaryMotion(component.config, `${path}.component`)
    : undefined;
  return {
    id,
    component,
    placement: requiredPlacement(alternative, "placement", `${path}.placement`),
    behavior,
    active: resolvedActive.value === true,
    isDefault: index === 0,
    enterMotion: boundaryMotion
      ?? requiredMotionContract(alternative, "enterMotion", `${path}.enterMotion`),
    exitMotion: boundaryMotion
      ?? requiredMotionContract(alternative, "exitMotion", `${path}.exitMotion`),
    activationFrame: resolvedActive.value === true ? resolvedActive.sourceKeyframeFrame : undefined,
  };
}

function visibleAlternatives(alternatives: ComponentStackAlternativeContract[]) {
  let visible: ComponentStackAlternativeContract[] = [];
  for (const alternative of alternatives) {
    if (!alternative.active) continue;
    visible = alternative.behavior === "replace" ? [alternative] : [...visible, alternative];
  }
  return visible;
}

function visibleAlternativesWithExits(
  payload: DesignPreviewPayload,
  rawAlternatives: Record<string, unknown>[],
  alternatives: ComponentStackAlternativeContract[],
) {
  const instance = parseObject(payload.instanceJson);
  const animation = optionalObject(instance, "animation", "Preview instance envelope");
  const frame = rootScreenFrame(payload);
  const desired = visibleAlternatives(alternatives);
  const desiredIds = new Set(desired.map((item) => item.id));
  const byId = new Map(alternatives.map((item) => [item.id, item]));
  validateTransientAnimationDocument(animation);
  const eventFrames = optionalObjectArray(animation, "tracks", "Component Stack animation")
    .filter((track) => optionalString(track, "fieldId") === "active" && byId.has(optionalString(track, "targetId")))
    .flatMap((track, index) => optionalObjectArray(track, "keyframes", `Component Stack active track[${index}]`))
    .filter((keyframe) => keyframe.enabled !== false)
    .map((keyframe) => requiredNumberValue(keyframe.frame, "Component Stack active keyframe frame"))
    .filter((eventFrame) => eventFrame <= frame)
    .sort((a, b) => b - a);
  const exiting: ComponentStackAlternativeContract[] = [];
  const exitingIds = new Set<string>();
  for (const eventFrame of [...new Set(eventFrames)]) {
    const before = new Set(selectedIdsAt(rawAlternatives, alternatives, animation, Math.max(0, eventFrame - 1)));
    const after = new Set(selectedIdsAt(rawAlternatives, alternatives, animation, eventFrame));
    for (const id of before) {
      if (after.has(id) || desiredIds.has(id) || exitingIds.has(id)) continue;
      const alternative = byId.get(id);
      if (!alternative) continue;
      const durationFrames = Math.ceil(
        motionTotalDurationMs(payload, alternative.exitMotion) / 1000 * Math.max(1, payload.frameRate),
      );
      if (durationFrames <= 0 || frame - eventFrame >= durationFrames) continue;
      exiting.push({ ...alternative, active: false, exitFrame: eventFrame });
      exitingIds.add(id);
    }
  }
  return [...desired, ...exiting];
}

function selectedIdsAt(
  rawAlternatives: Record<string, unknown>[],
  alternatives: ComponentStackAlternativeContract[],
  animation: Record<string, unknown>,
  frame: number,
) {
  return visibleAlternatives(alternatives.map((alternative, index) => ({
    ...alternative,
    active: index === 0
      || resolveParameterAnimation(animation, "active", alternative.id, frame, rawAlternatives[index]?.active === true).value === true,
  }))).map((item) => item.id);
}
