import { optionalObject, type JsonRecord } from "./previewJsonHelpers.js";
import { requiredNumberValue } from "./previewValueHelpers.js";

export type RuntimeTransitionDocument = {
  sourceFrame: number;
  previousValue: unknown;
};

export function optionalRuntimeTransition(
  owner: JsonRecord,
  key: string,
  path: string,
): RuntimeTransitionDocument | undefined {
  if (!Object.hasOwn(owner, key)) return undefined;
  const transition = optionalObject(owner, key, path);
  const sourceFrame = requiredNumberValue(transition.sourceFrame, `${path}.${key}.sourceFrame`);
  if (!Number.isInteger(sourceFrame) || sourceFrame <= 0) {
    throw new Error(`${path}.${key}.sourceFrame must be a positive integer`);
  }
  if (!Object.hasOwn(transition, "previousValue")) {
    throw new Error(`${path}.${key}.previousValue is required`);
  }
  return { sourceFrame, previousValue: transition.previousValue };
}
