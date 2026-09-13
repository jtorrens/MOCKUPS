import { optionalString, requiredRecord, requiredString } from "./componentResolverCommon.js";
import { optionalObject, optionalObjectArray, requiredObjectArray, type JsonRecord } from "./previewJsonHelpers.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";

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

export function resolveNestedRuntimeAnimationValues(
  collection: JsonRecord,
  item: JsonRecord,
  animation: JsonRecord,
  targetId: string,
  localFrame: (fieldId: string) => number,
) {
  const resolved = structuredClone(item);
  for (const field of runtimeNestedAnimationFields(collection, resolved)) {
    if (!field.fieldId.includes(".")) continue;
    if (!field.jsonKey) {
      throw new Error(`Runtime nested animation field '${field.fieldId}' requires an exact jsonKey`);
    }
    field.values[field.jsonKey] = resolveParameterAnimation(
      animation,
      field.fieldId,
      targetId,
      localFrame(field.fieldId),
      field.values[field.jsonKey],
    ).value;
  }
  return resolved;
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
