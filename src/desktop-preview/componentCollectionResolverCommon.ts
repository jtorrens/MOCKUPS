import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, optionalBoolean, optionalNumber, parseObject, requiredNumber, requiredRecord, requiredString } from "./componentResolverCommon.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { motionTotalDurationMs, requiredMotionContract } from "./previewMotionHelpers.js";
import type {
  ComponentCollectionAlignment,
  ComponentCollectionGapMode,
  ComponentCollectionItemContract,
} from "./componentCollectionContract.js";

export function resolveComponentCollectionItems(
  payload: DesignPreviewPayload,
  preview: Record<string, unknown>,
  ownerPath: string,
): ComponentCollectionItemContract[] {
  if (!Array.isArray(preview.items)) throw new Error(`Missing ${ownerPath} runtime items`);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const presetTypes = requiredRecord(bases, "presetTypes", "componentBaseConfigs.presetTypes");
  return preview.items.map((rawItem, index) => resolveComponentCollectionItem(
    payload,
    asRecord(rawItem),
    `${ownerPath}.items[${index}]`,
    presetTypes,
  ));
}

export function resolveComponentCollectionItem(
  payload: DesignPreviewPayload,
  item: Record<string, unknown>,
  itemPath: string,
  presetTypesOverride?: Record<string, unknown>,
  ownsPresence = true,
): ComponentCollectionItemContract {
    const bases = parseObject(payload.componentBaseConfigsJson);
    const presetTypes = presetTypesOverride
      ?? requiredRecord(bases, "presetTypes", "componentBaseConfigs.presetTypes");
    const rawId = requiredString(item, "id", `${itemPath}.id`);
    const instance = parseObject(payload.instanceJson ?? "{}");
    const frame = Math.max(0, Math.floor(Number(asRecord(instance.context).localFrame) || 0));
    const animation = asRecord(instance.animation);
    const presence = ownsPresence
      ? resolveParameterAnimation(animation, "present", rawId, frame, item.present === true)
      : { value: true, animated: false, sourceKeyframeFrame: undefined };
    const presenceMotion = requiredMotionContract(
      item,
      ownsPresence ? "presenceMotion" : "enterMotion",
      `${itemPath}.${ownsPresence ? "presenceMotion" : "enterMotion"}`,
    );
    const present = presence.value === true;
    const presenceTransition = optionalBoolean(item, "presenceTransition");
    const presenceElapsedMs = Math.max(0, optionalNumber(item, "presenceElapsedMs", 0));
    const exitDurationFrames = Math.ceil(
      motionTotalDurationMs(payload, presenceMotion) / 1000 * Math.max(1, payload.frameRate),
    );
    const exitFrame = !present
      && presence.sourceKeyframeFrame !== undefined
      && frame - presence.sourceKeyframeFrame < exitDurationFrames
        ? presence.sourceKeyframeFrame
        : undefined;
    const removalReflowStartFrame = !present && presence.sourceKeyframeFrame !== undefined
      ? presence.sourceKeyframeFrame + exitDurationFrames
      : undefined;
    const rawInputs = requiredRecord(item, "inputs", `${itemPath}.inputs`);
    const inputResolution = resolveAnimatedInputs(animation, rawInputs, rawId, frame);
    const reflowStartFrame = removalReflowStartFrame ?? inputResolution.changeFrame;
    const presetReference = requiredString(item, "presetId", `${itemPath}.presetId`);
    const componentType = presetTypes[presetReference];
    if (typeof componentType !== "string" || !componentType) {
      throw new Error(`Missing component type for ${itemPath} Variant ${presetReference}`);
    }
    const alignment = requiredString(item, "alignment", `${itemPath}.alignment`);
    if (alignment !== "start" && alignment !== "center" && alignment !== "end") {
      throw new Error(`Unsupported ${itemPath} alignment ${alignment}`);
    }
    const gapBeforeMode = requiredString(item, "gapBeforeMode", `${itemPath}.gapBeforeMode`);
    if (gapBeforeMode !== "fixed" && gapBeforeMode !== "reflow") {
      throw new Error(`Unsupported ${itemPath} gap-before mode ${gapBeforeMode}`);
    }
    return {
      id: rawId,
      componentType,
      presetReference,
      config: mergeComponentDefaults(
        componentPresetConfig(bases, componentType, presetReference),
        asRecord(item.overrides),
      ),
      alignment: alignment as ComponentCollectionAlignment,
      gapBeforeMode: gapBeforeMode as ComponentCollectionGapMode,
      gapBeforeToken: requiredString(item, "gapBeforeToken", `${itemPath}.gapBeforeToken`),
      gapBeforeWeight: Math.max(0, requiredNumber(item, "gapBeforeWeight", `${itemPath}.gapBeforeWeight`)),
      inputs: inputResolution.values,
      present,
      presenceMotion,
      activationFrame: present ? presence.sourceKeyframeFrame : undefined,
      exitFrame,
      reflowStartFrame,
      reflowFromInputs: inputResolution.previousValues,
      presenceTransition,
      presenceElapsedMs,
    };
}

function resolveAnimatedInputs(
  animation: Record<string, unknown>,
  inputs: Record<string, unknown>,
  targetId: string,
  frame: number,
) {
  const resolved = Object.fromEntries(Object.entries(inputs).map(([fieldId, value]) => [
    fieldId,
    resolveParameterAnimation(animation, fieldId, targetId, frame, value).value,
  ]));
  const sourceFrames = Object.keys(inputs).flatMap((fieldId) => {
    const source = resolveParameterAnimation(animation, fieldId, targetId, frame, inputs[fieldId]).sourceKeyframeFrame;
    return source === undefined || source <= 0 ? [] : [source];
  });
  const changeFrame = sourceFrames.length ? Math.max(...sourceFrames) : undefined;
  if (changeFrame === undefined) return { values: resolved, changeFrame, previousValues: undefined };
  const previousValues = Object.fromEntries(Object.entries(inputs).map(([fieldId, value]) => [
    fieldId,
    resolveParameterAnimation(animation, fieldId, targetId, Math.max(0, changeFrame - 1), value).value,
  ]));
  const transitionValues = Object.fromEntries(Object.keys(inputs).flatMap((fieldId) => {
    const sourceFrame = resolveParameterAnimation(animation, fieldId, targetId, frame, inputs[fieldId]).sourceKeyframeFrame;
    return sourceFrame === undefined || sourceFrame <= 0
      ? []
      : [[fieldId, { sourceFrame, previousValue: resolveParameterAnimation(
          animation,
          fieldId,
          targetId,
          Math.max(0, sourceFrame - 1),
          inputs[fieldId],
        ).value }]];
  }));
  return {
    values: { ...resolved, __runtimeTransitions: transitionValues },
    changeFrame,
    previousValues,
  };
}
