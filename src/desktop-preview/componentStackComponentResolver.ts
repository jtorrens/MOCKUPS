import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveComponentCollectionItem } from "./componentCollectionResolverCommon.js";
import {
  asRecord,
  optionalString,
  parseObject,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { motionTotalDurationMs, requiredMotionContract } from "./previewMotionHelpers.js";
import type {
  ComponentStackAlternativeContract,
  ComponentStackAlignment,
  ComponentStackDesignContract,
  ComponentStackGapMode,
  ComponentStackSizingMode,
  ComponentStackSlotContract,
} from "./componentStackComponentContract.js";

export function resolveComponentStackComponent(payload: DesignPreviewPayload): ComponentStackDesignContract {
  const preview = parseObject(payload.designPreviewJson);
  const sizingMode = requiredString(preview, "sizingMode", "componentStack.runtime.sizingMode");
  if (sizingMode !== "fill" && sizingMode !== "content") {
    throw new Error(`Unsupported component stack sizing mode ${sizingMode}`);
  }
  if (!Array.isArray(preview.items)) throw new Error("Missing componentStack runtime slots");
  return {
    id: "componentStack",
    sizingMode: sizingMode as ComponentStackSizingMode,
    startGapToken: requiredString(preview, "startGapToken", "componentStack.runtime.startGapToken"),
    endGapToken: requiredString(preview, "endGapToken", "componentStack.runtime.endGapToken"),
    slots: preview.items.map((rawSlot, index) => resolveSlot(payload, asRecord(rawSlot), index)),
  };
}

function resolveSlot(
  payload: DesignPreviewPayload,
  slot: Record<string, unknown>,
  index: number,
): ComponentStackSlotContract {
  const path = `componentStack.items[${index}]`;
  const alignment = requiredString(slot, "alignment", `${path}.alignment`);
  if (alignment !== "start" && alignment !== "center" && alignment !== "end") {
    throw new Error(`Unsupported componentStack alignment ${alignment}`);
  }
  const gapBeforeMode = requiredString(slot, "gapBeforeMode", `${path}.gapBeforeMode`);
  if (gapBeforeMode !== "fixed" && gapBeforeMode !== "reflow") {
    throw new Error(`Unsupported componentStack gap-before mode ${gapBeforeMode}`);
  }
  if (!Array.isArray(slot.alternatives)) throw new Error(`Missing Component Stack states at ${path}.alternatives`);
  const rawAlternatives = slot.alternatives.map(asRecord);
  const alternatives = rawAlternatives.map((rawAlternative, alternativeIndex) => resolveAlternative(
    payload,
    asRecord(rawAlternative),
    alternativeIndex,
    path,
    alignment as ComponentStackAlignment,
    gapBeforeMode as ComponentStackGapMode,
    requiredString(slot, "gapBeforeToken", `${path}.gapBeforeToken`),
    Math.max(0, requiredNumber(slot, "gapBeforeWeight", `${path}.gapBeforeWeight`)),
  ));
  return {
    id: requiredString(slot, "id", `${path}.id`),
    alignment: alignment as ComponentStackAlignment,
    gapBeforeMode: gapBeforeMode as ComponentStackGapMode,
    gapBeforeToken: requiredString(slot, "gapBeforeToken", `${path}.gapBeforeToken`),
    gapBeforeWeight: Math.max(0, requiredNumber(slot, "gapBeforeWeight", `${path}.gapBeforeWeight`)),
    alternatives: visibleAlternativesWithExits(payload, rawAlternatives, alternatives),
  };
}

function resolveAlternative(
  payload: DesignPreviewPayload,
  alternative: Record<string, unknown>,
  index: number,
  slotPath: string,
  alignment: ComponentStackAlignment,
  gapBeforeMode: ComponentStackGapMode,
  gapBeforeToken: string,
  gapBeforeWeight: number,
): ComponentStackAlternativeContract {
  const path = `${slotPath}.alternatives[${index}]`;
  const behavior = index === 0 ? "replace" : requiredString(alternative, "behavior", `${path}.behavior`);
  if (behavior !== "replace" && behavior !== "overlay") {
    throw new Error(`Unsupported Component Stack state behavior ${behavior}`);
  }
  const instance = parseObject(payload.instanceJson ?? "{}");
  const context = asRecord(instance.context);
  const frame = Math.max(0, Math.floor(Number(context.localFrame) || 0));
  const animation = asRecord(instance.animation);
  const id = requiredString(alternative, "id", `${path}.id`);
  const resolvedActive = index === 0
    ? { value: true, sourceKeyframeFrame: undefined }
    : resolveParameterAnimation(animation, "active", id, frame, alternative.active === true);
  const presetReference = optionalString(alternative, "presetId");
  const component = presetReference
    ? resolveComponentCollectionItem(payload, {
        ...alternative,
        alignment,
        gapBeforeMode,
        gapBeforeToken,
        gapBeforeWeight,
      }, path)
    : undefined;
  return {
    id,
    component,
    behavior,
    active: resolvedActive.value === true,
    isDefault: index === 0,
    enterMotion: requiredMotionContract(alternative, "enterMotion", `${path}.enterMotion`),
    exitMotion: requiredMotionContract(alternative, "exitMotion", `${path}.exitMotion`),
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
  const instance = parseObject(payload.instanceJson ?? "{}");
  const animation = asRecord(instance.animation);
  const frame = Math.max(0, Math.floor(Number(asRecord(instance.context).localFrame) || 0));
  const desired = visibleAlternatives(alternatives);
  const desiredIds = new Set(desired.map((item) => item.id));
  const byId = new Map(alternatives.map((item) => [item.id, item]));
  const eventFrames = (Array.isArray(animation.tracks) ? animation.tracks : [])
    .map(asRecord)
    .filter((track) => optionalString(track, "fieldId") === "active" && byId.has(optionalString(track, "targetId")))
    .flatMap((track) => Array.isArray(track.keyframes) ? track.keyframes.map(asRecord) : [])
    .filter((keyframe) => keyframe.enabled !== false)
    .map((keyframe) => Math.max(0, Math.floor(Number(keyframe.frame) || 0)))
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
