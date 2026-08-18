import assert from "node:assert/strict";
import test from "node:test";

import { resolveBubbleComponent } from "../../src/desktop-preview/bubbleComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Bubble preserves the resolved Surface tail enabled value for every message direction", () => {
  const source = committedComponentFixture("bubble");
  const config = JSON.parse(source.configJson) as {
    bubble: { surfaceSlot: { overrides: Record<string, unknown> } };
  };
  config.bubble.surfaceSlot.overrides = {
    surface: { tail: { enabled: false } },
  };
  source.configJson = JSON.stringify(config);

  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  for (const state of ["incoming", "outgoing", "system"]) {
    preview.state = state;
    source.designPreviewJson = JSON.stringify(preview);
    assert.equal(resolveBubbleComponent(source).surface.tail.enabled, false, state);
  }
});
