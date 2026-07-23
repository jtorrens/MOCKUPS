import { optionalNumber, optionalString, requiredString } from "./componentResolverCommon.js";
import { isRecord, optionalObject, optionalObjectArray } from "./previewJsonHelpers.js";
import { resolveBehaviorTimingFrames } from "./behaviorTiming.js";
import { requiredNumberValue } from "./previewValueHelpers.js";
import { validateTransientAnimationDocument } from "./transientAnimationDocument.js";

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
    validateTransientAnimationDocument(animation);
    let naturalEnd = Math.max(1, declaredBaseDuration(contract));
    const collections = optionalObjectArray(contract, "collections", "runtime owner contract");
    const collectionKeys = new Set<string>();
    for (const collection of collections) {
      validateCollectionTimeline(collection);
      const directFields = optionalObjectArray(collection, "fields", "runtime owner collection");
      for (const field of directFields) {
        validateFieldTimeline(field);
      }
      validateUniqueFieldIds(directFields, "runtime owner collection fields");
      const key = collectionKey(collection);
      if (collectionKeys.has(key)) {
        throw new Error(`runtime owner contract contains duplicate collection key '${key}'`);
      }
      collectionKeys.add(key);
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
        if (this.items.has(targetId)) {
          throw new Error(`runtime owner collections contain duplicate target id '${targetId}'`);
        }
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
    topInputs.forEach(validateFieldTimeline);
    validateUniqueFieldIds(topInputs, "runtime owner inputs");
    for (const definition of topInputs) {
      const fieldId = requiredString(definition, "id", "runtime owner input");
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
    const fieldTimeline = optionalFieldTimeline(definition, "runtime animation field timeline");
    const completion = optionalObject(fieldTimeline, "completion", "runtime animation field timeline");
    const minimum = optionalInteger(completion, "minimumEnabledKeyframes", 2);
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
      if (optionalFieldTimeline(definition, "runtime animation field timeline").extendsOwnerDuration !== false) {
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
    const fieldTimeline = optionalFieldTimeline(
      definition,
      `runtime animation field '${optionalString(definition, "id")}' timeline`,
    );
    const originDefinition = optionalObject(
      fieldTimeline,
      "origin",
      `runtime animation field '${optionalString(definition, "id")}' timeline`,
    );
    let origin = 0;
    if (optionalString(originDefinition, "kind") === "fieldCompletion") {
      const sourceId = optionalString(originDefinition, "fieldId");
      const source = ownerFields.find((field) => optionalString(field, "id") === sourceId);
      if (!source) throw new Error(`Animation field '${fieldId}' references missing field '${sourceId}'.`);
      origin = this.resolveFieldTiming(source, owner, targetId, ownerFields, resolving).completion
        + requiredNonNegativeInteger(
          originDefinition.offsetFrames,
          `runtime animation field '${fieldId}' origin offsetFrames`,
        );
    }
    resolving.delete(fieldId);
    const keyframes = enabledKeyframes(this.track(fieldId, targetId));
    const completion = optionalObject(
      fieldTimeline,
      "completion",
      `runtime animation field '${fieldId}' timeline`,
    );
    const baseFieldId = optionalString(completion, "baseDurationFieldId");
    const minimum = optionalInteger(completion, "minimumEnabledKeyframes", 2);
    if (baseFieldId && keyframes.length < minimum) {
      const baseDefinition = ownerFields.find((field) => optionalString(field, "id") === baseFieldId);
      if (!baseDefinition) {
        throw new Error(`Animation field '${fieldId}' references missing duration field '${baseFieldId}'.`);
      }
      const completionFrame = origin + (optionalString(baseDefinition, "valueKind") === "BehaviorTiming"
        ? resolveBehaviorTimingFrames(owner, baseDefinition, ownerFields, this.themeTokens)
        : fieldValue(owner, ownerFields, baseFieldId));
      return {
        origin,
        completion: completionFrame,
        endExclusive: Math.max(completionFrame, keyframes.length > 0 ? origin + 1 : 0),
      };
    }
    if (keyframes.length === 0) return { origin, completion: origin, endExclusive: 0 };
    const last = requiredNumberValue(
      keyframes[keyframes.length - 1]!.frame,
      "runtime animation keyframe frame",
    );
    return { origin, completion: origin + last, endExclusive: origin + last + 1 };
  }

  private lastFiniteActionEnd(
    collection: JsonRecord,
    item: JsonRecord,
    targetId: string,
    fields: JsonRecord[],
  ) {
    let lastEnd = 0;
    for (const action of itemActions(collection, item)) {
      if (!Object.hasOwn(action, "extendsModuleDuration") || action.extendsModuleDuration !== true) continue;
      const actionId = requiredString(action, "id", "finite runtime action id");
      const playFieldId = Object.hasOwn(action, "playFieldId")
        ? requiredString(action, "playFieldId", `finite runtime action '${actionId}' play field`)
        : requiredString(action, "playInputId", `finite runtime action '${actionId}' play input`);
      const definition = fields.find((field) => optionalString(field, "id") === playFieldId);
      if (!definition) {
        throw new Error(`Finite runtime action '${actionId}' references missing play field '${playFieldId}'`);
      }
      const origin = this.resolveFieldTiming(definition, item, targetId, fields, new Set()).origin;
      const keyframes = enabledKeyframes(this.track(playFieldId, targetId));
      const enabledJsonKey = requiredString(
        action,
        "durationEnabledInputId",
        `finite runtime action '${actionId}' enable input`,
      );
      const baseEnabled = requiredBooleanValue(
        item[enabledJsonKey],
        `finite runtime action '${actionId}' owner '${enabledJsonKey}'`,
      );
      const hasActiveKeyframe = keyframes.some((keyframe) => requiredBooleanValue(
        keyframe.value,
        `finite runtime action '${actionId}' play keyframe value`,
      ));
      if (!baseEnabled && !hasActiveKeyframe) continue;

      const durationInputId = requiredString(
        action,
        "durationInputId",
        `finite runtime action '${actionId}' duration input`,
      );
      const duration = requiredNumberValue(
        item[durationInputId],
        `finite runtime action '${actionId}' duration input '${durationInputId}'`,
      );
      if (duration <= 0) {
        throw new Error(`Finite runtime action '${actionId}' duration input '${durationInputId}' must be positive.`);
      }
      if (baseEnabled) lastEnd = Math.max(lastEnd, origin + duration);
      for (let index = 0; index < keyframes.length; index += 1) {
        const keyframe = keyframes[index]!;
        if (!requiredBooleanValue(
          keyframe.value,
          `finite runtime action '${actionId}' play keyframe value`,
        )) continue;
        const start = origin + requiredNumberValue(
          keyframe.frame,
          "runtime animation keyframe frame",
        );
        const replacement = keyframes[index + 1]
          ? origin + requiredNumberValue(
              keyframes[index + 1]!.frame,
              "runtime animation keyframe frame",
            )
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
    return optionalObjectArray(this.animation, "tracks", "runtime owner animation").find((track) =>
      optionalString(track, "fieldId") === fieldId
      && optionalString(track, "targetId") === targetId);
  }

  private itemOwnerOrigin(collection: JsonRecord, item: JsonRecord) {
    const origin = optionalObject(
      optionalObject(collection, "animationTimeline", "runtime owner collection animation timeline"),
      "ownerOrigin",
      "runtime owner collection animation timeline",
    );
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
      .map((keyframe) => requiredNumberValue(
        keyframe.frame,
        "runtime animation keyframe frame",
      ));
    return matchingFrames.length ? Math.min(...matchingFrames) : 0;
  }

  private targetDuration(targetId: string, natural: number) {
    const retime = optionalObject(this.animation, "retime", "runtime owner animation");
    const targets = optionalObject(retime, "targets", "runtime animation retime");
    const target = optionalObject(targets, targetId, "runtime animation retime targets");
    const duration = optionalNumber(target, "targetDurationFrames", 0);
    return duration > 0 ? duration : natural;
  }

  private rootTargetDuration(natural: number) {
    const duration = optionalNumber(
      optionalObject(this.animation, "retime", "runtime owner animation"),
      "targetDurationFrames",
      0,
    );
    return duration > 0 ? duration : natural;
  }
}

function optionalFieldTimeline(field: JsonRecord, path: string): JsonRecord {
  if (!Object.hasOwn(field, "animationTimeline") || field.animationTimeline === null) return {};
  return optionalObject(field, "animationTimeline", path);
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

function optionalInteger(owner: JsonRecord, key: string, fallback: number) {
  if (!Object.hasOwn(owner, key)) return fallback;
  return requiredNonNegativeInteger(owner[key], `${key}`);
}

function requiredNonNegativeInteger(value: unknown, path: string) {
  const number = requiredNumberValue(value, path);
  if (!Number.isInteger(number) || number < 0) {
    throw new Error(`${path} must be a non-negative integer`);
  }
  return number;
}

function requiredNonNegativeNumber(value: unknown, path: string) {
  const number = requiredNumberValue(value, path);
  if (number < 0) throw new Error(`${path} must not be negative`);
  return number;
}

function requiredBooleanValue(value: unknown, path: string) {
  if (typeof value !== "boolean") throw new Error(`${path} must be a JSON boolean`);
  return value;
}

function validateCollectionTimeline(collection: JsonRecord) {
  const timeline = optionalObject(
    collection,
    "animationTimeline",
    "runtime owner collection animation timeline",
  );
  if (Object.hasOwn(timeline, "sequence")) {
    const sequence = requiredString(timeline, "sequence", "runtime collection sequence");
    if (sequence !== "serial") throw new Error(`Unknown runtime collection sequence '${sequence}'`);
  }
  if (Object.hasOwn(timeline, "sequenceItems") && typeof timeline.sequenceItems !== "boolean") {
    throw new Error("runtime collection sequenceItems must be a boolean when present");
  }
  optionalStringArray(timeline, "preDurationFieldIds", "runtime collection animation timeline");
  optionalStringArray(timeline, "postDurationFieldIds", "runtime collection animation timeline");

  const ownerOrigin = optionalObject(
    timeline,
    "ownerOrigin",
    "runtime collection animation timeline",
  );
  if (Object.keys(ownerOrigin).length === 0 && !Object.hasOwn(timeline, "ownerOrigin")) return;
  const kind = requiredString(ownerOrigin, "kind", "runtime collection owner origin kind");
  if (kind !== "firstMatchingValue") {
    throw new Error(`Unknown runtime collection owner origin '${kind}'`);
  }
  for (const key of [
    "sourceCollectionJsonKey",
    "sourceTargetIdJsonKey",
    "sourceFieldId",
    "sourceValueJsonKey",
    "matchValueJsonKey",
  ]) {
    requiredString(ownerOrigin, key, `runtime collection owner origin ${key}`);
  }
}

function validateFieldTimeline(field: JsonRecord) {
  const fieldId = requiredString(field, "id", "runtime animation field id");
  const timeline = optionalFieldTimeline(field, `runtime animation field '${fieldId}' timeline`);
  if (Object.hasOwn(timeline, "extendsOwnerDuration")
    && typeof timeline.extendsOwnerDuration !== "boolean") {
    throw new Error(`runtime animation field '${fieldId}' extendsOwnerDuration must be a boolean`);
  }

  const origin = optionalObject(
    timeline,
    "origin",
    `runtime animation field '${fieldId}' timeline`,
  );
  if (Object.hasOwn(timeline, "origin")) {
    const kind = requiredString(origin, "kind", `runtime animation field '${fieldId}' origin kind`);
    if (kind === "fieldCompletion") {
      requiredString(origin, "fieldId", `runtime animation field '${fieldId}' origin field`);
      requiredNonNegativeInteger(
        origin.offsetFrames,
        `runtime animation field '${fieldId}' origin offsetFrames`,
      );
    } else if (kind !== "ownerStart") {
      throw new Error(`Unknown runtime animation field '${fieldId}' origin '${kind}'`);
    }
  }

  const completion = optionalObject(
    timeline,
    "completion",
    `runtime animation field '${fieldId}' timeline`,
  );
  if (!Object.hasOwn(timeline, "completion")) return;
  requiredString(
    completion,
    "baseDurationFieldId",
    `runtime animation field '${fieldId}' completion duration field`,
  );
  if (Object.hasOwn(completion, "trackOverride")) {
    const trackOverride = requiredString(
      completion,
      "trackOverride",
      `runtime animation field '${fieldId}' completion track override`,
    );
    if (trackOverride !== "lastEnabledKeyframe") {
      throw new Error(`Unknown runtime animation field '${fieldId}' track override '${trackOverride}'`);
    }
  }
  if (Object.hasOwn(completion, "minimumEnabledKeyframes")) {
    const minimum = requiredNonNegativeInteger(
      completion.minimumEnabledKeyframes,
      `runtime animation field '${fieldId}' completion minimumEnabledKeyframes`,
    );
    if (minimum < 2) {
      throw new Error(`runtime animation field '${fieldId}' minimumEnabledKeyframes must be at least 2`);
    }
  }
}

function collectionKey(collection: JsonRecord) {
  for (const key of ["storageCollectionJsonKey", "sourceCollectionJsonKey", "jsonKey"]) {
    if (!Object.hasOwn(collection, key)) continue;
    return requiredString(collection, key, "runtime owner collection");
  }
  throw new Error("runtime owner collection requires an explicit storage key");
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
  const fields = [...direct, ...embedded, ...runtime];
  fields.forEach(validateFieldTimeline);
  validateUniqueFieldIds(fields, "runtime owner item fields");
  return fields;
}

function validateUniqueFieldIds(fields: JsonRecord[], context: string) {
  const ids = new Set<string>();
  for (const field of fields) {
    const fieldId = requiredString(field, "id", context);
    if (ids.has(fieldId)) {
      throw new Error(`${context} contain duplicate id '${fieldId}'`);
    }
    ids.add(fieldId);
  }
}

function itemActions(collection: JsonRecord, item: JsonRecord) {
  const runtimeContractKey = optionalString(collection, "itemRuntimeContractJsonKey");
  const runtimeContract = runtimeContractKey
    ? requiredObject(item, runtimeContractKey, `projected Runtime collection item '${optionalString(item, "id")}'`)
    : {};
  const runtime = runtimeContractKey
    ? optionalObjectArray(runtimeContract, "actions", "projected Runtime contract")
    : [];
  return validateTemporalActions([
    ...optionalObjectArray(collection, "itemActions", "runtime owner collection"),
    ...runtime,
  ], "runtime owner item actions");
}

function validateTemporalActions(actions: JsonRecord[], context: string) {
  actions.forEach((action, index) => {
    for (const flag of ["definesModuleDuration", "extendsModuleDuration"]) {
      if (!Object.hasOwn(action, flag)) continue;
      requiredBooleanValue(action[flag], `${context}[${index}] '${flag}'`);
    }
    if (action.definesModuleDuration === true) {
      const actionId = requiredString(action, "id", `${context}[${index}] id`);
      requiredNonNegativeNumber(
        action.durationBaseFrames,
        `runtime action '${actionId}' durationBaseFrames`,
      );
    }
    if (action.extendsModuleDuration !== true) return;
    const actionId = requiredString(action, "id", `${context}[${index}] id`);
    requiredString(action, "playInputId", `finite runtime action '${actionId}' play input`);
    if (Object.hasOwn(action, "playFieldId")) {
      requiredString(action, "playFieldId", `finite runtime action '${actionId}' play field`);
    }
    requiredString(action, "durationInputId", `finite runtime action '${actionId}' duration input`);
    requiredString(
      action,
      "durationEnabledInputId",
      `finite runtime action '${actionId}' enable input`,
    );
  });
  return actions;
}

function fieldValue(owner: JsonRecord, fields: JsonRecord[], fieldId: string) {
  const definition = fields.find((field) => optionalString(field, "id") === fieldId);
  if (!definition) throw new Error(`Runtime animation duration references missing field '${fieldId}'`);
  const jsonKey = requiredString(definition, "jsonKey", `runtime animation duration field '${fieldId}' key`);
  if (Object.hasOwn(owner, jsonKey)) {
    return requiredNonNegativeNumber(
      owner[jsonKey],
      `runtime animation duration field '${fieldId}' value`,
    );
  }
  for (const value of Object.values(owner)) {
    if (!isRecord(value) || !Object.hasOwn(value, "inputs")) continue;
    const embeddedDefinitions = optionalObjectArray(value, "inputs", "embedded Runtime contract");
    if (embeddedDefinitions.some((field) => optionalString(field, "id") === fieldId)) {
      return requiredNonNegativeNumber(
        value[jsonKey],
        `embedded runtime animation duration field '${fieldId}' value`,
      );
    }
  }
  throw new Error(`Missing runtime animation duration field '${fieldId}' value`);
}

function enabledKeyframes(track?: JsonRecord) {
  if (!track) return [];
  return optionalObjectArray(track, "keyframes", "runtime animation track")
    .filter((keyframe) => keyframe.enabled !== false);
}

function declaredBaseDuration(contract: JsonRecord) {
  return validateTemporalActions(
    optionalObjectArray(contract, "actions", "runtime owner contract"),
    "runtime owner actions",
  )
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
