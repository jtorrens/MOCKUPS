import assert from "node:assert/strict";
import test from "node:test";
import { subtextBoxRelativeToText } from "../../src/desktop-preview/labelComponentRenderable.js";

const textBox = { x: 0, y: 0, width: 200, height: 20 };
const subtextSize = { width: 40, height: 10 };

test("Label aligns subtext against the measured primary text bounds", () => {
  assert.equal(subtextBoxRelativeToText(textBox, 100, "center", subtextSize, "left", "bottom", 8).x, 50);
  assert.equal(subtextBoxRelativeToText(textBox, 100, "center", subtextSize, "center", "bottom", 8).x, 80);
  assert.equal(subtextBoxRelativeToText(textBox, 100, "center", subtextSize, "right", "bottom", 8).x, 110);
});

test("Label places subtext above or below using the token-resolved gap", () => {
  assert.equal(subtextBoxRelativeToText(textBox, 100, "center", subtextSize, "left", "top", 8).y, -18);
  assert.equal(subtextBoxRelativeToText(textBox, 100, "center", subtextSize, "left", "bottom", 8).y, 28);
});
