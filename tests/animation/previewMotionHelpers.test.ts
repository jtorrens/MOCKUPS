import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import type { ComponentMotionContract } from "../../src/desktop-preview/previewComponentContracts.js";
import {
  motionTotalDurationMs,
  requiredMotionContract,
} from "../../src/desktop-preview/previewMotionHelpers.js";

const motion: ComponentMotionContract = {
  transition: "slide",
  direction: "bottom",
  bounds: "screen",
  fade: false,
  translate: true,
  scale: false,
};

function payload(themeTokens: unknown): DesignPreviewPayload {
  return {
    themeTokensJson: JSON.stringify(themeTokens),
  } as DesignPreviewPayload;
}

test("Motion requires its exact document root", () => {
  assert.deepEqual(
    requiredMotionContract({ motion }, "motion", "component.motion"),
    motion,
  );
  assert.throws(
    () => requiredMotionContract({}, "motion", "component.motion"),
    /Missing object value component\.motion/,
  );
  assert.throws(
    () => requiredMotionContract({ motion: [] }, "motion", "component.motion"),
    /Missing object value component\.motion/,
  );
});

test("Motion timing requires its exact Theme transition path", () => {
  assert.equal(
    motionTotalDurationMs(payload({
      motion: {
        transitions: {
          slide: {
            durationMs: 300,
            delayMs: 20,
            easing: "linear",
            intensity: 1,
          },
        },
      },
    }), motion),
    320,
  );
  assert.throws(
    () => motionTotalDurationMs(payload({ motion: [] }), motion),
    /Missing object value theme\.motion/,
  );
  assert.throws(
    () => motionTotalDurationMs(payload({ motion: { transitions: [] } }), motion),
    /Missing object value theme\.motion\.transitions/,
  );
  assert.throws(
    () => motionTotalDurationMs(payload({ motion: { transitions: {} } }), motion),
    /Missing object value theme\.motion\.transitions\.slide/,
  );
});
