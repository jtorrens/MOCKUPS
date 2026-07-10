import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";

export const previewTextFontFamily =
  "system-ui, -apple-system, BlinkMacSystemFont, \"Apple Color Emoji\", \"Segoe UI Emoji\", \"Noto Color Emoji\", sans-serif";

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
  if (fontId === "system") return "";
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
    : fontFamilyId;
  const emojiFontId = themeTypographyFontId(payload, "emojiFontFamilyId");
  const families = [
    familyForFontId(payload, primaryFontId),
    familyForFontId(payload, emojiFontId),
  ]
    .filter((family, index, list) => family && list.indexOf(family) === index)
    .map(quoteFamily);

  return families.length > 0
    ? `${families.join(", ")}, ${previewTextFontFamily}`
    : previewTextFontFamily;
}
