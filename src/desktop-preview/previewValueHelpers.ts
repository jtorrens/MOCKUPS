import type {
  AlignmentPlacementContract,
  IconSlotsContract,
  SurfaceStyleContract,
  TypographyStyleContract,
} from "./previewComponentContracts.js";
import { asRecord } from "./previewJsonHelpers.js";

export function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

export function requiredNumberValue(value: unknown, path: string) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  throw new Error(`Missing numeric theme value ${path}`);
}

export function requiredRecord(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "object" && raw !== null && !Array.isArray(raw)) {
    return raw as Record<string, unknown>;
  }

  throw new Error(`Missing object value ${path}`);
}

export function requiredString(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string" && raw.trim()) return raw;
  throw new Error(`Missing string value ${path}`);
}

export function requiredFontFamilyId(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string" && raw.trim()) return raw.trim();
  throw new Error(`Missing string value ${path}`);
}

export function optionalString(value: Record<string, unknown>, key: string) {
  const raw = value[key];
  return typeof raw === "string" ? raw : "";
}

export function requiredPossiblyEmptyString(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string") return raw;
  throw new Error(`Missing string value ${path}`);
}

export function requiredBoolean(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "boolean") return raw;
  throw new Error(`Missing boolean value ${path}`);
}

export function optionalBoolean(value: Record<string, unknown>, key: string) {
  const raw = value[key];
  return typeof raw === "boolean" ? raw : false;
}

export function requiredNumber(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "number" && Number.isFinite(raw)) return raw;
  if (typeof raw === "string") {
    const parsed = Number(raw.replace(",", "."));
    if (Number.isFinite(parsed)) return parsed;
  }
  throw new Error(`Missing numeric value ${path}`);
}

export function requiredNumberPair(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = requiredString(value, key, path);
  const [firstRaw, secondRaw] = raw.split("|", 2);
  const first = Number((firstRaw ?? "").replace(",", "."));
  const second = Number((secondRaw ?? "").replace(",", "."));
  if (Number.isFinite(first) && Number.isFinite(second)) {
    return { first, second };
  }

  throw new Error(`Missing numeric pair value ${path}`);
}

export function requiredStringPair(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = requiredString(value, key, path);
  const [firstRaw, secondRaw] = raw.split("|", 2);
  const first = firstRaw?.trim() ?? "";
  const second = secondRaw?.trim() ?? "";
  if (first.length > 0 && second.length > 0) {
    return { first, second };
  }

  throw new Error(`Missing string pair value ${path}`);
}

export function optionalNumber(
  value: Record<string, unknown>,
  key: string,
  defaultValue: number,
) {
  const raw = value[key];
  if (typeof raw === "number" && Number.isFinite(raw)) return raw;
  if (typeof raw === "string") {
    const parsed = Number(raw.replace(",", "."));
    if (Number.isFinite(parsed)) return parsed;
  }
  return defaultValue;
}

export function requiredAlpha(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  return Math.max(0, Math.min(1, requiredNumber(value, key, path)));
}

export function requiredPlacement(
  value: Record<string, unknown>,
  key: string,
  path: string,
): AlignmentPlacementContract {
  const raw = requiredRecord(value, key, path);
  const mode = requiredString(raw, "mode", `${path}.mode`);
  if (mode !== "center" && mode !== "edge") {
    throw new Error(`Unsupported alignment placement mode ${mode}`);
  }

  return {
    mode,
    alignX: clamp01(requiredNumber(raw, "alignX", `${path}.alignX`)),
    alignY: clamp01(requiredNumber(raw, "alignY", `${path}.alignY`)),
    offsetX: requiredNumber(raw, "offsetX", `${path}.offsetX`),
    offsetY: requiredNumber(raw, "offsetY", `${path}.offsetY`),
  };
}

export function resolveSurfaceStyle(
  style: Record<string, unknown>,
  path = "component.style",
): SurfaceStyleContract {
  return {
    shadowEnabled: requiredBoolean(style, "shadowEnabled", `${path}.shadowEnabled`),
    reliefEnabled: requiredBoolean(style, "reliefEnabled", `${path}.reliefEnabled`),
    borderWidth: requiredNumber(style, "borderWidth", `${path}.borderWidth`),
    borderColorToken: requiredString(
      style,
      "borderColorToken",
      `${path}.borderColorToken`,
    ),
    cornerRadiusToken: requiredString(
      style,
      "cornerRadiusToken",
      `${path}.cornerRadiusToken`,
    ),
    reliefAngle: requiredNumber(style, "reliefAngle", `${path}.reliefAngle`),
    reliefExtent: requiredNumber(style, "reliefExtent", `${path}.reliefExtent`),
    reliefSpread: requiredNumber(style, "reliefSpread", `${path}.reliefSpread`),
    reliefTopIntensity: requiredNumber(
      style,
      "reliefTopIntensity",
      `${path}.reliefTopIntensity`,
    ),
    reliefBottomIntensity: requiredNumber(
      style,
      "reliefBottomIntensity",
      `${path}.reliefBottomIntensity`,
    ),
  };
}

export function requiredIconSlots(
  value: Record<string, unknown>,
  key: string,
  path: string,
): IconSlotsContract {
  const raw = requiredRecord(value, key, path);
  return {
    left: requiredStringArray(raw, "left", `${path}.left`),
    center: requiredStringArray(raw, "center", `${path}.center`),
    right: requiredStringArray(raw, "right", `${path}.right`),
  };
}

function requiredStringArray(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (Array.isArray(raw) && raw.every((entry) => typeof entry === "string")) {
    return raw;
  }

  throw new Error(`Missing string array value ${path}`);
}

export function requiredTypographyStyle(
  value: Record<string, unknown>,
  key: string,
  path: string,
): TypographyStyleContract {
  const typography = asRecord(value[key]);
  const lineHeight = typography.lineHeight;
  if (
    typeof lineHeight !== "string" &&
    !(typeof lineHeight === "number" && Number.isFinite(lineHeight))
  ) {
    throw new Error(`Missing line height value ${path}.lineHeight`);
  }

  return {
    fontFamilyId: requiredFontFamilyId(
      typography,
      "fontFamilyId",
      `${path}.fontFamilyId`,
    ),
    weight: requiredString(typography, "weight", `${path}.weight`),
    style: requiredString(typography, "style", `${path}.style`),
    sizeToken: requiredString(typography, "sizeToken", `${path}.sizeToken`),
    lineHeight,
  };
}

function clamp01(value: number) {
  return Math.max(0, Math.min(1, value));
}
