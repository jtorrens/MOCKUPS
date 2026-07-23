import { asRecord } from "./previewJsonHelpers.js";
import { requiredRecord } from "./previewValueHelpers.js";

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

  const variants = requiredRecord(
    componentBaseConfigs,
    "variants",
    "componentBaseConfigs.variants",
  );
  if (!Object.hasOwn(variants, reference)) {
    throw new Error(`Missing component variant config ${reference}`);
  }
  return requiredRecord(variants, reference, `component variant config ${reference}`);
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
