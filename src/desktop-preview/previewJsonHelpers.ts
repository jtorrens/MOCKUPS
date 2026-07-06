export type JsonRecord = Record<string, unknown>;

export function asRecord(value: unknown): JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as JsonRecord)
    : {};
}

export function parseObject(json: string | undefined) {
  return asRecord(JSON.parse(json || "{}"));
}

