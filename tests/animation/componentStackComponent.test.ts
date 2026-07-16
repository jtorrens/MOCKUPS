import assert from "node:assert/strict";
import test from "node:test";

import { resolveComponentStackComponent } from "../../src/desktop-preview/componentStackComponentResolver.js";
import { componentStackComponentToRenderable } from "../../src/desktop-preview/componentStackComponentRenderable.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";

const motion = {
  transition: "slide",
  direction: "bottom",
  bounds: "screen",
  fade: false,
  translate: true,
  scale: false,
};

function alternative(id: string, presetId: string, active: boolean, behavior = "replace") {
  return {
    id,
    presetId,
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
    .map((item) => item.presetId)
    .filter((value): value is string => typeof value === "string" && value.length > 0);
  return {
    kind: "moduleInstance",
    componentType: "componentStack",
    componentBaseConfigsJson: JSON.stringify({
      presetTypes: Object.fromEntries(references.map((reference) => [reference, "stub"])),
      presets: Object.fromEntries(references.map((reference) => [reference, {}])),
    }),
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
    alternative("clock", "stub::preset::clock", false),
    alternative("password", "stub::preset::password", true),
    alternative("notification", "stub::preset::notification", true, "overlay"),
  ]));
  assert.deepEqual(resolved.slots[0]?.alternatives.map((item) => item.id), ["password", "notification"]);
});

test("an explicit empty Replace state clears the slot", () => {
  const resolved = resolveComponentStackComponent(payload([
    alternative("clock", "stub::preset::clock", false),
    alternative("empty", "", true),
  ]));
  assert.deepEqual(resolved.slots[0]?.alternatives.map((item) => item.id), ["empty"]);
  assert.equal(resolved.slots[0]?.alternatives[0]?.component, undefined);
});

test("Component Stack active tracks use each state stable id", () => {
  const source = payload([
    alternative("clock", "stub::preset::clock", false),
    alternative("password", "stub::preset::password", false),
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

test("Component Stack runtime state actions resolve the selected state and transition frame", () => {
  const source = payload([
    alternative("clock", "stub::preset::clock", false),
    alternative("password", "stub::preset::password", false),
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
  assert.equal(resolved.slots[0]?.alternatives[1]?.exitFrame, 10);
});

test("each Component Stack state resolves its own placement inside the assigned slot", () => {
  const states = [
    alternative("clock", "stub::preset::clock", false),
    {
      ...alternative("widget", "stub::preset::widget", true, "overlay"),
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
