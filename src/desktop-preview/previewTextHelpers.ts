import path from "node:path";
import * as fontkit from "fontkit";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { TypographyStyleContract } from "./previewComponentContracts.js";
import {
  numberOrThemeToken,
  numberToken,
  stringOrThemeToken,
} from "./previewColorHelpers.js";
import {
  fontFamilyForTypography,
  fontIdsForTypography,
} from "./previewFontHelpers.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";

export interface ResolvedTypographyStyle {
  fontFamily: string;
  fontSize: number;
  lineHeight: number;
  fontStyle: "italic" | undefined;
  fontWeight: number;
  measureTextWidth: (text: string) => number;
}

type ProductionFont = ReturnType<typeof fontkit.openSync> & {
  variationAxes?: Partial<Record<string, { min: number; default: number; max: number }>>;
  getVariation?: (coordinates: Record<string, number>) => ProductionFont;
  layout: (text: string) => { advanceWidth: number; glyphs: Array<{ id: number }> };
  unitsPerEm: number;
};

const productionFontCache = new Map<string, ProductionFont>();
const shapedAdvanceCache = new Map<string, number>();
const graphemeFontCache = new Map<string, "primary" | "emoji">();

function productionFont(
  payload: DesignPreviewPayload,
  fontId: string,
  weight: number,
  style: "normal" | "italic",
) {
  const faces = (payload.fontFaces ?? []).filter((face) => face.fontId === fontId);
  const styledFaces = faces.filter((face) => face.style === style);
  const candidates = styledFaces.length > 0 ? styledFaces : faces;
  const face = candidates.sort((left, right) =>
    Math.abs(left.weight - weight) - Math.abs(right.weight - weight))[0];
  if (!face) throw new Error(`Required production font face is unavailable: ${fontId}`);
  const fullPath = path.resolve(payload.projectMediaRoot ?? "", face.relativePath);
  const cacheKey = `${fullPath}\u001f${weight}\u001f${style}`;
  const cached = productionFontCache.get(cacheKey);
  if (cached) return { font: cached, cacheKey };
  const opened = fontkit.openSync(fullPath) as ProductionFont;
  const weightAxis = opened.variationAxes?.wght;
  const varied = opened.getVariation && weightAxis
    ? opened.getVariation({
        wght: Math.max(weightAxis.min, Math.min(weightAxis.max, weight)),
      })
    : opened;
  productionFontCache.set(cacheKey, varied);
  return { font: varied, cacheKey };
}

function typographyTextMeasurer(
  payload: DesignPreviewPayload,
  typography: TypographyStyleContract,
  fontSize: number,
  weight: number,
  style: "normal" | "italic",
) {
  const { primaryFontId, emojiFontId } = fontIdsForTypography(payload, typography.fontFamilyId);
  const primary = productionFont(payload, primaryFontId, weight, style);
  const emoji = productionFont(payload, emojiFontId, weight, "normal");
  const fontForGrapheme = (grapheme: string) => {
    if (/\p{Extended_Pictographic}/u.test(grapheme)) return emoji;
    const selectionKey = `${primary.cacheKey}\u001f${emoji.cacheKey}\u001f${grapheme}`;
    const cached = graphemeFontCache.get(selectionKey);
    if (cached) return cached === "primary" ? primary : emoji;
    const run = primary.font.layout(grapheme);
    const selected = run.glyphs.some((glyph) => glyph.id === 0) ? emoji : primary;
    graphemeFontCache.set(selectionKey, selected === primary ? "primary" : "emoji");
    return selected;
  };
  const runAdvance = (selected: typeof primary, text: string) => {
    const key = `${selected.cacheKey}\u001f${text}`;
    const cached = shapedAdvanceCache.get(key);
    if (cached !== undefined) return cached;
    const run = selected.font.layout(text);
    const advance = run.advanceWidth / Math.max(1, selected.font.unitsPerEm);
    shapedAdvanceCache.set(key, advance);
    return advance;
  };
  return (text: string) => {
    let width = 0;
    let currentFont: typeof primary | undefined;
    let currentText = "";
    const commitRun = () => {
      if (currentFont && currentText) width += runAdvance(currentFont, currentText);
      currentText = "";
    };
    for (const grapheme of textGraphemes(text)) {
      const selected = fontForGrapheme(grapheme);
      if (currentFont && selected.cacheKey !== currentFont.cacheKey) commitRun();
      currentFont = selected;
      currentText += grapheme;
    }
    commitRun();
    return width * fontSize;
  };
}

export function measuredTextWidth(text: string, typography: ResolvedTypographyStyle) {
  return typography.measureTextWidth(text);
}

export function typographyAtFontSize(
  typography: ResolvedTypographyStyle,
  fontSize: number,
  lineHeight = Math.max(fontSize, typography.lineHeight * fontSize / typography.fontSize),
): ResolvedTypographyStyle {
  const scale = fontSize / Math.max(1, typography.fontSize);
  return {
    ...typography,
    fontSize,
    lineHeight,
    measureTextWidth: (text) => typography.measureTextWidth(text) * scale,
  };
}

export function measuredMultilineTextSize(
  text: string,
  typography: ResolvedTypographyStyle,
) {
  const lines = text.split(/\r\n|\r|\n/u);
  return {
    width: Math.max(1, ...lines.map((line) => measuredTextWidth(line, typography))),
    height: Math.max(1, lines.length) * typography.lineHeight,
    lineCount: Math.max(1, lines.length),
  };
}

export function measuredWrappedTextLines(
  text: string,
  typography: ResolvedTypographyStyle,
  maxWidth: number,
) {
  return wrappedTextLines(text, maxWidth, (value) => measuredTextWidth(value, typography));
}

export function measuredWrappedTextSize(
  text: string,
  typography: ResolvedTypographyStyle,
  maxWidth: number,
) {
  const lines = measuredWrappedTextLines(text, typography, maxWidth);
  return {
    width: Math.min(
      Math.max(1, maxWidth),
      Math.max(1, ...lines.map((line) => measuredTextWidth(line, typography))),
    ),
    height: Math.max(1, lines.length) * typography.lineHeight,
    lineCount: Math.max(1, lines.length),
  };
}

function graphemeAdvance(grapheme: string) {
  if (!grapheme) return 0;
  if (grapheme === " ") return 0.32;
  if (/^\s$/u.test(grapheme)) return 0.32;
  // Color emoji glyphs are usually wider than the text font's em square.
  if (/\p{Extended_Pictographic}/u.test(grapheme)) return 1.25;
  if (/[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Hangul}]/u.test(grapheme)) return 1;
  if (/^[,.;:!?¡¿'"`´’‘“”()[\]{}]$/u.test(grapheme)) return 0.34;
  if (/^[ilI|]$/u.test(grapheme)) return 0.34;
  if (/^[fjrt]$/u.test(grapheme)) return 0.44;
  if (/^[mwMW]$/u.test(grapheme)) return 0.82;
  if (/^[A-ZÁÉÍÓÚÜÑ]$/u.test(grapheme)) return 0.66;
  if (/^[0-9]$/u.test(grapheme)) return 0.58;
  if ((grapheme.codePointAt(0) ?? 0) > 0xffff) return 1;
  return 0.56;
}

export function approximateTextWidth(text: string, fontSize: number) {
  return textGraphemes(text).reduce(
    (width, grapheme) => width + graphemeAdvance(grapheme),
    0,
  ) * fontSize;
}

export function approximateWrappedTextLines(
  text: string,
  fontSize: number,
  maxWidth: number,
) {
  return wrappedTextLines(text, maxWidth, (value) => approximateTextWidth(value, fontSize));
}

function wrappedTextLines(
  text: string,
  maxWidth: number,
  measure: (text: string) => number,
) {
  const wrapWidth = Math.max(1, maxWidth);
  return text.split(/\r\n|\r|\n/u).flatMap((sourceLine) => {
    const normalizedLine = sourceLine.replace(/[^\S\r\n]+/gu, " ").trim();
    if (normalizedLine.length === 0) {
      return [""];
    }

    const lines: string[] = [];
    let current = "";
    for (const word of normalizedLine.split(" ")) {
      const candidate = current ? `${current} ${word}` : word;
      if (measure(candidate) <= wrapWidth) {
        current = candidate;
        continue;
      }

      if (current) {
        lines.push(current);
        current = "";
      }

      if (measure(word) <= wrapWidth) {
        current = word;
        continue;
      }

      const pieces = splitLongWord(word, wrapWidth, measure);
      lines.push(...pieces.slice(0, -1));
      current = pieces.at(-1) ?? "";
    }

    return [...lines, current].filter((line, index, all) =>
      line.length > 0 || all.length === 1 || index < all.length - 1);
  });
}

function splitLongWord(word: string, maxWidth: number, measure: (text: string) => number) {
  const lines: string[] = [];
  let current = "";
  for (const grapheme of textGraphemes(word)) {
    const candidate = `${current}${grapheme}`;
    if (current && measure(candidate) > maxWidth) {
      lines.push(current);
      current = grapheme;
      continue;
    }

    current = candidate;
  }

  return [...lines, current || word];
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

  const fontWeight = numberOrThemeToken(payload, typography.weight);
  return {
    fontFamily: fontFamilyForTypography(payload, typography.fontFamilyId),
    fontSize,
    lineHeight: Math.max(
      fontSize * numberOrThemeToken(payload, typography.lineHeight),
      fontSize,
    ),
    fontStyle: style === "italic" ? "italic" : undefined,
    fontWeight,
    measureTextWidth: typographyTextMeasurer(
      payload,
      typography,
      fontSize,
      fontWeight,
      style,
    ),
  };
}
