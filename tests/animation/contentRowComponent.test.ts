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

test("Content Row preserves Button content mode and renders Screen-owned labels", () => {
  for (const contentMode of ["icon", "text", "iconText"]) {
    const source = committedComponentFixture("contentRow", "default");
    const config = JSON.parse(source.configJson);
    const preview = JSON.parse(source.designPreviewJson);
    const slot = config.contentRow.slots[0];
    slot.kind = "icon";
    slot.iconSlot.overrides.button = { contentMode, labelPosition: "bottom" };
    preview.slotInputs[0].label = "Screen label";
    const row = resolveContentRowComponent({ ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(preview) });
    const button = row.slots[0].content;
    assert.ok(button && "contentMode" in button);
    assert.equal(button.contentMode, contentMode);
    assert.equal(button.labelPosition, "bottom");
    assert.equal(button.appearance.label?.text, contentMode === "icon" ? undefined : "Screen label");
  }
});

test("Content Row follows stable slot identities through removal, addition and reorder", () => {
  const source = committedComponentFixture("contentRow", "default");
  const config = JSON.parse(source.configJson);
  const runtime = JSON.parse(source.designPreviewJson);
  const slot = config.contentRow.slots[0];
  const value = runtime.slotInputs[0];
  for (const count of [0, 2, 6]) {
    config.contentRow.slots = Array.from({ length: count }, (_, index) => ({
      ...structuredClone(slot), kind: "label", id: `item_${index.toString(16).padStart(32, "0")}`,
    })).reverse();
    runtime.slotInputs = [...config.contentRow.slots].reverse().map((item: { id: string }) => ({
      ...structuredClone(value), id: item.id, label: item.id,
    }));
    const row = resolveContentRowComponent({ ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(runtime) });
    assert.deepEqual(row.slots.map(item => item.id), config.contentRow.slots.map((item: { id: string }) => item.id));
    for (const item of row.slots) {
      assert.ok(item.content && "text" in item.content);
      assert.equal(item.content.text, item.id);
    }
    assert.ok(contentRowComponentToRenderable(source, row));
  }
  runtime.slotInputs.pop();
  assert.throws(() => resolveContentRowComponent({ ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(runtime) }), /missing slot/);
});

test("Content Row retains the exact Button boundary's prepared Badge default", () => {
  const source = committedComponentFixture("contentRow", "default");
  const config = JSON.parse(source.configJson);
  const runtime = JSON.parse(source.designPreviewJson);
  const catalog = JSON.parse(source.componentBaseConfigsJson);
  const slot = config.contentRow.slots[0];
  slot.kind = "icon";
  runtime.slotInputs[0].label = "";
  catalog.inputDefaults[slot.iconSlot.variantReference].showBadge = true;
  const row = resolveContentRowComponent({ ...source, configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(runtime), componentBaseConfigsJson: JSON.stringify(catalog) });
  const button = row.slots[0].content;
  assert.ok(button && "badge" in button && button.badge);
});
