import assert from "node:assert/strict";
import test from "node:test";
import { naturalWriteOnFrame, resolveBehaviorTimingFrames } from "../../src/desktop-preview/behaviorTiming.js";

const fields = [
  { id: "text", jsonKey: "text" },
  {
    id: "writeOn",
    jsonKey: "writeOnTiming",
    valueKind: "BehaviorTiming",
    naturalTiming: { sourceFieldId: "text", unit: "grapheme", baseFramesPerUnit: 7 },
  },
];

test("Natural Pace multiplies the module-owned base duration", () => {
  const owner = {
    text: "12345678901234567890123456789012345678901234567890",
    writeOnTiming: { mode: "natural", fixedFrames: 12, paceToken: "theme.motion.naturalPace.slow" },
  };
  assert.equal(resolveBehaviorTimingFrames(owner, fields[1]!, fields, {
    motion: { naturalPace: { slow: 1.5 } },
  }), 525);
});

test("Behavior Timing rejects inferred values and incomplete natural metadata", () => {
  const theme = { motion: { naturalPace: { slow: 1.5 } } };
  const validOwner = {
    text: "abc",
    writeOnTiming: { mode: "natural", fixedFrames: 12, paceToken: "theme.motion.naturalPace.slow" },
  };
  const resolve = (owner: Record<string, unknown>, definition: Record<string, unknown>) =>
    resolveBehaviorTimingFrames(owner, definition, fields, theme);

  assert.throws(() => resolve({ ...validOwner, writeOnTiming: null }, fields[1]!));
  assert.throws(() => resolve({ ...validOwner, writeOnTiming: { mode: "fixed", paceToken: "theme.motion.naturalPace.slow" } }, fields[1]!));
  assert.throws(() => resolve({ ...validOwner, writeOnTiming: { mode: "fixed", fixedFrames: "12", paceToken: "theme.motion.naturalPace.slow" } }, fields[1]!));
  assert.throws(() => resolve({ ...validOwner, writeOnTiming: { mode: "fixed", fixedFrames: -1, paceToken: "theme.motion.naturalPace.slow" } }, fields[1]!));
  assert.throws(() => resolve({ ...validOwner, writeOnTiming: { mode: "fixed", fixedFrames: 12, paceToken: "theme.motion.other" } }, fields[1]!));
  assert.throws(() => resolve({ ...validOwner, writeOnTiming: { mode: "fixed", fixedFrames: 12, paceToken: "theme.motion.naturalPace.missing" } }, fields[1]!));
  assert.throws(() => resolve({ ...validOwner, writeOnTiming: { mode: "natural", fixedFrames: 12, paceToken: "theme.motion.naturalPace.missing" } }, fields[1]!));
  assert.throws(() => resolve(validOwner, { ...fields[1]!, naturalTiming: { sourceFieldId: "text", unit: "grapheme" } }));
  assert.throws(() => resolve(validOwner, { ...fields[1]!, naturalTiming: { sourceFieldId: "text", unit: "grapheme", baseFramesPerUnit: 0 } }));
  assert.throws(() => resolve(validOwner, { ...fields[1]!, naturalTiming: { sourceFieldId: "missing", unit: "grapheme", baseFramesPerUnit: 7 } }));
});

test("natural Write On is deterministic, monotonic and keeps the final duration", () => {
  const text = "Natural pace";
  const timing = { mode: "natural" };
  const frames = Array.from({ length: 85 }, (_, frame) =>
    naturalWriteOnFrame(text, timing, frame, 84, "message-1:Natural pace"));
  assert.deepEqual(frames, Array.from({ length: 85 }, (_, frame) =>
    naturalWriteOnFrame(text, timing, frame, 84, "message-1:Natural pace")));
  assert.ok(frames.every((value, index) => index === 0 || value >= frames[index - 1]!));
  assert.equal(frames.at(-1), 84);
  assert.ok(new Set(frames.slice(1).map((value, index) => value - frames[index]!)).size > 1);
});
