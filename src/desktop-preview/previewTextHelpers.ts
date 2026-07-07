import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { TypographyStyleContract } from "./previewComponentContracts.js";
import {
  numberOrThemeToken,
  numberToken,
  stringOrThemeToken,
} from "./previewColorHelpers.js";

export interface ResolvedTypographyStyle {
  fontSize: number;
  lineHeight: number;
  fontStyle: "italic" | undefined;
  fontWeight: number;
}

export function approximateTextWidth(text: string, fontSize: number) {
  return text.length * fontSize * 0.58;
}

export function resolveTypographyStyle(
  payload: DesignPreviewPayload,
  typography: TypographyStyleContract,
  scale: number,
): ResolvedTypographyStyle {
  const fontSize = numberToken(payload, typography.sizeToken) * scale;
  const style = stringOrThemeToken(payload, typography.style);
  if (style !== "normal" && style !== "italic") {
    throw new Error(`Unsupported typography style ${style}`);
  }

  return {
    fontSize,
    lineHeight: Math.max(
      fontSize * numberOrThemeToken(payload, typography.lineHeight),
      fontSize,
    ),
    fontStyle: style === "italic" ? "italic" : undefined,
    fontWeight: numberOrThemeToken(payload, typography.weight),
  };
}
