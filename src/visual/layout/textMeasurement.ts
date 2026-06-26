import type { TextMeasurement } from "./types.js";

export interface MeasureTextApproximateInput {
  text: string;
  fontSize: number;
  lineHeight: number;
  maxWidth: number;
}

const FALLBACK_AVERAGE_GLYPH_WIDTH_RATIO = 0.48;

function glyphWidthRatio(character: string) {
  if (character === " ") return 0.26;
  if (/[,.;:!?'"`´’‘“”()[\]{}]/u.test(character)) return 0.28;
  if (/[ilI|]/u.test(character)) return 0.28;
  if (/[fjrt]/u.test(character)) return 0.36;
  if (/[mwMW]/u.test(character)) return 0.72;
  if (/[A-ZÁÉÍÓÚÜÑ]/u.test(character)) return 0.58;
  if (/[0-9]/u.test(character)) return 0.5;
  if (character.codePointAt(0) && character.codePointAt(0)! > 0xffff) {
    return 1;
  }
  return FALLBACK_AVERAGE_GLYPH_WIDTH_RATIO;
}

function measureLineWidth(text: string, fontSize: number) {
  return Array.from(text).reduce(
    (width, character) => width + glyphWidthRatio(character) * fontSize,
    0,
  );
}

function wrapLineWidths(text: string, fontSize: number, maxWidth: number) {
  const widths: number[] = [];
  let currentWidth = 0;
  let currentCharacters = 0;
  for (const character of Array.from(text || " ")) {
    const characterWidth = glyphWidthRatio(character) * fontSize;
    if (currentCharacters > 0 && currentWidth + characterWidth > maxWidth) {
      widths.push(currentWidth);
      currentWidth = characterWidth;
      currentCharacters = 1;
      continue;
    }
    currentWidth += characterWidth;
    currentCharacters += 1;
  }
  widths.push(currentWidth);
  return widths;
}

export function measureTextApproximate({
  text,
  fontSize,
  lineHeight,
  maxWidth,
}: MeasureTextApproximateInput): TextMeasurement {
  const averageGlyphWidth = fontSize * FALLBACK_AVERAGE_GLYPH_WIDTH_RATIO;
  const explicitLines = String(text || " ").split("\n");
  const wrappedWidths = explicitLines.flatMap((line) =>
    wrapLineWidths(line, fontSize, maxWidth),
  );
  const lineCount = Math.max(1, wrappedWidths.length);
  const maxCharsPerLine = Math.max(
    1,
    Math.floor(maxWidth / averageGlyphWidth),
  );
  const measuredWidth = Math.max(
    1,
    ...wrappedWidths,
    ...explicitLines.map((line) => measureLineWidth(line, fontSize)),
  );

  return {
    width: Math.round(Math.min(maxWidth, measuredWidth)),
    height: Math.round(lineCount * lineHeight),
    lineCount,
    maxCharsPerLine,
    averageGlyphWidth,
    strategy: "average_glyph_width",
  };
}
