export type JsonRecord = Record<string, unknown>;

export function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function asRecord(value: unknown): JsonRecord {
  return isRecord(value) ? value : {};
}

export function parseObject(json: string | undefined, label = "JSON object"): JsonRecord {
  if (typeof json !== "string" || json.trim().length === 0) {
    throw new Error(`Invalid ${label}: missing JSON object`);
  }

  let value: unknown;
  try {
    value = JSON.parse(json);
  } catch {
    throw new Error(`Invalid ${label}: malformed JSON`);
  }
  if (!isRecord(value)) {
    throw new Error(`Invalid ${label}: expected object root`);
  }
  return value;
}

export function optionalObjectArray(
  owner: JsonRecord,
  key: string,
  path: string,
): JsonRecord[] {
  if (!Object.hasOwn(owner, key)) return [];
  const value = owner[key];
  if (!Array.isArray(value)) {
    throw new Error(`${path} '${key}' must be an array when present`);
  }
  return value.map((entry, index) => {
    if (!isRecord(entry)) {
      throw new Error(`${path} '${key}'[${index}] must be an object`);
    }
    return entry;
  });
}
