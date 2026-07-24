import assert from "node:assert/strict";
import test from "node:test";

import { listItemComponentToRenderable } from "../../src/desktop-preview/listItemComponentRenderable.js";
import { resolveListItemComponent } from "../../src/desktop-preview/listItemComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

const fixture = (variantId = "calls") =>
  committedComponentFixture("listItem", variantId);

test("List Item Calls and Chats Variants share fixed child slots with Variant-owned order", () => {
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
  assert.equal(calls.activeSet, 1);
  assert.deepEqual(calls.size, { width: 360, height: 84 });
});

test("List Item animates the numeric active set and current state independently", () => {
  const source = fixture("calls");
  source.localFrame = 10;
  source.instanceJson = JSON.stringify({
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          id: "active-set",
          fieldId: "activeSet",
          targetId: "",
          keyframes: [{ id: "active-set-10", frame: 10, value: 2, interpolation: "hold" }],
        },
        {
          id: "item-state",
          fieldId: "state",
          targetId: "",
          keyframes: [{ id: "item-state-10", frame: 10, value: "inactive", interpolation: "hold" }],
        },
      ],
    },
  });

  const resolved = resolveListItemComponent(source);
  assert.equal(resolved.activeSet, 2);
  assert.equal(resolved.state, "inactive");
  assert.equal(resolved.elementsOpacity, 0.45);
  assert.equal(
    resolved.elements[1]?.componentType === "label"
      && resolved.elements[1].component.text,
    "+34 848 983 160",
  );
});

test("List Item rejects an Icon Row child Runtime that does not match its Variant slots", () => {
  const source = fixture("calls");
  const preview = JSON.parse(source.designPreviewJson) as {
    iconRowContent: Array<{ runtimeInputs: { buttonInputs: unknown[] } }>;
  };
  preview.iconRowContent[0]!.runtimeInputs.buttonInputs =
    preview.iconRowContent[0]!.runtimeInputs.buttonInputs.slice(0, 1);
  source.designPreviewJson = JSON.stringify(preview);
  assert.throws(
    () => resolveListItemComponent(source),
    /Button Runtime values must match the Variant items exactly/,
  );
});

test("List Item consumes exact child Runtime fields instead of parent-owned copies", () => {
  const source = fixture("calls");
  const preview = JSON.parse(source.designPreviewJson) as {
    activeSet: number;
    labelContent: Array<{
      contentSetId: string;
      runtimeInputs: { sampleText: string; textColorToken: string };
    }>;
  };
  preview.activeSet = 2;
  preview.labelContent[1]!.runtimeInputs.sampleText = "Exact Label Runtime";
  preview.labelContent[1]!.runtimeInputs.textColorToken = "theme.colors.badge";
  source.designPreviewJson = JSON.stringify(preview);

  const resolved = resolveListItemComponent(source);
  const label = resolved.elements.find((element) => element.componentType === "label");
  assert.equal(label?.componentType === "label" && label.component.text, "Exact Label Runtime");
  assert.equal(
    label?.componentType === "label" && label.component.textColorToken,
    "theme.colors.badge",
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

test("List Item flows visible children through padding and gap with an auto Avatar", () => {
  const source = fixture("calls");
  const contract = resolveListItemComponent(source);
  const node = listItemComponentToRenderable(source, contract);
  const children = node.children?.[1]?.children ?? [];

  assert.deepEqual(children.map((child) => child.box), [
    { x: 8, y: 322, width: 76, height: 76 },
    { x: 92, y: 322, width: 160, height: 76 },
    { x: 260, y: 338, width: 92, height: 44 },
  ]);
});

test("List Item rejects a Runtime width that cannot contain its Variant flow", () => {
  const source = fixture("calls");
  const preview = JSON.parse(source.designPreviewJson) as { width: number };
  preview.width = 100;
  source.designPreviewJson = JSON.stringify(preview);
  const contract = resolveListItemComponent(source);

  assert.throws(
    () => listItemComponentToRenderable(source, contract),
    /components, padding and gaps exceed the Runtime width/,
  );
});
