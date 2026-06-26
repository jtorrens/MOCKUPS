import type { TextMeasurement, TextMeasurer, TextMeasureStyle } from "./types.js";

export interface MeasureTextApproximateInput {
  text: string;
  fontFamily?: string;
  fontSize: number;
  fontWeight?: string | number;
  lineHeight: number;
  maxWidth: number;
  measurer?: TextMeasurer;
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

function measuredLineWidth(
  text: string,
  fontSize: number,
  style: TextMeasureStyle | undefined,
  measurer: TextMeasurer | undefined,
) {
  if (style && measurer) {
    const measured = measurer.measureLineWidth(text, style);
    if (typeof measured === "number" && Number.isFinite(measured)) {
      return measured;
    }
  }
  return measureLineWidth(text, fontSize);
}

function wrapLineWidths(
  text: string,
  fontSize: number,
  maxWidth: number,
  style: TextMeasureStyle | undefined,
  measurer: TextMeasurer | undefined,
) {
  const widths: number[] = [];
  let currentWidth = 0;
  let currentText = "";
  for (const character of Array.from(text || " ")) {
    const nextText = `${currentText}${character}`;
    const nextWidth = measuredLineWidth(nextText, fontSize, style, measurer);
    if (currentText.length > 0 && nextWidth > maxWidth) {
      widths.push(currentWidth);
      currentText = character;
      currentWidth = measuredLineWidth(currentText, fontSize, style, measurer);
      continue;
    }
    currentText = nextText;
    currentWidth = nextWidth;
  }
  widths.push(currentWidth);
  return widths;
}

export function measureTextApproximate({
  text,
  fontFamily,
  fontSize,
  fontWeight,
  lineHeight,
  maxWidth,
  measurer,
}: MeasureTextApproximateInput): TextMeasurement {
  const averageGlyphWidth = fontSize * FALLBACK_AVERAGE_GLYPH_WIDTH_RATIO;
  const style = fontFamily
    ? { fontFamily, fontSize, ...(fontWeight ? { fontWeight } : {}) }
    : undefined;
  const explicitLines = String(text || " ").split("\n");
  const wrappedWidths = explicitLines.flatMap((line) =>
    wrapLineWidths(line, fontSize, maxWidth, style, measurer),
  );
  const lineCount = Math.max(1, wrappedWidths.length);
  const maxCharsPerLine = Math.max(
    1,
    Math.floor(maxWidth / averageGlyphWidth),
  );
  const measuredWidth = Math.max(
    1,
    ...wrappedWidths,
    ...explicitLines.map((line) =>
      measuredLineWidth(line, fontSize, style, measurer),
    ),
  );

  return {
    width: Math.round(Math.min(maxWidth, measuredWidth)),
    height: Math.round(lineCount * lineHeight),
    lineCount,
    maxCharsPerLine,
    averageGlyphWidth,
    strategy:
      style && measurer?.measureLineWidth("M", style) !== undefined
        ? "font_metrics"
        : "average_glyph_width",
  };
}
