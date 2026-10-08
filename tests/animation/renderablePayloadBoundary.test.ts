import assert from "node:assert/strict";
import test from "node:test";

import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { resolveRenderablePayload } from "../../src/desktop-preview/renderablePayloadBoundary.js";
import { forwardedRuntimeInputPatch, prepareEmbeddedRuntimePayload, requirePreparedRuntimePayload } from "../../src/desktop-preview/runtimePreviewDocumentContract.js";
import { routeComponentClassToRenderable } from "../../src/desktop-preview/componentClassRenderableRegistry.js";

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
    moduleTransparency: { enabled: false, mode: "fixed", paletteColor: "gray_000", backgroundOpacity: 1, fixedStart: 0, minimumOpaqueExtent: 0, gradientHeight: 1, variableOffset: 0 },
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
  assert.deepEqual(JSON.parse(JSON.stringify(resolveRenderablePayload(payload))), payload);
});

test("only the Runtime boundary can prepare a payload for registry dispatch", () => {
  assert.throws(() => requirePreparedRuntimePayload(payload), /prepared parent/);
  const forged = { ...payload, runtimeValuesPrepared: true };
  assert.throws(() => resolveRenderablePayload(forged), /not a current payload/);
  const prepared = resolveRenderablePayload(payload);
  assert.doesNotThrow(() => requirePreparedRuntimePayload(prepared));
  const child = prepareEmbeddedRuntimePayload(prepared, "label", {}, {});
  assert.doesNotThrow(() => requirePreparedRuntimePayload(child));
  assert.equal(child.runtimeContractJson, prepared.runtimeContractJson);
  assert.deepEqual(resolveRenderablePayload(child), child);
});

// Compiler-backed negative capability assertions; never executed.
function cannotDispatchAuthoredPayload() {
  // @ts-expect-error An authored payload is not a prepared registry input.
  routeComponentClassToRenderable(payload, () => { throw new Error("not executed"); });
  // @ts-expect-error An authored parent cannot construct a prepared embedded child.
  prepareEmbeddedRuntimePayload(payload, "label", {}, {});
}
void cannotDispatchAuthoredPayload;

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
    designPreviewJson: JSON.stringify({ inputs: [{ id: "forwarded.title", jsonKey: "titleValue" }], titleValue: "Title" }),
  }));
});

test("a parent addresses one forwarded child input by its stable field id", () => {
  const config = {
    child: {
      $forwardedInputs: {
        title: {
          id: "forwarded.child.title",
          jsonKey: "forwarded_child_title",
        },
      },
    },
  };
  assert.deepEqual(
    forwardedRuntimeInputPatch(config, "forwarded.child.title", "Runtime title", {}),
    { inputs: [{ id: "forwarded.child.title", jsonKey: "forwarded_child_title", source: "runtime" }], forwarded_child_title: "Runtime title" },
  );
  assert.throws(
    () => forwardedRuntimeInputPatch(config, "forwarded.child.missing", "Missing", {}),
    /exactly one target; found 0/,
  );
});

test("a duplicated forwarded field id is rejected at the parent boundary", () => {
  const definition = {
    id: "forwarded.child.title",
    jsonKey: "forwarded_child_title",
  };
  assert.throws(
    () => forwardedRuntimeInputPatch({
      first: { $forwardedInputs: { title: definition } },
      second: { $forwardedInputs: { title: definition } },
    }, "forwarded.child.title", "Ambiguous", {}),
    /exactly one target; found 2/,
  );
});
