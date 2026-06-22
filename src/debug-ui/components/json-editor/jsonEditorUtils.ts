export type JsonValue =
  | string
  | number
  | boolean
  | null
  | JsonValue[]
  | { [key: string]: JsonValue };

export type JsonPath = Array<string | number>;

export function isJsonObject(
  value: JsonValue,
): value is { [key: string]: JsonValue } {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

export function parseJsonObject(
  raw: string,
): { ok: true; value: JsonValue } | { ok: false; error: string } {
  try {
    const value = JSON.parse(raw) as unknown;
    if (!isJsonCompatible(value)) {
      return { ok: false, error: "Value must be JSON-compatible." };
    }
    if (!isJsonObject(value)) {
      return { ok: false, error: "JSON field root must be an object." };
    }
    return { ok: true, value };
  } catch (error) {
    return {
      ok: false,
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

export function stringifyJson(value: JsonValue): string {
  return JSON.stringify(value ?? {}, null, 2);
}

export function cloneJson<T extends JsonValue>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export function pathLabel(path: JsonPath): string {
  if (path.length === 0) return "root";
  return path
    .map((part) => (typeof part === "number" ? `[${part}]` : part))
    .join(".");
}

export function normalizedHintPath(path: JsonPath): string {
  return path
    .map((part) => (typeof part === "number" ? "[]" : part))
    .join(".");
}

export function getAtPath(value: JsonValue, path: JsonPath): JsonValue {
  return path.reduce<JsonValue>((current, part) => {
    if (Array.isArray(current) && typeof part === "number") {
      return current[part] ?? null;
    }
    if (isJsonObject(current) && typeof part === "string") {
      return current[part] ?? null;
    }
    return null;
  }, value);
}

export function hasAtPath(value: JsonValue | undefined, path: JsonPath): boolean {
  if (value === undefined) return false;
  let current: JsonValue = value;
  for (const part of path) {
    if (Array.isArray(current) && typeof part === "number") {
      if (part < 0 || part >= current.length) return false;
      current = current[part];
      continue;
    }
    if (isJsonObject(current) && typeof part === "string") {
      if (!Object.hasOwn(current, part)) return false;
      current = current[part];
      continue;
    }
    return false;
  }
  return true;
}

export function setAtPath(
  value: JsonValue,
  path: JsonPath,
  nextValue: JsonValue,
): JsonValue {
  if (path.length === 0) return nextValue;
  const [head, ...tail] = path;
  if (Array.isArray(value)) {
    const copy = [...value];
    if (typeof head === "number") {
      copy[head] = setAtPath(
        copy[head] ?? containerForPath(tail),
        tail,
        nextValue,
      );
    }
    return copy;
  }
  if (isJsonObject(value)) {
    const copy = { ...value };
    if (typeof head === "string") {
      copy[head] = setAtPath(
        copy[head] ?? containerForPath(tail),
        tail,
        nextValue,
      );
    }
    return copy;
  }
  return setAtPath(containerForHead(head), path, nextValue);
}

function containerForPath(path: JsonPath): JsonValue {
  const [head] = path;
  return containerForHead(head);
}

function containerForHead(head: string | number | undefined): JsonValue {
  return typeof head === "number" ? [] : {};
}

export function deleteAtPath(value: JsonValue, path: JsonPath): JsonValue {
  if (path.length === 0) return value;
  const parentPath = path.slice(0, -1);
  const leaf = path[path.length - 1];
  const parent = getAtPath(value, parentPath);
  if (Array.isArray(parent) && typeof leaf === "number") {
    const nextParent = parent.filter((_, index) => index !== leaf);
    return setAtPath(value, parentPath, nextParent);
  }
  if (isJsonObject(parent) && typeof leaf === "string") {
    const nextParent = { ...parent };
    delete nextParent[leaf];
    return setAtPath(value, parentPath, nextParent);
  }
  return value;
}

export function deleteAtPathAndPrune(
  value: JsonValue,
  path: JsonPath,
): JsonValue {
  const next = deleteAtPath(value, path);
  return pruneEmptyContainers(next);
}

function pruneEmptyContainers(value: JsonValue): JsonValue {
  if (Array.isArray(value)) {
    return value.map(pruneEmptyContainers);
  }
  if (isJsonObject(value)) {
    const entries = Object.entries(value)
      .map(([key, entryValue]) => [key, pruneEmptyContainers(entryValue)] as const)
      .filter(([, entryValue]) => {
        if (Array.isArray(entryValue)) return true;
        return !(isJsonObject(entryValue) && Object.keys(entryValue).length === 0);
      });
    return Object.fromEntries(entries) as JsonValue;
  }
  return value;
}

export function deepEqualJson(left: JsonValue, right: JsonValue): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

export function renameKeyAtPath(
  value: JsonValue,
  objectPath: JsonPath,
  oldKey: string,
  newKey: string,
): { ok: true; value: JsonValue } | { ok: false; error: string } {
  const objectValue = getAtPath(value, objectPath);
  if (!isJsonObject(objectValue)) {
    return { ok: false, error: "Selected value is not an object." };
  }
  const trimmed = newKey.trim();
  if (!trimmed) {
    return { ok: false, error: "Key name cannot be empty." };
  }
  if (trimmed !== oldKey && Object.hasOwn(objectValue, trimmed)) {
    return { ok: false, error: `Key "${trimmed}" already exists.` };
  }
  const nextObject: Record<string, JsonValue> = {};
  for (const [key, entryValue] of Object.entries(objectValue)) {
    nextObject[key === oldKey ? trimmed : key] = entryValue;
  }
  return { ok: true, value: setAtPath(value, objectPath, nextObject) };
}

export function isJsonCompatible(value: unknown): value is JsonValue {
  if (
    value === null ||
    typeof value === "string" ||
    typeof value === "number" ||
    typeof value === "boolean"
  ) {
    return typeof value !== "number" || Number.isFinite(value);
  }
  if (Array.isArray(value)) {
    return value.every(isJsonCompatible);
  }
  if (typeof value === "object") {
    return Object.values(value as Record<string, unknown>).every(
      isJsonCompatible,
    );
  }
  return false;
}

export function defaultJsonValue(kind: string): JsonValue {
  switch (kind) {
    case "string":
      return "";
    case "number":
      return 0;
    case "boolean":
      return false;
    case "array":
      return [];
    case "null":
      return null;
    case "object":
    default:
      return {};
  }
}

export function coercePrimitiveValue(
  raw: string,
  currentValue: JsonValue,
): JsonValue {
  if (typeof currentValue === "number") {
    const next = Number(raw);
    return Number.isFinite(next) ? next : currentValue;
  }
  if (currentValue === null && raw !== "null") {
    return raw;
  }
  return raw;
}
