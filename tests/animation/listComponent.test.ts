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
  const callsSource = fixture("calls");
  const chatsSource = fixture("chats");
  const calls = resolveListComponent(callsSource);
  const chats = resolveListComponent(chatsSource);
  const callsRuntime = JSON.parse(callsSource.designPreviewJson) as {
    items: unknown[];
  };
  const chatsRuntime = JSON.parse(chatsSource.designPreviewJson) as {
    items: unknown[];
  };

  assert.equal(calls.stack.distributionMode, "flow");
  assert.ok(callsRuntime.items.length > 0);
  assert.ok(chatsRuntime.items.length > 0);
  assert.equal(calls.stack.items.length, callsRuntime.items.length);
  assert.equal(chats.stack.items.length, chatsRuntime.items.length);
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
  const runtime = JSON.parse(source.designPreviewJson) as {
    itemWidth: number;
    itemHeight: number;
    items: unknown[];
  };
  const contract = resolveListComponent(source);
  const node = listComponentToRenderable(source, contract, renderChild);

  assert.equal(node.id, "component.list");
  assert.equal(node.children?.length, runtime.items.length);
  assert.equal(node.box?.width, runtime.itemWidth);
  assert.equal(node.box?.height, runtime.itemHeight * runtime.items.length);
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

test("List requires exact Collection Stack and List Item slot types", () => {
  const wrongStack = fixture("calls");
  const wrongStackConfig = JSON.parse(wrongStack.configJson) as {
    list: { collectionStackSlot: { variantReference: string } };
  };
  wrongStackConfig.list.collectionStackSlot.variantReference =
    "component_project_foqn_s2_list_item::variant::calls";
  wrongStack.configJson = JSON.stringify(wrongStackConfig);
  assert.throws(
    () => resolveListComponent(wrongStack),
    /collectionStackSlot.*must resolve to Component 'collectionStack'/,
  );

  const wrongItem = fixture("calls");
  const wrongItemConfig = JSON.parse(wrongItem.configJson) as {
    list: { listItemSlot: { variantReference: string } };
  };
  wrongItemConfig.list.listItemSlot.variantReference =
    "component_project_foqn_s2_button::variant::default";
  wrongItem.configJson = JSON.stringify(wrongItemConfig);
  assert.throws(
    () => resolveListComponent(wrongItem),
    /listItemSlot.*must resolve to Component 'listItem'/,
  );
});
