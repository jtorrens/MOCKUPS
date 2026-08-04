import assert from "node:assert/strict";
import test from "node:test";
import type { RenderableNode } from "../../src/visual/renderable/types.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  applyDeviceModuleTransparency,
  requiredDeviceModuleTransparency,
} from "../../src/desktop-preview/deviceModuleTransparency.js";
import { previewCanvasBackground } from "../../src/desktop-preview/renderDesignPreviewMarkup.js";

const payload: DesignPreviewPayload = {
  kind: "module",
  componentType: "module.test",
  componentBaseConfigsJson: "{}",
  appConfigJson: "{}",
  instanceJson: "{}",
  frameRate: 24,
  localFrame: 12,
  configJson: "{}",
  designPreviewJson: "{}",
  runtimeContractJson: "{}",
  previewFrame: {
    canvasWidth: 100,
    canvasHeight: 200,
    screenX: 0,
    screenY: 0,
    screenWidth: 100,
    screenHeight: 200,
    moduleTransparency: {
      enabled: true,
      mode: "fixed",
      paletteColor: "gray_020",
      backgroundOpacity: 0.75,
      fixedStart: 80,
      gradientHeight: 40,
      variableOffset: 0,
    },
  },
  paletteColors: { gray_020: "#202020" },
  themeMode: "light",
  themeTokensJson: "{}",
};

const module: RenderableNode = {
  id: "module.test",
  type: "group",
  box: { x: 0, y: 0, width: 100, height: 200 },
  style: { overflow: "hidden" },
  children: [
    {
      id: "wallpaper.image",
      type: "image",
      box: { x: 0, y: 0, width: 100, height: 200 },
      asset: { type: "image", uri: "data:image/png;base64,AA==" },
      metadata: { paintRole: "moduleBackground" },
    },
    {
      id: "content.motion",
      type: "group",
      box: { x: 0, y: 0, width: 100, height: 200 },
      transform: { y: 5 },
      children: [
        {
          id: "content.last",
          type: "surface",
          box: { x: 10, y: 40, width: 80, height: 20 },
          style: { background: "#FFFFFF" },
        },
      ],
    },
  ],
};

test("fixed Device module transparency separates background opacity from the global Module mask", () => {
  const rendered = applyDeviceModuleTransparency(payload, module);
  assert.equal(rendered.children?.some((child) => child.id === "wallpaper.image"), false);
  assert.equal(rendered.children?.[0]?.id, "device.moduleTransparency.background");
  assert.equal(rendered.children?.[0]?.style?.background, "rgba(32, 32, 32, 0.75)");
  assert.deepEqual(rendered.style?.opacityMask, {
    axis: "vertical",
    start: 80,
    end: 120,
    beforeOpacity: 1,
    afterOpacity: 0,
  });
  assert.equal(rendered.children?.some((child) => child.id === "content.motion"), true);
});

test("enabled Device module transparency leaves the Preview canvas transparent", () => {
  assert.equal(previewCanvasBackground(payload), undefined);
});

test("variable Device module transparency measures the last pre-background pixel each frame", () => {
  const rendered = applyDeviceModuleTransparency({
    ...payload,
    previewFrame: {
      ...payload.previewFrame,
      moduleTransparency: {
        ...payload.previewFrame.moduleTransparency,
        mode: "variable",
        variableOffset: -5,
      },
    },
  }, module);
  assert.deepEqual(rendered.style?.opacityMask, {
    axis: "vertical",
    start: 60,
    end: 100,
    beforeOpacity: 1,
    afterOpacity: 0,
  });
});

test("disabled Device module transparency preserves the exact Module tree", () => {
  const rendered = applyDeviceModuleTransparency({
    ...payload,
    previewFrame: {
      ...payload.previewFrame,
      moduleTransparency: {
        ...payload.previewFrame.moduleTransparency,
        enabled: false,
      },
    },
  }, module);
  assert.equal(rendered, module);
});

test("Device module transparency rejects incomplete and legacy payloads", () => {
  assert.throws(() => requiredDeviceModuleTransparency({ enabled: false }));
  assert.throws(() => requiredDeviceModuleTransparency({
    enabled: true,
    mode: "fixed",
    paletteColor: "gray_020",
    opacity: 0.75,
    fixedStart: 80,
    gradientHeight: 40,
    variableOffset: 0,
  }));
  assert.throws(() => requiredDeviceModuleTransparency({
    ...payload.previewFrame.moduleTransparency,
    legacyStart: 20,
  }));
});
