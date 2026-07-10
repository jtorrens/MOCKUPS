import {
  resolveFieldValue,
  type FieldDefinition,
} from "./FieldDefinition.js";
import {
  ValueRegistry,
  type ValueKind,
} from "./ValueRegistry.js";

export type JsonPath = readonly string[];
export type JsonObject = Record<string, unknown>;

export interface JsonFieldBinding {
  readonly field?: FieldDefinition;
  readonly fieldId?: string;
  readonly outputPath: JsonPath;
  readonly inputPaths?: readonly JsonPath[];
  readonly kind?: ValueKind;
  readonly fallback?: unknown;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function getJsonValueAtPath(
  source: unknown,
  path: JsonPath,
): unknown {
  let current = source;
  for (const segment of path) {
    if (!isObject(current) || !(segment in current)) return undefined;
    current = current[segment];
  }
  return current;
}

function setJsonValueAtPath(
  target: JsonObject,
  path: JsonPath,
  value: unknown,
) {
  let current = target;
  for (const segment of path.slice(0, -1)) {
    const existing = current[segment];
    if (isObject(existing)) {
      current = existing;
    } else {
      const next: JsonObject = {};
      current[segment] = next;
      current = next;
    }
  }
  const last = path.at(-1);
  if (last) current[last] = value;
}

export function resolveJsonFieldBinding(
  descriptor: JsonFieldBinding,
  scopes: readonly unknown[],
): unknown {
  // Scopes are ordered from the most local value to the oldest parent.
  // A field is resolved when a concrete value exists at its logical path.
  // If the local scope does not define it, resolution keeps walking parents
  // until it reaches a real value or the descriptor fallback.
  const inputPaths = descriptor.inputPaths?.length
    ? descriptor.inputPaths
    : [descriptor.outputPath];
  const candidates = scopes.flatMap((scope, scopeIndex) =>
    inputPaths.map((path) => ({
      source: `${scopeIndex}:${path.join(".")}`,
      value: getJsonValueAtPath(scope, path),
    })),
  );

  const field =
    descriptor.field ??
    (descriptor.kind
      ? {
          id: descriptor.fieldId ?? descriptor.outputPath.join("."),
          kind: descriptor.kind,
          defaultValue: descriptor.fallback,
        }
      : undefined);

  if (field) {
    return resolveFieldValue(
      field,
      candidates,
    )?.value;
  }

  for (const candidate of candidates) {
    const value = candidate.value;
    if (value !== undefined && !ValueRegistry.isInherited(value)) {
      return value;
    }
  }

  return descriptor.fallback;
}

export function resolveJsonFieldBindingGroup(
  descriptors: readonly JsonFieldBinding[],
  scopes: readonly unknown[],
): JsonObject {
  const resolved: JsonObject = {};
  for (const descriptor of descriptors) {
    const value = resolveJsonFieldBinding(descriptor, scopes);
    if (value !== undefined) {
      setJsonValueAtPath(resolved, descriptor.outputPath, value);
    }
  }
  return resolved;
}
