import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  asRecord,
  parseObject,
  requiredAlpha,
  requiredNumber,
  requiredString,
  resolveSurfaceStyle,
  type SurfaceStyleContract,
} from "./componentResolverCommon.js";

export interface LabelDesignContract {
  id: string;
  text: string;
  subtext: string;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: { x: number; y: number };
  backgroundColorToken: string;
  surfaceAlpha: number;
  textColorToken: string;
  textSizeToken: string;
  textStyle: "normal" | "italic";
  textAlign: "left" | "center" | "right";
  textGap: number;
  subtextColorToken: string;
  subtextSizeToken: string;
  subtextStyle: "normal" | "italic";
  surface: SurfaceStyleContract;
}

function requiredText(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string") return raw;
  throw new Error(`Missing text component value ${path}`);
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
  return resolveLabelComponentFromRecords(config, preview, "component.label");
}

export function resolveLabelComponentFromRecords(
  config: Record<string, unknown>,
  preview: Record<string, unknown>,
  id: string,
): LabelDesignContract {
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

  const textAlign = requiredString(label, "textAlign", "component.label.textAlign");
  if (textAlign !== "left" && textAlign !== "center" && textAlign !== "right") {
    throw new Error(`Unsupported label text align ${textAlign}`);
  }

  const subtextStyle = requiredString(
    label,
    "subtextStyle",
    "component.label.subtextStyle",
  );
  if (subtextStyle !== "normal" && subtextStyle !== "italic") {
    throw new Error(`Unsupported label subtext style ${subtextStyle}`);
  }

  return {
    id,
    text: requiredString(preview, "sampleText", "component.label.preview.sampleText"),
    subtext: requiredText(
      preview,
      "sampleSubtext",
      "component.label.preview.sampleSubtext",
    ),
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
    textAlign,
    textGap: requiredNumber(label, "textGap", "component.label.textGap"),
    subtextColorToken: requiredString(
      label,
      "subtextColorToken",
      "component.label.subtextColorToken",
    ),
    subtextSizeToken: requiredString(
      label,
      "subtextSizeToken",
      "component.label.subtextSizeToken",
    ),
    subtextStyle,
    surface: resolveSurfaceStyle(style),
  };
}
