import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export interface LabelDesignContract {
  id: "component.label";
  text: string;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: { x: number; y: number };
  backgroundColorToken: string;
  surfaceAlpha: number;
  textColorToken: string;
  textSizeToken: string;
  textStyle: "normal" | "italic";
  surface: {
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
  };
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function parseObject(json: string | undefined) {
  return asRecord(JSON.parse(json || "{}"));
}

function requiredString(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string" && raw.trim()) return raw;
  throw new Error(`Missing string component value ${path}`);
}

function requiredBoolean(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "boolean") return raw;
  throw new Error(`Missing boolean component value ${path}`);
}

function requiredNumber(
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

function requiredAlpha(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  return Math.max(0, Math.min(1, requiredNumber(value, key, path)));
}

function requiredPair(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = requiredString(value, key, path);
  const parts = raw.split("|");
  const first = Number(parts[0]?.replace(",", "."));
  const second = Number(parts[1]?.replace(",", "."));
  if (Number.isFinite(first) && Number.isFinite(second)) {
    return { first, second };
  }
  throw new Error(`Missing numeric pair component value ${path}`);
}

export function resolveLabelComponent(
  payload: DesignPreviewPayload,
): LabelDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const label = asRecord(config.label);
  const style = asRecord(config.style);
  const size = requiredPair(label, "size", "component.label.size");
  const padding = requiredPair(label, "padding", "component.label.padding");
  const dimensionMode = requiredString(
    label,
    "dimensionMode",
    "component.label.dimensionMode",
  );
  if (dimensionMode !== "content" && dimensionMode !== "fixed") {
    throw new Error(`Unsupported label dimension mode ${dimensionMode}`);
  }

  const textStyle = requiredString(label, "textStyle", "component.label.textStyle");
  if (textStyle !== "normal" && textStyle !== "italic") {
    throw new Error(`Unsupported label text style ${textStyle}`);
  }

  return {
    id: "component.label",
    text: requiredString(preview, "sampleText", "component.label.preview.sampleText"),
    dimensionMode,
    size: { width: size.first, height: size.second },
    padding: { x: padding.first, y: padding.second },
    backgroundColorToken: requiredString(
      label,
      "backgroundColorToken",
      "component.label.backgroundColorToken",
    ),
    surfaceAlpha: requiredAlpha(label, "alpha", "component.label.alpha"),
    textColorToken: requiredString(
      label,
      "textColorToken",
      "component.label.textColorToken",
    ),
    textSizeToken: requiredString(
      label,
      "textSizeToken",
      "component.label.textSizeToken",
    ),
    textStyle,
    surface: {
      shadowEnabled: requiredBoolean(
        style,
        "shadowEnabled",
        "component.style.shadowEnabled",
      ),
      reliefEnabled: requiredBoolean(
        style,
        "reliefEnabled",
        "component.style.reliefEnabled",
      ),
      borderWidth: requiredNumber(style, "borderWidth", "component.style.borderWidth"),
      borderColorToken: requiredString(
        style,
        "borderColorToken",
        "component.style.borderColorToken",
      ),
      cornerRadiusToken: requiredString(
        style,
        "cornerRadiusToken",
        "component.style.cornerRadiusToken",
      ),
      reliefAngle: requiredNumber(style, "reliefAngle", "component.style.reliefAngle"),
      reliefExtent: requiredNumber(style, "reliefExtent", "component.style.reliefExtent"),
      reliefSpread: requiredNumber(style, "reliefSpread", "component.style.reliefSpread"),
      reliefTopIntensity: requiredNumber(
        style,
        "reliefTopIntensity",
        "component.style.reliefTopIntensity",
      ),
      reliefBottomIntensity: requiredNumber(
        style,
        "reliefBottomIntensity",
        "component.style.reliefBottomIntensity",
      ),
    },
  };
}
