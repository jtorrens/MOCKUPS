import assert from "node:assert/strict";
import test from "node:test";

import { contentRowComponentToRenderable } from "../../src/desktop-preview/contentRowComponentRenderable.js";
import { resolveContentRowComponent } from "../../src/desktop-preview/contentRowComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Content Row resolves its slot collection and applies horizontal and vertical padding", () => {
  const source = committedComponentFixture("contentRow", "default");
  const config = JSON.parse(source.configJson) as { contentRow: { slots: Array<Record<string, unknown>> } };
  const preview = JSON.parse(source.designPreviewJson) as { slotInputs: Array<Record<string, unknown>> };
  config.contentRow.slots[0]!.kind = "label";
  config.contentRow.slots[2]!.kind = "label";
  config.contentRow.slots[4]!.kind = "icon";
  preview.slotInputs[0]!.label = "Left";
  preview.slotInputs[2]!.label = "Center";
  preview.slotInputs[4]!.state = "active";
  const payload = { ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(preview) };
  const row = resolveContentRowComponent(payload);
  assert.equal(row.slots.length, 5);
  assert.deepEqual(row.padding, { xToken: "theme.spacing.s", yToken: "theme.spacing.s" });
  const node = contentRowComponentToRenderable(payload, row, { x: 0, y: 0, width: 390, height: 80 });
  assert.equal(node.children?.filter(child => child.id.startsWith("component.contentRow.slot.")).length, 3);
  assert.ok((node.box?.height ?? 0) > 0);
});

test("Content Row keeps separator and vertical alignment in Variant state", () => {
  const source = committedComponentFixture("contentRow", "default");
  const config = JSON.parse(source.configJson) as { contentRow: Record<string, unknown> };
  config.contentRow.showSeparator = true;
  config.contentRow.verticalAlignment = "bottom";
  const payload = { ...source, configJson: JSON.stringify(config) };
  const row = resolveContentRowComponent(payload);
  const node = contentRowComponentToRenderable(payload, row, { x: 0, y: 0, width: 390, height: 80 });
  assert.equal(row.verticalAlignment, "bottom");
  assert.ok(node.children?.some(child => child.id === "component.contentRow.separator"));
});
import assert from "node:assert/strict";
import test from "node:test";

import { resolveContentRowComponent } from "../../src/desktop-preview/contentRowComponentResolver.js";

test("contentRow scaffold remains explicitly unregistered until semantics exist", () => {
  assert.equal(typeof resolveContentRowComponent, "function");
});
