import { asRecord } from "./previewJsonHelpers.js";

type JsonRecord = Record<string, unknown>;

export function componentVariantConfig(
  componentBaseConfigs: JsonRecord,
  componentType: string,
  variantReference: unknown,
): JsonRecord {
  const reference = typeof variantReference === "string" ? variantReference.trim() : "";
  if (!reference) {
    throw new Error(`Missing component variant reference for ${componentType}`);
  }

  if (!/^[A-Za-z0-9_.-]+::variant::[A-Za-z0-9_.-]+$/.test(reference)) {
    throw new Error(`Unsupported component variant reference ${reference}`);
  }

  const variants = asRecord(componentBaseConfigs.variants);
  const config = variants[reference];
  if (config === undefined) {
    throw new Error(`Missing component variant config ${reference}`);
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
