import assert from "node:assert/strict";
import test from "node:test";

import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { resolveNotificationsComponent } from "../../src/desktop-preview/notificationsComponentResolver.js";

const motion = {
  transition: "slide",
  direction: "bottom",
  bounds: "screen",
  fade: false,
  translate: true,
  scale: false,
};

function notificationsConfig() {
  return {
    notifications: {
      collectionStackSlot: { variantReference: "stub.stack::variant::default", overrides: {} },
      notificationSlot: { variantReference: "stub.notification::variant::default", overrides: {} },
      notificationInputs: {},
      badgeSlot: { variantReference: "stub.badge::variant::default", overrides: {} },
      badgeInputs: {},
      closedItemLimit: 3,
      distributionMotion: motion,
      sizingMode: "content",
      startGapToken: "theme.spacing.none",
      endGapToken: "theme.spacing.none",
      stackDirection: "down",
      stackOffsetToken: "theme.spacing.m",
      itemSizingMode: "largest",
      scaleRatio: 0.95,
      opacityRatio: 0.6,
      showBadge: false,
      itemAlignment: "center",
      itemGapBeforeMode: "fixed",
      itemGapBeforeToken: "theme.spacing.m",
      itemGapBeforeWeight: 1,
      itemPresenceMotion: motion,
    },
  };
}

function payload(): DesignPreviewPayload {
  return {
    kind: "componentClass",
    componentType: "notifications",
    componentBaseConfigsJson: JSON.stringify({
      variantTypes: {},
      variants: {
        "stub.stack::variant::default": {},
        "stub.notification::variant::default": {},
        "stub.badge::variant::default": {},
      },
    }),
    appConfigJson: "{}",
    instanceJson: "{}",
    frameRate: 25,
    localFrame: 0,
    configJson: JSON.stringify(notificationsConfig()),
    designPreviewJson: JSON.stringify({ distributionMode: "stacked", items: [] }),
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
      spacing: { none: 0, m: 8 },
      motion: {
        reflowDurationMs: 240,
        reflowEasing: "linear",
        transitions: { slide: { durationMs: 240, delayMs: 0, easing: "linear", intensity: 1 } },
      },
    }),
  };
}

test("Notifications requires exact config, slots, Inputs and Overrides objects", () => {
  assert.doesNotThrow(() => resolveNotificationsComponent(payload()));
  for (const [label, mutate] of [
    ["notifications", (config: Record<string, unknown>) => { config.notifications = null; }],
    ["collectionStackSlot", (config: Record<string, unknown>) => {
      (config.notifications as Record<string, unknown>).collectionStackSlot = [];
    }],
    ["collectionStack Overrides", (config: Record<string, unknown>) => {
      ((config.notifications as Record<string, unknown>).collectionStackSlot as Record<string, unknown>).overrides = null;
    }],
    ["notificationSlot", (config: Record<string, unknown>) => {
      (config.notifications as Record<string, unknown>).notificationSlot = null;
    }],
    ["notification Overrides", (config: Record<string, unknown>) => {
      ((config.notifications as Record<string, unknown>).notificationSlot as Record<string, unknown>).overrides = [];
    }],
    ["notificationInputs", (config: Record<string, unknown>) => {
      (config.notifications as Record<string, unknown>).notificationInputs = [];
    }],
    ["badgeSlot", (config: Record<string, unknown>) => {
      (config.notifications as Record<string, unknown>).badgeSlot = null;
    }],
    ["badge Overrides", (config: Record<string, unknown>) => {
      ((config.notifications as Record<string, unknown>).badgeSlot as Record<string, unknown>).overrides = [];
    }],
    ["badgeInputs", (config: Record<string, unknown>) => {
      (config.notifications as Record<string, unknown>).badgeInputs = null;
    }],
  ] as const) {
    const source = payload();
    const config = notificationsConfig() as Record<string, unknown>;
    mutate(config);
    assert.throws(() => resolveNotificationsComponent({
      ...source,
      configJson: JSON.stringify(config),
    }), label);
  }
});

test("Notifications requires an exact runtime item array", () => {
  const source = payload();
  for (const [label, items] of [
    ["missing", undefined],
    ["null", null],
    ["object", {}],
    ["non-object entry", [null]],
  ] as const) {
    assert.throws(() => resolveNotificationsComponent({
      ...source,
      designPreviewJson: JSON.stringify({ distributionMode: "stacked", ...(items === undefined ? {} : { items }) }),
    }), label);
  }
});
