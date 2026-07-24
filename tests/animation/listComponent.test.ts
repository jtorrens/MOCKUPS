import assert from "node:assert/strict";
import test from "node:test";

import type { RenderableBox } from "../../src/visual/renderable/types.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { listComponentToRenderable } from "../../src/desktop-preview/listComponentRenderable.js";
import { resolveListComponent } from "../../src/desktop-preview/listComponentResolver.js";
import { routeComponentClassToRenderable } from "../../src/desktop-preview/componentClassRenderableRegistry.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

const fixture = (variantId = "calls") =>
  committedComponentFixture("list", variantId);

function renderChild(payload: DesignPreviewPayload, assignedBox?: RenderableBox) {
  return routeComponentClassToRenderable(payload, renderChild, assignedBox);
}

test("List Calls and Chats Variants select one exact List Item Variant", () => {
  const calls = resolveListComponent(fixture("calls"));
  const chats = resolveListComponent(fixture("chats"));

  assert.equal(calls.stack.distributionMode, "flow");
  assert.equal(calls.stack.items.length, 5);
  assert.ok(calls.stack.items.every((item) =>
    item.componentType === "listItem"
    && item.variantReference.endsWith("::variant::calls")));
  assert.ok(chats.stack.items.every((item) =>
    item.componentType === "listItem"
    && item.variantReference.endsWith("::variant::chats")));
});

test("List forwards one shared Runtime size and the exact List Item Runtime", () => {
  const source = fixture("calls");
  const preview = JSON.parse(source.designPreviewJson) as {
    itemWidth: number;
    itemHeight: number;
    items: Array<{
      id: string;
      listItemInputs: {
        activeSet: number;
        state: string;
        labelContent: Array<{
          runtimeInputs: { sampleText: string };
        }>;
      };
    }>;
  };
  preview.itemWidth = 344;
  preview.itemHeight = 76;
  const first = preview.items[0]!;
  first.listItemInputs.labelContent[1]!.runtimeInputs.sampleText = "Alternate caller";
  source.designPreviewJson = JSON.stringify(preview);
  source.runtimeContractJson = source.designPreviewJson;
  source.localFrame = 12;
  source.instanceJson = JSON.stringify({
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          id: "active-content",
          fieldId: "activeSet",
          targetId: first.id,
          keyframes: [{
            id: "active-content-12",
            frame: 12,
            value: 2,
            interpolation: "hold",
          }],
        },
        {
          id: "item-state",
          fieldId: "state",
          targetId: first.id,
          keyframes: [{
            id: "item-state-12",
            frame: 12,
            value: "pressed",
            interpolation: "hold",
          }],
        },
      ],
    },
  });

  const resolved = resolveListComponent(source);
  assert.equal(resolved.stack.items[0]?.inputs.activeSet, 2);
  assert.equal(resolved.stack.items[0]?.inputs.state, "pressed");
  assert.equal(resolved.stack.items[0]?.inputs.width, 344);
  assert.equal(resolved.stack.items[0]?.inputs.height, 76);
  const node = listComponentToRenderable(source, resolved, renderChild);
  const firstItem = node.children?.[0];
  assert.equal(firstItem?.id, "component.listItem");
  assert.equal(firstItem?.box?.width, 344);
  assert.equal(firstItem?.box?.height, 76);
  assert.equal(firstItem?.children?.[1]?.transform?.opacity, 0.82);
});

test("List renders a vertical Collection Stack without owning item internals", () => {
  const source = fixture("chats");
  const contract = resolveListComponent(source);
  const node = listComponentToRenderable(source, contract, renderChild);

  assert.equal(node.id, "component.list");
  assert.equal(node.children?.length, 5);
  assert.equal(node.box?.width, 360);
  assert.equal(node.box?.height, 420);
  assert.ok(node.children?.every((item) => item.id === "component.listItem"));
});

test("List rejects undeclared per-item fields", () => {
  const source = fixture("calls");
  const preview = JSON.parse(source.designPreviewJson) as {
    items: Array<Record<string, unknown>>;
  };
  preview.items[0]!.implicitVariant = "chats";
  source.designPreviewJson = JSON.stringify(preview);

  assert.throws(
    () => resolveListComponent(source),
    /contains undeclared fields: implicitVariant/,
  );
});
