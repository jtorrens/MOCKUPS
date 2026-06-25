import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import {
  normalizeGroupValue,
  parsedJsonValue,
  parsedObject,
} from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

interface JsonGroupDraftsOptions {
  drafts: Record<string, string>;
  defaultGroupValue: (groupKey: string) => JsonValue;
  setDrafts: (nextDrafts: Record<string, string>) => void;
}

export function useJsonGroupDrafts({
  drafts,
  defaultGroupValue,
  setDrafts,
}: JsonGroupDraftsOptions) {
  function rawForJsonGroupValue(column: string, groupKey: string) {
    const root = parsedObject(drafts[column] ?? "{}");
    const value = root[groupKey];
    return stringifyJson(normalizeGroupValue(value, defaultGroupValue(groupKey)));
  }

  function updateJsonGroupValue(
    column: string,
    groupKey: string,
    nextRawText: string,
  ) {
    const root = parsedObject(drafts[column] ?? "{}");
    const fallback = defaultGroupValue(groupKey);
    setDrafts({
      ...drafts,
      [column]: stringifyJson({
        ...root,
        [groupKey]: parsedJsonValue(nextRawText, fallback),
      }),
    });
  }

  return {
    rawForJsonGroupValue,
    updateJsonGroupValue,
  };
}
