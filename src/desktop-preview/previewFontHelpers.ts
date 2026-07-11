import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";

export function previewFontFaceFamily(fontId: string) {
  const normalized = fontId.trim().replace(/[^a-zA-Z0-9_-]/g, "_");
  return normalized ? `MockupsFont_${normalized}` : "";
}

function quoteFamily(family: string) {
  return `"${family.replace(/"/g, '\\"')}"`;
}

function themeTypographyFontId(payload: DesignPreviewPayload, key: string) {
  const root = parseObject(payload.themeTokensJson);
  const typography = asRecord(root.typography);
  const value = typography[key];
  return typeof value === "string" ? value : "";
}

function familyForFontId(payload: DesignPreviewPayload, fontId: string) {
  if (!fontId.trim()) return "";
  return payload.fontFaces?.some((face) => face.fontId === fontId)
    ? previewFontFaceFamily(fontId)
    : "";
}

export function fontFamilyForTypography(
  payload: DesignPreviewPayload,
  fontFamilyId: string,
) {
  const primaryFontId = fontFamilyId === "theme"
    ? themeTypographyFontId(payload, "fontFamilyId")
    : fontFamilyId === "theme.system"
      ? themeTypographyFontId(payload, "systemFontFamilyId")
    : fontFamilyId;
  const emojiFontId = themeTypographyFontId(payload, "emojiFontFamilyId");
  const primaryFamily = familyForFontId(payload, primaryFontId);
  const emojiFamily = familyForFontId(payload, emojiFontId);
  if (!primaryFontId || !primaryFamily) {
    throw new Error(`Required production text font is unavailable: ${primaryFontId || "<empty>"}`);
  }
  if (!emojiFontId || !emojiFamily) {
    throw new Error(`Required production emoji font is unavailable: ${emojiFontId || "<empty>"}`);
  }

  return [primaryFamily, emojiFamily]
    .filter((family, index, list) => family && list.indexOf(family) === index)
    .map(quoteFamily)
    .join(", ");
}
