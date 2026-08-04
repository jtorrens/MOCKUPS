import assert from "node:assert/strict";
import test from "node:test";
import {
  placeChild,
  screenPercentToDesignWidth,
  translateRenderableNode,
} from "../../src/desktop-preview/previewGeometryHelpers.js";

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

test("screen percentage resolves from the required Screen width", () => {
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
      moduleTransparency: { enabled: false, mode: "fixed", paletteColor: "gray_000", opacity: 1, fixedStart: 0, gradientHeight: 1, variableOffset: 0 },
    },
    themeMode: "light",
    themeTokensJson: "{}",
  }, 90), 324);
});

test("a nested root overlay interpolates only its remaining parent translation", () => {
  const overlay = {
    id: "media.fullscreen",
    type: "group" as const,
    frame: 0,
    box: { x: 20, y: 30, width: 100, height: 200 },
    style: {
      rootOverlay: true,
      rootOverlayTranslationFactor: 0.25,
    },
    children: [{
      id: "media.image",
      type: "image" as const,
      frame: 0,
      box: { x: 20, y: 30, width: 100, height: 200 },
    }],
  };

  const translated = translateRenderableNode(overlay, { x: 40, y: 80 });
  assert.deepEqual(translated.box, { x: 30, y: 50, width: 100, height: 200 });
  assert.deepEqual(
    translated.children?.[0]?.box,
    { x: 30, y: 50, width: 100, height: 200 },
  );

  const stable = translateRenderableNode({
    ...overlay,
    style: { rootOverlay: true },
  }, { x: 40, y: 80 });
  assert.deepEqual(stable.box, overlay.box);
});
