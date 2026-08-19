import assert from "node:assert/strict";
import test from "node:test";

import { resolveBubbleComponent } from "../../src/desktop-preview/bubbleComponentResolver.js";
import { bubbleComponentToRenderable } from "../../src/desktop-preview/bubbleComponentRenderable.js";
import type { RenderableNode } from "../../src/visual/renderable/types.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Bubble preserves the resolved Surface tail enabled value for every message direction", () => {
  const source = committedComponentFixture("bubble");
  const config = JSON.parse(source.configJson) as {
    bubble: { surfaceSlot: { overrides: Record<string, unknown> } };
  };
  config.bubble.surfaceSlot.overrides = {
    surface: { tail: { enabled: false } },
  };
  source.configJson = JSON.stringify(config);

  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  for (const state of ["incoming", "outgoing", "system"]) {
    preview.state = state;
    source.designPreviewJson = JSON.stringify(preview);
    assert.equal(resolveBubbleComponent(source).surface.tail.enabled, false, state);
  }
});

test("Bubble reserves message text for an Avatar anchored to any Bubble edge", () => {
  const placements = [
    {
      name: "left",
      placement: { mode: "insideEdge" as const, alignX: 0, alignY: 0.5, offsetX: 8, offsetY: 0 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.x >= avatar.box!.x + avatar.box!.width),
    },
    {
      name: "top",
      placement: { mode: "insideEdge" as const, alignX: 0.5, alignY: 0, offsetX: 0, offsetY: 8 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.y >= avatar.box!.y + avatar.box!.height),
    },
    {
      name: "right",
      placement: { mode: "insideEdge" as const, alignX: 1, alignY: 0.5, offsetX: -8, offsetY: 0 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.x + text.box!.width <= avatar.box!.x),
    },
    {
      name: "bottom",
      placement: { mode: "insideEdge" as const, alignX: 0.5, alignY: 1, offsetX: 0, offsetY: -8 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.y + text.box!.height <= avatar.box!.y),
    },
  ];

  for (const { placement, assertReserved } of placements) {
    const payload = committedComponentFixture("bubble", "default_copy");
    const config = JSON.parse(payload.configJson) as {
      bubble: { avatarSlot: Record<string, unknown> };
    };
    config.bubble.avatarSlot.reserveTextSpace = true;
    config.bubble.avatarSlot.placement = placement;
    payload.configJson = JSON.stringify(config);

    const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
    const text = requiredNode(rendered, "component.bubble.textBox");
    const avatar = requiredNode(rendered, "component.bubble.avatar");
    assertReserved(text, avatar);
  }
});

test("Bubble minimum height contains a reserved interior Avatar without message text", () => {
  const payload = committedComponentFixture("bubble", "default_copy");
  const config = JSON.parse(payload.configJson) as {
    bubble: { avatarSlot: { overrides: Record<string, unknown>; reserveTextSpace?: boolean } };
  };
  config.bubble.avatarSlot.reserveTextSpace = true;
  config.bubble.avatarSlot.overrides = {
    avatar: { defaultSize: 96 },
  };
  payload.configJson = JSON.stringify(config);
  const preview = JSON.parse(payload.designPreviewJson) as Record<string, unknown>;
  preview.sampleText = "";
  payload.designPreviewJson = JSON.stringify(preview);

  const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
  const surface = requiredNode(rendered, "component.bubble.surface");
  const avatar = requiredNode(rendered, "component.bubble.avatar");
  assert.ok(surface.box!.height >= avatar.box!.height);
});

test("Bubble requires an explicit Avatar text-space reservation choice", () => {
  const payload = committedComponentFixture("bubble", "default_copy");
  const config = JSON.parse(payload.configJson) as {
    bubble: { avatarSlot: Record<string, unknown> };
  };
  delete config.bubble.avatarSlot.reserveTextSpace;
  payload.configJson = JSON.stringify(config);

  assert.throws(
    () => resolveBubbleComponent(payload),
    /component\.bubble\.avatar\.reserveTextSpace/,
  );
});

function requiredNode(node: RenderableNode, id: string): RenderableNode {
  if (node.id === id) return node;
  for (const child of node.children ?? []) {
    const found = findNode(child, id);
    if (found) return found;
  }
  throw new Error(`Missing renderable node '${id}'.`);
}

function findNode(node: RenderableNode, id: string): RenderableNode | undefined {
  if (node.id === id) return node;
  for (const child of node.children ?? []) {
    const found = findNode(child, id);
    if (found) return found;
  }
  return undefined;
}
