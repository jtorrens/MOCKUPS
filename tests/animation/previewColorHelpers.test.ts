import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  numberToken,
  selectedColor,
  signedColorBlend,
  variants,
} from "../../src/desktop-preview/previewColorHelpers.js";

function payload(tokens: unknown, themeMode = "light"): DesignPreviewPayload {
  return {
    themeTokensJson: JSON.stringify(tokens),
    themeMode,
    paletteColors: {
      gray: "#808080",
      blue: "#0000FF",
    },
    paletteNeutralColors: { gray: "#808080" },
  } as DesignPreviewPayload;
}

const tokens = {
  modes: {
    light: { colors: { background: "gray", "icons.primary": "blue" } },
    dark: { colors: { background: "blue", "icons.primary": "gray" } },
  },
  spacing: { m: 8 },
  neutralTint: { hueDeg: 120, saturation: 0 },
};

test("signed blend screens negative amounts, multiplies positive amounts and preserves alpha", () => {
  assert.equal(signedColorBlend("#808080", "#808080", -1), "#C0C0C0");
  assert.equal(signedColorBlend("#808080", "#808080", 1), "#404040");
  assert.equal(signedColorBlend("#808080", "#808080", -0.5), "#A0A0A0");
  assert.equal(signedColorBlend("#808080", "#808080", 0.5), "#606060");
  assert.equal(signedColorBlend("rgba(128, 128, 128, 0.35)", "#808080", 1), "rgba(64, 64, 64, 0.35)");
  assert.equal(signedColorBlend("#808080", "rgba(128, 128, 128, 0.5)", 1), "#606060");
  assert.equal(signedColorBlend("#804020", "#FF0080", 1), "#800010");
  for (const value of ["#abcdef", "transparent", "rgba(1, 2, 3, 0.1)"]) {
    assert.equal(signedColorBlend(value, "#808080", 0), value);
  }
  for (const value of [-1.01, 1.01, NaN, Infinity]) {
    assert.throws(() => signedColorBlend("#808080", "#808080", value), /between -1 and 1/);
  }
  assert.throws(() => signedColorBlend("invalid", "#808080", 1), /Unsupported resolved RGB/);
});

test("Theme colors preserve explicit mode and global token precedence", () => {
  assert.deepEqual(variants(payload(tokens)), ["light", "dark"]);
  assert.equal(selectedColor(payload(tokens), "theme.colors.background"), "#808080");
  assert.equal(selectedColor(payload(tokens), "theme.icons.primary"), "#0000FF");
  assert.equal(numberToken(payload(tokens), "theme.spacing.m"), 8);
  assert.throws(
    () => selectedColor(payload(tokens, ""), "theme.colors.background"),
    /Unsupported Preview Theme mode <empty>/,
  );
});

test("Theme token lookup distinguishes absence from a wrong object path", () => {
  assert.throws(
    () => variants(payload({ ...tokens, modes: [] })),
    /Missing object value theme\.modes/,
  );
  assert.throws(
    () => variants(payload({ ...tokens, modes: {} })),
    /at least one explicit mode/,
  );
  assert.throws(
    () => numberToken(payload({ ...tokens, spacing: 8 }), "theme.spacing.m"),
    /Theme token path theme\.spacing must be an object/,
  );
  assert.throws(
    () => selectedColor(payload({
      ...tokens,
      modes: { ...tokens.modes, light: { colors: [] } },
    }), "theme.colors.background"),
    /Theme token path theme\.modes\.light\.colors must be an object/,
  );
});

test("Neutral Palette colors require the exact Theme tint document", () => {
  assert.throws(
    () => selectedColor(payload({ ...tokens, neutralTint: [] }), "theme.colors.background"),
    /Missing object value theme\.neutralTint/,
  );
  assert.throws(
    () => selectedColor(payload({
      ...tokens,
      neutralTint: { hueDeg: 120, saturation: "0" },
    }), "theme.colors.background"),
    /Missing numeric theme value theme\.neutralTint\.saturation/,
  );
});
