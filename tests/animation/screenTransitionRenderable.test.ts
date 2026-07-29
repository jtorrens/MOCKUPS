import assert from "node:assert/strict";
import test from "node:test";

import type {
  DesignPreviewPayload,
  ScreenTransitionPayload,
} from "../../src/desktop-preview/designPreviewPayload.js";
import { screenTransitionLayers } from "../../src/desktop-preview/screenTransitionRenderable.js";
import type { RenderableNode } from "../../src/visual/renderable/types.js";

const frame = {
  canvasWidth: 360,
  canvasHeight: 720,
  screenX: 0,
  screenY: 0,
  screenWidth: 360,
  screenHeight: 720,
};
const themeTokensJson = JSON.stringify({
  motion: {
    transitions: {
      slide: {
        durationMs: 200,
        delayMs: 0,
        easing: "linear",
        intensity: 1,
      },
    },
  },
});

function owner(localFrame: number): DesignPreviewPayload {
  return {
    kind: "moduleInstance",
    componentType: "module.test",
    componentBaseConfigsJson: "{}",
    appConfigJson: "{}",
    instanceJson: "{}",
    frameRate: 25,
    localFrame,
    configJson: "{}",
    designPreviewJson: "{}",
    runtimeContractJson: "{}",
    previewFrame: frame,
    themeMode: "light",
    themeTokensJson,
  };
}

const outgoing = owner(24);
const incoming = owner(0);
const leftMotion = JSON.stringify({
  transition: "slide",
  direction: "left",
  bounds: "screen",
  fade: true,
  translate: true,
  scale: false,
});
const rightMotion = JSON.stringify({
  transition: "slide",
  direction: "right",
  bounds: "screen",
  fade: true,
  translate: true,
  scale: false,
});
const outgoingNode: RenderableNode = {
  id: "outgoing",
  type: "group",
  frame: 24,
  box: {
    x: 0,
    y: 0,
    width: 360,
    height: 720,
  },
  children: [],
};
const incomingNode: RenderableNode = {
  ...outgoingNode,
  id: "incoming",
  frame: 0,
};

function transition(
  elapsedMilliseconds: number,
): ScreenTransitionPayload {
  return {
    outgoing,
    incoming,
    outgoingMotionJson: leftMotion,
    incomingMotionJson: rightMotion,
    elapsedMilliseconds,
    durationFrames: 5,
  };
}

test("Screen exit and entry Motion start simultaneously", () => {
  const [exit, enter] = screenTransitionLayers(
    {
      ...incoming,
      kind: "screenTransition",
    },
    transition(0),
    outgoingNode,
    incomingNode,
  );

  assert.equal(exit.children?.[0]?.transform?.x, 0);
  assert.equal(exit.children?.[0]?.transform?.opacity, 1);
  assert.equal(enter.children?.[0]?.transform?.x, 360);
  assert.equal(enter.children?.[0]?.transform?.opacity, 0);
});

test("Screen exit and entry Motion share the same elapsed transition clock", () => {
  const [exit, enter] = screenTransitionLayers(
    {
      ...incoming,
      kind: "screenTransition",
    },
    transition(100),
    outgoingNode,
    incomingNode,
  );

  assert.equal(exit.children?.[0]?.transform?.x, -180);
  assert.equal(exit.children?.[0]?.transform?.opacity, 0.5);
  assert.equal(enter.children?.[0]?.transform?.x, 180);
  assert.equal(enter.children?.[0]?.transform?.opacity, 0.5);
});
