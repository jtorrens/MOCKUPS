export type SurfaceStyleFieldKind =
  | "boolean"
  | "number"
  | "themeColorToken"
  | "themeRadiusToken";

export interface SurfaceStyleFieldDefinition {
  readonly path: readonly string[];
  readonly label: string;
  readonly kind: SurfaceStyleFieldKind;
  readonly defaultValue: boolean | number | string;
  readonly min?: number;
  readonly step?: number | "any";
  readonly options?: readonly string[];
  readonly group?: "base" | "relief";
}

export type SurfaceStyleValue = Record<string, unknown>;

export const SURFACE_STYLE_FIELDS = [
  {
    path: ["shadowEnabled"],
    label: "Shadow",
    kind: "boolean",
    defaultValue: false,
    group: "base",
  },
  {
    path: ["surfaceReliefEnabled"],
    label: "Relief",
    kind: "boolean",
    defaultValue: false,
    group: "base",
  },
  {
    path: ["borderWidth"],
    label: "Border width",
    kind: "number",
    defaultValue: 0,
    min: 0,
    step: "any",
    group: "base",
  },
  {
    path: ["borderColorToken"],
    label: "Border color",
    kind: "themeColorToken",
    defaultValue: "borders.primary",
    options: ["borders.primary", "borders.secondary", "borders.alternate"],
    group: "base",
  },
  {
    path: ["cornerRadiusToken"],
    label: "Corner radius",
    kind: "themeRadiusToken",
    defaultValue: "radii.surface",
    options: [
      "radii.control",
      "radii.card",
      "radii.panel",
      "radii.surface",
      "radii.pill",
      "radii.avatar",
      "radii.full",
    ],
    group: "base",
  },
  {
    path: ["surfaceRelief", "angleDeg"],
    label: "Angle",
    kind: "number",
    defaultValue: -45,
    step: "any",
    group: "relief",
  },
  {
    path: ["surfaceRelief", "extension"],
    label: "Extension",
    kind: "number",
    defaultValue: 1,
    min: 0,
    step: "any",
    group: "relief",
  },
  {
    path: ["surfaceRelief", "spread"],
    label: "Spread",
    kind: "number",
    defaultValue: 0,
    min: 0,
    step: "any",
    group: "relief",
  },
  {
    path: ["surfaceRelief", "upperIntensity"],
    label: "Upper",
    kind: "number",
    defaultValue: 0.12,
    step: "any",
    group: "relief",
  },
  {
    path: ["surfaceRelief", "lowerIntensity"],
    label: "Lower",
    kind: "number",
    defaultValue: -0.08,
    step: "any",
    group: "relief",
  },
] satisfies readonly SurfaceStyleFieldDefinition[];

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function surfaceStyleDefaultValue(): SurfaceStyleValue {
  return surfaceStyleNormalize({});
}

export function surfaceStyleGet(
  value: SurfaceStyleValue,
  path: readonly string[],
): unknown {
  let current: unknown = value;
  for (const part of path) {
    if (!isObject(current)) return undefined;
    current = current[part];
  }
  return current;
}

export function surfaceStyleSet(
  value: SurfaceStyleValue,
  path: readonly string[],
  nextValue: unknown,
): SurfaceStyleValue {
  if (!path.length) return value;
  const [head, ...tail] = path;
  if (!tail.length) {
    return { ...value, [head]: nextValue };
  }
  const child = isObject(value[head]) ? value[head] : {};
  return {
    ...value,
    [head]: surfaceStyleSet(child, tail, nextValue),
  };
}

function normalizeFieldValue(
  field: SurfaceStyleFieldDefinition,
  value: unknown,
) {
  if (field.kind === "boolean") return value === true;
  if (field.kind === "number") {
    return typeof value === "number" && Number.isFinite(value)
      ? value
      : field.defaultValue;
  }
  if (field.kind === "themeColorToken") {
    return typeof value === "string" && value.trim()
      ? value
      : field.defaultValue;
  }
  if (field.kind === "themeRadiusToken") {
    return typeof value === "string" && value.trim()
      ? value
      : field.defaultValue;
  }
  return value;
}

export function surfaceStyleNormalize(value: unknown): SurfaceStyleValue {
  const root = isObject(value) ? value : {};
  return SURFACE_STYLE_FIELDS.reduce<SurfaceStyleValue>((nextRoot, field) => {
    const currentValue = surfaceStyleGet(root, field.path);
    return surfaceStyleSet(
      nextRoot,
      field.path,
      normalizeFieldValue(field, currentValue),
    );
  }, {});
}

export function surfaceStyleIsValid(value: unknown): boolean {
  return isObject(value);
}
