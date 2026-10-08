import assert from "node:assert/strict";
import test from "node:test";
import { execFileSync } from "node:child_process";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { DesktopRenderableHtmlAdapter } from "../../src/desktop-preview/DesktopRenderableHtmlAdapter.js";
import { iconTokenStyle } from "../../src/desktop-preview/previewIconHelpers.js";
import { mediaFrameUriForPath } from "../../src/desktop-preview/previewAssetResolver.js";
import { resolveListItemComponent } from "../../src/desktop-preview/listItemComponentResolver.js";
import { resolveKeypadComponent } from "../../src/desktop-preview/keypadComponentResolver.js";
import { resolvePasswordComponent } from "../../src/desktop-preview/passwordComponentResolver.js";
import { resolveSurfaceStackComponent } from "../../src/desktop-preview/surfaceStackComponentResolver.js";
import { resolveRenderablePayload } from "../../src/desktop-preview/renderablePayloadBoundary.js";
import { embeddedComponentPayload } from "../../src/desktop-preview/previewPayloadHelpers.js";
import { requiredNumber, requiredNumberPair, requiredStringPair, optionalString, optionalBoolean, optionalNumber } from "../../src/desktop-preview/previewValueHelpers.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Content Set references survive reorder and reject indices or stale identities", () => {
  const payload = committedComponentFixture("listItem", "calls");
  const values = JSON.parse(payload.designPreviewJson);
  values.activeSet = values.contentSets[1].id;
  values.contentSets.reverse();
  payload.designPreviewJson = JSON.stringify(values);
  const selected = resolveListItemComponent(payload);
  assert.equal(selected.activeSet, "set_b");
  for (const invalid of [1, "2", "absent-id"]) {
    payload.designPreviewJson = JSON.stringify({ ...values, activeSet: invalid });
    assert.throws(() => resolveListItemComponent(payload));
  }
  const config = JSON.parse(payload.configJson);
  config.listItem.components.label.order = config.listItem.components.avatar.order;
  payload.configJson = JSON.stringify(config);
  payload.designPreviewJson = JSON.stringify(values);
  assert.throws(() => resolveListItemComponent(payload), /order .*duplicated/);
});

test("Keypad selection uses only exact IDs and rejects duplicate IDs and semantic values", () => {
  const payload = committedComponentFixture("keypad");
  const values = JSON.parse(payload.designPreviewJson);
  const config = JSON.parse(payload.configJson);
  const key = config.keypad.keys.find((key: { value: string }) => key.value === "5");
  key.id = "unrelated-stable-identity";
  payload.configJson = JSON.stringify(config);
  payload.designPreviewJson = JSON.stringify({ ...values, pushedKey: key.id });
  assert.equal(resolveKeypadComponent(payload).keys.find(k => k.id === key.id)?.state, "pushed");
  for (const invalid of ["5", "missing"]) {
    payload.designPreviewJson = JSON.stringify({ ...values, pushedKey: invalid });
    assert.throws(() => resolveKeypadComponent(payload), /Unknown Keypad key id/);
  }
  payload.designPreviewJson = JSON.stringify(values);
  const duplicateId = structuredClone(config);
  duplicateId.keypad.keys[1].id = duplicateId.keypad.keys[0].id;
  assert.throws(() => resolveKeypadComponent({ ...payload, configJson: JSON.stringify(duplicateId) }), /Duplicate keypad key id/);
  const duplicateValue = structuredClone(config);
  duplicateValue.keypad.keys[1].value = duplicateValue.keypad.keys[0].value;
  assert.throws(() => resolveKeypadComponent({ ...payload, configJson: JSON.stringify(duplicateValue) }), /Duplicate keypad key value/);
});

test("the common root boundary resolves unrelated Component fields once and preserves the temporal envelope", () => {
  for (const [type, key, value] of [["listItem", "activeSet", "set_b"], ["keypad", "pushedKey", "key_5"]] as const) {
    const payload = committedComponentFixture(type);
    payload.localFrame = 8;
    payload.instanceJson = JSON.stringify({ animation: { schemaVersion: 2, tracks: [{
      fieldId: key, targetId: "", keyframes: [{ frame: 8, value, interpolation: "hold" }],
    }] } });
    const original = structuredClone(payload);
    const prepared = resolveRenderablePayload(payload);
    assert.equal(JSON.parse(prepared.designPreviewJson)[key], value);
    assert.equal(prepared.runtimeContractJson, original.runtimeContractJson);
    assert.deepEqual(payload, original);
    const child = embeddedComponentPayload(prepared, type, JSON.parse(prepared.configJson), {
      ...JSON.parse(prepared.designPreviewJson), [key]: "child-owned-value",
    });
    assert.equal(JSON.parse(resolveRenderablePayload(child).designPreviewJson)[key], "child-owned-value");
  }
});

test("Password maps its credential digit to the selected Keypad's identity before dispatch", () => {
  const payload = committedComponentFixture("password");
  const config = JSON.parse(payload.configJson);
  config.password.mode = "pin";
  payload.configJson = JSON.stringify(config);
  const bases = JSON.parse(payload.componentBaseConfigsJson);
  const keypad = bases.variants[config.password.keypadSlot.variantReference].keypad;
  for (const key of keypad.keys) key.id = `unique-${key.id}`;
  payload.componentBaseConfigsJson = JSON.stringify(bases);
  payload.designPreviewJson = JSON.stringify({ ...JSON.parse(payload.designPreviewJson),
    expectedPassword: "1234", attemptPassword: "1234", entryTrigger: true, entryFrame: 0,
  });
  const resolved = resolvePasswordComponent(payload);
  assert.equal(resolved.input.kind, "keypad");
  if (resolved.input.kind !== "keypad") throw new Error("Expected Keypad input");
  assert.equal(resolved.input.component.keys.find(key => key.state === "pushed")?.id, "unique-key_1");
});

test("Surface Stack consumes only its declared slot boundary without accepting flattened substitutes", () => {
  const payload = committedComponentFixture("surfaceStack");
  const values = JSON.parse(payload.designPreviewJson);
  const motion = { transition: "none", direction: "bottom", bounds: "screen", fade: false, translate: false, scale: false };
  values.items = [{ id: "slot-1", gapBeforeMode: "fixed", gapBeforeToken: "theme.spacing.none", gapBeforeWeight: 1,
    alternatives: [{ id: "alternative-1", componentSlot: null, inputs: {}, active: true,
      placement: { mode: "center", alignX: 0.5, alignY: 0.5, offsetX: 0, offsetY: 0 },
      enterMotion: motion, exitMotion: motion }],
  }];
  payload.designPreviewJson = JSON.stringify(values);
  assert.doesNotThrow(() => resolveSurfaceStackComponent(payload));
  const alternative = values.items[0].alternatives[0];
  const reference = "component_project_foqn_s2_label::variant::default";
  alternative.componentSlot = { variantReference: reference, overrides: {} };
  alternative.inputs = JSON.parse(committedComponentFixture("label").designPreviewJson);
  payload.designPreviewJson = JSON.stringify(values);
  assert.equal(resolveSurfaceStackComponent(payload).layout.slots[0]?.alternatives[0]?.component?.variantReference, reference);
  alternative.variantReference = alternative.componentSlot?.variantReference ?? "ignored::variant::default";
  alternative.overrides = {};
  delete alternative.componentSlot;
  payload.designPreviewJson = JSON.stringify(values);
  assert.throws(() => resolveSurfaceStackComponent(payload), /Missing object value/);
});

test("strict value helpers reject coercion, malformed pairs and invalid optional values", () => {
  for (const value of ["", "12", null, false, NaN]) assert.throws(() => requiredNumber({ value }, "value", "fixture"));
  for (const value of ["1|", "|2", "1|2|3", "1,5|2"]) assert.throws(() => requiredNumberPair({ value }, "value", "fixture"));
  for (const value of ["one|", "|two", "one|two|three"]) assert.throws(() => requiredStringPair({ value }, "value", "fixture"));
  assert.throws(() => optionalString({ value: false }, "value"));
  assert.throws(() => optionalBoolean({ value: "false" }, "value"));
  assert.throws(() => optionalNumber({ value: "1" }, "value", 0));
  assert.equal(requiredNumber({ value: 0 }, "value", "fixture"), 0);
});

test("missing icons are resolved red squares at the authored box in the generic HTML output", () => {
  const payload = committedComponentFixture("button");
  const style = iconTokenStyle(payload, "missing-icon-identity", "#00ff00");
  assert.equal(style.color, "#ff0000");
  const html = renderToStaticMarkup(createElement(DesktopRenderableHtmlAdapter, { tree: {
    id: "missing", type: "icon", frame: 0, box: { x: 12, y: 7, width: 28, height: 28 }, style,
  } }));
  assert.match(html, /width:28px/);
  assert.match(html, /height:28px/);
  assert.match(html, /#ff0000/);
  assert.match(html, /mask-image/);
  assert.doesNotMatch(html, /missing-icon-identity/);
});

test("media resolution never searches another root or strips an authored directory prefix", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "mockups-exact-media-"));
  try {
    const payload = { ...committedComponentFixture("media"), projectMediaRoot: directory };
    writeFileSync(path.join(directory, "photo.png"), Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aE1sAAAAASUVORK5CYII=", "base64"));
    assert.ok(mediaFrameUriForPath(payload, "photo.png", 0).uri);
    for (const source of ["", `${path.basename(directory)}/photo.png`, "assets/system/application/mockups-icon-1024.png"]) {
      const result = mediaFrameUriForPath(payload, source, 0);
      assert.equal(result.uri, "");
      assert.equal(result.error, "Media ausente");
    }
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test("a failed requested video frame cannot reuse a successful earlier frame", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "mockups-frame-failure-"));
  const oldExecutable = process.env.MOCKUPS_FFMPEG;
  try {
    const video = path.join(directory, "source.mp4");
    execFileSync("ffmpeg", ["-v", "error", "-f", "lavfi", "-i", "color=c=blue:s=32x32:r=25", "-t", "1", "-pix_fmt", "yuv420p", video]);
    const payload = { ...committedComponentFixture("media"), projectMediaRoot: directory };
    assert.ok(mediaFrameUriForPath(payload, video, 0).uri);
    process.env.MOCKUPS_FFMPEG = process.execPath; // Existing non-ffmpeg executable: extraction must fail.
    const failed = mediaFrameUriForPath(payload, video, 0.5);
    assert.equal(failed.uri, "");
    assert.equal(failed.error, "Error al leer media");
  } finally {
    if (oldExecutable === undefined) delete process.env.MOCKUPS_FFMPEG;
    else process.env.MOCKUPS_FFMPEG = oldExecutable;
    rmSync(directory, { recursive: true, force: true });
  }
});
