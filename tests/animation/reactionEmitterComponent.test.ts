import assert from "node:assert/strict";
import test from "node:test";
import { committedComponentFixture } from "./committedComponentFixture.js";
import { resolveReactionEmitterComponent } from "../../src/desktop-preview/reactionEmitterComponentResolver.js";
import { reactionEmitterComponentToRenderable } from "../../src/desktop-preview/reactionEmitterComponentRenderable.js";
import { resolveTimelineEvents } from "../../src/desktop-preview/parameterAnimationResolver.js";

test("event tracks preserve repeated values and owner-relative age on nonsequential seeks", () => {
  const burst = { count: 4, intervalFrames: 3 };
  const keys = [{ id: "a", frame: 20, value: "heart" },
    { id: "b", frame: 24, value: "heart" }];
  const animation = { tracks: [{ fieldId: "reaction", keyframes: keys },
    { fieldId: "flash", targetId: "lamp", keyframes: [
      { id: "c", frame: 0, value: true },
    ] }] };
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 29, 8, burst),
    [{ id: "a:1", value: "heart", age: 6 }, { id: "a:2", value: "heart", age: 3 },
      { id: "a:3", value: "heart", age: 0 }, { id: "b:0", value: "heart", age: 5 },
      { id: "b:1", value: "heart", age: 2 }]);
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 19, 8, burst), []);
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 41, 8, burst), []);
  assert.deepEqual(resolveTimelineEvents(animation, "flash", "lamp", 0, 8, { count: 2, intervalFrames: 0 }),
    [{ id: "c:0", value: true, age: 0 }, { id: "c:1", value: true, age: 0 }]);
});

test("reaction glyphs grow from configured scale and opacity and expire at the end of their rise", () => {
  const payload = committedComponentFixture("reactionEmitter");
  const fixture = JSON.parse(payload.designPreviewJson);
  const animation = fixture.testValues.animation;
  const at = (frame: number) => {
    const emissions = resolveTimelineEvents(animation, "emit", "", frame, 48, JSON.parse(payload.configJson).reactionEmitter.burst);
    const prepared = { ...payload, designPreviewJson: JSON.stringify({ emissions }) };
    return reactionEmitterComponentToRenderable(prepared, resolveReactionEmitterComponent(prepared)).children!;
  };
  const first = at(0)[0]!;
  assert.equal(first.text, "❤️");
  assert.equal(first.style?.opacity, 0);
  const entered = at(5)[0]!;
  assert.ok(entered.box!.width > first.box!.width);
  assert.equal(entered.style?.opacity, 1);
  const overlap = at(36);
  assert.deepEqual(overlap.map(node => node.text), ["❤️", "😂", "👏", "🔥", "😮"]);
  assert.ok(overlap[0]!.box!.y < overlap[4]!.box!.y);
  assert.ok(Number(overlap[0]!.style?.opacity) < 1);
  assert.deepEqual(at(80), []);
  assert.deepEqual(at(36), overlap);
});

test("emitter travel uses the root screen height and the placement inside its assigned box", () => {
  const payload = committedComponentFixture("reactionEmitter");
  const config = JSON.parse(payload.configJson);
  Object.assign(config.reactionEmitter, { riseHeightPercent: 50, dispersionWidth: 80,
    placement: { mode: "center", alignX: 0.5, alignY: 1, offsetX: 8, offsetY: -12 } });
  payload.configJson = JSON.stringify(config);
  payload.rootPreviewFrame = { ...payload.previewFrame, screenWidth: 360, screenHeight: 800 };
  payload.designPreviewJson = JSON.stringify({ emissions: [{ id: "k:1", value: "heart", age: 24 }] });
  const emitter = resolveReactionEmitterComponent(payload);
  const group = reactionEmitterComponentToRenderable(payload, emitter, { x: 10, y: 20, width: 100, height: 200 });
  const box = group.children![0]!.box!;
  assert.equal(box.y + box.height / 2, 220 - 12 - 400 * 0.75);
  assert.ok(Math.abs(box.x + box.width / 2 - 68) <= 40);
});
