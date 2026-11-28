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
    [{ id: "a:1", triggerId: "a", burstIndex: 1, burstCount: 4, value: "heart", age: 6 }, { id: "a:2", triggerId: "a", burstIndex: 2, burstCount: 4, value: "heart", age: 3 },
      { id: "a:3", triggerId: "a", burstIndex: 3, burstCount: 4, value: "heart", age: 0 }, { id: "b:0", triggerId: "b", burstIndex: 0, burstCount: 4, value: "heart", age: 5 },
      { id: "b:1", triggerId: "b", burstIndex: 1, burstCount: 4, value: "heart", age: 2 }]);
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 19, 8, burst), []);
  assert.deepEqual(resolveTimelineEvents(animation, "reaction", "", 41, 8, burst), []);
  assert.deepEqual(resolveTimelineEvents(animation, "flash", "lamp", 0, 8, { count: 2, intervalFrames: 0 }),
    [{ id: "c:0", triggerId: "c", burstIndex: 0, burstCount: 2, value: true, age: 0 }, { id: "c:1", triggerId: "c", burstIndex: 1, burstCount: 2, value: true, age: 0 }]);
});

test("reaction glyphs grow from configured scale and opacity and expire at the end of their rise", () => {
  const payload = committedComponentFixture("reactionEmitter");
  const config = JSON.parse(payload.configJson);
  Object.assign(config.reactionEmitter, { duration: 48, burst: { count: 1, intervalFrames: 0 },
    entryFrames: 5, entryScale: 15, entryOpacity: 0, fadeStart: 70 });
  payload.configJson = JSON.stringify(config);
  const fixture = JSON.parse(payload.designPreviewJson);
  const animation = fixture.testValues.animation;
  const at = (frame: number) => {
    const emitter = config.reactionEmitter;
    const catalog = emitter.reactions.map(({ id }: { id: string }) => ({
      id, emissions: resolveTimelineEvents(animation, "emit", id, frame, emitter.duration, emitter.burst),
    }));
    const prepared = { ...payload, designPreviewJson: JSON.stringify({ showEmoticons: true, catalog }) };
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

test("emitter travel uses the root screen height and a fixed bottom-center origin in its assigned box", () => {
  const payload = committedComponentFixture("reactionEmitter");
  const config = JSON.parse(payload.configJson);
  Object.assign(config.reactionEmitter, { duration: 48, riseHeightPercent: 50, dispersionWidth: 80 });
  payload.configJson = JSON.stringify(config);
  payload.rootPreviewFrame = { ...payload.previewFrame, screenWidth: 360, screenHeight: 800 };
  payload.designPreviewJson = JSON.stringify({ showEmoticons: true, catalog: [{ id: "heart", emissions: [{ id: "k:1", triggerId: "k", burstIndex: 1, burstCount: 2, value: "emit", age: 24 }] }] });
  const emitter = resolveReactionEmitterComponent(payload);
  const group = reactionEmitterComponentToRenderable(payload, emitter, { x: 10, y: 20, width: 100, height: 200 });
  const box = group.children![0]!.box!;
  assert.equal(box.y + box.height / 2, 220 - emitter.size / 2 - 400 * 0.75);
  assert.ok(Math.abs(box.x + box.width / 2 - 60) <= 40);
});

test("each burst assigns one stable horizontal destination per zone from a shared origin", () => {
  const payload = committedComponentFixture("reactionEmitter");
  const animation = { tracks: [{ fieldId: "emit", targetId: "heart", keyframes: [
    { id: "first", frame: 0, value: "emit" }, { id: "second", frame: 4, value: "emit" },
  ] }] };
  const at = (frame: number, intervalFrames: number) => {
    const emissions = resolveTimelineEvents(animation, "emit", "heart", frame, 20, { count: 3, intervalFrames });
    const catalog = [{ id: "heart", emissions }];
    return resolveReactionEmitterComponent({ ...payload,
      designPreviewJson: JSON.stringify({ showEmoticons: true, catalog }),
    }).particles;
  };
  for (const interval of [0, 5]) {
    for (let index = 0; index < 3; index++) {
      const birth = at(index * interval, interval).find(particle => particle.id === "first:" + index)!;
      assert.equal(Math.abs(birth.drift), 0);
    }
    const group = at(16, interval);
    for (const trigger of ["first", "second"]) {
      const zones = group.filter(particle => particle.id.startsWith(trigger + ":"))
        .map(particle => Math.floor((particle.drift / particle.rise + 1) * 3 / 2));
      assert.deepEqual(zones.sort(), [0, 1, 2]);
    }
    const destination = (frame: number) => {
      const birth = at(frame, interval).find(particle => particle.id === "first:2")!;
      return birth.drift / birth.rise;
    };
    assert.ok(Math.abs(destination(interval === 0 ? 18 : 21) - destination(16)) < 1e-12);
    assert.deepEqual(at(16, interval), group);
  }
});
