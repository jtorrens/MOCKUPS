import assert from "node:assert/strict";
import test from "node:test";

import { collectionStackComponentToRenderable } from "../../src/desktop-preview/collectionStackComponentRenderable.js";
import type { CollectionStackDesignContract } from "../../src/desktop-preview/collectionStackComponentContract.js";
import { resolveCollectionStackComponent } from "../../src/desktop-preview/collectionStackComponentResolver.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import type { RenderableNode } from "../../src/visual/renderable/types.js";

const payload: DesignPreviewPayload = {
  kind: "componentClass",
  componentType: "collectionStack",
  frameRate: 25,
  localFrame: 0,
  configJson: "{}",
  designPreviewJson: "{}",
  previewFrame: {
    canvasWidth: 360,
    canvasHeight: 720,
    screenX: 0,
    screenY: 0,
    screenWidth: 360,
    screenHeight: 720,
    scaleToPixels: 1,
  },
  themeMode: "light",
  themeTokensJson: JSON.stringify({
    spacing: { none: 0, s: 5, m: 10 },
  }),
};

const items = ["first", "second"].map((id) => ({
  id,
  componentType: "stub",
  presetReference: `stub::preset::${id}`,
  config: {},
  alignment: "center" as const,
  gapBeforeMode: "fixed" as const,
  gapBeforeToken: "theme.spacing.none",
  gapBeforeWeight: 1,
  inputs: { id },
}));

function child(payload: DesignPreviewPayload): RenderableNode {
  const id = (JSON.parse(payload.designPreviewJson ?? "{}") as { id: string }).id;
  return {
    id,
    type: "group",
    frame: 0,
    box: { x: 0, y: 0, width: 20, height: 20 },
    children: [],
  };
}

function contract(direction: "down" | "up"): CollectionStackDesignContract {
  return {
    id: "collectionStack",
    distributionMode: "stacked",
    sizingMode: "fill",
    startGapToken: "theme.spacing.s",
    endGapToken: "theme.spacing.s",
    stackDirection: direction,
    stackOffsetToken: "theme.spacing.m",
    items,
  };
}

test("Collection Stack layers items in collection order with a downward offset", () => {
  const result = collectionStackComponentToRenderable(payload, contract("down"), child);
  assert.deepEqual(result.children?.map((item) => item.id), ["first", "second"]);
  assert.deepEqual(result.children?.map((item) => item.box), [
    { x: 170, y: 5, width: 20, height: 20 },
    { x: 170, y: 15, width: 20, height: 20 },
  ]);
});

test("Collection Stack anchors upward distribution at the end boundary", () => {
  const result = collectionStackComponentToRenderable(payload, contract("up"), child);
  assert.deepEqual(result.children?.map((item) => item.box), [
    { x: 170, y: 695, width: 20, height: 20 },
    { x: 170, y: 685, width: 20, height: 20 },
  ]);
});

test("Stacked distribution resolves to fit-content even when stale runtime sizing says fill", () => {
  const resolved = resolveCollectionStackComponent({
    ...payload,
    componentBaseConfigsJson: JSON.stringify({ presetTypes: {} }),
    designPreviewJson: JSON.stringify({
      distributionMode: "stacked",
      sizingMode: "fill",
      startGapToken: "theme.spacing.none",
      endGapToken: "theme.spacing.none",
      stackDirection: "down",
      stackOffsetToken: "theme.spacing.m",
      items: [],
    }),
  });
  assert.equal(resolved.sizingMode, "content");
});
