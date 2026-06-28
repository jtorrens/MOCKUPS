import {
  resolveFieldValue,
  type FieldDefinition,
} from "../value-system/FieldDefinition.js";
import {
  ValueRegistry,
  type ValueKind,
} from "../value-system/ValueRegistry.js";

export type TokenPath = readonly string[];
export type TokenScope = Record<string, unknown>;

export interface InheritableTokenDescriptor {
  readonly field?: FieldDefinition;
  readonly fieldId?: string;
  readonly outputPath: TokenPath;
  readonly inputPaths?: readonly TokenPath[];
  readonly kind?: ValueKind;
  readonly fallback?: unknown;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function getTokenAtPath(
  source: unknown,
  path: TokenPath,
): unknown {
  let current = source;
  for (const segment of path) {
    if (!isObject(current) || !(segment in current)) return undefined;
    current = current[segment];
  }
  return current;
}

function setTokenAtPath(
  target: TokenScope,
  path: TokenPath,
  value: unknown,
) {
  let current = target;
  for (const segment of path.slice(0, -1)) {
    const existing = current[segment];
    if (isObject(existing)) {
      current = existing;
    } else {
      const next: TokenScope = {};
      current[segment] = next;
      current = next;
    }
  }
  const last = path.at(-1);
  if (last) current[last] = value;
}

export function resolveInheritedToken(
  descriptor: InheritableTokenDescriptor,
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
      value: getTokenAtPath(scope, path),
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

export function resolveInheritedTokenGroup(
  descriptors: readonly InheritableTokenDescriptor[],
  scopes: readonly unknown[],
): TokenScope {
  const resolved: TokenScope = {};
  for (const descriptor of descriptors) {
    const value = resolveInheritedToken(descriptor, scopes);
    if (value !== undefined) {
      setTokenAtPath(resolved, descriptor.outputPath, value);
    }
  }
  return resolved;
}
