import assert from "node:assert/strict";
import test from "node:test";
import { resolveCalculatedText } from "../../src/desktop-preview/calculatedText.js";
import { resolveLabelComponent } from "../../src/desktop-preview/labelComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("calculated label text resolves count up from the owner-local frame", () => {
  assert.equal(resolveCalculatedText("04:23", "countUp", "MM:SS", 0, 25), "04:23");
  assert.equal(resolveCalculatedText("04:23", "countUp", "MM:SS", 50, 25), "04:25");
});

test("calculated label text resolves count down and clamps at zero", () => {
  assert.equal(resolveCalculatedText("4:23", "countDown", "M:SS", 50, 25), "4:21");
  assert.equal(resolveCalculatedText("0:01", "countDown", "M:SS", 100, 25), "0:00");
});

test("calculated label text resolves hour clocks without inferring their units", () => {
  assert.equal(
    resolveCalculatedText("01:05", "countUp", "HH:MM", 3600, 60),
    "01:06",
  );
  assert.equal(
    resolveCalculatedText("1:01:05", "countUp", "H:MM:SS", 120, 60),
    "1:01:07",
  );
});

test("calculated label text resolves numeric masks with optional and required digits", () => {
  assert.equal(resolveCalculatedText("7", "countUp", "###0", 50, 25), "9");
  assert.equal(resolveCalculatedText("7", "countUp", "0000", 50, 25), "0009");
  assert.equal(resolveCalculatedText("1", "countDown", "##00", 100, 25), "00");
});

test("calculated label text rejects values that do not match their explicit format", () => {
  assert.throws(
    () => resolveCalculatedText("4.23", "countUp", "M:SS", 0, 25),
    /does not match its clock format/,
  );
  assert.throws(
    () => resolveCalculatedText("4:72", "countDown", "M:SS", 0, 25),
    /does not match its clock format/,
  );
  assert.throws(
    () => resolveCalculatedText("00:00", "countUp", "###0", 0, 25),
    /must be a non-negative integer/,
  );
  assert.throws(
    () => resolveCalculatedText("0", "countUp", "MM", 0, 25),
    /must use a supported clock mask/,
  );
});

test("Label resolves calculated formats from its Variant instead of Runtime inputs", () => {
  const payload = committedComponentFixture("label");
  const preview = JSON.parse(payload.designPreviewJson) as Record<string, unknown>;
  const config = JSON.parse(payload.configJson) as {
    label: { textFormat: string };
  };
  preview.sampleText = "7";
  preview.textMode = "countUp";
  delete preview.textFormat;
  config.label.textFormat = "0000";
  payload.designPreviewJson = JSON.stringify(preview);
  payload.configJson = JSON.stringify(config);
  payload.localFrame = 50;
  payload.frameRate = 25;

  assert.equal(resolveLabelComponent(payload).text, "0009");
});
