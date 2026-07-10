import { asRecord } from "./previewJsonHelpers.js";

type JsonRecord = Record<string, unknown>;

export function componentPresetConfig(
  componentBaseConfigs: JsonRecord,
  componentType: string,
  presetReference: unknown,
): JsonRecord {
  const reference = typeof presetReference === "string" ? presetReference.trim() : "";
  if (!reference) {
    throw new Error(`Missing component preset reference for ${componentType}`);
  }

  if (!reference.includes("::preset::")) {
    throw new Error(`Unsupported component preset reference ${reference}`);
  }

  const presets = asRecord(componentBaseConfigs.presets);
  const config = presets[reference];
  if (config === undefined) {
    throw new Error(`Missing component preset config ${reference}`);
  }

  return asRecord(config);
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
