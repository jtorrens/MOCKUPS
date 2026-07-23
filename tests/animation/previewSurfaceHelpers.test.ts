import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { shadow } from "../../src/desktop-preview/previewSurfaceHelpers.js";

function payload(shadows: unknown): DesignPreviewPayload {
  return {
    themeTokensJson: JSON.stringify({ shadows }),
    paletteColors: { shadow: "#000000" },
    paletteNeutralColors: {},
    previewFrame: {
      canvasWidth: 360,
      canvasHeight: 800,
      screenX: 0,
      screenY: 0,
      screenWidth: 360,
      screenHeight: 800,
    },
  } as DesignPreviewPayload;
}

test("Shadow requires its exact Theme object path", () => {
  assert.deepEqual(shadow(payload({
    default: {
      offsetX: 0,
      offsetY: 2,
      blur: 4,
      color: { color: "shadow", alpha: 0.5 },
    },
  })), {
    offsetX: 0,
    offsetY: 2,
    blur: 4,
    color: "rgba(0, 0, 0, 0.5)",
  });
  assert.throws(
    () => shadow(payload([])),
    /Missing object value theme\.shadows/,
  );
  assert.throws(
    () => shadow(payload({ default: [] })),
    /Missing object value theme\.shadows\.default/,
  );
  assert.throws(
    () => shadow(payload({
      default: { offsetX: 0, offsetY: 2, blur: 4, color: [] },
    })),
    /Missing object value theme\.shadows\.default\.color/,
  );
});
