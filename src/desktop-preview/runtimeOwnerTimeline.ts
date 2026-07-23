import { asRecord, optionalNumber, optionalString, requiredString } from "./componentResolverCommon.js";
import { isRecord } from "./previewJsonHelpers.js";
import { resolveBehaviorTimingFrames } from "./behaviorTiming.js";
import { requiredNumberValue } from "./previewValueHelpers.js";

type JsonRecord = Record<string, unknown>;
type FieldTiming = { origin: number; completion: number; endExclusive: number };
type ItemTiming = {
  collection: JsonRecord;
  item: JsonRecord;
  rootStart: number;
  naturalSpan: number;
  effectiveSpan: number;
  naturalSequence: number;
  effectiveSequence: number;
  fields: Map<string, FieldTiming>;
};

export class RuntimeOwnerTimeline {
  readonly naturalDuration: number;
  readonly durationFrames: number;
  private readonly items = new Map<string, ItemTiming>();
  private readonly topFields = new Map<string, FieldTiming>();

  constructor(
    private readonly contract: JsonRecord,
    private readonly runtime: JsonRecord,
    private readonly animation: JsonRecord,
    private readonly themeTokens: JsonRecord = {},
    storedFallback = 0,
  ) {
    let naturalEnd = Math.max(1, declaredBaseDuration(contract));
    for (const collection of optionalObjectArray(contract, "collections", "runtime owner contract")) {
      const key = collectionKey(collection);
      const values = optionalObjectArray(runtime, key, `runtime owner collection '${key}'`);
      const collectionTimeline = optionalObject(
        collection,
        "animationTimeline",
        "runtime owner collection animation timeline",
      );
      const sequenceItems = collectionTimeline.sequenceItems !== false;
      let cursor = 0;
      for (const item of values) {
        const fields = itemFields(collection, item);
        const targetId = requiredString(item, "id", `runtime owner collection '${key}' item id`);
        const pre = optionalStringArray(
          collectionTimeline,
          "preDurationFieldIds",
          "runtime collection animation timeline",
        )
          .reduce((sum, fieldId) => sum + fieldValue(item, fields, fieldId), 0);
        const start = (sequenceItems ? cursor : this.itemOwnerOrigin(collection, item)) + pre;
        const durations = this.itemDurations(collection, item, targetId);
        const effectiveSpan = this.targetDuration(targetId, durations.span);
        const effectiveSequence = scale(durations.sequence, durations.span, effectiveSpan);
        this.items.set(targetId, {
          collection,
          item,
          rootStart: start,
          naturalSpan: durations.span,
          effectiveSpan,
          naturalSequence: durations.sequence,
          effectiveSequence,
          fields: new Map(),
        });
        if (sequenceItems) cursor = start + effectiveSequence;
        naturalEnd = Math.max(naturalEnd, start + effectiveSpan);
      }
      if (sequenceItems) naturalEnd = Math.max(naturalEnd, cursor);
    }
    const topInputs = optionalObjectArray(contract, "inputs", "runtime owner contract");
    for (const definition of topInputs) {
      const fieldId = optionalString(definition, "id");
      if (!fieldId) continue;
      const timing = this.resolveFieldTiming(
        definition,
        runtime,
        "",
        topInputs,
        new Set(),
      );
      this.topFields.set(fieldId, timing);
      naturalEnd = Math.max(naturalEnd, timing.endExclusive);
    }
    if (naturalEnd <= 1 && storedFallback > 0) naturalEnd = storedFallback;
    this.naturalDuration = Math.max(1, naturalEnd);
    this.durationFrames = Math.max(1, round(this.rootTargetDuration(this.naturalDuration)));
  }

  screenFrame(fieldId: string, targetId: string, localFrame: number) {
    const rootNatural = this.rootNaturalFrame(fieldId, targetId, Math.max(0, localFrame));
    return round(scale(rootNatural, this.naturalDuration, this.durationFrames));
  }

  ownsTarget(targetId: string) {
    return this.items.has(targetId);
  }

  localFrame(fieldId: string, targetId: string, screenFrame: number) {
    const rootNatural = unscale(Math.max(0, screenFrame), this.naturalDuration, this.durationFrames);
    if (!targetId) return rootNatural - this.topField(fieldId).origin;
    const item = this.items.get(targetId);
    if (!item) return 0;
    const ownerEffective = rootNatural - item.rootStart;
    const ownerNatural = unscale(ownerEffective, item.naturalSpan, item.effectiveSpan);
    return ownerNatural - this.itemField(item, fieldId).origin;
  }

  itemStartFrame(targetId: string) {
    const item = this.items.get(targetId);
    return item
      ? round(scale(item.rootStart, this.naturalDuration, this.durationFrames))
      : 0;
  }

  itemEndFrame(targetId: string) {
    const item = this.items.get(targetId);
    return item
      ? round(scale(item.rootStart + item.effectiveSequence, this.naturalDuration, this.durationFrames))
      : 0;
  }

  itemOwnerFrame(targetId: string, naturalOwnerFrame: number) {
    const item = this.items.get(targetId);
    if (!item) return 0;
    const ownerEffective = scale(naturalOwnerFrame, item.naturalSpan, item.effectiveSpan);
    return round(scale(item.rootStart + ownerEffective, this.naturalDuration, this.durationFrames));
  }

  fieldCompletionLocal(fieldId: string, targetId: string) {
    if (!targetId) return this.topField(fieldId).completion;
    const item = this.items.get(targetId);
    return item ? this.itemField(item, fieldId).completion : 0;
  }

  fieldCompletionFrame(fieldId: string, targetId: string) {
    if (!targetId) return this.screenFrame(fieldId, "", round(this.topField(fieldId).completion));
    const item = this.items.get(targetId);
    if (!item) return 0;
    const field = this.itemField(item, fieldId);
    const ownerEffective = scale(field.completion, item.naturalSpan, item.effectiveSpan);
    return round(scale(item.rootStart + ownerEffective, this.naturalDuration, this.durationFrames));
  }

  usesTrackCompletion(fieldId: string, targetId: string) {
    const definition = this.fieldDefinition(fieldId, targetId);
    const completion = asRecord(asRecord(definition.animationTimeline).completion);
    const minimum = Math.max(2, Math.floor(optionalNumber(completion, "minimumEnabledKeyframes", 2)));
    return !!optionalString(completion, "baseDurationFieldId")
      && enabledKeyframes(this.track(fieldId, targetId)).length >= minimum;
  }

  private rootNaturalFrame(fieldId: string, targetId: string, localFrame: number) {
    if (!targetId) return this.topField(fieldId).origin + localFrame;
    const item = this.items.get(targetId);
    if (!item) return localFrame;
    const natural = this.itemField(item, fieldId).origin + localFrame;
    return item.rootStart + scale(natural, item.naturalSpan, item.effectiveSpan);
  }

  private topField(fieldId: string) {
    const existing = this.topFields.get(fieldId);
    if (existing) return existing;
    const fields = optionalObjectArray(this.contract, "inputs", "runtime owner contract");
    const definition = fields.find((field) => optionalString(field, "id") === fieldId);
    const timing = definition
      ? this.resolveFieldTiming(definition, this.runtime, "", fields, new Set())
      : { origin: 0, completion: 0, endExclusive: 0 };
    this.topFields.set(fieldId, timing);
    return timing;
  }

  private itemField(item: ItemTiming, fieldId: string) {
    const existing = item.fields.get(fieldId);
    if (existing) return existing;
    const fields = itemFields(item.collection, item.item);
    const definition = fields.find((field) => optionalString(field, "id") === fieldId);
    const timing = definition
      ? this.resolveFieldTiming(definition, item.item, optionalString(item.item, "id"), fields, new Set())
      : { origin: 0, completion: 0, endExclusive: 0 };
    item.fields.set(fieldId, timing);
    return timing;
  }

  private itemDurations(collection: JsonRecord, item: JsonRecord, targetId: string) {
    const fields = itemFields(collection, item);
    let sequenceBodyEnd = 0;
    let spanEnd = 0;
    for (const definition of fields) {
      const end = this.resolveFieldTiming(
        definition,
        item,
        targetId,
        fields,
        new Set(),
      ).endExclusive;
      spanEnd = Math.max(spanEnd, end);
      if (asRecord(definition.animationTimeline).extendsOwnerDuration !== false) {
        sequenceBodyEnd = Math.max(sequenceBodyEnd, end);
      }
    }
    const actionEnd = this.lastFiniteActionEnd(collection, item, targetId, fields);
    sequenceBodyEnd = Math.max(sequenceBodyEnd, actionEnd);
    spanEnd = Math.max(spanEnd, actionEnd);
    const post = optionalStringArray(
      optionalObject(collection, "animationTimeline", "runtime owner collection animation timeline"),
      "postDurationFieldIds",
      "runtime collection animation timeline",
    )
      .reduce((sum, fieldId) => sum + fieldValue(item, fields, fieldId), 0);
    const sequence = Math.max(1, sequenceBodyEnd + post);
    return { sequence, span: Math.max(sequence, spanEnd) };
  }

  private resolveFieldTiming(
    definition: JsonRecord,
    owner: JsonRecord,
    targetId: string,
    ownerFields: JsonRecord[],
    resolving: Set<string>,
  ): FieldTiming {
    const fieldId = optionalString(definition, "id");
    if (resolving.has(fieldId)) throw new Error(`Animation timeline dependency cycle at field '${fieldId}'.`);
    resolving.add(fieldId);
    const fieldTimeline = asRecord(definition.animationTimeline);
    const originDefinition = asRecord(fieldTimeline.origin);
    let origin = 0;
    if (optionalString(originDefinition, "kind") === "fieldCompletion") {
      const sourceId = optionalString(originDefinition, "fieldId");
      const source = ownerFields.find((field) => optionalString(field, "id") === sourceId);
      if (!source) throw new Error(`Animation field '${fieldId}' references missing field '${sourceId}'.`);
      origin = this.resolveFieldTiming(source, owner, targetId, ownerFields, resolving).completion
        + Math.max(0, optionalNumber(originDefinition, "offsetFrames", 0));
    }
    resolving.delete(fieldId);
    const keyframes = enabledKeyframes(this.track(fieldId, targetId));
    const completion = asRecord(fieldTimeline.completion);
    const baseFieldId = optionalString(completion, "baseDurationFieldId");
    const minimum = Math.max(2, Math.floor(optionalNumber(completion, "minimumEnabledKeyframes", 2)));
    if (baseFieldId && keyframes.length < minimum) {
      const baseDefinition = ownerFields.find((field) => optionalString(field, "id") === baseFieldId);
      const completionFrame = origin + (optionalString(baseDefinition ?? {}, "valueKind") === "BehaviorTiming"
        ? resolveBehaviorTimingFrames(owner, baseDefinition!, ownerFields, this.themeTokens)
        : fieldValue(owner, ownerFields, baseFieldId));
      return {
        origin,
        completion: completionFrame,
        endExclusive: Math.max(completionFrame, keyframes.length > 0 ? origin + 1 : 0),
      };
    }
    if (keyframes.length === 0) return { origin, completion: origin, endExclusive: 0 };
    const last = Math.max(0, optionalNumber(keyframes[keyframes.length - 1]!, "frame", 0));
    return { origin, completion: origin + last, endExclusive: origin + last + 1 };
  }

  private lastFiniteActionEnd(
    collection: JsonRecord,
    item: JsonRecord,
    targetId: string,
    fields: JsonRecord[],
  ) {
    let lastEnd = 0;
    for (const action of itemActions(collection, item)
      .filter((candidate) => candidate.extendsModuleDuration === true)) {
      const playFieldId = optionalString(action, "playFieldId") || optionalString(action, "playInputId");
      const definition = fields.find((field) => optionalString(field, "id") === playFieldId);
      if (!definition) continue;
      const origin = this.resolveFieldTiming(definition, item, targetId, fields, new Set()).origin;
      const keyframes = enabledKeyframes(this.track(playFieldId, targetId));
      const baseEnabled = item[optionalString(action, "durationEnabledInputId")] === true;
      const hasActiveKeyframe = keyframes.some((keyframe) => keyframe.value === true);
      if (!baseEnabled && !hasActiveKeyframe) continue;

      const durationInputId = optionalString(action, "durationInputId");
      if (!durationInputId) {
        throw new Error("A finite runtime action that extends Module duration requires durationInputId.");
      }
      const duration = requiredNumberValue(
        item[durationInputId],
        `runtime action duration input '${durationInputId}'`,
      );
      if (duration <= 0) {
        throw new Error(`Runtime action duration input '${durationInputId}' must be positive.`);
      }
      if (baseEnabled) lastEnd = Math.max(lastEnd, origin + duration);
      for (let index = 0; index < keyframes.length; index += 1) {
        const keyframe = keyframes[index]!;
        if (keyframe.value !== true) continue;
        const start = origin + Math.max(0, optionalNumber(keyframe, "frame", 0));
        const replacement = keyframes[index + 1]
          ? origin + Math.max(0, optionalNumber(keyframes[index + 1]!, "frame", 0))
          : Number.POSITIVE_INFINITY;
        lastEnd = Math.max(lastEnd, Math.min(start + duration, replacement));
      }
    }
    return lastEnd;
  }

  private fieldDefinition(fieldId: string, targetId: string) {
    if (!targetId) {
      return optionalObjectArray(this.contract, "inputs", "runtime owner contract")
        .find((field) => optionalString(field, "id") === fieldId) ?? {};
    }
    const item = this.items.get(targetId);
    return item ? itemFields(item.collection, item.item).find((field) => optionalString(field, "id") === fieldId) ?? {} : {};
  }

  private track(fieldId: string, targetId: string) {
    return records(this.animation.tracks).find((track) =>
      optionalString(track, "fieldId") === fieldId
      && optionalString(track, "targetId") === targetId);
  }

  private itemOwnerOrigin(collection: JsonRecord, item: JsonRecord) {
    const origin = asRecord(asRecord(collection.animationTimeline).ownerOrigin);
    if (optionalString(origin, "kind") !== "firstMatchingValue") return 0;

    const sourceCollectionKey = optionalString(origin, "sourceCollectionJsonKey");
    const sourceTargetIdJsonKey = optionalString(origin, "sourceTargetIdJsonKey");
    const sourceFieldId = optionalString(origin, "sourceFieldId");
    const sourceValueJsonKey = optionalString(origin, "sourceValueJsonKey");
    const matchValueJsonKey = optionalString(origin, "matchValueJsonKey");
    const sourceTargetId = optionalString(item, sourceTargetIdJsonKey);
    const matchValue = optionalString(item, matchValueJsonKey);
    if (!sourceCollectionKey || !sourceTargetId || !sourceFieldId || !sourceValueJsonKey || !matchValue) {
      throw new Error("Incomplete firstMatchingValue owner-origin contract");
    }

    const sourceItem = optionalObjectArray(
      this.runtime,
      sourceCollectionKey,
      `owner-origin source collection '${sourceCollectionKey}'`,
    )
      .find((candidate) => optionalString(candidate, "id") === sourceTargetId);
    if (!sourceItem) {
      throw new Error(`Owner-origin source item '${sourceTargetId}' is missing from '${sourceCollectionKey}'`);
    }
    if (optionalString(sourceItem, sourceValueJsonKey) === matchValue) return 0;
    const matchingFrames = enabledKeyframes(this.track(sourceFieldId, sourceTargetId))
      .filter((keyframe) => optionalString(keyframe, "value") === matchValue)
      .map((keyframe) => Math.max(0, optionalNumber(keyframe, "frame", 0)));
    return matchingFrames.length ? Math.min(...matchingFrames) : 0;
  }

  private targetDuration(targetId: string, natural: number) {
    const target = asRecord(asRecord(asRecord(this.animation.retime).targets)[targetId]);
    const duration = optionalNumber(target, "targetDurationFrames", 0);
    return duration > 0 ? duration : natural;
  }

  private rootTargetDuration(natural: number) {
    const duration = optionalNumber(asRecord(this.animation.retime), "targetDurationFrames", 0);
    return duration > 0 ? duration : natural;
  }
}

function records(value: unknown): JsonRecord[] {
  return Array.isArray(value) ? value.map(asRecord) : [];
}

function optionalObjectArray(owner: JsonRecord, key: string, path: string): JsonRecord[] {
  if (!Object.hasOwn(owner, key)) return [];
  const value = owner[key];
  if (!Array.isArray(value)) {
    throw new Error(`${path} '${key}' must be an array when present`);
  }
  return value.map((entry, index) => {
    if (!isRecord(entry)) {
      throw new Error(`${path} '${key}'[${index}] must be an object`);
    }
    return entry;
  });
}

function optionalObject(owner: JsonRecord, key: string, path: string): JsonRecord {
  if (!Object.hasOwn(owner, key)) return {};
  const value = owner[key];
  if (!isRecord(value)) {
    throw new Error(`${path} '${key}' must be an object when present`);
  }
  return value;
}

function requiredObject(owner: JsonRecord, key: string, path: string): JsonRecord {
  const value = owner[key];
  if (!isRecord(value)) {
    throw new Error(`${path} must contain object '${key}'`);
  }
  return value;
}

function optionalStringArray(owner: JsonRecord, key: string, path: string): string[] {
  if (!Object.hasOwn(owner, key)) return [];
  const value = owner[key];
  if (!Array.isArray(value)) {
    throw new Error(`${path} '${key}' must be an array when present`);
  }
  return value.map((entry, index) => {
    if (typeof entry !== "string" || !entry.trim()) {
      throw new Error(`${path} '${key}'[${index}] must be a non-empty string`);
    }
    return entry;
  });
}

function collectionKey(collection: JsonRecord) {
  return optionalString(collection, "storageCollectionJsonKey")
    || optionalString(collection, "sourceCollectionJsonKey")
    || optionalString(collection, "jsonKey");
}

function itemFields(collection: JsonRecord, item: JsonRecord) {
  const direct = optionalObjectArray(collection, "fields", "runtime owner collection");
  const componentItems = optionalObject(collection, "componentItems", "runtime owner collection");
  const inputsKey = optionalString(componentItems, "inputsJsonKey");
  const embeddedInputs = inputsKey
    ? requiredObject(item, inputsKey, `embedded Runtime collection item '${optionalString(item, "id")}'`)
    : {};
  const embedded = inputsKey
    ? optionalObjectArray(embeddedInputs, "inputs", "embedded Runtime contract")
    : [];
  const runtimeContractKey = optionalString(collection, "itemRuntimeContractJsonKey");
  const runtimeContract = runtimeContractKey
    ? requiredObject(item, runtimeContractKey, `projected Runtime collection item '${optionalString(item, "id")}'`)
    : {};
  const runtime = runtimeContractKey
    ? optionalObjectArray(runtimeContract, "inputs", "projected Runtime contract")
    : [];
  return [...direct, ...embedded, ...runtime];
}

function itemActions(collection: JsonRecord, item: JsonRecord) {
  const runtimeContractKey = optionalString(collection, "itemRuntimeContractJsonKey");
  const runtimeContract = runtimeContractKey
    ? requiredObject(item, runtimeContractKey, `projected Runtime collection item '${optionalString(item, "id")}'`)
    : {};
  const runtime = runtimeContractKey
    ? optionalObjectArray(runtimeContract, "actions", "projected Runtime contract")
    : [];
  return [
    ...optionalObjectArray(collection, "itemActions", "runtime owner collection"),
    ...runtime,
  ];
}

function fieldValue(owner: JsonRecord, fields: JsonRecord[], fieldId: string) {
  const definition = fields.find((field) => optionalString(field, "id") === fieldId);
  const jsonKey = optionalString(definition ?? {}, "jsonKey");
  if (Object.hasOwn(owner, jsonKey)) return Math.max(0, optionalNumber(owner, jsonKey, 0));
  for (const value of Object.values(owner)) {
    const embedded = asRecord(value);
    if (records(embedded.inputs).some((field) => optionalString(field, "id") === fieldId)) {
      return Math.max(0, optionalNumber(embedded, jsonKey, 0));
    }
  }
  return 0;
}

function enabledKeyframes(track?: JsonRecord) {
  return records(track?.keyframes)
    .filter((keyframe) => keyframe.enabled !== false)
    .sort((left, right) => optionalNumber(left, "frame", 0) - optionalNumber(right, "frame", 0));
}

function declaredBaseDuration(contract: JsonRecord) {
  return optionalObjectArray(contract, "actions", "runtime owner contract")
    .filter((action) => action.definesModuleDuration === true)
    .reduce((maximum, action) => {
      const duration = requiredNumberValue(
        action.durationBaseFrames,
        `runtime action '${optionalString(action, "id")}' durationBaseFrames`,
      );
      if (duration < 0) {
        throw new Error(`Runtime action '${optionalString(action, "id")}' durationBaseFrames must not be negative.`);
      }
      return Math.max(maximum, duration);
    }, 0);
}

function scale(value: number, natural: number, effective: number) {
  return natural <= 0 ? value : value * effective / natural;
}

function unscale(value: number, natural: number, effective: number) {
  return effective <= 0 ? value : value * natural / effective;
}

function round(value: number) {
  return Math.round(value);
}
