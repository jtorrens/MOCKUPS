import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { iconUriForToken } from "../../src/desktop-preview/previewAssetResolver.js";

const payload = {
  projectMediaRoot: "/project",
} as DesignPreviewPayload;

test("Icon resolution preserves explicit absence without fabricating a mapping", () => {
  assert.equal(iconUriForToken(payload, "send"), "");
  assert.equal(iconUriForToken({
    ...payload,
    iconMappingJson: JSON.stringify({ tokens: {} }),
  }, "send"), "");
});

test("Present Icon Theme mappings require exact token documents", () => {
  assert.throws(
    () => iconUriForToken({ ...payload, iconMappingJson: "{}" }, "send"),
    /Missing object value icon mapping\.tokens/,
  );
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: [] }),
    }, "send"),
    /Missing object value icon mapping\.tokens/,
  );
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: { send: [] } }),
    }, "send"),
    /Missing object value icon mapping\.tokens\.send/,
  );
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: { send: {} } }),
    }, "send"),
    /Missing string value icon mapping\.tokens\.send\.file/,
  );
});

test("Icon files remain explicit safe SVG filenames under an exact asset root", () => {
  for (const file of ["send.png", "nested/send.svg", "nested\\send.svg"]) {
    assert.throws(
      () => iconUriForToken({
        ...payload,
        iconAssetRoot: "icon-themes/example",
        iconMappingJson: JSON.stringify({ tokens: { send: { file } } }),
      }, "send"),
      /Invalid local SVG file/,
    );
  }
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: { send: { file: "send.svg" } } }),
    }, "send"),
    /Missing Icon Theme asset root for token send/,
  );
  assert.equal(iconUriForToken({
    ...payload,
    iconAssetRoot: "icon-themes/missing",
    iconMappingJson: JSON.stringify({ tokens: { send: { file: "send.svg" } } }),
  }, "send"), "");
});
