import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { isRecord, optionalObject, optionalObjectArray, parseObject } from "./previewJsonHelpers.js";
import { requiredRecord, requiredString } from "./previewValueHelpers.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";
import { rootScreenFrame } from "./previewFrameContext.js";
import { resolveRuntimeAnimationValues } from "./runtimeNestedAnimationFields.js";

type JsonRecord = Record<string, unknown>;
const storageKey = "$forwardedInputs";
const runtimeFieldIdsKey = "__runtimeFieldIds";
const runtimeCollectionSourcesKey = "__runtimeCollectionSources";

// In-process proof owned by this boundary. Serialized requests cannot supply it.
const preparedRuntimePayload: unique symbol = Symbol("prepared Runtime payload");
export type PreparedRuntimePreviewPayload = DesignPreviewPayload & {
  readonly [preparedRuntimePayload]: true;
};

function prepared(payload: DesignPreviewPayload): PreparedRuntimePreviewPayload {
  return { ...payload, [preparedRuntimePayload]: true };
}

export function requirePreparedRuntimePayload(payload: DesignPreviewPayload): asserts payload is PreparedRuntimePreviewPayload {
  if (!(preparedRuntimePayload in payload) || payload[preparedRuntimePayload] !== true) {
    throw new Error("Embedded Runtime values require a prepared parent payload.");
  }
}

export function prepareEmbeddedRuntimePayload(
  payload: PreparedRuntimePreviewPayload, type: string,
  config: Record<string, unknown>, inputs: Record<string, unknown>,
): PreparedRuntimePreviewPayload {
  requirePreparedRuntimePayload(payload);
  return prepared({ ...payload, componentType: type,
    configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(inputs) });
}

export function prepareRuntimePreviewPayload(payload: DesignPreviewPayload): PreparedRuntimePreviewPayload {
  if ("runtimeValuesPrepared" in payload) throw new Error("A serialized Runtime preparation flag is not a current payload contract.");
  const forwarded = applyRuntimeInputForwarding(payload);
  if (preparedRuntimePayload in payload || payload.kind !== "componentClass") return prepared(forwarded);
  const document = parseObject(payload.designPreviewJson, "Component Runtime values");
  const animation = optionalObject(parseObject(payload.instanceJson), "animation", "Preview instance");
  const contract = parseObject(payload.runtimeContractJson, "Runtime temporal envelope");
  const timeline = new RuntimeOwnerTimeline(contract, contract, animation,
    parseObject(payload.themeTokensJson), 0, payload.frameRate);
  const resolved = resolveRuntimeAnimationValues({ fields: optionalObjectArray(document, "inputs", "Component Runtime") }, document, animation, "",
    (fieldId) => timeline.temporalLocalFrame(fieldId, "", rootScreenFrame(payload)));
  return prepared({ ...forwarded, designPreviewJson: JSON.stringify(resolved.values) });
}

export function applyRuntimeInputForwarding(
  payload: DesignPreviewPayload,
): DesignPreviewPayload {
  const config = parseObject(payload.configJson, "component config");
  const runtime = parseObject(payload.designPreviewJson, "component runtime payload");
  apply(config, runtimeSources(runtime), "", forwardedFrameValue(payload));
  return {
    ...payload,
    configJson: JSON.stringify(config),
  };
}

export function forwardedRuntimeInputPatch(
  config: JsonRecord,
  fieldId: string,
  value: unknown,
  runtime: JsonRecord,
): JsonRecord {
  const matches: Array<{ jsonKey: string }> = [];
  collectForwardedInput(config, fieldId, matches);
  if (matches.length !== 1) {
    throw new Error(
      `Forwarded runtime input '${fieldId}' must have exactly one target; found ${matches.length}`,
    );
  }
  const jsonKey = matches[0]!.jsonKey;
  const inputs = runtime.inputs === undefined ? [] : runtime.inputs;
  if (!Array.isArray(inputs)) throw new Error("Runtime inputs must be an array");
  const existing = inputs.filter((input) => isRecord(input) && input.id === fieldId);
  if (existing.length > 1 || existing.some((input) => input.jsonKey !== jsonKey)) {
    throw new Error(`Ambiguous forwarded Runtime field '${fieldId}'`);
  }
  return {
    ...runtime,
    inputs: existing.length ? inputs : [...inputs, { id: fieldId, jsonKey, source: "runtime" }],
    [jsonKey]: value,
  };
}

function collectForwardedInput(
  node: unknown,
  fieldId: string,
  matches: Array<{ jsonKey: string }>,
) {
  if (Array.isArray(node)) {
    node.forEach((child) => collectForwardedInput(child, fieldId, matches));
    return;
  }
  if (!isRecord(node)) return;

  const forwarding = node[storageKey];
  if (forwarding !== undefined && !isRecord(forwarding)) {
    throw new Error(`${storageKey} must be an object when present`);
  }
  if (isRecord(forwarding)) {
    for (const [targetKey, rawDefinition] of Object.entries(forwarding)) {
      if (!isRecord(rawDefinition)
          || typeof rawDefinition.id !== "string"
          || typeof rawDefinition.jsonKey !== "string") {
        throw new Error(`Invalid forwarded runtime input definition ${targetKey}`);
      }
      if (rawDefinition.id === fieldId) {
        matches.push({ jsonKey: rawDefinition.jsonKey });
      }
    }
  }

  for (const [key, child] of Object.entries(node)) {
    if (key !== storageKey) collectForwardedInput(child, fieldId, matches);
  }
}

function apply(
  node: unknown,
  sources: RuntimeSource[],
  inheritedOwnerId: string,
  frameValue: (source: RuntimeSource) => unknown,
) {
  if (Array.isArray(node)) {
    node.forEach((child) => apply(child, sources, inheritedOwnerId, frameValue));
    return;
  }
  if (!isRecord(node)) return;

  const ownerId = typeof node.id === "string" && node.id ? node.id : inheritedOwnerId;

  const forwarding = node[storageKey];
  if (forwarding !== undefined && !isRecord(forwarding)) {
    throw new Error(`${storageKey} must be an object when present`);
  }
  if (isRecord(forwarding)) {
    const entries = Object.entries(forwarding).map(([targetKey, rawDefinition]) => {
      if (!isRecord(rawDefinition) || typeof rawDefinition.jsonKey !== "string") {
        throw new Error(`Invalid forwarded runtime input definition ${targetKey}`);
      }
      const matches = sources.filter((source) =>
        source.fieldId === rawDefinition.id
        && source.jsonKey === rawDefinition.jsonKey
        && (source.ownerId === "" || source.ownerId === ownerId));
      if (matches.length > 1) {
        throw new Error(`Ambiguous forwarded Runtime field '${rawDefinition.id}' for owner '${ownerId}'`);
      }
      const source = matches[0];
      const runtimeOwner = source?.values;
      if (runtimeOwner && !Object.hasOwn(runtimeOwner, rawDefinition.jsonKey)) {
        throw new Error(`Missing forwarded runtime value ${rawDefinition.jsonKey}`);
      }
      return { targetKey, rawDefinition, runtimeOwner, source };
    });
    const ownedEntries = entries.filter(({ rawDefinition, runtimeOwner }) =>
      !!runtimeOwner && Object.hasOwn(runtimeOwner, rawDefinition.jsonKey as string));
    if (ownedEntries.length > 0 && ownedEntries.length !== entries.length) {
      const missing = entries.find(({ rawDefinition, runtimeOwner }) =>
        !runtimeOwner || !Object.hasOwn(runtimeOwner, rawDefinition.jsonKey as string))!;
      throw new Error(`Missing forwarded runtime value ${missing.rawDefinition.jsonKey}`);
    }
    for (const { targetKey, rawDefinition, runtimeOwner, source } of ownedEntries) {
      if (!runtimeOwner || !source) {
        throw new Error(`Missing forwarded runtime value ${rawDefinition.jsonKey}`);
      }
      if (typeof rawDefinition.id !== "string" || !rawDefinition.id) {
        throw new Error(`Forwarded runtime input '${targetKey}' has no stable field id`);
      }
      const currentRuntimeFieldIds = node[runtimeFieldIdsKey];
      if (currentRuntimeFieldIds !== undefined && !isRecord(currentRuntimeFieldIds)) {
        throw new Error(`${runtimeFieldIdsKey} must be an object when present`);
      }
      const runtimeFieldIds = isRecord(currentRuntimeFieldIds)
        ? currentRuntimeFieldIds as JsonRecord
        : {};
      runtimeFieldIds[targetKey] = rawDefinition.id;
      node[runtimeFieldIdsKey] = runtimeFieldIds;
      node[targetKey] = structuredClone(frameValue(source));
      if (isRecord(rawDefinition.collection)) {
        const sourceKey = `${rawDefinition.jsonKey}__variantSource`;
        const source = runtimeOwner[sourceKey];
        if (!Array.isArray(source)) {
          throw new Error(`Forwarded runtime collection '${rawDefinition.jsonKey}' has no structural source.`);
        }
        const currentSources = node[runtimeCollectionSourcesKey];
        if (currentSources !== undefined && !isRecord(currentSources)) {
          throw new Error(`${runtimeCollectionSourcesKey} must be an object when present`);
        }
        const sources = isRecord(currentSources) ? currentSources : {};
        sources[targetKey] = source;
        node[runtimeCollectionSourcesKey] = sources;
      }
      if (
        typeof rawDefinition.resolvedJsonKey === "string" &&
        rawDefinition.resolvedJsonKey &&
        typeof rawDefinition.targetResolvedJsonKey === "string" &&
        rawDefinition.targetResolvedJsonKey &&
        Object.hasOwn(runtimeOwner, rawDefinition.resolvedJsonKey)
      ) {
        node[rawDefinition.targetResolvedJsonKey] = runtimeOwner[rawDefinition.resolvedJsonKey];
      }
    }
    if (ownedEntries.length > 0) delete node[storageKey];
  }

  for (const [key, child] of Object.entries(node)) {
    if (key !== storageKey) apply(child, sources, ownerId, frameValue);
  }
}

interface RuntimeSource {
  ownerId: string;
  fieldId: string;
  jsonKey: string;
  values: JsonRecord;
  definition: JsonRecord;
}

/** Resolve forwarded scalars before dispatch without replacing the authored
 * Runtime envelope used for presence, transitions and action clocks. */
function forwardedFrameValue(payload: DesignPreviewPayload) {
  const animation = optionalObject(parseObject(payload.instanceJson), "animation", "Preview instance envelope");
  const tracks = optionalObjectArray(animation, "tracks", "Runtime animation");
  let timeline: RuntimeOwnerTimeline | undefined;
  return (source: RuntimeSource): unknown => {
    const value = source.values[source.jsonKey];
    if (!tracks.some((track) => track.fieldId === source.fieldId && track.targetId === source.ownerId)) return value;
    if (!timeline) {
      const contract = parseObject(payload.runtimeContractJson, "Runtime temporal envelope");
      timeline = new RuntimeOwnerTimeline(contract, contract, animation,
        parseObject(payload.themeTokensJson), 0, payload.frameRate);
    }
    if (source.ownerId && !timeline.ownsTarget(source.ownerId)) {
      throw new Error(`Forwarded Runtime owner '${source.ownerId}' has no declared temporal owner`);
    }
    const endFrame = payload.screenTiming?.actionDurationFrames ?? timeline.durationFrames;
    const localFrame = timeline.temporalLocalFrame(source.fieldId, source.ownerId, rootScreenFrame(payload),
      source.ownerId && timeline.itemHasExplicitPresenceEnd(source.ownerId)
        ? timeline.itemPresenceEndFrame(source.ownerId, endFrame) : undefined);
    const valueKind = source.definition.valueKind === "IntegerPair" ? "integerPair"
      : source.definition.valueKind === "DecimalPair" ? "decimalPair" : undefined;
    return resolveParameterAnimation(animation, source.fieldId, source.ownerId, localFrame, value, valueKind).value;
  };
}

/** Traverse only declared Runtime ownership, never arbitrary objects or names. */
function runtimeSources(runtime: JsonRecord): RuntimeSource[] {
  const result: RuntimeSource[] = [];
  function definitions(owner: JsonRecord, key: string): JsonRecord[] {
    if (!Object.hasOwn(owner, key)) return [];
    const value = owner[key];
    if (!Array.isArray(value) || value.some((item) => !isRecord(item))) {
      throw new Error(`Runtime ${key} must contain object declarations`);
    }
    return value as JsonRecord[];
  }
  function fields(declarations: JsonRecord[], values: JsonRecord, ownerId: string) {
    const ids = new Set<string>();
    for (const definition of declarations) {
      const fieldId = requiredString(definition, "id", "Runtime field declaration");
      const jsonKey = requiredString(definition, "jsonKey", `Runtime field '${fieldId}'`);
      if (ids.has(fieldId)) throw new Error(`Duplicate Runtime field '${fieldId}'`);
      ids.add(fieldId);
      result.push({ fieldId, jsonKey, values, ownerId, definition });
      if (isRecord(definition.structuredCollection)) {
        collection(definition.structuredCollection, values[jsonKey]);
      }
    }
  }
  function collection(definition: JsonRecord, value: unknown) {
    if (!Array.isArray(value)) throw new Error(`Runtime collection '${definition.id}' must be an array`);
    const ids = new Set<string>();
    for (const item of value) {
      if (!isRecord(item)) throw new Error(`Runtime collection '${definition.id}' must contain objects`);
      const id = requiredString(item, "id", "Runtime collection item");
      if (ids.has(id)) throw new Error(`Duplicate Runtime collection item '${id}'`);
      ids.add(id);
      fields(definitions(definition, "fields"), item, id);
      const componentItems = definition.componentItems;
      const key = definition.itemRuntimeContractJsonKey
        || (isRecord(componentItems) ? componentItems.inputsJsonKey : undefined);
      if (typeof key === "string" && key) {
        document(requiredRecord(item, key, `Runtime item '${id}'`), id);
      }
    }
  }
  function document(value: JsonRecord, ownerId: string) {
    fields(definitions(value, "inputs"), value, ownerId);
    for (const definition of definitions(value, "collections")) {
      const jsonKey = requiredString(definition, "jsonKey", "Runtime collection");
      result.push({ ownerId, fieldId: requiredString(definition, "id", "Runtime collection"), jsonKey, values: value, definition });
      collection(definition, value[jsonKey]);
    }
  }
  document(runtime, "");
  return result;
}

/** A declared link, never a match inferred from a property name. */
export interface RuntimeValueBinding {
  fieldId: string;
  runtimeJsonKey: string;
  configPath: readonly string[];
  boundaries?: readonly {
    fieldId: string;
    slotPath: readonly string[];
    componentType: string;
  }[];
}

/**
 * Frame values have already been evaluated by the common owner timeline.
 * This is the final value-source boundary shared by root and embedded owners.
 * A present value (including null, false, zero and empty text) is authoritative.
 */
export function prepareRuntimeValues(
  config: JsonRecord,
  runtime: JsonRecord,
  bindings: readonly RuntimeValueBinding[],
  catalog: JsonRecord = {},
): JsonRecord {
  const values = structuredClone(runtime);
  const ids = new Set<string>();
  const keys = new Set<string>();
  for (const binding of bindings) {
    if (!binding.fieldId || !binding.runtimeJsonKey || !binding.configPath.length
        || ids.has(binding.fieldId) || keys.has(binding.runtimeJsonKey)) {
      throw new Error(`Invalid or duplicate Runtime value binding '${binding.fieldId}'`);
    }
    ids.add(binding.fieldId);
    keys.add(binding.runtimeJsonKey);
    const declarations = runtime.inputs;
    const declared = Array.isArray(declarations)
      ? declarations.filter((input) => isRecord(input) && input.id === binding.fieldId)
      : [];
    if (declared.length > 1 || declared.some((input) => input.jsonKey !== binding.runtimeJsonKey)) {
      throw new Error(`Ambiguous Runtime value binding '${binding.fieldId}'`);
    }
    if (Object.hasOwn(values, binding.runtimeJsonKey)) {
      if (values[binding.runtimeJsonKey] === undefined) {
        throw new Error(`Runtime field '${binding.fieldId}' cannot contain undefined`);
      }
      continue;
    }
    if (declared.some((input) => input.source !== "variant")) {
      throw new Error(`Declared Runtime field '${binding.fieldId}' has no prepared value`);
    }
    let ownerConfig = config;
    for (const boundary of binding.boundaries ?? []) {
      if (!boundary.fieldId || !boundary.slotPath.length || !boundary.componentType) {
        throw new Error(`Invalid Runtime boundary for '${binding.fieldId}'`);
      }
      const slot = configValue(ownerConfig, boundary.slotPath, boundary.fieldId);
      if (!isRecord(slot)) throw new Error(`Runtime boundary '${boundary.fieldId}' must be an object`);
      ownerConfig = embeddedComponentConfig(catalog, slot, boundary.componentType, boundary.fieldId);
    }
    values[binding.runtimeJsonKey] = structuredClone(configValue(ownerConfig, binding.configPath, binding.fieldId));
  }
  return values;
}

function configValue(config: JsonRecord, path: readonly string[], fieldId: string): unknown {
  let value: unknown = config;
  for (const segment of path) {
    if (!isRecord(value) || !Object.hasOwn(value, segment)) {
      throw new Error(`Runtime field '${fieldId}' has no configured value at '${path.join(".")}'`);
    }
    value = value[segment];
  }
  if (value === undefined) throw new Error(`Configured field '${fieldId}' cannot contain undefined`);
  return value;
}

export interface RuntimeValueLink {
  sourceFieldId: string;
  sourceJsonKey: string;
  targetFieldId: string;
  targetJsonKey: string;
}

export function prepareComponentInputValues(
  catalog: JsonRecord,
  variantReference: string,
  config: JsonRecord,
  bindings: readonly RuntimeValueBinding[],
  values: JsonRecord,
): JsonRecord {
  const definitions = requiredRecord(catalog, "inputDefaults", "Component Runtime defaults");
  const defaults = requiredRecord(definitions, variantReference, `Runtime defaults '${variantReference}'`);
  return {
    ...structuredClone(defaults),
    ...prepareRuntimeValues(config, {}, bindings, catalog),
    ...structuredClone(values),
  };
}

/** Projects only explicit field links; absence retains the child's own source. */
export function projectRuntimeValues(
  source: JsonRecord,
  links: readonly RuntimeValueLink[],
): JsonRecord {
  const result: JsonRecord = {};
  const targets = new Set<string>();
  for (const link of links) {
    if (!link.sourceFieldId || !link.sourceJsonKey || !link.targetFieldId || !link.targetJsonKey
        || targets.has(link.targetJsonKey)) {
      throw new Error(`Invalid or duplicate Runtime value link '${link.targetFieldId}'`);
    }
    targets.add(link.targetJsonKey);
    if (Object.hasOwn(source, link.sourceJsonKey)) {
      if (source[link.sourceJsonKey] === undefined) throw new Error(`Runtime field '${link.sourceFieldId}' cannot contain undefined`);
      result[link.targetJsonKey] = structuredClone(source[link.sourceJsonKey]);
    }
  }
  return result;
}

export function componentVariantConfig(
  componentBaseConfigs: JsonRecord,
  componentType: string,
  variantReference: unknown,
): JsonRecord {
  const reference = typeof variantReference === "string" ? variantReference.trim() : "";
  if (!reference) {
    throw new Error(`Missing component variant reference for ${componentType}`);
  }

  if (!/^[A-Za-z0-9_.-]+::variant::[A-Za-z0-9_.-]+$/.test(reference)) {
    throw new Error(`Unsupported component variant reference ${reference}`);
  }

  const variants = requiredRecord(
    componentBaseConfigs,
    "variants",
    "componentBaseConfigs.variants",
  );
  if (!Object.hasOwn(variants, reference)) {
    throw new Error(`Missing component variant config ${reference}`);
  }
  return requiredRecord(variants, reference, `component variant config ${reference}`);
}

export function requireComponentVariantType(
  componentBaseConfigs: JsonRecord,
  slot: JsonRecord,
  expectedType: string,
  path: string,
) {
  const reference = requiredString(slot, "variantReference", `${path}.variantReference`);
  const variantTypes = requiredRecord(
    componentBaseConfigs,
    "variantTypes",
    "componentBaseConfigs.variantTypes",
  );
  if (variantTypes[reference] !== expectedType) {
    throw new Error(
      `${path} Variant '${reference}' must resolve to Component '${expectedType}'`,
    );
  }
}

export function mergeComponentDefaults(
  defaults: JsonRecord,
  overrides: JsonRecord,
): JsonRecord {
  const merged: JsonRecord = structuredClone(defaults);
  for (const [key, value] of Object.entries(overrides)) {
    const defaultValue = merged[key];
    merged[key] =
      isRecord(defaultValue) && isRecord(value) && !isExactComponentVariantSlot(value)
        ? mergeComponentDefaults(defaultValue, value)
        : structuredClone(value);
  }
  return merged;
}

function isExactComponentVariantSlot(value: JsonRecord) {
  const keys = Object.keys(value);
  return keys.length === 2
    && keys.includes("variantReference")
    && keys.includes("overrides")
    && typeof value.variantReference === "string"
    && /^[A-Za-z0-9_.-]+::variant::[A-Za-z0-9_.-]+$/.test(value.variantReference)
    && isRecord(value.overrides);
}

export function embeddedComponentConfig(
  componentBaseConfigs: JsonRecord,
  slot: JsonRecord,
  componentType: string,
  path: string,
) {
  const variantReference = requiredString(slot, "variantReference", `${path}.variantReference`);
  const overrides = requiredRecord(slot, "overrides", `${path}.overrides`);
  return prepareComponentConfiguration(componentBaseConfigs, componentType, variantReference, overrides);
}

export function prepareComponentConfiguration(
  componentBaseConfigs: JsonRecord,
  componentType: string,
  variantReference: string,
  overrides: JsonRecord,
): JsonRecord {
  return mergeComponentDefaults(
    componentVariantConfig(componentBaseConfigs, componentType, variantReference),
    overrides,
  );
}
