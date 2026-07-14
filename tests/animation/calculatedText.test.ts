import assert from "node:assert/strict";
import test from "node:test";
import { resolveCalculatedText } from "../../src/desktop-preview/calculatedText.js";

test("calculated label text resolves count up from the owner-local frame", () => {
  assert.equal(resolveCalculatedText("04:23", "countUp", 0, 25), "04:23");
  assert.equal(resolveCalculatedText("04:23", "countUp", 50, 25), "04:25");
});

test("calculated label text resolves count down and clamps at zero", () => {
  assert.equal(resolveCalculatedText("4:23", "countDown", 50, 25), "4:21");
  assert.equal(resolveCalculatedText("0:01", "countDown", 100, 25), "0:00");
});

test("calculated label text rejects implicit or malformed formats", () => {
  assert.throws(
    () => resolveCalculatedText("4.23", "countUp", 0, 25),
    /must use M:SS or MM:SS format/,
  );
  assert.throws(
    () => resolveCalculatedText("4:72", "countDown", 0, 25),
    /must use M:SS or MM:SS format/,
  );
});
