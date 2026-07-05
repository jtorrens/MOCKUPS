type JsonRecord = Record<string, unknown>;

function asRecord(value: unknown): JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as JsonRecord)
    : {};
}

export function mergeComponentDefaults(
  defaults: JsonRecord,
  overrides: JsonRecord,
): JsonRecord {
  const merged: JsonRecord = { ...defaults };
  for (const [key, value] of Object.entries(overrides)) {
    const defaultValue = merged[key];
    merged[key] =
      typeof defaultValue === "object" &&
      defaultValue !== null &&
      !Array.isArray(defaultValue) &&
      typeof value === "object" &&
      value !== null &&
      !Array.isArray(value)
        ? mergeComponentDefaults(asRecord(defaultValue), asRecord(value))
        : value;
  }
  return merged;
}
