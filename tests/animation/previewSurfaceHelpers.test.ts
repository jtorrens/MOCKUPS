import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { shadow } from "../../src/desktop-preview/previewSurfaceHelpers.js";
import { previewPayloadInBox } from "../../src/desktop-preview/previewPayloadHelpers.js";
import { renderScale } from "../../src/desktop-preview/previewGeometryHelpers.js";

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
      moduleTransparency: { enabled: false, mode: "fixed", paletteColor: "gray_000", backgroundOpacity: 1, fixedStart: 0, minimumOpaqueExtent: 0, gradientHeight: 1, variableOffset: 0 },
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

test("Embedded boxes retain Screen units for surfaces at every size", () => {
  const source = payload({ default: {
    offsetX: 0, offsetY: 2, blur: 4, color: { color: "shadow", alpha: 0.5 },
  } });
  for (const screenWidth of [360, 1080]) {
    source.previewFrame.screenWidth = screenWidth;
    for (const width of [60, 120, 360]) {
      const nested = previewPayloadInBox(previewPayloadInBox(source,
        { x: 10, y: 20, width: 300, height: 600 }),
      { x: 30, y: 40, width, height: 180 });
      assert.equal(renderScale(nested), screenWidth / 360);
      assert.deepEqual(shadow(nested), shadow(source));
      assert.equal(nested.previewFrame.screenWidth, width);
    }
  }
});
