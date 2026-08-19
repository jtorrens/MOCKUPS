import assert from "node:assert/strict";
import test from "node:test";

import { resolveIconBarComponent } from "../../src/desktop-preview/iconBarComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Icon Bar requires its prepared Runtime state", () => {
  const source = committedComponentFixture("iconBar");
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  delete preview.state;
  source.designPreviewJson = JSON.stringify(preview);
  assert.throws(
    () => resolveIconBarComponent(source),
    /component\.iconBar\.input\.state/,
  );
});
