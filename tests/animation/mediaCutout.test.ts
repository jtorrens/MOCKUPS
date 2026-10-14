import assert from "node:assert/strict";
import test from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { chromium } from "playwright";
import type { RenderableNode } from "../../src/visual/renderable/types.js";
import { DesktopRenderableHtmlAdapter } from "../../src/desktop-preview/DesktopRenderableHtmlAdapter.js";
import { renderableToSvg } from "../../src/desktop-preview/RenderableSvgAdapter.js";
import { resolveMediaComponent } from "../../src/desktop-preview/mediaComponentResolver.js";
import { mediaComponentToRenderable } from "../../src/desktop-preview/mediaComponentRenderable.js";
import { resolveGalleryComponent } from "../../src/desktop-preview/galleryComponentResolver.js";
import { galleryComponentToRenderable } from "../../src/desktop-preview/galleryComponentRenderable.js";
import { prepareRuntimePreviewPayload } from "../../src/desktop-preview/runtimePreviewDocumentContract.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

function nodes(root: RenderableNode): RenderableNode[] {
  return [root, ...(root.children ?? []).flatMap(nodes)];
}

test("Media cutout skips missing source extraction and retains final viewport under image zoom", () => {
  const fixture = committedComponentFixture("media");
  const values = JSON.parse(fixture.designPreviewJson);
  values.mediaCutout = true;
  values.motionElapsedMs = 0;
  values.mediaSource = "missing-video.mp4";
  values.mediaType = "video";
  let original: unknown;
  for (const mediaScale of [0.2, 1, 4]) {
    const payload = { ...fixture, designPreviewJson: JSON.stringify({ ...values, mediaScale }) };
    const cutouts = nodes(mediaComponentToRenderable(payload, resolveMediaComponent(payload)))
      .filter(n => n.type === "cutout");
    assert.equal(cutouts.length, 1);
    if (original) assert.deepEqual(cutouts[0]!.box, original);
    original = cutouts[0]!.box;
  }
});

test("Gallery forwards its single cutout to every image and hold changes at the owner frame", () => {
  const fixture = committedComponentFixture("gallery");
  const values = { ...JSON.parse(fixture.designPreviewJson), mediaCutout: false, mediaDirectory: "media" };
  const source = { ...fixture, projectMediaRoot: "/tmp/missing-cutout-fixture",
    projectMediaFiles: ["media/a.png", "media/b.png"],
    designPreviewJson: JSON.stringify(values), runtimeContractJson: JSON.stringify(values),
    instanceJson: JSON.stringify({ animation: { schemaVersion: 2, tracks: [{ fieldId: "mediaCutout", targetId: "",
      keyframes: [{ frame: 0, value: false, interpolation: "hold" }, { frame: 10, value: true, interpolation: "hold" }] }] } }) };
  for (const frame of [9, 10]) {
    const payload = prepareRuntimePreviewPayload({ ...source, localFrame: frame });
    const resolved = resolveGalleryComponent(payload);
    assert.equal(resolved.items.length, 2);
    assert.ok(resolved.items.every(item => item.media.mediaCutout === (frame === 10)));
    if (frame === 10) assert.equal(nodes(galleryComponentToRenderable(payload, resolved)).filter(n => n.type === "cutout").length, 2);
  }
});

test("HTML and SVG pixels erase ancestor backgrounds but preserve later overlays and rounded clips", async () => {
  const tree: RenderableNode = { id: "root", type: "group", box: { x: 0, y: 0, width: 120, height: 100 },
    style: { background: "red" }, children: [{ id: "nested", type: "group",
      box: { x: 20, y: 10, width: 70, height: 70 }, transform: { x: 10 },
      style: { background: "green", overflow: "hidden", borderRadius: 12 }, children: [
        { id: "erase", type: "cutout", box: { x: 20, y: 10, width: 100, height: 70 } },
        { id: "overlay", type: "surface", box: { x: 40, y: 30, width: 10, height: 10 }, style: { background: "blue" } },
      ] }] };
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 120, height: 100 }, deviceScaleFactor: 1 });
    for (const markup of [renderToStaticMarkup(React.createElement(DesktopRenderableHtmlAdapter, { tree })), renderableToSvg(tree)]) {
      await page.setContent(`<style>html,body{margin:0;background:transparent}</style>${markup}`);
      await page.evaluate(async () => { await Promise.all([...document.images].map(i => i.decode())); });
      const png = await page.screenshot({ omitBackground: true });
      const pixels = await page.evaluate(async data => {
        const img = new Image(); img.src = data; await img.decode();
        const canvas = document.createElement("canvas"); canvas.width = 120; canvas.height = 100;
        const context = canvas.getContext("2d")!; context.drawImage(img, 0, 0);
        return [[60, 50], [55, 35], [25, 40], [105, 40], [31, 11]].map(([x, y]) =>
          [...context.getImageData(x!, y!, 1, 1).data]);
      }, `data:image/png;base64,${png.toString("base64")}`);
      assert.deepEqual(pixels, [[0,0,0,0], [0,0,255,255], [255,0,0,255], [255,0,0,255], [255,0,0,255]]);
    }
  } finally { await browser.close(); }
});
