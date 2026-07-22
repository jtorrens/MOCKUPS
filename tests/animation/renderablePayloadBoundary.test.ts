import assert from "node:assert/strict";
import test from "node:test";

import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { resolveRenderablePayload } from "../../src/desktop-preview/renderablePayloadBoundary.js";

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
  themeTokensJson: "{}",
};

const requiredDocuments = [
  "configJson",
  "designPreviewJson",
  "runtimeContractJson",
  "componentBaseConfigsJson",
  "appConfigJson",
  "instanceJson",
  "themeTokensJson",
] as const;

test("renderable payload accepts complete object documents", () => {
  assert.deepEqual(resolveRenderablePayload(payload), payload);
});

for (const key of requiredDocuments) {
  test(`renderable payload rejects missing ${key}`, () => {
    assert.throws(() => resolveRenderablePayload({ ...payload, [key]: undefined } as unknown as DesignPreviewPayload));
  });

  test(`renderable payload rejects blank ${key}`, () => {
    assert.throws(() => resolveRenderablePayload({ ...payload, [key]: "" }));
  });

  test(`renderable payload rejects malformed ${key}`, () => {
    assert.throws(() => resolveRenderablePayload({ ...payload, [key]: "{" }));
  });

  test(`renderable payload rejects wrong-root ${key}`, () => {
    assert.throws(() => resolveRenderablePayload({ ...payload, [key]: "[]" }));
  });
}

test("renderable payload allows an absent optional icon mapping", () => {
  assert.doesNotThrow(() => resolveRenderablePayload(payload));
});

test("renderable payload rejects an invalid present icon mapping", () => {
  assert.throws(() => resolveRenderablePayload({ ...payload, iconMappingJson: "[]" }));
});

test("renderable payload rejects a non-object forwarding envelope", () => {
  assert.throws(() => resolveRenderablePayload({
    ...payload,
    configJson: JSON.stringify({ owner: { $forwardedInputs: [] } }),
  }));
});

test("renderable payload rejects a non-object forwarded definition", () => {
  assert.throws(() => resolveRenderablePayload({
    ...payload,
    configJson: JSON.stringify({ owner: { $forwardedInputs: { title: false } } }),
  }));
});

test("renderable payload rejects invalid runtime field id metadata", () => {
  assert.throws(() => resolveRenderablePayload({
    ...payload,
    configJson: JSON.stringify({
      owner: {
        id: "owner",
        $forwardedInputs: {
          title: { id: "forwarded.title", jsonKey: "titleValue" },
        },
        __runtimeFieldIds: [],
      },
    }),
    designPreviewJson: JSON.stringify({ titleValue: "Title" }),
  }));
});
