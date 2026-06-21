import type { TextMeasurement } from "./types.js";

export interface MeasureTextApproximateInput {
  text: string;
  fontSize: number;
  lineHeight: number;
  maxWidth: number;
}

const AVERAGE_GLYPH_WIDTH_RATIO = 0.52;

export function measureTextApproximate({
  text,
  fontSize,
  lineHeight,
  maxWidth,
}: MeasureTextApproximateInput): TextMeasurement {
  const averageGlyphWidth = fontSize * AVERAGE_GLYPH_WIDTH_RATIO;
  const maxCharsPerLine = Math.max(
    1,
    Math.floor(maxWidth / averageGlyphWidth),
  );
  const characterCount = Math.max(1, Array.from(text).length);
  const lineCount = Math.max(1, Math.ceil(characterCount / maxCharsPerLine));
  const unwrappedWidth = characterCount * averageGlyphWidth;

  return {
    width: Math.round(Math.min(maxWidth, unwrappedWidth)),
    height: Math.round(lineCount * lineHeight),
    lineCount,
    maxCharsPerLine,
    averageGlyphWidth,
    strategy: "average_glyph_width",
  };
}
