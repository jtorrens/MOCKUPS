import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { parseObject } from "./previewJsonHelpers.js";
import { requiredRecord, requiredString } from "./previewValueHelpers.js";

export function previewFontFaceFamily(fontId: string) {
  const normalized = fontId.trim().replace(/[^a-zA-Z0-9_-]/g, "_");
  return normalized ? `MockupsFont_${normalized}` : "";
}

function quoteFamily(family: string) {
  return `"${family.replace(/"/g, '\\"')}"`;
}

function themeTypographyFontId(payload: DesignPreviewPayload, key: string) {
  const root = parseObject(payload.themeTokensJson);
  const typography = requiredRecord(root, "typography", "theme.typography");
  return requiredString(typography, key, `theme.typography.${key}`);
}

export function fontIdsForTypography(
  payload: DesignPreviewPayload,
  fontFamilyId: string,
) {
  return {
    primaryFontId: fontFamilyId === "theme"
      ? themeTypographyFontId(payload, "fontFamilyId")
      : fontFamilyId === "theme.system"
        ? themeTypographyFontId(payload, "systemFontFamilyId")
        : fontFamilyId,
    emojiFontId: themeTypographyFontId(payload, "emojiFontFamilyId"),
  };
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
  const { primaryFontId, emojiFontId } = fontIdsForTypography(payload, fontFamilyId);
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
