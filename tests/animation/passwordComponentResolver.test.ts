import assert from "node:assert/strict";
import test from "node:test";

import { passwordProgress } from "../../src/desktop-preview/passwordComponentResolver.js";

test("Password distributes four-frame digit pushes deterministically", () => {
  assert.deepEqual(passwordProgress(true, 0, 16, "2345", "2345"), {
    state: "initial", filledCount: 0, pushedKey: "2",
  });
  assert.deepEqual(passwordProgress(true, 4, 16, "2345", "2345"), {
    state: "initial", filledCount: 1, pushedKey: "3",
  });
  assert.deepEqual(passwordProgress(true, 15, 16, "2345", "2345"), {
    state: "initial", filledCount: 3, pushedKey: "5",
  });
  assert.deepEqual(passwordProgress(true, 16, 16, "2345", "2345"), {
    state: "correct", filledCount: 4, pushedKey: "",
  });
});

test("Password keeps idle and incorrect outcomes explicit", () => {
  assert.deepEqual(passwordProgress(false, 16, 16, "2345", "2345"), {
    state: "initial", filledCount: 0, pushedKey: "",
  });
  assert.deepEqual(passwordProgress(true, 16, 16, "9999", "2345"), {
    state: "incorrect", filledCount: 4, pushedKey: "",
  });
});
