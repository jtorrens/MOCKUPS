import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  numberToken,
  selectedColor,
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
