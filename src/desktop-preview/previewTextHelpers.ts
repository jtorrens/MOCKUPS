import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { TypographyStyleContract } from "./previewComponentContracts.js";
import {
  numberOrThemeToken,
  numberToken,
  stringOrThemeToken,
} from "./previewColorHelpers.js";
import { fontFamilyForTypography } from "./previewFontHelpers.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";

export interface ResolvedTypographyStyle {
  fontFamily: string;
  fontSize: number;
  lineHeight: number;
  fontStyle: "italic" | undefined;
  fontWeight: number;
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

export function approximateWrappedTextSize(
  text: string,
  fontSize: number,
  lineHeight: number,
  maxWidth: number,
) {
  const lines = approximateWrappedTextLines(text, fontSize, maxWidth);
  const width = Math.max(
    1,
    ...lines.map((line) => approximateTextWidth(line, fontSize)),
  );

  return {
    width: Math.min(Math.max(1, maxWidth), width),
    height: Math.max(1, lines.length) * lineHeight,
    lineCount: Math.max(1, lines.length),
  };
}

export function approximateWrappedTextLines(
  text: string,
  fontSize: number,
  maxWidth: number,
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
      if (approximateTextWidth(candidate, fontSize) <= wrapWidth) {
        current = candidate;
        continue;
      }

      if (current) {
        lines.push(current);
        current = "";
      }

      if (approximateTextWidth(word, fontSize) <= wrapWidth) {
        current = word;
        continue;
      }

      const pieces = splitLongWord(word, fontSize, wrapWidth);
      lines.push(...pieces.slice(0, -1));
      current = pieces.at(-1) ?? "";
    }

    return [...lines, current].filter((line, index, all) =>
      line.length > 0 || all.length === 1 || index < all.length - 1);
  });
}

function splitLongWord(word: string, fontSize: number, maxWidth: number) {
  const lines: string[] = [];
  let current = "";
  for (const grapheme of textGraphemes(word)) {
    const candidate = `${current}${grapheme}`;
    if (current && approximateTextWidth(candidate, fontSize) > maxWidth) {
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
