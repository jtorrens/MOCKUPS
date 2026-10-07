import assert from "node:assert/strict";
import test from "node:test";
import { committedComponentFixture } from "./committedComponentFixture.js";
import { resolveSurfaceComponentFromRecords } from "../../src/desktop-preview/surfaceComponentResolver.js";
import { surfaceComponentToRenderableAt } from "../../src/desktop-preview/surfaceComponentRenderable.js";
import { resolveButtonComponentFromRecords } from "../../src/desktop-preview/buttonComponentResolver.js";
import { resolveIconRowComponent } from "../../src/desktop-preview/iconRowComponentResolver.js";
import { resolveRuntimeDocumentAnimationValues } from "../../src/desktop-preview/runtimeNestedAnimationFields.js";

const paletteId = "palette_project_foqn_s2_gray_050";
const box = { x: 10, y: 20, width: 100, height: 60 };

test("Surface owns tint after semantic colors for rectangles, tails and modes without changing geometry", () => {
  const payload = committedComponentFixture("surface");
  payload.paletteColors = { ...payload.paletteColors, [paletteId]: "#808080" };
  payload.paletteNeutralColors = {};
  const config = JSON.parse(payload.configJson);
  config.style.shadowEnabled = false;
  config.style.reliefEnabled = false;
  config.style.borderWidth = 0;
  const colors = {
    background: "rgba(128, 128, 128, 0.5)", borderColor: "#123456",
    colorModes: { light: { background: "#808080", borderColor: "#123456" }, dark: { background: "#404040", borderColor: "#ABCDEF" } },
  };
  for (const tail of [false, true]) {
    config.surface.tail.enabled = tail;
    config.surface.tail.size = "10|10";
    const resolved = resolveSurfaceComponentFromRecords(config, { size: "100|60", tintPaletteColor: paletteId, tintAmount: 1 }, "surface");
    const tinted = surfaceComponentToRenderableAt(payload, resolved, box, colors);
    const neutral = surfaceComponentToRenderableAt(payload, { ...resolved, tintAmount: 0 }, box, colors);
    assert.deepEqual(tinted.box, neutral.box);
    assert.deepEqual(tinted.style?.colorModes, {
      light: { background: "#404040", borderColor: "#123456" },
      dark: { background: "#202020", borderColor: "#ABCDEF" },
    });
    if (tail) {
      const uri = tinted.children?.[0]?.asset?.uri;
      assert.ok(uri);
      const svg = decodeURIComponent(uri.slice(uri.indexOf(",") + 1));
      assert.match(svg, /rgba\(64, 64, 64, 0.5\)/);
      assert.deepEqual(tinted.children?.[0]?.box, neutral.children?.[0]?.box);
    } else {
      assert.equal(tinted.style?.background, "rgba(64, 64, 64, 0.5)");
      assert.equal(tinted.style?.borderColor, colors.borderColor);
      assert.equal(neutral.style?.background, colors.background);
    }
  }
  assert.throws(() => resolveSurfaceComponentFromRecords(config, { size: "1|1", tintAmount: 2 }, "surface"), /between -1 and 1/);
});

test("Button tint follows exact Surface Variant and Override before explicit Runtime zero", () => {
  const source = committedComponentFixture("button");
  const config = JSON.parse(source.configJson);
  const runtime = JSON.parse(source.designPreviewJson);
  const catalog = JSON.parse(source.componentBaseConfigsJson);
  runtime.inputs = runtime.inputs.filter((field: { id: string }) => !["surfaceTintPaletteColor", "surfaceTintAmount"].includes(field.id));
  delete runtime.surfaceTintPaletteColor;
  delete runtime.surfaceTintAmount;
  config.button.appearance.surfaceSlot.overrides = { surface: { tintAmount: -0.6 } };
  const configured = resolveButtonComponentFromRecords(config, runtime, catalog, "button");
  assert.equal(configured.appearance.surface.tintAmount, -0.6);
  assert.equal(configured.appearance.surface.tintPaletteColor, paletteId);
  const explicit = resolveButtonComponentFromRecords(config, { ...runtime, surfaceTintAmount: 0 }, catalog, "button");
  assert.equal(explicit.appearance.surface.tintAmount, 0);
});

test("Icon Row hold tracks reach Button Surface by stable item identity without altering siblings", () => {
  const source = committedComponentFixture("iconRow");
  const runtime = JSON.parse(source.designPreviewJson);
  const firstId = runtime.buttonInputs[0].id;
  const definition = runtime.inputs.find((field: { id: string }) => field.id === "buttonInputs");
  for (const id of ["surfaceTintPaletteColor", "surfaceTintAmount"]) {
    const field = definition.structuredCollection.fields.find((field: { id: string }) => field.id === id);
    assert.equal(field.animatable, true);
    assert.deepEqual(field.animationInterpolations, ["hold"]);
  }
  const baseline = JSON.stringify(runtime);
  const animation = { schemaVersion: 2, tracks: [
    { fieldId: `buttonInputs.${firstId}.surfaceTintAmount`, targetId: "", keyframes: [
      { frame: 0, value: 0, interpolation: "hold" },
      { frame: 7, value: 1, interpolation: "hold" },
      { frame: 10, value: -1, interpolation: "hold" },
    ] },
    { fieldId: `buttonInputs.${firstId}.surfaceTintPaletteColor`, targetId: "", keyframes: [
      { frame: 7, value: paletteId, interpolation: "hold" },
    ] },
  ] };
  for (const [frame, expected] of [[0, 0], [6, 0], [7, 1], [9, 1], [10, -1], [20, -1]]) {
    const animated = resolveRuntimeDocumentAnimationValues(runtime, animation, () => frame!);
    const row = resolveIconRowComponent({ ...source, designPreviewJson: JSON.stringify(animated.values) });
    const first = row.items.find((item) => item.id === firstId)!;
    assert.equal(first.button.appearance.surface.tintAmount, expected);
    const shape = surfaceComponentToRenderableAt(source, first.button.appearance.surface, box, { background: "#808080" });
    const surfaces = [shape, ...(shape.children ?? [])];
    const fill = surfaces.find((node) => node.type === "surface");
    assert.ok(fill);
    assert.equal(fill.style?.background, expected === 0 ? "#808080" : expected === 1 ? "#404040" : "#C0C0C0");
    for (const sibling of row.items.filter((item) => item.id !== firstId)) assert.equal(sibling.button.appearance.surface.tintAmount, 0);
  }
  assert.equal(JSON.stringify(runtime), baseline);
});
