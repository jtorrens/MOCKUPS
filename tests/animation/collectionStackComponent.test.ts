import assert from "node:assert/strict";
import test from "node:test";

import { collectionStackComponentToRenderable } from "../../src/desktop-preview/collectionStackComponentRenderable.js";
import type { CollectionStackDesignContract } from "../../src/desktop-preview/collectionStackComponentContract.js";
import { resolveCollectionStackComponent } from "../../src/desktop-preview/collectionStackComponentResolver.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import type { RenderableBox, RenderableNode } from "../../src/visual/renderable/types.js";

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
    motion: {
      reflowDurationMs: 240,
      reflowEasing: "ease-out",
      transitions: { slide: { durationMs: 240, delayMs: 0, easing: "linear", intensity: 1 } },
    },
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
  present: true,
  presenceMotion: {
    transition: "none" as const,
    direction: "bottom" as const,
    bounds: "parent" as const,
    fade: false,
    translate: false,
    scale: false,
  },
}));

function child(payload: DesignPreviewPayload, assignedBox?: RenderableBox): RenderableNode {
  const id = (JSON.parse(payload.designPreviewJson ?? "{}") as { id: string }).id;
  return {
    id,
    type: "group",
    frame: 0,
    box: assignedBox ?? { x: 0, y: 0, width: 20, height: 20 },
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
    itemSizingMode: "intrinsic",
    scaleRatio: 1,
    opacityRatio: 1,
    items,
  };
}

test("Collection Stack layers items in collection order with a downward offset", () => {
  const result = collectionStackComponentToRenderable(payload, contract("down"), child);
  assert.deepEqual(result.children?.map((item) => item.id), ["second", "first"]);
  assert.deepEqual(result.children?.map((item) => item.box), [
    { x: 170, y: 15, width: 20, height: 20 },
    { x: 170, y: 5, width: 20, height: 20 },
  ]);
});

test("Collection Stack anchors upward distribution at the end boundary", () => {
  const result = collectionStackComponentToRenderable(payload, contract("up"), child);
  assert.deepEqual(result.children?.map((item) => item.box), [
    { x: 170, y: 685, width: 20, height: 20 },
    { x: 170, y: 695, width: 20, height: 20 },
  ]);
});

test("Collection Stack uses the largest frame and applies deterministic depth ratios", () => {
  const configured = { ...contract("down"), itemSizingMode: "largest" as const, scaleRatio: 0.95, opacityRatio: 0.8 };
  const variableChild = (childPayload: DesignPreviewPayload, assignedBox?: RenderableBox): RenderableNode => {
    const id = (JSON.parse(childPayload.designPreviewJson ?? "{}") as { id: string }).id;
    return {
      id,
      type: "group",
      frame: 0,
      box: assignedBox ?? (id === "first"
        ? { x: 0, y: 0, width: 20, height: 15 }
        : { x: 0, y: 0, width: 30, height: 25 }),
      children: [],
    };
  };
  const result = collectionStackComponentToRenderable(payload, configured, variableChild);
  assert.deepEqual(result.children?.map((item) => item.box), [
    { x: 165, y: 15, width: 30, height: 25 },
    { x: 165, y: 5, width: 30, height: 25 },
  ]);
  assert.deepEqual(result.children?.map((item) => item.transform), [
    { scale: 0.95, opacity: 0.8 },
    { scale: 1, opacity: 1 },
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
      itemSizingMode: "intrinsic",
      scaleRatio: 1,
      opacityRatio: 1,
      items: [],
    }),
  });
  assert.equal(resolved.sizingMode, "content");
});

test("Present keeps an outgoing item through its exit and starts Reflow afterwards", () => {
  const item = (id: string) => ({
    id,
    presetId: `stub::preset::${id}`,
    overrides: {},
    inputs: { id },
    present: true,
    presenceMotion: {
      transition: "slide", direction: "bottom", bounds: "screen",
      fade: false, translate: true, scale: false,
    },
    alignment: "center",
    gapBeforeMode: "fixed",
    gapBeforeToken: "theme.spacing.none",
    gapBeforeWeight: 1,
  });
  const resolveAt = (frame: number) => resolveCollectionStackComponent({
    ...payload,
    localFrame: frame,
    componentBaseConfigsJson: JSON.stringify({
      presetTypes: { "stub::preset::first": "stub", "stub::preset::second": "stub" },
      presets: { "stub::preset::first": {}, "stub::preset::second": {} },
    }),
    instanceJson: JSON.stringify({
      context: { localFrame: frame },
      animation: { tracks: [{
        fieldId: "present",
        targetId: "first",
        keyframes: [{ frame: 0, value: true }, { frame: 10, value: false }],
      }] },
    }),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow",
      sizingMode: "content",
      startGapToken: "theme.spacing.none",
      endGapToken: "theme.spacing.none",
      stackDirection: "down",
      stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "intrinsic",
      scaleRatio: 1,
      opacityRatio: 1,
      items: [item("first"), item("second")],
    }),
  });
  const exiting = resolveAt(12);
  assert.equal(exiting.items.length, 2);
  assert.equal(exiting.items[0]?.exitFrame, 10);
  assert.equal(exiting.reflow, undefined);
  const reflowing = resolveAt(16);
  assert.deepEqual(reflowing.items.map(({ id }) => id), ["second"]);
  assert.deepEqual(reflowing.reflow?.fromItems.map(({ id }) => id), ["first", "second"]);
  assert.equal(reflowing.reflow?.progress, 0);
  assert.equal(resolveAt(22).reflow, undefined);
});

test("an embedded runtime state change supplies the previous item to Reflow", () => {
  const frame = 6;
  const resolved = resolveCollectionStackComponent({
    ...payload,
    localFrame: frame,
    componentBaseConfigsJson: JSON.stringify({
      presetTypes: { "stub::preset::notice": "stub" },
      presets: { "stub::preset::notice": {} },
    }),
    instanceJson: JSON.stringify({
      context: { localFrame: frame },
      animation: { tracks: [{
        fieldId: "displayMode",
        targetId: "notice",
        keyframes: [{ frame: 0, value: "summary" }, { frame: 5, value: "detail" }],
      }] },
    }),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow", sizingMode: "content",
      startGapToken: "theme.spacing.none", endGapToken: "theme.spacing.none",
      stackDirection: "down", stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "intrinsic", scaleRatio: 1, opacityRatio: 1,
      items: [{
        id: "notice", presetId: "stub::preset::notice", overrides: {},
        inputs: { displayMode: "summary" }, present: true,
        presenceMotion: { transition: "none", direction: "bottom", bounds: "parent", fade: false, translate: false, scale: false },
        alignment: "center", gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.none", gapBeforeWeight: 1,
      }],
    }),
  });
  assert.equal(resolved.items[0]?.inputs.displayMode, "detail");
  assert.equal(resolved.reflow?.fromItems[0]?.inputs.displayMode, "summary");
});
