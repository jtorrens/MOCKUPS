import assert from "node:assert/strict";
import test from "node:test";
import { committedComponentFixture } from "./committedComponentFixture.js";
import { resolveReactionEmitterComponent } from "../../src/desktop-preview/reactionEmitterComponentResolver.js";
import { reactionEmitterComponentToRenderable } from "../../src/desktop-preview/reactionEmitterComponentRenderable.js";
import { resolveTimelineEvents } from "../../src/desktop-preview/parameterAnimationResolver.js";

test("event tracks preserve repeated values and owner-relative age on nonsequential seeks", () => {
  const keys = [{ id: "a", frame: 2, value: "heart" }, { id: "b", frame: 4, value: "heart" }];
  const animation = { tracks: [{ fieldId: "reaction", keyframes: keys },
    { fieldId: "flash", targetId: "lamp", keyframes: [{ id: "c", frame: 0, value: true }] }] };
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 6, 8),
    [{ id: "a", value: "heart", age: 4 }, { id: "b", value: "heart", age: 2 }]);
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 0, 8), []);
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 10, 8), [{ id: "b", value: "heart", age: 6 }]);
  assert.deepEqual(resolveTimelineEvents(animation, "flash", "lamp", 2, 8), [{ id: "c", value: true, age: 2 }]);
});

test("reaction glyphs grow from configured scale and opacity and expire at the end of their rise", () => {
  const payload = committedComponentFixture("reactionEmitter");
  const fixture = JSON.parse(payload.designPreviewJson);
  const animation = fixture.testValues.animation;
  const at = (frame: number) => {
    const emissions = resolveTimelineEvents(animation, "emit", "", frame, 48);
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
