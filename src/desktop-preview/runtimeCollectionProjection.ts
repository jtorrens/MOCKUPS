import { optionalString, requiredString } from "./componentResolverCommon.js";
import { isRecord, requiredObjectArray } from "./previewJsonHelpers.js";

export function resolvedRuntimeCollectionItems(
  preview: Record<string, unknown>,
  jsonKey: string,
  owner: string,
): Record<string, unknown>[] {
  const runtimeItems = requiredObjectArray(preview, jsonKey, `${owner} runtime values`);
  const collectionDefinitions = preview.collections;
  const collection = Array.isArray(collectionDefinitions)
    ? collectionDefinitions
      .filter(isRecord)
      .find((candidate) => requiredString(candidate, "jsonKey", "runtime collection definition") === jsonKey)
    : undefined;
  const sourceKey = collection ? optionalString(collection, "sourceCollectionJsonKey") : "";
  const embeddedSources = preview.__runtimeCollectionSources;
  const structuralSource = sourceKey
    ? preview[sourceKey]
    : isRecord(embeddedSources) ? embeddedSources[jsonKey] : undefined;
  if (structuralSource === undefined) return runtimeItems;
  if (!Array.isArray(structuralSource)) {
    throw new Error(`${owner} structural source for '${jsonKey}' must be an array`);
  }
  const structuralItems = structuralSource.map((item, index) => {
    if (!isRecord(item)) throw new Error(`${owner} structural item at index ${index} must be an object`);
    return item;
  });
  const runtimeById = new Map(runtimeItems.map((item) => [
    requiredString(item, "id", `${owner} runtime item`),
    item,
  ]));
  const ids = new Set<string>();
  return structuralItems.map((structuralItem, index) => {
    const id = requiredString(structuralItem, "id", `${owner} structural item at index ${index}`);
    if (ids.has(id)) throw new Error(`${owner} structural item id '${id}' is duplicated`);
    ids.add(id);
    const runtimeItem = runtimeById.get(id);
    if (!runtimeItem) throw new Error(`${owner} runtime value for structural item '${id}' is missing`);
    return { ...structuralItem, ...runtimeItem };
  });
}
