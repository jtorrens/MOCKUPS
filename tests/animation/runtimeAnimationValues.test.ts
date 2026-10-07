import assert from "node:assert/strict";
import test from "node:test";
import {
  resolveRuntimeAnimationValues,
  resolveRuntimeDocumentAnimationValues,
} from "../../src/desktop-preview/runtimeNestedAnimationFields.js";

function track(fieldId: string, targetId: string, value: unknown, interpolation = "hold") {
  return { fieldId, targetId, keyframes: [
    { frame: 0, value: "base", interpolation: "hold" },
    { frame: 5, value, interpolation },
  ] };
}

test("every declared scalar uses its stable field identity, not its storage key or meaning", () => {
  for (const value of [false, 0, "", null, "theme.colors.positive"]) {
    const contract = { fields: [{ id: "stable-field", jsonKey: "arbitraryStorageKey" }] };
    const authored = { arbitraryStorageKey: "payload", unrelated: "unchanged" };
    const animation = { tracks: [track("stable-field", "entity", value)] };
    assert.equal(resolveRuntimeAnimationValues(contract, authored, animation, "entity", () => 4).values.arbitraryStorageKey, "base");
    for (const frame of [5, 6, 100]) {
      const resolved = resolveRuntimeAnimationValues(contract, authored, animation, "entity", () => frame);
      assert.equal(resolved.values.arbitraryStorageKey, value);
      assert.equal(resolved.values.unrelated, "unchanged");
      assert.equal(resolved.field("stable-field").sourceKeyframeFrame, 5);
    }
    assert.deepEqual(authored, { arbitraryStorageKey: "payload", unrelated: "unchanged" });
  }
});

test("unanimated fields preserve the exact prepared payload including explicit null", () => {
  const result = resolveRuntimeAnimationValues(
    { fields: [{ id: "value", jsonKey: "stored" }] }, { stored: null }, { tracks: [] }, "", () => 0,
  );
  assert.deepEqual(result.field("value"), { value: null, animated: false });
  assert.throws(() => result.field("stored"), /not declared/);
});

test("nested structures and embedded contracts share the enclosing temporal target", () => {
  const contract = { fields: [{
    id: "children", jsonKey: "members", structuredCollection: {
      fields: [], itemRuntimeContractJsonKey: "values",
    },
  }] };
  const member = (id: string) => ({ id, values: {
    inputs: [{ id: "state", jsonKey: "stored" }], stored: "payload",
  } });
  const first = member("first");
  const second = member("second");
  const animation = { tracks: [track("children.second.state", "parent", "changed")] };
  for (const members of [[first, second], [second, first]]) {
    const result = resolveRuntimeAnimationValues(contract, { members }, animation, "parent", () => 5);
    const values = result.values.members as typeof members;
    assert.equal(values.find(({ id }) => id === "first")!.values.stored, "payload");
    assert.equal(values.find(({ id }) => id === "second")!.values.stored, "changed");
  }
  assert.equal(second.values.stored, "payload");
});

test("pair interpolation follows the declared ValueKind at any depth", () => {
  for (const [valueKind, expected] of [["IntegerPair", "2|3"], ["DecimalPair", "1.5|3"]]) {
    const result = resolveRuntimeAnimationValues(
      { fields: [{ id: "pair", jsonKey: "pair", valueKind }] },
      { pair: "0|0" },
      { tracks: [{ fieldId: "pair", keyframes: [
        { frame: 0, value: "0|0", interpolation: "hold" },
        { frame: 10, value: "3|6", interpolation: "linear" },
      ] }] }, "", () => 5,
    );
    assert.equal(result.values.pair, expected);
  }
});

test("the complete document resolves root and collection fields using their own clocks", () => {
  const document = {
    inputs: [{ id: "field", jsonKey: "stored" }], stored: "payload",
    collections: [{ id: "collection", jsonKey: "items", fields: [{ id: "field", jsonKey: "stored" }] }],
    items: [{ id: "entity", stored: "payload" }],
  };
  const resolved = resolveRuntimeDocumentAnimationValues(document,
    { tracks: [track("field", "", "root"), track("field", "entity", "child")] },
    (_fieldId, targetId) => targetId ? 4 : 5,
  );
  assert.equal(resolved.values.stored, "root");
  assert.equal(resolved.field("field", "entity").value, "base");
  assert.throws(() => resolved.field("field", "missing"), /owner 'missing' is not declared/);
  assert.equal(document.stored, "payload");
});

test("Variant and calculated fields are not replaced by Runtime parameter tracks", () => {
  const result = resolveRuntimeAnimationValues(
    { fields: [
      { id: "variant", jsonKey: "variant", source: "variant" },
      { id: "derived", jsonKey: "derived", source: "calculated" },
    ] }, { variant: "config", derived: 0 },
    { tracks: [track("variant", "", "wrong"), track("derived", "", "wrong")] }, "", () => 10,
  );
  assert.deepEqual(result.values, { variant: "config", derived: 0 });
});

test("missing prepared values and ambiguous field identities fail without defaults", () => {
  assert.throws(() => resolveRuntimeAnimationValues(
    { fields: [{ id: "field", jsonKey: "stored", defaultValue: "not a Runtime fallback" }] },
    {}, { tracks: [] }, "", () => 0,
  ), /missing its prepared value 'stored'/);
  assert.throws(() => resolveRuntimeAnimationValues(
    { fields: [{ id: "field", jsonKey: "a" }, { id: "field", jsonKey: "b" }] },
    { a: 0, b: 0 }, { tracks: [] }, "", () => 0,
  ), /Duplicate Runtime animation field/);
  assert.throws(() => resolveRuntimeAnimationValues(
    { fields: [{ id: "first", jsonKey: "value" }, { id: "second", jsonKey: "value" }] },
    { value: 0 }, { tracks: [] }, "", () => 0,
  ), /share the same prepared value/);
});
