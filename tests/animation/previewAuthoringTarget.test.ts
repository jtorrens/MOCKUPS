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
    authoringSlotFieldIds: [] as string[],
  } as DesignPreviewPayload;
  const media = renderAuthoringSlot(
    payload,
    "component.bubble.media.image.editor",
    (mediaPayload) => ({
      id: "component.bubble.image",
      type: "group",
      children: [renderAuthoringSlot(
        mediaPayload,
        "component.media.inlineTopIconBar.editor",
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
});

test("a non-authoring payload emits no navigation metadata", () => {
  const node = withAuthoringTarget(
    {} as DesignPreviewPayload,
    { id: "production.frame", type: "group" },
  );
  assert.equal(node.metadata?.authoringTarget, undefined);
});
