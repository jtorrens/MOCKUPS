import {
  surfaceStyleIsValid,
  surfaceStyleNormalize,
} from "./SurfaceStyleDefinition.js";

export const INHERITED_FIELD_VALUE = "inherited";

export type ValueKind =
  | "integer"
  | "decimal"
  | "text"
  | "boolean"
  | "enum"
  | "hexColor"
  | "fontFamily"
  | "fontWeight"
  | "fontStyle"
  | "paletteColorToken"
  | "themeColorToken"
  | "alpha"
  | "iconToken"
  | "recordReference"
  | "filePath"
  | "relativeFilePath"
  | "surfaceStyle"
  | "componentOverride"
  | "jsonObject"
  | "jsonArray";

export interface ValueValidationIssue {
  readonly kind: ValueKind;
  readonly message: string;
  readonly value: unknown;
}

export type ValueValidationResult =
  | { readonly ok: true; readonly value: unknown }
  | { readonly ok: false; readonly issue: ValueValidationIssue };

export interface ValueKindDefinition {
  readonly kind: ValueKind;
  readonly label: string;
  readonly acceptsInherited?: boolean;
  readonly normalize?: (value: unknown) => unknown;
  readonly validate: (value: unknown) => boolean;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0;
}

function normalizeFontWeight(value: unknown) {
  if (isFiniteNumber(value)) return value;
  if (!isNonEmptyString(value)) return value;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : value;
}

function normalizeHexColor(value: unknown) {
  if (!isNonEmptyString(value)) return value;
  const trimmed = value.trim();
  return /^#[0-9A-Fa-f]{6}$/.test(trimmed) ? trimmed.toUpperCase() : value;
}

function isHexColor(value: unknown) {
  return isNonEmptyString(value) && /^#[0-9A-Fa-f]{6}$/.test(value.trim());
}

function isFontWeight(value: unknown) {
  const normalized = normalizeFontWeight(value);
  return (
    isFiniteNumber(normalized) &&
    normalized >= 1 &&
    normalized <= 1000
  );
}

const SYSTEM_VALUE_KIND_DEFINITIONS = [
  {
    kind: "integer",
    label: "Integer",
    normalize: (value) =>
      isFiniteNumber(value) ? Math.round(value) : value,
    validate: (value) => Number.isInteger(value),
  },
  {
    kind: "decimal",
    label: "Decimal",
    validate: isFiniteNumber,
  },
  {
    kind: "text",
    label: "Text",
    validate: (value) => typeof value === "string",
  },
  {
    kind: "boolean",
    label: "Boolean",
    validate: (value) => typeof value === "boolean",
  },
  {
    kind: "enum",
    label: "Enum",
    validate: isNonEmptyString,
  },
  {
    kind: "hexColor",
    label: "HEX color",
    normalize: normalizeHexColor,
    validate: isHexColor,
  },
  {
    kind: "fontFamily",
    label: "Font family",
    validate: isNonEmptyString,
  },
  {
    kind: "fontWeight",
    label: "Font weight",
    normalize: normalizeFontWeight,
    validate: isFontWeight,
  },
  {
    kind: "fontStyle",
    label: "Font style",
    validate: (value) => value === "normal" || value === "italic",
  },
  {
    kind: "paletteColorToken",
    label: "Palette color token",
    validate: isNonEmptyString,
  },
  {
    kind: "themeColorToken",
    label: "Theme color token",
    validate: isNonEmptyString,
  },
  {
    kind: "alpha",
    label: "Alpha",
    normalize: (value) =>
      isFiniteNumber(value) ? Math.max(0, Math.min(1, value)) : value,
    validate: (value) => isFiniteNumber(value) && value >= 0 && value <= 1,
  },
  {
    kind: "iconToken",
    label: "Icon token",
    validate: isNonEmptyString,
  },
  {
    kind: "recordReference",
    label: "Record reference",
    validate: isNonEmptyString,
  },
  {
    kind: "filePath",
    label: "File path",
    validate: isNonEmptyString,
  },
  {
    kind: "relativeFilePath",
    label: "Relative file path",
    validate: (value) =>
      isNonEmptyString(value) &&
      !value.startsWith("/") &&
      !/^[a-zA-Z]:[\\/]/.test(value),
  },
  {
    kind: "surfaceStyle",
    label: "Surface style",
    normalize: surfaceStyleNormalize,
    validate: surfaceStyleIsValid,
  },
  {
    kind: "componentOverride",
    label: "Component override",
    validate: isObject,
  },
  {
    kind: "jsonObject",
    label: "JSON object",
    validate: isObject,
  },
  {
    kind: "jsonArray",
    label: "JSON array",
    validate: Array.isArray,
  },
] satisfies readonly ValueKindDefinition[];

export class ValueRegistry {
  private static readonly definitions = new Map<ValueKind, ValueKindDefinition>(
    SYSTEM_VALUE_KIND_DEFINITIONS.map((definition) => [
      definition.kind,
      definition,
    ]),
  );

  static hasKind(kind: ValueKind): boolean {
    return this.definitions.has(kind);
  }

  static allKinds(): readonly ValueKind[] {
    return [...this.definitions.keys()];
  }

  static definition(kind: ValueKind): ValueKindDefinition {
    const definition = this.definitions.get(kind);
    if (!definition) {
      throw new Error(`Unknown value kind "${kind}"`);
    }
    return definition;
  }

  static isInherited(value: unknown): boolean {
    return value === INHERITED_FIELD_VALUE;
  }

  static normalize(kind: ValueKind, value: unknown): unknown {
    const definition = this.definition(kind);
    if (this.isInherited(value)) return value;
    return definition.normalize ? definition.normalize(value) : value;
  }

  static validate(kind: ValueKind, value: unknown): ValueValidationResult {
    const definition = this.definition(kind);
    if (this.isInherited(value)) {
      return definition.acceptsInherited === false
        ? {
            ok: false,
            issue: {
              kind,
              value,
              message: `${definition.label} does not accept inherited values`,
            },
          }
        : { ok: true, value };
    }
    const normalized = this.normalize(kind, value);
    return definition.validate(normalized)
      ? { ok: true, value: normalized }
      : {
          ok: false,
          issue: {
            kind,
            value,
            message: `Invalid ${definition.label} value`,
          },
        };
  }

  static assert(kind: ValueKind, value: unknown): unknown {
    const result = this.validate(kind, value);
    if (result.ok) return result.value;
    throw new Error(
      `${result.issue.message}: ${JSON.stringify(result.issue.value)}`,
    );
  }
}
