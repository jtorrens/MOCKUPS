import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentVariantConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, optionalBoolean, optionalNumber, optionalString, parseObject, requiredNumber, requiredRecord, requiredString } from "./componentResolverCommon.js";
import { optionalObjectArray } from "./previewJsonHelpers.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { motionTotalDurationMs, requiredMotionContract } from "./previewMotionHelpers.js";
import { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";
import { resolveBehaviorTimingFrames } from "./behaviorTiming.js";
import { requiredNumberValue } from "./previewValueHelpers.js";
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
  const variantTypes = requiredRecord(bases, "variantTypes", "componentBaseConfigs.variantTypes");
  return preview.items.map((rawItem, index) => resolveComponentCollectionItem(
    payload,
    asRecord(rawItem),
    `${ownerPath}.items[${index}]`,
    variantTypes,
  ));
}

export function resolveComponentCollectionItem(
  payload: DesignPreviewPayload,
  item: Record<string, unknown>,
  itemPath: string,
  variantTypesOverride?: Record<string, unknown>,
  ownsPresence = true,
): ComponentCollectionItemContract {
    const bases = parseObject(payload.componentBaseConfigsJson);
    const variantTypes = variantTypesOverride
      ?? requiredRecord(bases, "variantTypes", "componentBaseConfigs.variantTypes");
    const rawId = requiredString(item, "id", `${itemPath}.id`);
    const instance = parseObject(payload.instanceJson);
    const frame = Math.max(0, Math.floor(Number(asRecord(instance.context).localFrame) || 0));
    const animation = asRecord(instance.animation);
    const runtimeContract = parseObject(payload.runtimeContractJson);
    const themeTokens = parseObject(payload.themeTokensJson);
    const timeline = new RuntimeOwnerTimeline(runtimeContract, runtimeContract, animation, themeTokens);
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
    const inputResolution = resolveAnimatedInputs(timeline, animation, rawInputs, rawId, frame, themeTokens, payload.frameRate);
    const reflowStartFrame = removalReflowStartFrame ?? inputResolution.changeFrame;
    const variantReference = requiredString(item, "variantReference", `${itemPath}.variantReference`);
    const componentType = variantTypes[variantReference];
    if (typeof componentType !== "string" || !componentType) {
      throw new Error(`Missing component type for ${itemPath} Variant ${variantReference}`);
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
      variantReference,
      config: mergeComponentDefaults(
        componentVariantConfig(bases, componentType, variantReference),
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
  timeline: RuntimeOwnerTimeline,
  animation: Record<string, unknown>,
  inputs: Record<string, unknown>,
  targetId: string,
  screenFrame: number,
  themeTokens: Record<string, unknown>,
  frameRate: number,
) {
  const definitions = runtimeInputDefinitions(inputs);
  const runtimeFieldIds = runtimeFieldIdMap(inputs);
  const declaredFields = definitions.flatMap(({ id, jsonKey }) => {
    if (!Object.hasOwn(inputs, jsonKey)) return [];
    return [{
      jsonKey,
      fieldId: Object.hasOwn(runtimeFieldIds, jsonKey)
        ? requiredString(runtimeFieldIds, jsonKey, `component collection Runtime field id '${jsonKey}'`)
        : id,
    }];
  });
  const declaredKeys = new Set(definitions.map((definition) => definition.jsonKey));
  const fields = [
    ...declaredFields,
    ...Object.keys(runtimeFieldIds)
      .filter((jsonKey) => !declaredKeys.has(jsonKey))
      .map((jsonKey) => ({
        jsonKey,
        fieldId: requiredString(runtimeFieldIds, jsonKey, `component collection Runtime field id '${jsonKey}'`),
      })),
  ];
  const localFrame = (fieldId: string, frame = screenFrame) => timeline.ownsTarget(targetId)
    ? Math.floor(timeline.localFrame(fieldId, targetId, frame))
    : frame;
  const resolved: Record<string, unknown> = { ...inputs };
  for (const field of fields) {
    resolved[field.jsonKey] = resolveParameterAnimation(
      animation,
      field.fieldId,
      targetId,
      localFrame(field.fieldId),
      inputs[field.jsonKey],
    ).value;
  }
  resolveAnimatedActions(
    resolved,
    definitions.map(({ definition }) => definition),
    runtimeFieldIds,
    timeline,
    animation,
    targetId,
    screenFrame,
    themeTokens,
    frameRate,
  );
  const sourceFrames = fields.flatMap((field) => {
    const source = resolveParameterAnimation(
      animation,
      field.fieldId,
      targetId,
      localFrame(field.fieldId),
      inputs[field.jsonKey],
    ).sourceKeyframeFrame;
    return source === undefined || source <= 0 ? [] : [timeline.screenFrame(field.fieldId, targetId, source)];
  });
  const changeFrame = sourceFrames.length ? Math.max(...sourceFrames) : undefined;
  if (changeFrame === undefined) return { values: resolved, changeFrame, previousValues: undefined };
  const previousValues: Record<string, unknown> = { ...inputs };
  for (const field of fields) {
    previousValues[field.jsonKey] = resolveParameterAnimation(
      animation,
      field.fieldId,
      targetId,
      localFrame(field.fieldId, Math.max(0, changeFrame - 1)),
      inputs[field.jsonKey],
    ).value;
  }
  const transitionValues = Object.fromEntries(fields.flatMap((field) => {
    const sourceLocalFrame = resolveParameterAnimation(
      animation,
      field.fieldId,
      targetId,
      localFrame(field.fieldId),
      inputs[field.jsonKey],
    ).sourceKeyframeFrame;
    const sourceFrame = sourceLocalFrame === undefined
      ? undefined
      : timeline.screenFrame(field.fieldId, targetId, sourceLocalFrame);
    return sourceFrame === undefined || sourceFrame <= 0
      ? []
      : [[field.jsonKey, { sourceFrame, previousValue: resolveParameterAnimation(
          animation,
          field.fieldId,
          targetId,
          localFrame(field.fieldId, Math.max(0, sourceFrame - 1)),
          inputs[field.jsonKey],
        ).value }]];
  }));
  return {
    values: { ...resolved, __runtimeTransitions: transitionValues },
    changeFrame,
    previousValues,
  };
}

type RuntimeInputDefinition = {
  definition: Record<string, unknown>;
  id: string;
  jsonKey: string;
};

function runtimeInputDefinitions(inputs: Record<string, unknown>): RuntimeInputDefinition[] {
  const ids = new Set<string>();
  const jsonKeys = new Set<string>();
  return optionalObjectArray(inputs, "inputs", "component collection embedded Runtime contract")
    .map((definition, index) => {
      const path = `component collection embedded Runtime input[${index}]`;
      const id = requiredString(definition, "id", `${path}.id`);
      const jsonKey = requiredString(definition, "jsonKey", `${path}.jsonKey`);
      if (ids.has(id)) throw new Error(`Duplicate ${path} id '${id}'`);
      if (jsonKeys.has(jsonKey)) throw new Error(`Duplicate ${path} jsonKey '${jsonKey}'`);
      ids.add(id);
      jsonKeys.add(jsonKey);
      return { definition, id, jsonKey };
    });
}

function runtimeFieldIdMap(inputs: Record<string, unknown>) {
  if (!Object.hasOwn(inputs, "__runtimeFieldIds")) return {};
  const fieldIds = requiredRecord(
    inputs,
    "__runtimeFieldIds",
    "component collection embedded Runtime field ids",
  );
  for (const key of Object.keys(fieldIds)) {
    requiredString(fieldIds, key, `component collection Runtime field id '${key}'`);
    if (!Object.hasOwn(inputs, key) || key === "inputs" || key === "actions" || key === "__runtimeFieldIds") {
      throw new Error(`Component collection Runtime field id '${key}' has no local value`);
    }
  }
  return fieldIds;
}

function resolveAnimatedActions(
  values: Record<string, unknown>,
  definitions: Record<string, unknown>[],
  runtimeFieldIds: Record<string, unknown>,
  timeline: RuntimeOwnerTimeline,
  animation: Record<string, unknown>,
  targetId: string,
  screenFrame: number,
  themeTokens: Record<string, unknown>,
  frameRate: number,
) {
  const actions = embeddedRuntimeActions(values);
  for (const { action, id, playJsonKey, timeJsonKey, timeUnit, completion } of actions) {
    const playDefinition = definitions.find((definition) => optionalString(definition, "jsonKey") === playJsonKey);
    const mappedPlayFieldId = Object.hasOwn(runtimeFieldIds, playJsonKey)
      ? requiredString(runtimeFieldIds, playJsonKey, `embedded runtime action '${id}' forwarded play field`)
      : "";
    const actionPlayFieldId = Object.hasOwn(action, "playFieldId")
      ? requiredString(action, "playFieldId", `embedded runtime action '${id}' play field`)
      : "";
    if (mappedPlayFieldId && actionPlayFieldId && mappedPlayFieldId !== actionPlayFieldId) {
      throw new Error(`Embedded runtime action '${id}' has conflicting forwarded play field ids`);
    }
    const playFieldId = mappedPlayFieldId
      || actionPlayFieldId
      || optionalString(playDefinition ?? {}, "id");
    if (!Object.hasOwn(values, playJsonKey)) continue;
    if (!playFieldId) {
      throw new Error(`Embedded runtime action '${id}' play value '${playJsonKey}' has no stable field id`);
    }
    const ownerFrame = timeline.ownsTarget(targetId)
      ? Math.floor(timeline.localFrame(playFieldId, targetId, screenFrame))
      : screenFrame;
    const resolvedPlay = resolveParameterAnimation(
      animation,
      playFieldId,
      targetId,
      ownerFrame,
      values[playJsonKey],
    );
    if (!resolvedPlay.animated) continue;
    if (typeof resolvedPlay.value !== "boolean") {
      throw new Error(`Embedded runtime action '${id}' play value must be boolean`);
    }
    if (resolvedPlay.value !== true || resolvedPlay.sourceKeyframeFrame === undefined) {
      values[playJsonKey] = false;
      values[timeJsonKey] = 0;
      continue;
    }
    const durationFrames = actionDurationFrames(action, values, definitions, themeTokens, frameRate);
    const elapsedFrames = Math.max(0, ownerFrame - resolvedPlay.sourceKeyframeFrame);
    if (completion === "reset" && elapsedFrames >= durationFrames) {
      values[playJsonKey] = false;
      values[timeJsonKey] = 0;
      continue;
    }
    values[playJsonKey] = true;
    values[timeJsonKey] = actionTimeValue(
      Math.min(elapsedFrames, durationFrames),
      timeUnit,
      frameRate,
    );
  }
}

type EmbeddedRuntimeAction = {
  action: Record<string, unknown>;
  id: string;
  playJsonKey: string;
  timeJsonKey: string;
  timeUnit: "frames" | "milliseconds" | "seconds";
  completion: "reset" | "holdFinal";
};

function embeddedRuntimeActions(values: Record<string, unknown>): EmbeddedRuntimeAction[] {
  const ids = new Set<string>();
  return optionalObjectArray(values, "actions", "component collection embedded Runtime contract")
    .map((action, index) => {
      const path = `component collection embedded Runtime action[${index}]`;
      const id = requiredString(action, "id", `${path}.id`);
      requiredString(action, "label", `${path}.label`);
      const playJsonKey = requiredString(action, "playInputId", `${path}.playInputId`);
      const timeJsonKey = requiredString(action, "timeJsonKey", `${path}.timeJsonKey`);
      const timeUnit = requiredString(action, "timeUnit", `${path}.timeUnit`);
      if (timeUnit !== "frames" && timeUnit !== "milliseconds" && timeUnit !== "seconds") {
        throw new Error(`Unsupported ${path}.timeUnit '${timeUnit}'`);
      }
      const completion = requiredString(action, "completionBehavior", `${path}.completionBehavior`);
      if (completion !== "reset" && completion !== "holdFinal") {
        throw new Error(`Unsupported ${path}.completionBehavior '${completion}'`);
      }
      if (ids.has(id)) throw new Error(`Duplicate embedded Runtime action id '${id}'`);
      ids.add(id);
      return { action, id, playJsonKey, timeJsonKey, timeUnit, completion };
    });
}

function actionDurationFrames(
  action: Record<string, unknown>,
  values: Record<string, unknown>,
  definitions: Record<string, unknown>[],
  themeTokens: Record<string, unknown>,
  frameRate: number,
) {
  const timingId = optionalString(action, "durationBehaviorTimingInputId");
  if (timingId) {
    const timing = definitions.find((definition) => optionalString(definition, "id") === timingId);
    if (!timing) throw new Error(`Runtime action references missing BehaviorTiming '${timingId}'`);
    return resolveBehaviorTimingFrames(values, timing, definitions, themeTokens);
  }
  const durationInputId = optionalString(action, "durationInputId");
  if (durationInputId) {
    const definition = definitions.find((candidate) => optionalString(candidate, "id") === durationInputId);
    if (!definition) throw new Error(`Runtime action references missing duration input '${durationInputId}'`);
    const duration = requiredNumberValue(
      values[optionalString(definition, "jsonKey")],
      `runtime action '${optionalString(action, "id")}' duration input '${durationInputId}'`,
    );
    if (duration <= 0) {
      throw new Error(`Runtime action '${optionalString(action, "id")}' duration input '${durationInputId}' must be positive.`);
    }
    return Math.max(1, actionFrames(duration, optionalString(action, "timeUnit"), frameRate));
  }
  if (Object.hasOwn(action, "durationSeconds")) {
    const durationSeconds = requiredNumberValue(
      action.durationSeconds,
      `runtime action '${optionalString(action, "id")}' durationSeconds`,
    );
    if (durationSeconds <= 0) {
      throw new Error(`Runtime action '${optionalString(action, "id")}' durationSeconds must be positive.`);
    }
    return Math.max(1, Math.round(durationSeconds * Math.max(1, frameRate)));
  }
  throw new Error(`Runtime action '${optionalString(action, "id")}' has no finite duration contract`);
}

function actionFrames(value: number, timeUnit: string, frameRate: number) {
  if (timeUnit === "frames") return Math.round(value);
  if (timeUnit === "milliseconds") return Math.round(value / 1000 * Math.max(1, frameRate));
  if (timeUnit === "seconds") return Math.round(value * Math.max(1, frameRate));
  throw new Error(`Unsupported runtime action timeUnit '${timeUnit}'`);
}

function actionTimeValue(frames: number, timeUnit: string, frameRate: number) {
  if (timeUnit === "frames") return frames;
  if (timeUnit === "milliseconds") return frames / Math.max(1, frameRate) * 1000;
  if (timeUnit === "seconds") return frames / Math.max(1, frameRate);
  throw new Error(`Unsupported runtime action timeUnit '${timeUnit}'`);
}
