import assert from "node:assert/strict";
import test from "node:test";

import { listItemComponentToRenderable } from "../../src/desktop-preview/listItemComponentRenderable.js";
import { resolveListItemComponent } from "../../src/desktop-preview/listItemComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

const fixture = (variantId = "calls") =>
  committedComponentFixture("listItem", variantId);

test("List Item Calls and Chats Variants share the element model boundary", () => {
  const calls = resolveListItemComponent(fixture("calls"));
  const chats = resolveListItemComponent(fixture("chats"));

  assert.deepEqual(
    calls.elements.map((element) => element.componentType),
    ["avatar", "label", "iconRow"],
  );
  assert.deepEqual(
    chats.elements.map((element) => element.componentType),
    ["avatar", "label", "iconRow"],
  );
  assert.equal(calls.elements[2]?.componentType === "iconRow"
    && calls.elements[2].component.orientation, "horizontal");
  assert.equal(chats.elements[2]?.componentType === "iconRow"
    && chats.elements[2].component.orientation, "vertical");
  assert.equal(calls.selectedSetId, "set_a");
});

test("List Item animates selected content set and the selected set state independently", () => {
  const source = fixture("calls");
  source.localFrame = 10;
  source.instanceJson = JSON.stringify({
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          id: "selected-set",
          fieldId: "selectedSetId",
          targetId: "",
          keyframes: [{ id: "selected-set-10", frame: 10, value: "set_b", interpolation: "hold" }],
        },
        {
          id: "set-state",
          fieldId: "state",
          targetId: "set_b",
          keyframes: [{ id: "set-state-10", frame: 10, value: "inactive", interpolation: "hold" }],
        },
      ],
    },
  });

  const resolved = resolveListItemComponent(source);
  assert.equal(resolved.selectedSetId, "set_b");
  assert.equal(resolved.state, "inactive");
  assert.equal(resolved.elementsOpacity, 0.45);
  assert.equal(
    resolved.elements[1]?.componentType === "label"
      && resolved.elements[1].component.text,
    "+34 848 983 160",
  );
});

test("List Item rejects content sets whose Icon Row values do not match Variant slots", () => {
  const source = fixture("calls");
  const preview = JSON.parse(source.designPreviewJson) as {
    contentSets: Array<{ iconRowValues: unknown[] }>;
  };
  preview.contentSets[0]!.iconRowValues = preview.contentSets[0]!.iconRowValues.slice(0, 1);
  source.designPreviewJson = JSON.stringify(preview);
  assert.throws(
    () => resolveListItemComponent(source),
    /must match the Variant slots exactly/,
  );
});

test("List Item renderable keeps Surface outside the element opacity group", () => {
  const source = fixture("chats");
  const contract = resolveListItemComponent(source);
  const node = listItemComponentToRenderable(source, contract);

  assert.deepEqual(node.box, {
    x: 0,
    y: 318,
    width: 360,
    height: 84,
  });
  assert.equal(node.children?.length, 2);
  assert.equal(node.children?.[0]?.id, "component.listItem.normal.surface");
  assert.equal(node.children?.[1]?.id, "component.listItem.elements");
  assert.equal(node.children?.[1]?.transform?.opacity, 1);
  assert.equal(node.children?.[1]?.children?.length, 3);
});
