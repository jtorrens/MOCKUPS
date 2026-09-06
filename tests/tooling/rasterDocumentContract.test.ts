import assert from "node:assert/strict";
import test from "node:test";

import {
  rasterDocumentRequiresFullLoad,
  rasterDocumentSections,
} from "../../src/desktop-preview/rasterDocumentContract.js";

test("raster documents reload their complete head when font styles change", () => {
  const first = rasterDocumentSections(`
    <!doctype html>
    <html>
      <head><style>@font-face{font-family:"First"}</style></head>
      <body><div data-renderable-id="design_preview.surface">first</div></body>
    </html>
  `);
  const second = rasterDocumentSections(`
    <!doctype html>
    <html>
      <head><style>@font-face{font-family:"Second"}</style></head>
      <body><div data-renderable-id="design_preview.surface">second</div></body>
    </html>
  `);

  assert.equal(
    rasterDocumentRequiresFullLoad("1000x1500", first.headHtml, "1000x1500", first.headHtml),
    false,
  );
  assert.equal(
    rasterDocumentRequiresFullLoad("1000x1500", first.headHtml, "1000x1500", second.headHtml),
    true,
  );
  assert.equal(
    rasterDocumentRequiresFullLoad("1000x1500", first.headHtml, "1080x1920", first.headHtml),
    true,
  );
  assert.match(second.bodyHtml, /design_preview\.surface/);
});

test("raster documents require both complete head and body ownership", () => {
  assert.throws(
    () => rasterDocumentSections("<html><body></body></html>"),
    /head is unavailable/,
  );
  assert.throws(
    () => rasterDocumentSections("<html><head></head></html>"),
    /body is unavailable/,
  );
});
