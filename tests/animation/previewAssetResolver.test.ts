import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  iconUriForToken,
  mediaFrameUriForPath,
} from "../../src/desktop-preview/previewAssetResolver.js";

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

test("HEIF assets publish oriented image geometry for cover crop", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "mockups-heif-size-"));
  const file = path.join(directory, "photo.heic");
  try {
    writeFileSync(file, Buffer.concat([
      heifSpatialExtent(320, 240),
      heifSpatialExtent(4032, 3024),
      heifSpatialExtent(768, 576),
      heifRotation(3),
    ]));
    const frame = mediaFrameUriForPath({ projectMediaRoot: directory } as DesignPreviewPayload, file, 0);
    assert.equal(frame.width, 3024);
    assert.equal(frame.height, 4032);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

function heifSpatialExtent(width: number, height: number) {
  const box = Buffer.alloc(20);
  box.writeUInt32BE(20, 0);
  box.write("ispe", 4, "ascii");
  box.writeUInt32BE(width, 12);
  box.writeUInt32BE(height, 16);
  return box;
}

function heifRotation(quarterTurns: number) {
  const box = Buffer.alloc(9);
  box.writeUInt32BE(9, 0);
  box.write("irot", 4, "ascii");
  box.writeUInt8(quarterTurns & 0x03, 8);
  return box;
}
