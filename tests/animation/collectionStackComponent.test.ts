import assert from "node:assert/strict";
import test from "node:test";

import { collectionStackComponentToRenderable } from "../../src/desktop-preview/collectionStackComponentRenderable.js";
import { interpolateComponentCollectionReflow } from "../../src/desktop-preview/componentCollectionRenderableCommon.js";
import type { CollectionStackDesignContract } from "../../src/desktop-preview/collectionStackComponentContract.js";
import { resolveCollectionStackComponent } from "../../src/desktop-preview/collectionStackComponentResolver.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import type { RenderableBox, RenderableNode } from "../../src/visual/renderable/types.js";

const payload: DesignPreviewPayload = {
  kind: "componentClass",
  componentType: "collectionStack",
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
    scaleToPixels: 1,
  },
  themeMode: "light",
  themeTokensJson: JSON.stringify({
    modes: { light: {} },
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
  variantReference: `stub::variant::${id}`,
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
    componentBaseConfigsJson: JSON.stringify({ variantTypes: {} }),
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

test("Collection Stack rejects malformed item and Overrides documents", () => {
  const item = {
    id: "notice", variantReference: "stub::variant::notice", overrides: {}, inputs: {}, present: true,
    presenceMotion: { transition: "none", direction: "bottom", bounds: "parent", fade: false, translate: false, scale: false },
    alignment: "center", gapBeforeMode: "fixed",
    gapBeforeToken: "theme.spacing.none", gapBeforeWeight: 1,
  };
  const resolve = (runtimeItems: unknown) => resolveCollectionStackComponent({
    ...payload,
    componentBaseConfigsJson: JSON.stringify({
      variantTypes: { "stub::variant::notice": "stub" },
      variants: { "stub::variant::notice": {} },
    }),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow", sizingMode: "content",
      startGapToken: "theme.spacing.none", endGapToken: "theme.spacing.none",
      stackDirection: "down", stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "intrinsic", scaleRatio: 1, opacityRatio: 1,
      items: runtimeItems,
    }),
  });
  for (const [label, runtimeItems] of [
    ["null items", null],
    ["wrong items root", {}],
    ["non-object item", [null]],
    ["wrong Overrides root", [{ ...item, overrides: [] }]],
  ] as const) {
    assert.throws(() => resolve(runtimeItems), label);
  }
});

test("Present keeps an outgoing item through its exit and starts Reflow afterwards", () => {
  const item = (id: string) => ({
    id,
    variantReference: `stub::variant::${id}`,
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
      variantTypes: { "stub::variant::first": "stub", "stub::variant::second": "stub" },
      variants: { "stub::variant::first": {}, "stub::variant::second": {} },
    }),
    instanceJson: JSON.stringify({
      context: { screenFrame: frame },
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
      variantTypes: { "stub::variant::notice": "stub" },
      variants: { "stub::variant::notice": {} },
    }),
    instanceJson: JSON.stringify({
      context: { screenFrame: frame },
      animation: { tracks: [{
        fieldId: "display_mode_field",
        targetId: "notice",
        keyframes: [{ frame: 0, value: "summary" }, { frame: 5, value: "detail" }],
      }, {
        fieldId: "undeclaredMode",
        targetId: "notice",
        keyframes: [{ frame: 0, value: "base" }, { frame: 5, value: "changed" }],
      }, {
        fieldId: "forwarded_mode_field",
        targetId: "notice",
        keyframes: [{ frame: 0, value: "base" }, { frame: 5, value: "changed" }],
      }] },
    }),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow", sizingMode: "content",
      startGapToken: "theme.spacing.none", endGapToken: "theme.spacing.none",
      stackDirection: "down", stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "intrinsic", scaleRatio: 1, opacityRatio: 1,
      items: [{
        id: "notice", variantReference: "stub::variant::notice", overrides: {},
        inputs: {
          inputs: [{ id: "display_mode_field", jsonKey: "displayMode", valueKind: "OptionToken" }],
          displayMode: "summary",
          undeclaredMode: "base",
          forwardedMode: "base",
          __runtimeFieldIds: { forwardedMode: "forwarded_mode_field" },
        }, present: true,
        presenceMotion: { transition: "none", direction: "bottom", bounds: "parent", fade: false, translate: false, scale: false },
        alignment: "center", gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.none", gapBeforeWeight: 1,
      }],
    }),
  });
  assert.equal(resolved.items[0]?.inputs.displayMode, "detail");
  assert.equal(resolved.items[0]?.inputs.undeclaredMode, "base");
  assert.equal(resolved.items[0]?.inputs.forwardedMode, "changed");
  assert.equal(resolved.reflow?.fromItems[0]?.inputs.displayMode, "summary");
});

test("embedded Runtime field identities reject malformed or inferred metadata", () => {
  const resolve = (inputs: Record<string, unknown>) => resolveCollectionStackComponent({
    ...payload,
    componentBaseConfigsJson: JSON.stringify({
      variantTypes: { "stub::variant::notice": "stub" },
      variants: { "stub::variant::notice": {} },
    }),
    instanceJson: JSON.stringify({ context: { screenFrame: 0 }, animation: { tracks: [] } }),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow", sizingMode: "content",
      startGapToken: "theme.spacing.none", endGapToken: "theme.spacing.none",
      stackDirection: "down", stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "intrinsic", scaleRatio: 1, opacityRatio: 1,
      items: [{
        id: "notice", variantReference: "stub::variant::notice", overrides: {}, inputs, present: true,
        presenceMotion: { transition: "none", direction: "bottom", bounds: "parent", fade: false, translate: false, scale: false },
        alignment: "center", gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.none", gapBeforeWeight: 1,
      }],
    }),
  });
  const malformed: [string, Record<string, unknown>][] = [
    ["wrong definitions root", { inputs: {} }],
    ["non-object definition", { inputs: [null] }],
    ["missing id", { inputs: [{ jsonKey: "displayMode" }], displayMode: "summary" }],
    ["missing jsonKey", { inputs: [{ id: "displayMode" }], displayMode: "summary" }],
    ["duplicate id", { inputs: [{ id: "one", jsonKey: "one" }, { id: "one", jsonKey: "two" }] }],
    ["duplicate jsonKey", { inputs: [{ id: "one", jsonKey: "same" }, { id: "two", jsonKey: "same" }] }],
    ["wrong field map root", { inputs: [{ id: "displayMode", jsonKey: "displayMode" }], __runtimeFieldIds: [] }],
    ["blank mapped id", { inputs: [{ id: "displayMode", jsonKey: "displayMode" }], __runtimeFieldIds: { displayMode: "" } }],
    ["mapped key without value", { inputs: [{ id: "displayMode", jsonKey: "displayMode" }], __runtimeFieldIds: { unknown: "owner.field" } }],
  ];
  for (const [label, inputs] of malformed) assert.throws(() => resolve(inputs), label);
});

test("a forwarded embedded action derives its requested-frame clock from the stable field track", () => {
  const resolveAt = (frame: number, duration: number | string = 4) => resolveCollectionStackComponent({
    ...payload,
    localFrame: frame,
    frameRate: 30,
    componentBaseConfigsJson: JSON.stringify({
      variantTypes: { "stub::variant::notice": "stub" },
      variants: { "stub::variant::notice": {} },
    }),
    instanceJson: JSON.stringify({
      context: { screenFrame: frame },
      animation: { tracks: [{
        fieldId: "forwarded.notice.play",
        targetId: "notice",
        keyframes: [{ frame: 0, value: false }, { frame: 5, value: true }],
      }] },
    }),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow", sizingMode: "content",
      startGapToken: "theme.spacing.none", endGapToken: "theme.spacing.none",
      stackDirection: "down", stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "intrinsic", scaleRatio: 1, opacityRatio: 1,
      items: [{
        id: "notice", variantReference: "stub::variant::notice", overrides: {},
        inputs: {
          inputs: [
            { id: "play", jsonKey: "play", valueKind: "Boolean" },
            { id: "duration", jsonKey: "duration", valueKind: "Number" },
          ],
          actions: [{
            id: "playOnce", label: "Play", playInputId: "play", timeJsonKey: "actionFrame",
            durationInputId: "duration", timeUnit: "frames", completionBehavior: "holdFinal",
          }],
          __runtimeFieldIds: { play: "forwarded.notice.play" },
          play: false, actionFrame: 0, duration,
        },
        present: true,
        presenceMotion: { transition: "none", direction: "bottom", bounds: "parent", fade: false, translate: false, scale: false },
        alignment: "center", gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.none", gapBeforeWeight: 1,
      }],
    }),
  }).items[0]?.inputs;

  assert.equal(resolveAt(7)?.play, true);
  assert.equal(resolveAt(7)?.actionFrame, 2);
  assert.equal(resolveAt(12)?.play, true);
  assert.equal(resolveAt(12)?.actionFrame, 4);
  assert.throws(() => resolveAt(7, "4"));
  assert.throws(() => resolveAt(7, 0));
});

test("embedded Runtime actions reject filtered entries and inferred play identities", () => {
  const validAction: Record<string, unknown> = {
    id: "playOnce", label: "Play", playInputId: "play", timeJsonKey: "actionFrame",
    durationInputId: "duration", timeUnit: "frames", completionBehavior: "holdFinal",
  };
  const without = (key: string) => {
    const action = { ...validAction };
    delete action[key];
    return [action];
  };
  const resolve = ({
    actions = [validAction],
    definitions = [
      { id: "play", jsonKey: "play", valueKind: "Boolean" },
      { id: "duration", jsonKey: "duration", valueKind: "Number" },
    ],
    fieldIds,
    trackValue = true,
  }: {
    actions?: unknown;
    definitions?: unknown;
    fieldIds?: unknown;
    trackValue?: unknown;
  }) => resolveCollectionStackComponent({
    ...payload,
    localFrame: 2,
    componentBaseConfigsJson: JSON.stringify({
      variantTypes: { "stub::variant::notice": "stub" },
      variants: { "stub::variant::notice": {} },
    }),
    instanceJson: JSON.stringify({
      context: { screenFrame: 2 },
      animation: { tracks: [{
        fieldId: typeof fieldIds === "object" && fieldIds !== null && !Array.isArray(fieldIds)
          ? (fieldIds as Record<string, unknown>).play ?? "play"
          : "play",
        targetId: "notice",
        keyframes: [{ frame: 0, value: trackValue }],
      }] },
    }),
    designPreviewJson: JSON.stringify({
      distributionMode: "flow", sizingMode: "content",
      startGapToken: "theme.spacing.none", endGapToken: "theme.spacing.none",
      stackDirection: "down", stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "intrinsic", scaleRatio: 1, opacityRatio: 1,
      items: [{
        id: "notice", variantReference: "stub::variant::notice", overrides: {},
        inputs: {
          inputs: definitions,
          actions,
          ...(fieldIds === undefined ? {} : { __runtimeFieldIds: fieldIds }),
          play: false,
          actionFrame: 0,
          duration: 4,
        },
        present: true,
        presenceMotion: { transition: "none", direction: "bottom", bounds: "parent", fade: false, translate: false, scale: false },
        alignment: "center", gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.none", gapBeforeWeight: 1,
      }],
    }),
  });

  const malformed: [string, Parameters<typeof resolve>[0]][] = [
    ["null actions", { actions: null }],
    ["wrong actions root", { actions: {} }],
    ["non-object action", { actions: [null] }],
    ["missing id", { actions: without("id") }],
    ["missing label", { actions: without("label") }],
    ["missing play input", { actions: without("playInputId") }],
    ["missing time key", { actions: without("timeJsonKey") }],
    ["missing time unit", { actions: without("timeUnit") }],
    ["missing completion", { actions: without("completionBehavior") }],
    ["duplicate action id", { actions: [validAction, { ...validAction }] }],
    ["unknown time unit", { actions: [{ ...validAction, timeUnit: "ticks" }] }],
    ["unknown completion", { actions: [{ ...validAction, completionBehavior: "loop" }] }],
    ["null explicit play field", { actions: [{ ...validAction, playFieldId: null }] }],
    ["conflicting forwarded play ids", {
      actions: [{ ...validAction, playFieldId: "other.play" }],
      fieldIds: { play: "owner.play" },
    }],
    ["play value without identity", {
      definitions: [{ id: "duration", jsonKey: "duration", valueKind: "Number" }],
    }],
    ["non-boolean animated play value", { trackValue: "true" }],
  ];
  for (const [label, options] of malformed) assert.throws(() => resolve(options), label);
});

test("Reflow interpolates the complete stable child geometry instead of scaling its frame", () => {
  const [item] = items;
  const from: RenderableNode = {
    id: "collectionStack", type: "group", frame: 0,
    box: { x: 100, y: 200, width: 100, height: 40 },
    children: [{
      id: "component.notification", type: "group", frame: 0,
      box: { x: 100, y: 200, width: 100, height: 40 },
      children: [{
        id: "component.notification.surface", type: "surface", frame: 0,
        box: { x: 100, y: 200, width: 100, height: 40 },
      }],
    }],
  };
  const to: RenderableNode = {
    id: "collectionStack", type: "group", frame: 0,
    box: { x: 80, y: 170, width: 140, height: 100 },
    children: [{
      id: "component.notification", type: "group", frame: 0,
      box: { x: 80, y: 170, width: 140, height: 100 },
      children: [{
        id: "component.notification.surface", type: "surface", frame: 0,
        box: { x: 80, y: 170, width: 140, height: 100 },
      }],
    }],
  };
  const result = interpolateComponentCollectionReflow(from, [item!], to, [item!], 0.5);
  assert.deepEqual(result.box, { x: 90, y: 185, width: 120, height: 70 });
  assert.deepEqual(result.children?.[0]?.box, { x: 90, y: 185, width: 120, height: 70 });
  assert.deepEqual(result.children?.[0]?.children?.[0]?.box, { x: 90, y: 185, width: 120, height: 70 });
  assert.equal(result.children?.[0]?.transform?.scale, undefined);
});
