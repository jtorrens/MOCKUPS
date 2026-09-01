import assert from "node:assert/strict";
import test from "node:test";

import type { RenderableNode } from "../../src/visual/renderable/types.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { galleryComponentToRenderable } from "../../src/desktop-preview/galleryComponentRenderable.js";
import { resolveGalleryComponent } from "../../src/desktop-preview/galleryComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Gallery resolver export is available", () => {
  assert.equal(typeof resolveGalleryComponent, "function");
});

test("Carousel moves complete media cards and centers the selected card", () => {
  const payload = carouselPayload(1);
  const gallery = resolveGalleryComponent(payload);
  const renderable = galleryComponentToRenderable(payload, gallery);
  const selected = requiredNode(renderable, "component.gallery.item.media_b_jpg");

  assert.equal(centerX(selected), centerX(renderable));
  assert.equal(selected.transform?.scale, gallery.selectedScale);
  assert.equal(hasNodeIdPrefix(renderable, "gallery.selection"), false);
  assert.equal(renderable.style?.overflow, "visible");
  const surfaceWithShadow = findNode(
    renderable,
    (node) => node.style?.shadow !== undefined,
  );
  assert.ok(surfaceWithShadow);
  assert.equal(isDescendant(requiredNode(renderable, "gallery.fade.start"), surfaceWithShadow.id), false);
  const fadeViewport = requiredNode(renderable, "gallery.fade.start");
  const items = requiredNode(renderable, "gallery.items");
  assert.ok(fadeViewport.box);
  assert.ok(items.box);
  assert.ok(fadeViewport.box.y < items.box.y);
  assert.ok(
    fadeViewport.box.y + fadeViewport.box.height
      > items.box.y + items.box.height,
  );

  const movingPayload = carouselPayload(1.25);
  const movingGallery = resolveGalleryComponent(movingPayload);
  const moving = galleryComponentToRenderable(movingPayload, movingGallery);
  const previous = requiredNode(moving, "component.gallery.item.media_b_jpg");
  const next = requiredNode(moving, "component.gallery.item.media_c_jpg");
  const step = centerX(next) - centerX(previous);

  assert.ok(Math.abs(centerX(previous) - (centerX(renderable) - step * 0.25)) < 0.001);
  assert.ok(Math.abs(centerX(next) - (centerX(renderable) + step * 0.75)) < 0.001);
  assert.ok((previous.transform?.scale ?? 1) > (next.transform?.scale ?? 1));
});

function carouselPayload(selectedIndex: number): DesignPreviewPayload {
  const fixture = committedComponentFixture("gallery");
  const inputs = JSON.parse(fixture.designPreviewJson) as Record<string, unknown>;
  inputs.mediaDirectory = "media";
  inputs.viewportSize = "360|320";
  inputs.selectedIndex = selectedIndex;
  return {
    ...fixture,
    designPreviewJson: JSON.stringify(inputs),
    runtimeContractJson: JSON.stringify(inputs),
    projectMediaRoot: "/tmp/mockups-gallery-media",
    projectMediaFiles: ["media/a.jpg", "media/b.jpg", "media/c.jpg"],
  };
}

function requiredNode(root: RenderableNode, id: string): RenderableNode {
  if (root.id === id) return root;
  for (const child of root.children ?? []) {
    const found = optionalNode(child, id);
    if (found) return found;
  }
  throw new Error(`Missing renderable ${id}`);
}

function optionalNode(root: RenderableNode, id: string): RenderableNode | undefined {
  if (root.id === id) return root;
  for (const child of root.children ?? []) {
    const found = optionalNode(child, id);
    if (found) return found;
  }
  return undefined;
}

function centerX(node: RenderableNode) {
  assert.ok(node.box);
  return node.box.x + node.box.width / 2;
}

function hasNodeIdPrefix(root: RenderableNode, prefix: string): boolean {
  return root.id.startsWith(prefix)
    || (root.children ?? []).some((child) => hasNodeIdPrefix(child, prefix));
}

function findNode(
  root: RenderableNode,
  predicate: (node: RenderableNode) => boolean,
): RenderableNode | undefined {
  if (predicate(root)) return root;
  for (const child of root.children ?? []) {
    const found = findNode(child, predicate);
    if (found) return found;
  }
  return undefined;
}

function isDescendant(root: RenderableNode, id: string): boolean {
  return root.id === id || (root.children ?? []).some((child) => isDescendant(child, id));
}
