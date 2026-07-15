import assert from "node:assert/strict";
import test from "node:test";
import { placeChild, screenPercentToDesignWidth } from "../../src/desktop-preview/previewGeometryHelpers.js";

const parent = { x: 10, y: 20, width: 100, height: 80 };
const child = { width: 20, height: 10 };

test("alignment placement keeps center semantics and distinguishes inside/outside edges", () => {
  assert.deepEqual(
    placeChild(parent, child, { mode: "center", alignX: 0, alignY: 1, offsetX: 0, offsetY: 0 }),
    { x: 0, y: 95, width: 20, height: 10 },
  );
  assert.deepEqual(
    placeChild(parent, child, { mode: "insideEdge", alignX: 0, alignY: 1, offsetX: 0, offsetY: 0 }),
    { x: 10, y: 90, width: 20, height: 10 },
  );
  assert.deepEqual(
    placeChild(parent, child, { mode: "outsideEdge", alignX: 0, alignY: 1, offsetX: 0, offsetY: 0 }),
    { x: -10, y: 100, width: 20, height: 10 },
  );
});

test("inside edge uses the padded box supplied by its parent", () => {
  const paddedParent = { x: 18, y: 28, width: 84, height: 64 };
  assert.deepEqual(
    placeChild(paddedParent, child, { mode: "insideEdge", alignX: 1, alignY: 0, offsetX: 0, offsetY: 0 }),
    { x: 82, y: 28, width: 20, height: 10 },
  );
});

test("screen percentage resolves to design width independently of preview scale", () => {
  assert.equal(screenPercentToDesignWidth({
    kind: "componentClass",
    frameRate: 25,
    localFrame: 0,
    configJson: "{}",
    previewFrame: {
      canvasWidth: 720,
      canvasHeight: 1440,
      screenX: 0,
      screenY: 0,
      screenWidth: 720,
      screenHeight: 1440,
      scaleToPixels: 2,
    },
    themeMode: "light",
    themeTokensJson: "{}",
  }, 90), 324);
});
