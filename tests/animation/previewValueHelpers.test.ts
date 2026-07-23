import assert from "node:assert/strict";
import test from "node:test";
import { requiredTypographyStyle } from "../../src/desktop-preview/previewValueHelpers.js";

const typography = {
  fontFamilyId: "theme",
  weight: "400",
  style: "normal",
  sizeToken: "theme.typography.sizes.m",
  lineHeight: "theme.typography.lineHeights.normal",
};

test("Preview Typography Style requires its exact object root", () => {
  assert.deepEqual(
    requiredTypographyStyle({ typography }, "typography", "component.typography"),
    typography,
  );
  assert.throws(
    () => requiredTypographyStyle({}, "typography", "component.typography"),
    /Missing object value component\.typography/,
  );
  assert.throws(
    () => requiredTypographyStyle({ typography: [] }, "typography", "component.typography"),
    /Missing object value component\.typography/,
  );
});
