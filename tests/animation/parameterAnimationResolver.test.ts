import assert from "node:assert/strict";
import test from "node:test";
import { resolveParameterAnimation } from "../../src/desktop-preview/parameterAnimationResolver.js";

function animation(
  keyframes: Array<Record<string, unknown>>,
  fieldId = "value",
  targetId = "",
) {
  return {
    schemaVersion: 2,
    tracks: [{ id: "track-1", fieldId, targetId, keyframes }],
  };
}

test("an absent track returns the base value without claiming animation", () => {
  assert.deepEqual(resolveParameterAnimation({ schemaVersion: 2, tracks: [] }, "value", "", 4, "base"), {
    value: "base",
    animated: false,
  });
});

test("a disabled-only track is animated but resolves to base", () => {
  const result = resolveParameterAnimation(animation([
    { id: "k0", frame: 0, value: "hidden", interpolation: "hold", enabled: false },
  ]), "value", "", 10, "base");
  assert.deepEqual(result, { value: "base", animated: true });
});

test("interval boundaries are exact and the last keyframe holds", () => {
  const data = animation([
    { id: "k0", frame: 2, value: 10, interpolation: "hold", enabled: true },
    { id: "k1", frame: 6, value: 30, interpolation: "linear", enabled: true },
  ]);
  assert.equal(resolveParameterAnimation(data, "value", "", 1, 5).value, 5);
  assert.equal(resolveParameterAnimation(data, "value", "", 2, 5).value, 10);
  assert.equal(resolveParameterAnimation(data, "value", "", 5, 5).value, 25);
  assert.equal(resolveParameterAnimation(data, "value", "", 6, 5).value, 30);
  assert.equal(resolveParameterAnimation(data, "value", "", 99, 5).value, 30);
});

test("interpolation belongs to the destination keyframe", () => {
  const holdDestination = animation([
    { id: "k0", frame: 0, value: 0, interpolation: "linear" },
    { id: "k1", frame: 10, value: 100, interpolation: "hold" },
  ]);
  const linearDestination = animation([
    { id: "k0", frame: 0, value: 0, interpolation: "hold" },
    { id: "k1", frame: 10, value: 100, interpolation: "linear" },
  ]);
  assert.equal(resolveParameterAnimation(holdDestination, "value", "", 5, -1).value, 0);
  assert.equal(resolveParameterAnimation(linearDestination, "value", "", 5, -1).value, 50);
});

test("ease-in-out uses smoothstep and discrete values hold", () => {
  const numeric = animation([
    { id: "k0", frame: 0, value: 0, interpolation: "hold" },
    { id: "k1", frame: 4, value: 8, interpolation: "easeInOut" },
  ]);
  const discrete = animation([
    { id: "k0", frame: 0, value: false, interpolation: "hold" },
    { id: "k1", frame: 4, value: true, interpolation: "linear" },
  ]);
  assert.equal(resolveParameterAnimation(numeric, "value", "", 1, -1).value, 1.25);
  assert.equal(resolveParameterAnimation(numeric, "value", "", 2, -1).value, 4);
  assert.equal(resolveParameterAnimation(discrete, "value", "", 3, true).value, false);
  assert.equal(resolveParameterAnimation(discrete, "value", "", 4, false).value, true);
});

test("write-on rewrites by grapheme for deletion and insertion", () => {
  const data = animation([
    { id: "k0", frame: 0, value: "A👨‍👩‍👧‍👦é", interpolation: "hold" },
    { id: "k1", frame: 4, value: "A🙂Z", interpolation: "writeOn" },
  ]);
  assert.equal(resolveParameterAnimation(data, "value", "", 0, "").value, "A👨‍👩‍👧‍👦é");
  assert.equal(resolveParameterAnimation(data, "value", "", 1, "").value, "A👨‍👩‍👧‍👦");
  assert.equal(resolveParameterAnimation(data, "value", "", 2, "").value, "A");
  assert.equal(resolveParameterAnimation(data, "value", "", 3, "").value, "A🙂");
  assert.equal(resolveParameterAnimation(data, "value", "", 4, "").value, "A🙂Z");
});

test("fieldId and targetId select one exact track", () => {
  const data = {
    schemaVersion: 2,
    tracks: [
      { id: "a", fieldId: "text", targetId: "m1", keyframes: [{ id: "a0", frame: 0, value: "one" }] },
      { id: "b", fieldId: "text", targetId: "m2", keyframes: [{ id: "b0", frame: 0, value: "two" }] },
    ],
  };
  assert.equal(resolveParameterAnimation(data, "text", "m1", 0, "base").value, "one");
  assert.equal(resolveParameterAnimation(data, "text", "m2", 0, "base").value, "two");
  assert.equal(resolveParameterAnimation(data, "text", "m3", 0, "base").value, "base");
});

test("parameter resolution rejects malformed transient animation documents", () => {
  const malformed = [
    { tracks: null },
    { tracks: {} },
    { tracks: [null] },
    { tracks: [{ fieldId: "", keyframes: [] }] },
    { tracks: [{ fieldId: "value", targetId: null, keyframes: [] }] },
    { tracks: [
      { fieldId: "value", keyframes: [] },
      { fieldId: "value", targetId: "", keyframes: [] },
    ] },
    { tracks: [{ fieldId: "value", keyframes: {} }] },
    { tracks: [{ fieldId: "value", keyframes: [null] }] },
    { tracks: [{ fieldId: "value", keyframes: [{ frame: 0 }] }] },
    { tracks: [{ fieldId: "value", keyframes: [{ frame: 0, value: 1, enabled: "true" }] }] },
    { tracks: [{ fieldId: "value", keyframes: [{ frame: 0, value: 1, interpolation: null }] }] },
    { tracks: [{ fieldId: "value", keyframes: [{ frame: 0, value: 1, interpolation: "spline" }] }] },
    { tracks: [{ fieldId: "value", keyframes: [{ frame: 2, value: 1 }, { frame: 1, value: 2 }] }] },
  ];
  for (const value of malformed) {
    assert.throws(() => resolveParameterAnimation(value, "value", "", 0, "base"));
  }
});
