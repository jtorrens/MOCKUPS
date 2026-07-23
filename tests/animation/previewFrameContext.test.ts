import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  currentBoundaryLocalFrame,
  rootScreenFrame,
  validateRootFrameIdentity,
} from "../../src/desktop-preview/previewFrameContext.js";

const payload: DesignPreviewPayload = {
  kind: "moduleInstance",
  componentType: "module.core.chat",
  componentBaseConfigsJson: "{}",
  appConfigJson: "{}",
  instanceJson: JSON.stringify({ context: { screenFrame: 12 } }),
  frameRate: 25,
  localFrame: 12,
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
  themeTokensJson: "{}",
};

test("root Screen frame survives a rebased embedded local frame", () => {
  const embedded = { ...payload, localFrame: 5 };
  assert.equal(currentBoundaryLocalFrame(embedded), 5);
  assert.equal(rootScreenFrame(embedded), 12);
  assert.throws(() => validateRootFrameIdentity(embedded));
  assert.doesNotThrow(() => validateRootFrameIdentity(payload));
});

test("isolated Design uses its current boundary frame without a Screen context", () => {
  const isolated = {
    ...payload,
    kind: "componentClass" as const,
    componentType: "label",
    localFrame: 7,
    instanceJson: "{}",
  };
  assert.equal(rootScreenFrame(isolated), 7);
});

test("Module Instance Screen context rejects the retired frame name and invalid frames", () => {
  for (const instance of [
    {},
    { context: { localFrame: 12 } },
    { context: { localFrame: 12, screenFrame: 12 } },
    { context: { screenFrame: "12" } },
    { context: { screenFrame: -1 } },
    { context: { screenFrame: 1.5 } },
  ]) {
    assert.throws(() => rootScreenFrame({
      ...payload,
      instanceJson: JSON.stringify(instance),
    }));
  }
});
