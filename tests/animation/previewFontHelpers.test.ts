import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { fontIdsForTypography } from "../../src/desktop-preview/previewFontHelpers.js";

function payload(typography: unknown): DesignPreviewPayload {
  return {
    themeTokensJson: JSON.stringify({ typography }),
  } as DesignPreviewPayload;
}

test("Theme font selection requires its exact Typography ids", () => {
  assert.deepEqual(fontIdsForTypography(payload({
    fontFamilyId: "text_font",
    systemFontFamilyId: "system_font",
    emojiFontFamilyId: "emoji_font",
  }), "theme"), {
    primaryFontId: "text_font",
    emojiFontId: "emoji_font",
  });
  assert.deepEqual(fontIdsForTypography(payload({
    fontFamilyId: "text_font",
    systemFontFamilyId: "system_font",
    emojiFontFamilyId: "emoji_font",
  }), "theme.system"), {
    primaryFontId: "system_font",
    emojiFontId: "emoji_font",
  });
  assert.throws(
    () => fontIdsForTypography(payload([]), "theme"),
    /Missing object value theme\.typography/,
  );
  assert.throws(
    () => fontIdsForTypography(payload({
      fontFamilyId: "text_font",
      systemFontFamilyId: "system_font",
    }), "theme"),
    /Missing string value theme\.typography\.emojiFontFamilyId/,
  );
});
