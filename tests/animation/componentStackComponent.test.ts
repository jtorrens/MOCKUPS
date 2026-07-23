import assert from "node:assert/strict";
import test from "node:test";

import { resolveComponentStackComponent } from "../../src/desktop-preview/componentStackComponentResolver.js";
import { componentStackComponentToRenderable } from "../../src/desktop-preview/componentStackComponentRenderable.js";
import { componentVariantConfig } from "../../src/desktop-preview/componentPreviewDefaults.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";

test("Component Variant references require the exact stable full-reference grammar", () => {
  const bases = { variants: { "stub::variant::default": {} } };
  assert.deepEqual(componentVariantConfig(bases, "stub", "stub::variant::default"), {});
  for (const malformed of [
    "default",
    "stub::preset::default",
    "stub::variant::default::variant::extra",
    "stub with spaces::variant::default",
  ]) {
    assert.throws(() => componentVariantConfig(bases, "stub", malformed));
  }
  for (const invalidBases of [
    {},
    { variants: null },
    { variants: [] },
    { variants: { "stub::variant::default": null } },
    { variants: { "stub::variant::default": [] } },
    { variants: { "stub::variant::default": "default" } },
  ]) {
    assert.throws(() => componentVariantConfig(invalidBases, "stub", "stub::variant::default"));
  }
});

const motion = {
  transition: "slide",
  direction: "bottom",
  bounds: "screen",
  fade: false,
  translate: true,
  scale: false,
};

function alternative(id: string, variantReference: string, active: boolean, behavior = "replace") {
  return {
    id,
    variantReference,
    overrides: {},
    inputs: { id },
    active,
    behavior,
    placement: { mode: "center", alignX: 0.5, alignY: 0.5, offsetX: 0, offsetY: 0 },
    enterMotion: motion,
    exitMotion: motion,
  };
}

function payload(alternatives: Record<string, unknown>[], frame = 0): DesignPreviewPayload {
  const references = alternatives
    .map((item) => item.variantReference)
    .filter((value): value is string => typeof value === "string" && value.length > 0);
  return {
    kind: "moduleInstance",
    componentType: "componentStack",
    componentBaseConfigsJson: JSON.stringify({
      variantTypes: Object.fromEntries(references.map((reference) => [reference, "stub"])),
      variants: Object.fromEntries(references.map((reference) => [reference, {}])),
    }),
    appConfigJson: "{}",
    instanceJson: JSON.stringify({ context: { localFrame: frame }, animation: { tracks: [] } }),
    frameRate: 25,
    localFrame: frame,
    configJson: "{}",
    designPreviewJson: JSON.stringify({
      sizingMode: "fill",
      startGapToken: "theme.spacing.none",
      endGapToken: "theme.spacing.none",
      items: [{
        id: "central",
        gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.none",
        gapBeforeWeight: 1,
        alternatives,
      }],
    }),
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
      spacing: { none: 0 },
      motion: {
        transitions: {
          slide: { durationMs: 240, delayMs: 0, easing: "linear", intensity: 1 },
        },
      },
    }),
  };
}

test("Component Stack resolves ordered Replace and Overlay states deterministically", () => {
  const resolved = resolveComponentStackComponent(payload([
    alternative("clock", "stub::variant::clock", false),
    alternative("password", "stub::variant::password", true),
    alternative("notification", "stub::variant::notification", true, "overlay"),
  ]));
  assert.deepEqual(resolved.slots[0]?.alternatives.map((item) => item.id), ["password", "notification"]);
});

test("Component Stack rejects malformed slot, State and Overrides documents", () => {
  const source = payload([alternative("clock", "stub::variant::clock", true)]);
  const original = JSON.parse(source.designPreviewJson) as { items: Record<string, unknown>[] };
  const resolveItems = (items: unknown) => resolveComponentStackComponent({
    ...source,
    designPreviewJson: JSON.stringify({ ...original, items }),
  });
  const slot = original.items[0]!;
  const state = (slot.alternatives as Record<string, unknown>[])[0]!;
  for (const [label, slotItems] of [
    ["null slots", null],
    ["wrong slots root", {}],
    ["non-object slot", [null]],
    ["null States", [{ ...slot, alternatives: null }]],
    ["wrong States root", [{ ...slot, alternatives: {} }]],
    ["non-object State", [{ ...slot, alternatives: [null] }]],
    ["wrong State Overrides root", [{ ...slot, alternatives: [{ ...state, overrides: [] }] }]],
  ] as const) {
    assert.throws(() => resolveItems(slotItems), label);
  }
});

test("Component Stack distinguishes absent instance members from invalid present roots", () => {
  const source = payload([alternative("clock", "stub::variant::clock", true)]);
  assert.doesNotThrow(() => resolveComponentStackComponent({ ...source, instanceJson: "{}" }));
  for (const instance of [
    { context: null },
    { context: [] },
    { animation: null },
    { animation: [] },
  ]) {
    assert.throws(() => resolveComponentStackComponent({
      ...source,
      instanceJson: JSON.stringify(instance),
    }));
  }
});

test("Component Stack consumes one exact transient animation document", () => {
  const source = payload([alternative("clock", "stub::variant::clock", true)]);
  for (const animation of [
    { tracks: null },
    { tracks: [null] },
    { tracks: [{ fieldId: "active", targetId: "clock", keyframes: null }] },
    { tracks: [{ fieldId: "active", targetId: "clock", keyframes: [null] }] },
    { tracks: [{ fieldId: "active", targetId: "clock", keyframes: [{ frame: "0", value: true }] }] },
    { tracks: [{ fieldId: "active", targetId: "clock", keyframes: [{ frame: 0, value: true, enabled: "true" }] }] },
    { tracks: [{ fieldId: "active", targetId: "clock", keyframes: [{ frame: 0 }] }] },
    { tracks: [{ fieldId: "active", targetId: "clock", keyframes: [{ frame: 0, value: true, interpolation: "unknown" }] }] },
  ]) {
    assert.throws(() => resolveComponentStackComponent({
      ...source,
      instanceJson: JSON.stringify({ animation }),
    }));
  }
});

test("an explicit empty Replace state clears the slot", () => {
  const resolved = resolveComponentStackComponent(payload([
    alternative("clock", "stub::variant::clock", false),
    alternative("empty", "", true),
  ]));
  assert.deepEqual(resolved.slots[0]?.alternatives.map((item) => item.id), ["empty"]);
  assert.equal(resolved.slots[0]?.alternatives[0]?.component, undefined);
});

test("Component Stack active tracks use each state stable id", () => {
  const source = payload([
    alternative("clock", "stub::variant::clock", false),
    alternative("password", "stub::variant::password", false),
  ], 12);
  source.instanceJson = JSON.stringify({
    context: { localFrame: 12 },
    animation: {
      tracks: [{
        fieldId: "active",
        targetId: "password",
        keyframes: [{ frame: 0, value: false }, { frame: 10, value: true }],
      }],
    },
  });
  const resolved = resolveComponentStackComponent(source);
  assert.deepEqual(resolved.slots[0]?.alternatives.map((item) => item.id), ["password", "clock"]);
  assert.equal(resolved.slots[0]?.alternatives[0]?.activationFrame, 10);
  assert.equal(resolved.slots[0]?.alternatives[1]?.exitFrame, 10);
});

test("Component Stack runtime transition frames use explicit action time when owner frame is static", () => {
  const source = payload([
    alternative("clock", "stub::variant::clock", false),
    alternative("password", "stub::variant::password", false),
  ]);
  const preview = JSON.parse(source.designPreviewJson ?? "{}") as { items: Record<string, unknown>[] };
  Object.assign(preview.items[0]!, {
    runtimeStateId: "password",
    runtimeStateFromId: "clock",
    runtimeStateTransition: true,
    runtimeStateElapsedMs: 120,
  });
  source.designPreviewJson = JSON.stringify(preview);

  const resolved = resolveComponentStackComponent(source);
  const renderable = componentStackComponentToRenderable(source, resolved, (child) => ({
    id: JSON.parse(child.designPreviewJson ?? "{}").id ?? "stub",
    type: "group",
    frame: 0,
    box: { x: 100, y: 100, width: 20, height: 10 },
    children: [],
  }));
  const entering = renderable.children?.[0]?.children?.[0];
  const outgoing = renderable.children?.[0]?.children?.[1];
  assert.equal(entering?.id, "password.motion");
  assert.equal(outgoing?.id, "clock.exit-motion");
  assert.notEqual(entering?.children?.[0]?.transform?.y, 710);
  assert.notEqual(outgoing?.children?.[0]?.transform?.y, 0);
});

test("Component Stack runtime state actions resolve the selected state and transition frame", () => {
  const source = payload([
    alternative("clock", "stub::variant::clock", false),
    alternative("password", "stub::variant::password", false),
  ], 12);
  const preview = JSON.parse(source.designPreviewJson ?? "{}") as { items: Record<string, unknown>[] };
  Object.assign(preview.items[0]!, {
    runtimeStateId: "password",
    runtimeStateFromId: "clock",
    runtimeStateTransition: true,
    runtimeStateElapsedMs: 80,
  });
  source.designPreviewJson = JSON.stringify(preview);

  const resolved = resolveComponentStackComponent(source);
  assert.deepEqual(resolved.slots[0]?.alternatives.map((item) => item.id), ["password", "clock"]);
  assert.equal(resolved.slots[0]?.alternatives[0]?.activationFrame, 10);
  assert.equal(resolved.slots[0]?.alternatives[0]?.enterElapsedMs, 80);
  assert.equal(resolved.slots[0]?.alternatives[1]?.exitFrame, 10);
  assert.equal(resolved.slots[0]?.alternatives[1]?.exitElapsedMs, 80);
});

test("Component Stack animatable State keyframes derive the outgoing state and transition clock", () => {
  const source = payload([
    alternative("clock", "stub::variant::clock", false),
    alternative("password", "stub::variant::password", false),
  ], 12);
  source.instanceJson = JSON.stringify({
    context: { localFrame: 12 },
    animation: {
      tracks: [{
        fieldId: "runtimeStateId",
        targetId: "central",
        keyframes: [
          { frame: 0, value: "clock", interpolation: "hold" },
          { frame: 10, value: "password", interpolation: "hold" },
        ],
      }],
    },
  });

  const resolved = resolveComponentStackComponent(source);
  assert.deepEqual(resolved.slots[0]?.alternatives.map((item) => item.id), ["password", "clock"]);
  assert.equal(resolved.slots[0]?.alternatives[0]?.enterElapsedMs, 80);
  assert.equal(resolved.slots[0]?.alternatives[1]?.exitElapsedMs, 80);
});

test("each Component Stack state resolves its own placement inside the assigned slot", () => {
  const states = [
    alternative("clock", "stub::variant::clock", false),
    {
      ...alternative("widget", "stub::variant::widget", true, "overlay"),
      placement: { mode: "insideEdge", alignX: 1, alignY: 0.5, offsetX: 0, offsetY: 0 },
    },
  ];
  const source = payload(states);
  const resolved = resolveComponentStackComponent(source);
  const renderable = componentStackComponentToRenderable(source, resolved, (child) => ({
    id: child.componentType ?? "stub",
    type: "group",
    frame: 0,
    box: { x: 100, y: 100, width: 20, height: 10 },
    children: [],
  }));
  const slot = renderable.children?.[0];
  assert.equal(slot?.children?.[0]?.box?.x, 170);
  assert.equal(slot?.children?.[1]?.box?.x, 340);
  assert.equal(slot?.children?.[0]?.box?.y, 0);
  assert.equal(slot?.children?.[1]?.box?.y, 0);
});

test("independent slots keep their occupied flow space when a later slot changes state", () => {
  const source = payload([alternative("clock", "stub::variant::clock", false)]);
  const preview = JSON.parse(source.designPreviewJson ?? "{}") as { items: Record<string, unknown>[] };
  preview.items = [
    {
      ...preview.items[0],
      id: "clock-slot",
      alternatives: [alternative("clock", "stub::variant::clock", false)],
    },
    {
      id: "password-slot",
      gapBeforeMode: "fixed",
      gapBeforeToken: "theme.spacing.gap",
      gapBeforeWeight: 1,
      runtimeStateId: "password",
      alternatives: [
        alternative("empty", "", false),
        alternative("password", "stub::variant::password", false),
      ],
    },
  ];
  source.designPreviewJson = JSON.stringify(preview);
  source.componentBaseConfigsJson = JSON.stringify({
    variantTypes: {
      "stub::variant::clock": "stub",
      "stub::variant::password": "stub",
    },
    variants: {
      "stub::variant::clock": {},
      "stub::variant::password": {},
    },
  });
  source.themeTokensJson = JSON.stringify({
    spacing: { none: 0, gap: 20 },
    motion: {
      transitions: {
        slide: { durationMs: 240, delayMs: 0, easing: "linear", intensity: 1 },
      },
    },
  });

  const resolved = resolveComponentStackComponent(source);
  const renderable = componentStackComponentToRenderable(source, resolved, (child, assignedBox) => {
    if (child.designPreviewJson?.includes("password")) {
      return {
        id: "password",
        type: "group",
        frame: 0,
        box: assignedBox ?? { x: 0, y: 0, width: 360, height: 720 },
        children: [],
      };
    }
    return {
      id: "clock",
      type: "group",
      frame: 0,
      box: { x: 100, y: 100, width: 160, height: 80 },
      children: [],
    };
  });

  const clockSlot = renderable.children?.[0]?.box;
  const passwordSlot = renderable.children?.[1]?.box;
  assert.deepEqual(clockSlot, { x: 0, y: 0, width: 360, height: 80 });
  assert.deepEqual(passwordSlot, { x: 0, y: 100, width: 360, height: 620 });
});
