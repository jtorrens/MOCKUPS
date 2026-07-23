import assert from "node:assert/strict";
import test from "node:test";

import React from "react";
import { renderToStaticMarkup } from "react-dom/server";

import { DesktopRenderableHtmlAdapter } from "../../src/desktop-preview/DesktopRenderableHtmlAdapter.js";
import { renderableToSvg } from "../../src/desktop-preview/RenderableSvgAdapter.js";

test("the generic HTML adapter paints a resolved text shadow", () => {
  const markup = renderToStaticMarkup(React.createElement(DesktopRenderableHtmlAdapter, {
    tree: {
      id: "label.content",
      type: "group",
      frame: 0,
      box: { x: 0, y: 0, width: 120, height: 32 },
      style: {
        textShadow: {
          offsetX: 1,
          offsetY: 2,
          blur: 3,
          color: "rgba(0, 0, 0, 0.5)",
        },
      },
      children: [{
        id: "label.text",
        type: "text",
        frame: 0,
        box: { x: 0, y: 0, width: 120, height: 32 },
        text: "Text",
        style: { fontFamily: "Test Font" },
      }],
    },
  }));

  assert.match(markup, /text-shadow:1px 2px 3px rgba\(0, 0, 0, 0\.5\)/);
});

test("both generic renderers consume the same exact Renderable metadata", () => {
  const tree = {
    id: "label.text",
    type: "text" as const,
    frame: 0,
    box: { x: 0, y: 0, width: 120, height: 32 },
    text: "Text",
    style: { fontFamily: "Test Font" },
    metadata: {
      inlineCursor: { color: "#FF0000", width: 2, opacity: 0.5 },
    },
  };
  const html = renderToStaticMarkup(React.createElement(DesktopRenderableHtmlAdapter, { tree }));
  const svg = renderableToSvg(tree);
  assert.match(html, /background:#FF0000/);
  assert.match(svg, /fill="#FF0000"/);

  const invalid = {
    ...tree,
    metadata: { inlineCursor: [] },
  };
  assert.throws(() => renderToStaticMarkup(React.createElement(
    DesktopRenderableHtmlAdapter,
    { tree: invalid as unknown as typeof tree },
  )));
  assert.throws(() => renderableToSvg(invalid as unknown as typeof tree));
});

test("both generic renderers reject incomplete complex visual styles", () => {
  const base = {
    id: "surface",
    type: "surface" as const,
    frame: 0,
    box: { x: 0, y: 0, width: 120, height: 32 },
  };
  for (const style of [
    { shadow: [] },
    { textShadow: { offsetX: 1, offsetY: 2, blur: 3 } },
    { surfaceRelief: { angleDeg: -45, extension: 1, spread: 0 } },
  ]) {
    const invalid = { ...base, style };
    assert.throws(() => renderToStaticMarkup(React.createElement(
      DesktopRenderableHtmlAdapter,
      { tree: invalid as unknown as typeof base },
    )));
    assert.throws(() => renderableToSvg(invalid as unknown as typeof base));
  }
});
