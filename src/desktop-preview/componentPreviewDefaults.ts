import { isRecord } from "./previewJsonHelpers.js";
import { requiredRecord, requiredString } from "./previewValueHelpers.js";

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
      isRecord(defaultValue) && isRecord(value)
        ? mergeComponentDefaults(defaultValue, value)
        : value;
  }
  return merged;
}

export function embeddedComponentConfig(
  componentBaseConfigs: JsonRecord,
  slot: JsonRecord,
  componentType: string,
  path: string,
) {
  const variantReference = requiredString(slot, "variantReference", `${path}.variantReference`);
  const overrides = requiredRecord(slot, "overrides", `${path}.overrides`);
  return mergeComponentDefaults(
    componentVariantConfig(componentBaseConfigs, componentType, variantReference),
    overrides,
  );
}
