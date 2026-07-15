import assert from "node:assert/strict";
import test from "node:test";

import { resolveComponentStackComponent } from "../../src/desktop-preview/componentStackComponentResolver.js";
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
        alignment: "center",
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
