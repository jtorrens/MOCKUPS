import assert from "node:assert/strict";
import test from "node:test";

import { previewPayloadInBox } from "../../src/desktop-preview/previewPayloadHelpers.js";
import { wrapExitMotionFrame, wrapMotionFrame } from "../../src/desktop-preview/previewMotionHelpers.js";
import type { ComponentMotionContract } from "../../src/desktop-preview/previewComponentContracts.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";

const payload: DesignPreviewPayload = {
  kind: "componentClass",
  componentType: "label",
  componentBaseConfigsJson: "{}",
  appConfigJson: "{}",
  instanceJson: "{}",
  frameRate: 25,
  localFrame: 0,
  configJson: "{}",
  designPreviewJson: "{}",
  runtimeContractJson: "{}",
  previewFrame: {
    canvasWidth: 360,
    canvasHeight: 720,
    screenX: 0,
    screenY: 0,
    screenWidth: 360,
    screenHeight: 720,
  },
  themeMode: "light",
  themeTokensJson: JSON.stringify({
    motion: {
      transitions: {
        slide: { durationMs: 240, delayMs: 0, easing: "linear", intensity: 1 },
      },
    },
  }),
};

const finalBox = { x: 100, y: 100, width: 80, height: 40 };
const node = { id: "item", type: "group" as const, frame: 0, box: finalBox, children: [] };
const parentBox = { x: 0, y: 50, width: 360, height: 600 };
const baseMotion: ComponentMotionContract = {
  transition: "slide",
  direction: "bottom",
  bounds: "screen",
  fade: false,
  translate: true,
  scale: false,
};

test("Transition Screen bounds survive nested parent frames", () => {
  const nested = previewPayloadInBox(previewPayloadInBox(payload, parentBox), {
    x: 20,
    y: 80,
    width: 320,
    height: 500,
  });
  const screen = wrapMotionFrame(nested, node, baseMotion, { trigger: true, elapsedMs: 0 }, finalBox, parentBox);
  const parent = wrapMotionFrame(
    nested,
    node,
    { ...baseMotion, bounds: "parent" },
    { trigger: true, elapsedMs: 0 },
    finalBox,
    parentBox,
  );

  assert.deepEqual(screen.box, { x: 0, y: 0, width: 360, height: 720 });
  assert.equal(screen.children?.[0]?.transform?.y, 620);
  assert.deepEqual(parent.box, parentBox);
  assert.equal(parent.children?.[0]?.transform?.y, 550);
});

test("Transition Exit uses the same generic Screen and Parent bounds", () => {
  const nested = previewPayloadInBox(payload, parentBox);
  const screen = wrapExitMotionFrame(nested, node, baseMotion, { trigger: true, elapsedMs: 240 }, finalBox, parentBox);
  const parent = wrapExitMotionFrame(
    nested,
    node,
    { ...baseMotion, bounds: "parent" },
    { trigger: true, elapsedMs: 240 },
    finalBox,
    parentBox,
  );

  assert.equal(screen.children?.[0]?.transform?.y, 620);
  assert.equal(parent.children?.[0]?.transform?.y, 550);
});
