import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  cssColorWithAlpha,
  requiredNumberValue,
  resolvePaletteColor,
} from "./previewColorHelpers.js";
import { renderScale } from "./previewGeometryHelpers.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";

export function shadow(payload: DesignPreviewPayload) {
  const root = parseObject(payload.themeTokensJson);
  const shadowRoot = asRecord(asRecord(root.shadows).default);
  const color = asRecord(shadowRoot.color);
  const colorToken = color.color;
  if (typeof colorToken !== "string" || !colorToken.trim()) {
    throw new Error("Missing theme.shadows.default.color.color");
  }

  const scale = renderScale(payload);
  return {
    offsetX:
      requiredNumberValue(shadowRoot.offsetX, "theme.shadows.default.offsetX") *
      scale,
    offsetY:
      requiredNumberValue(shadowRoot.offsetY, "theme.shadows.default.offsetY") *
      scale,
    blur:
      requiredNumberValue(shadowRoot.blur, "theme.shadows.default.blur") *
      scale,
    color: cssColorWithAlpha(
      resolvePaletteColor(payload, colorToken),
      requiredNumberValue(color.alpha, "theme.shadows.default.color.alpha"),
    ),
  };
}

export function surfaceVisualPadding(
  borderWidth: number,
  shadowValue: Record<string, unknown> | undefined,
  surfaceRelief: Record<string, unknown> | undefined,
) {
  const shadowPadding = shadowValue
    ? Math.max(
        Math.abs(typeof shadowValue.offsetX === "number" ? shadowValue.offsetX : 0),
        Math.abs(typeof shadowValue.offsetY === "number" ? shadowValue.offsetY : 0),
      ) + (typeof shadowValue.blur === "number" ? shadowValue.blur * 2 : 0)
    : 0;
  const reliefPadding = surfaceRelief
    ? Math.max(
        typeof surfaceRelief.extension === "number" ? surfaceRelief.extension : 0,
        typeof surfaceRelief.spread === "number" ? surfaceRelief.spread : 0,
      )
    : 0;
  return Math.ceil(Math.max(borderWidth, shadowPadding, reliefPadding, 0));
}

