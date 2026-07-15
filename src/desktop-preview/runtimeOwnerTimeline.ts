import { asRecord, optionalNumber, optionalString } from "./componentResolverCommon.js";
import { resolveBehaviorTimingFrames } from "./behaviorTiming.js";

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
    for (const collection of records(contract.collections)) {
      const values = records(runtime[collectionKey(collection)]);
      let cursor = 0;
      for (const item of values) {
        const fields = itemFields(collection, item);
        const targetId = optionalString(item, "id");
        if (!targetId) continue;
        const timeline = asRecord(collection.animationTimeline);
        const pre = strings(timeline.preDurationFieldIds)
          .reduce((sum, fieldId) => sum + fieldValue(item, fields, fieldId), 0);
        const start = cursor + pre;
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
        cursor = start + effectiveSequence;
        naturalEnd = Math.max(naturalEnd, start + effectiveSpan);
      }
      naturalEnd = Math.max(naturalEnd, cursor);
    }
    for (const definition of records(contract.inputs)) {
      const fieldId = optionalString(definition, "id");
      if (!fieldId) continue;
      const timing = this.resolveFieldTiming(
        definition,
        runtime,
        "",
        records(contract.inputs),
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
    const fields = records(this.contract.inputs);
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
    const post = strings(asRecord(collection.animationTimeline).postDurationFieldIds)
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
    for (const action of records(collection.itemActions)
      .filter((candidate) => candidate.extendsModuleDuration === true)) {
      const playFieldId = optionalString(action, "playInputId");
      const definition = fields.find((field) => optionalString(field, "id") === playFieldId);
      if (!definition) continue;
      const origin = this.resolveFieldTiming(definition, item, targetId, fields, new Set()).origin;
      const duration = Math.max(1, optionalNumber(item, optionalString(action, "durationInputId"), 1));
      if (item[optionalString(action, "durationEnabledInputId")] === true) {
        lastEnd = Math.max(lastEnd, origin + duration);
      }
      const keyframes = enabledKeyframes(this.track(playFieldId, targetId));
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
    if (!targetId) return records(this.contract.inputs).find((field) => optionalString(field, "id") === fieldId) ?? {};
    const item = this.items.get(targetId);
    return item ? itemFields(item.collection, item.item).find((field) => optionalString(field, "id") === fieldId) ?? {} : {};
  }

  private track(fieldId: string, targetId: string) {
    return records(this.animation.tracks).find((track) =>
      optionalString(track, "fieldId") === fieldId
      && optionalString(track, "targetId") === targetId);
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

function strings(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === "string") : [];
}

function collectionKey(collection: JsonRecord) {
  return optionalString(collection, "sourceCollectionJsonKey") || optionalString(collection, "jsonKey");
}

function itemFields(collection: JsonRecord, item: JsonRecord) {
  const direct = records(collection.fields);
  const inputsKey = optionalString(asRecord(collection.componentItems), "inputsJsonKey");
  const embedded = inputsKey ? records(asRecord(item[inputsKey]).inputs) : [];
  return [...direct, ...embedded];
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
  return records(contract.actions)
    .filter((action) => action.definesModuleDuration === true)
    .reduce((maximum, action) => Math.max(maximum, optionalNumber(action, "durationBaseFrames", 0)), 0);
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
