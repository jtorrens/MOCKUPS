import assert from "node:assert/strict";
import test from "node:test";

import React from "react";
import { renderToStaticMarkup } from "react-dom/server";

import { DesktopRenderableHtmlAdapter } from "../../src/desktop-preview/DesktopRenderableHtmlAdapter.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  authoringVariantPayload,
  renderAuthoringCollectionItem,
  renderAuthoringSlot,
  withAuthoringTarget,
} from "../../src/desktop-preview/previewAuthoringTarget.js";
import {
  embeddedVariantComponentPayload,
} from "../../src/desktop-preview/previewPayloadHelpers.js";
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

test("full Component Variant boundaries replace the authoring owner exactly", () => {
  const modulePayload = {
    authoringOwnerId: "module_conversation::variant::default",
    authoringRecordClassId: "module.conversation",
    authoringSlotFieldIds: ["invalid.previous.slot"],
    authoringFocusFieldId: "module.conversation.textInputBarVariant",
  } as DesignPreviewPayload;
  const child = authoringVariantPayload(
    modulePayload,
    "component_text_input::variant::chat",
    "component.textInputBar",
  );

  assert.deepEqual(withAuthoringTarget(child, {
    id: "component.textInputBar",
    type: "group",
  }).metadata?.authoringTarget, {
    ownerId: "component_text_input::variant::chat",
    slotFieldIds: [],
  });
});

test("generic embedded Variant payloads publish the exact child owner", () => {
  const modulePayload = {
    authoringOwnerId: "module_lock_screen::variant::default",
    authoringRecordClassId: "module.core.lockScreen",
    authoringSlotFieldIds: ["invalid.previous.slot"],
    authoringFocusFieldId: "module.lockScreen.stack",
  } as DesignPreviewPayload;
  const child = embeddedVariantComponentPayload(
    modulePayload,
    "label",
    "component_label::variant::clock",
    {},
    { sampleText: "00:10" },
  );

  assert.deepEqual(withAuthoringTarget(child, {
    id: "component.label",
    type: "group",
  }).metadata?.authoringTarget, {
    ownerId: "component_label::variant::clock",
    slotFieldIds: [],
  });
  assert.throws(
    () => embeddedVariantComponentPayload(
      modulePayload,
      "plausible",
      "component_plausible::variant::default",
      {},
      {},
    ),
    /Unsupported embedded Component type 'plausible'/,
  );
});

test("structured authoring targets preserve the exact stable item id", () => {
  const payload = {
    authoringOwnerId: "component_icon_row::variant::default",
    authoringRecordClassId: "component.iconRow",
    authoringSlotFieldIds: [] as string[],
  } as DesignPreviewPayload;
  const item = renderAuthoringCollectionItem(
    payload,
    "component.iconRow",
    "component.iconRow.items",
    "button_attachment",
    () => ({
      id: "component.button.normal",
      type: "group",
    }),
  );
  assert.deepEqual(item.metadata?.authoringTarget, {
    focusFieldId: "component.iconRow.items",
    focusItemId: "button_attachment",
    ownerId: "component_icon_row::variant::default",
    slotFieldIds: [],
  });

  const markup = renderToStaticMarkup(React.createElement(
    DesktopRenderableHtmlAdapter,
    { tree: item },
  ));
  assert.match(
    markup,
    /data-preview-authoring-focus-item-id="button_attachment"/,
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
