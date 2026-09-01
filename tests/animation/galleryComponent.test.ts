import assert from "node:assert/strict";
import test from "node:test";

import { resolveGalleryComponent } from "../../src/desktop-preview/galleryComponentResolver.js";

test("Gallery resolver export is available", () => {
  assert.equal(typeof resolveGalleryComponent, "function");
});
