import { optionalString, requiredRecord, requiredString } from "./componentResolverCommon.js";
import { optionalObject, optionalObjectArray, requiredObjectArray, type JsonRecord } from "./previewJsonHelpers.js";
import { resolveParameterAnimation, type ResolvedParameterAnimation } from "./parameterAnimationResolver.js";

export type RuntimeNestedAnimationField = {
  fieldId: string;
  definition: JsonRecord;
  values: JsonRecord;
  jsonKey: string;
};

export function runtimeNestedAnimationFields(
  collection: JsonRecord,
  item: JsonRecord,
): RuntimeNestedAnimationField[] {
  const result: RuntimeNestedAnimationField[] = [];
  addFields(result, optionalObjectArray(collection, "fields", "Runtime collection"), item, "");
  addItemRuntimeFields(result, collection, item, "");
  return result;
}

export function resolveRuntimeAnimationValues(
  collection: JsonRecord,
  item: JsonRecord,
  animation: JsonRecord,
  targetId: string,
  localFrame: (fieldId: string) => number,
) {
  const resolved = structuredClone(item);
  const resolutions = new Map<string, ResolvedParameterAnimation>();
  const storageKeys = new WeakMap<JsonRecord, Set<string>>();
  const fields: Array<RuntimeNestedAnimationField & { resolution: ResolvedParameterAnimation }> = [];
  for (const field of runtimeNestedAnimationFields(collection, resolved)) {
    if (field.definition.source === "variant" || field.definition.source === "calculated"
        || field.definition.structuredCollection != null) continue;
    if (!field.jsonKey) {
      throw new Error(`Runtime nested animation field '${field.fieldId}' requires an exact jsonKey`);
    }
    if (resolutions.has(field.fieldId)) {
      throw new Error(`Duplicate Runtime animation field '${field.fieldId}'`);
    }
    const keys = storageKeys.get(field.values) ?? new Set<string>();
    if (keys.has(field.jsonKey)) {
      throw new Error(`Runtime animation fields share the same prepared value '${field.jsonKey}'`);
    }
    keys.add(field.jsonKey);
    storageKeys.set(field.values, keys);
    if (!Object.hasOwn(field.values, field.jsonKey)) {
      throw new Error(`Runtime animation field '${field.fieldId}' is missing its prepared value '${field.jsonKey}'`);
    }
    const valueKind = field.definition.valueKind === "IntegerPair" ? "integerPair"
      : field.definition.valueKind === "DecimalPair" ? "decimalPair" : undefined;
    const resolution = resolveParameterAnimation(
      animation,
      field.fieldId,
      targetId,
      localFrame(field.fieldId),
      field.values[field.jsonKey],
      valueKind,
    );
    field.values[field.jsonKey] = resolution.value;
    resolutions.set(field.fieldId, resolution);
    fields.push({ ...field, resolution });
  }
  return {
    values: resolved,
    fields,
    field(fieldId: string): ResolvedParameterAnimation {
      const resolution = resolutions.get(fieldId);
      if (!resolution) throw new Error(`Runtime animation field '${fieldId}' is not declared in this owner`);
      return resolution;
    },
  };
}

/** Evaluate declared values; timeline ownership and behavior remain with their owners. */
export function resolveRuntimeDocumentAnimationValues(
  document: JsonRecord,
  animation: JsonRecord,
  localFrame: (fieldId: string, targetId: string) => number,
) {
  const root = resolveRuntimeAnimationValues(
    { fields: document.inputs }, document, animation, "",
    (fieldId) => localFrame(fieldId, ""),
  );
  const owners = new Map([["", root]]);
  for (const collection of optionalObjectArray(document, "collections", "Runtime document")) {
    const key = requiredString(collection, "jsonKey", "Runtime collection");
    root.values[key] = requiredObjectArray(document, key, "Runtime document").map((item) => {
      const id = requiredString(item, "id", `Runtime collection '${key}' item`);
      if (owners.has(id)) throw new Error(`Duplicate Runtime animation owner '${id}'`);
      const resolution = resolveRuntimeAnimationValues(
        collection, item, animation, id, (fieldId) => localFrame(fieldId, id),
      );
      owners.set(id, resolution);
      return resolution.values;
    });
  }
  return {
    values: root.values,
    field(fieldId: string, targetId = "") {
      const owner = owners.get(targetId);
      if (!owner) throw new Error(`Runtime animation owner '${targetId}' is not declared`);
      return owner.field(fieldId);
    },
  };
}

function addFields(
  result: RuntimeNestedAnimationField[],
  fields: JsonRecord[],
  values: JsonRecord,
  prefix: string,
) {
  for (const field of fields) {
    const id = requiredString(field, "id", "Runtime animation field");
    const jsonKey = optionalString(field, "jsonKey");
    const fieldId = join(prefix, id);
    result.push({ fieldId, definition: { ...field, id: fieldId }, values, jsonKey });
    if (field.structuredCollection == null) continue;
    const collectionJsonKey = requiredString(
      field,
      "jsonKey",
      `Runtime structured animation field '${fieldId}'`,
    );
    const nestedCollection = optionalObject(
      field,
      "structuredCollection",
      `Runtime animation field '${fieldId}'`,
    );
    if (!nestedCollection) continue;
    const items = requiredObjectArray(values, collectionJsonKey, `Runtime structured collection '${fieldId}'`);
    for (const nestedItem of items) {
      const itemId = requiredString(nestedItem, "id", `Runtime structured collection '${fieldId}' item`);
      const nestedPrefix = join(fieldId, itemId);
      addFields(
        result,
        optionalObjectArray(nestedCollection, "fields", `Runtime structured collection '${fieldId}'`),
        nestedItem,
        nestedPrefix,
      );
      addItemRuntimeFields(result, nestedCollection, nestedItem, nestedPrefix);
    }
  }
}

function addItemRuntimeFields(
  result: RuntimeNestedAnimationField[],
  collection: JsonRecord,
  item: JsonRecord,
  prefix: string,
) {
  const componentItems = optionalObject(collection, "componentItems", "Runtime collection");
  const runtimeKey = optionalString(collection, "itemRuntimeContractJsonKey")
    || optionalString(componentItems, "inputsJsonKey");
  if (!runtimeKey) return;
  const runtime = requiredRecord(item, runtimeKey, `Runtime collection item '${optionalString(item, "id")}'`);
  addFields(
    result,
    optionalObjectArray(runtime, "inputs", "Embedded Runtime contract"),
    runtime,
    prefix,
  );
}

function join(...segments: string[]) {
  return segments.filter(Boolean).join(".");
}
