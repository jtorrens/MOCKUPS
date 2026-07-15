import assert from "node:assert/strict";
import test from "node:test";

import { passwordProgress } from "../../src/desktop-preview/passwordComponentResolver.js";

test("Password distributes four-frame digit pushes deterministically", () => {
  assert.deepEqual(passwordProgress(true, 0, 16, "2345", "2345"), {
    state: "initial", filledCount: 0, visibleCount: 1, pushedKey: "2", progress: 0, playing: true,
  });
  assert.deepEqual(passwordProgress(true, 4, 16, "2345", "2345"), {
    state: "initial", filledCount: 1, visibleCount: 2, pushedKey: "3", progress: 0.25, playing: true,
  });
  assert.deepEqual(passwordProgress(true, 15, 16, "2345", "2345"), {
    state: "initial", filledCount: 3, visibleCount: 4, pushedKey: "5", progress: 0.9375, playing: true,
  });
  assert.deepEqual(passwordProgress(true, 16, 16, "2345", "2345"), {
    state: "correct", filledCount: 4, visibleCount: 4, pushedKey: "", progress: 1, playing: true,
  });
});

test("Password keeps idle and incorrect outcomes explicit", () => {
  assert.deepEqual(passwordProgress(false, 16, 16, "2345", "2345"), {
    state: "initial", filledCount: 0, visibleCount: 0, pushedKey: "", progress: 0, playing: false,
  });
  assert.deepEqual(passwordProgress(true, 16, 16, "9999", "2345"), {
    state: "incorrect", filledCount: 4, visibleCount: 4, pushedKey: "", progress: 1, playing: true,
  });
});
