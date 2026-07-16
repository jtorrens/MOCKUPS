import assert from "node:assert/strict";
import test from "node:test";

import React from "react";
import { renderToStaticMarkup } from "react-dom/server";

import { DesktopRenderableHtmlAdapter } from "../../src/desktop-preview/DesktopRenderableHtmlAdapter.js";

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
