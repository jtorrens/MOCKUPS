export interface AlignmentPlacementContract {
  mode: "center" | "edge";
  alignX: number;
  alignY: number;
  offsetX: number;
  offsetY: number;
}

export interface SurfaceStyleContract {
  shadowEnabled: boolean;
  reliefEnabled: boolean;
  borderWidth: number;
  borderColorToken: string;
  cornerRadiusToken: string;
  reliefAngle: number;
  reliefExtent: number;
  reliefSpread: number;
  reliefTopIntensity: number;
  reliefBottomIntensity: number;
}

export function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

export function parseObject(json: string | undefined) {
  return asRecord(JSON.parse(json || "{}"));
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

  throw new Error(`Missing object component value ${path}`);
}

export function requiredString(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string" && raw.trim()) return raw;
  throw new Error(`Missing string component value ${path}`);
}

export function requiredBoolean(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "boolean") return raw;
  throw new Error(`Missing boolean component value ${path}`);
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
  throw new Error(`Missing numeric component value ${path}`);
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

function clamp01(value: number) {
  return Math.max(0, Math.min(1, value));
}
