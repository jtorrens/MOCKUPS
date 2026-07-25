import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import type { ComponentMotionContract } from "../../src/desktop-preview/previewComponentContracts.js";
import {
  easingProgress,
  motionTotalDurationMs,
  requiredMotionContract,
  resolveMotionFrame,
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

test("Motion frame resolution owns trigger, delay, easing, completion and reverse", () => {
  const source = payload({
    motion: {
      transitions: {
        slide: {
          durationMs: 200,
          delayMs: 40,
          easing: "linear",
          intensity: 1,
        },
      },
    },
  });
  assert.deepEqual(
    resolveMotionFrame(source, motion, { trigger: false, elapsedMs: 100 }),
    { active: false, progress: 1 },
  );
  assert.deepEqual(
    resolveMotionFrame(source, motion, {
      trigger: true,
      elapsedMs: 20,
      reverse: true,
    }),
    { active: true, progress: 0, reverse: true },
  );
  assert.equal(
    resolveMotionFrame(source, motion, { trigger: true, elapsedMs: 140 }).progress,
    0.5,
  );
  assert.equal(
    resolveMotionFrame(source, motion, { trigger: true, elapsedMs: 240 }).progress,
    1,
  );
});

test("Motion none is inert unless fade is explicitly enabled", () => {
  const source = payload({
    motion: {
      transitions: {
        fade: {
          durationMs: 100,
          delayMs: 0,
          easing: "linear",
          intensity: 1,
        },
      },
    },
  });
  const none = { ...motion, transition: "none" as const, fade: false };
  const fade = { ...none, fade: true };
  assert.equal(motionTotalDurationMs(source, none), 0);
  assert.equal(
    resolveMotionFrame(source, none, { trigger: true, elapsedMs: 20 }).progress,
    1,
  );
  assert.equal(motionTotalDurationMs(source, fade), 100);
  assert.equal(
    resolveMotionFrame(source, fade, { trigger: true, elapsedMs: 50 }).progress,
    0.5,
  );
});

test("every supported easing preserves endpoints and rejects unknown values", () => {
  for (const easing of [
    "linear",
    "ease-in",
    "ease-out",
    "ease",
    "spring",
    "bounce",
  ]) {
    assert.equal(easingProgress(easing, 0, 1), 0);
    assert.equal(easingProgress(easing, 1, 1), 1);
    assert.ok(Number.isFinite(easingProgress(easing, 0.5, 1)));
  }
  assert.equal(easingProgress("spring", 0.5, 0), 0.5);
  assert.equal(easingProgress("bounce", 0.5, 0), 0.5);
  assert.throws(() => easingProgress("unknown", 0.5, 1), /Unsupported easing/);
});
