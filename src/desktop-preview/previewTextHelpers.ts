import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { TypographyStyleContract } from "./previewComponentContracts.js";
import {
  numberOrThemeToken,
  numberToken,
  stringOrThemeToken,
} from "./previewColorHelpers.js";
import { fontFamilyForTypography } from "./previewFontHelpers.js";

export interface ResolvedTypographyStyle {
  fontFamily: string;
  fontSize: number;
  lineHeight: number;
  fontStyle: "italic" | undefined;
  fontWeight: number;
}

type GraphemeSegmenter = {
  segment(value: string): Iterable<{ segment: string }>;
};

type GraphemeSegmenterConstructor = new (
  locale: string | undefined,
  options: { granularity: "grapheme" },
) => GraphemeSegmenter;

function textGraphemes(text: string) {
  const segmenterConstructor = (Intl as typeof Intl & {
    Segmenter?: GraphemeSegmenterConstructor;
  }).Segmenter;
  if (!segmenterConstructor) {
    return Array.from(text);
  }

  return Array.from(
    new segmenterConstructor(undefined, { granularity: "grapheme" }).segment(text),
    (segment) => segment.segment,
  );
}

function graphemeAdvance(grapheme: string) {
  if (!grapheme) return 0;
  if (/^\s$/u.test(grapheme)) return 0.33;
  if (/\p{Extended_Pictographic}/u.test(grapheme)) return 1;
  if (/[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Hangul}]/u.test(grapheme)) return 1;
  if (/^[.,:;!¡?¿'"`´]$/u.test(grapheme)) return 0.28;
  if (/^[ilI|]$/u.test(grapheme)) return 0.32;
  if (/^[mwMW@#%&]$/u.test(grapheme)) return 0.78;
  return 0.54;
}

export function approximateTextWidth(text: string, fontSize: number) {
  return textGraphemes(text).reduce(
    (width, grapheme) => width + graphemeAdvance(grapheme),
    0,
  ) * fontSize;
}

export function approximateMultilineTextSize(
  text: string,
  fontSize: number,
  lineHeight: number,
) {
  const lines = text.split(/\r\n|\r|\n/u);
  return {
    width: Math.max(1, ...lines.map((line) => approximateTextWidth(line, fontSize))),
    height: Math.max(1, lines.length) * lineHeight,
    lineCount: Math.max(1, lines.length),
  };
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
    fontFamily: fontFamilyForTypography(payload, typography.fontFamilyId),
    fontSize,
    lineHeight: Math.max(
      fontSize * numberOrThemeToken(payload, typography.lineHeight),
      fontSize,
    ),
    fontStyle: style === "italic" ? "italic" : undefined,
    fontWeight: numberOrThemeToken(payload, typography.weight),
  };
}
