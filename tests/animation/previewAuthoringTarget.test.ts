import assert from "node:assert/strict";
import test from "node:test";

import React from "react";
import { renderToStaticMarkup } from "react-dom/server";

import { DesktopRenderableHtmlAdapter } from "../../src/desktop-preview/DesktopRenderableHtmlAdapter.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  renderAuthoringSlot,
  withAuthoringTarget,
} from "../../src/desktop-preview/previewAuthoringTarget.js";
import { RenderableNodeSchema } from "../../src/visual/renderable/schema.js";

test("authoring targets preserve the exact owner and nested slot chain", () => {
  const payload = {
    authoringOwnerId: "component_bubble::variant::default",
    authoringRecordClassId: "component.bubble",
    authoringSlotFieldIds: [] as string[],
  } as DesignPreviewPayload;
  const media = renderAuthoringSlot(
    payload,
    "component.bubble",
    "component.bubble.media.image.editor",
    "component.media",
    "component.media.surface.editor",
    (mediaPayload) => ({
      id: "component.bubble.image",
      type: "group",
      children: [renderAuthoringSlot(
        mediaPayload,
        "component.media",
        "component.media.inlineTopIconBar.editor",
        "component.iconBar",
        "component.iconBar.edgePadding",
        () => ({
          id: "component.media.topIconBar",
          type: "group",
          children: [],
        }),
      )],
    }),
  );
  const root = RenderableNodeSchema.parse(withAuthoringTarget(
    payload,
    {
      id: "component.bubble",
      type: "group",
      children: [media],
    },
  ));

  assert.deepEqual(root.metadata?.authoringTarget, {
    ownerId: "component_bubble::variant::default",
    slotFieldIds: [],
  });
  assert.deepEqual(root.children?.[0]?.children?.[0]?.metadata?.authoringTarget, {
    focusFieldId: "component.iconBar.edgePadding",
    ownerId: "component_bubble::variant::default",
    slotFieldIds: [
      "component.bubble.media.image.editor",
      "component.media.inlineTopIconBar.editor",
    ],
  });

  const markup = renderToStaticMarkup(React.createElement(
    DesktopRenderableHtmlAdapter,
    { tree: root },
  ));
  assert.match(
    markup,
    /data-preview-authoring-owner-id="component_bubble::variant::default"/,
  );
  assert.match(
    markup,
    /data-preview-authoring-slot-field-ids="\[&quot;component\.bubble\.media\.image\.editor&quot;,&quot;component\.media\.inlineTopIconBar\.editor&quot;\]"/,
  );
  assert.match(
    markup,
    /data-preview-authoring-focus-field-id="component\.iconBar\.edgePadding"/,
  );
});

test("a non-authoring payload emits no navigation metadata", () => {
  const node = withAuthoringTarget(
    {} as DesignPreviewPayload,
    { id: "production.frame", type: "group" },
  );
  assert.equal(node.metadata?.authoringTarget, undefined);
});

test("a child cannot publish a slot when its authoring parent was not crossed", () => {
  const textInputPayload = {
    authoringOwnerId: "component_text_input::variant::default",
    authoringRecordClassId: "component.textInputBar",
    authoringSlotFieldIds: [] as string[],
  } as DesignPreviewPayload;
  const invalidShortcut = renderAuthoringSlot(
    textInputPayload,
    "component.textBox",
    "component.textBox.rightIconRow.editor",
    "component.iconRow",
    "component.iconRow.items",
    () => ({
      id: "component.textBox.rightIconRow",
      type: "group",
    }),
  );

  assert.equal(invalidShortcut.metadata?.authoringTarget, undefined);
});
