import assert from "node:assert/strict";
import test from "node:test";
import {
  applyRuntimeInputForwarding,
  prepareComponentConfiguration,
  prepareComponentInputValues,
  prepareRuntimeValues,
  prepareRuntimePreviewPayload,
  projectRuntimeValues,
} from "../../src/desktop-preview/runtimePreviewDocumentContract.js";
import { resolveRuntimeDocumentAnimationValues } from "../../src/desktop-preview/runtimeNestedAnimationFields.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { committedComponentFixture } from "./committedComponentFixture.js";
import { resolveButtonComponentFromRecords } from "../../src/desktop-preview/buttonComponentResolver.js";
import { resolveListComponent } from "../../src/desktop-preview/listComponentResolver.js";
import { resolveNotificationsComponent } from "../../src/desktop-preview/notificationsComponentResolver.js";
import { resolveLabelComponentFromRecords, literalLabelPreview, staticLabelFrameContext } from "../../src/desktop-preview/labelComponentResolver.js";

const binding = [{ fieldId: "property-01", runtimeJsonKey: "payloadValue", configPath: ["owner", "visualValue"] }];
const config = { owner: { visualValue: "variant" } };

test("one prepared value path preserves animation > Runtime > Override > Variant", () => {
  const catalog = { variants: { "class-01::variant::variant-01": config } };
  const effective = prepareComponentConfiguration(catalog, "fixture", "class-01::variant::variant-01", {
    owner: { visualValue: "override" },
  });
  assert.equal(prepareRuntimeValues(config, {}, binding).payloadValue, "variant");
  assert.equal(prepareRuntimeValues(effective, {}, binding).payloadValue, "override");
  const runtime = {
    inputs: [{ id: "property-01", jsonKey: "payloadValue", valueKind: "StringSingleLine" }],
    payloadValue: "payload",
  };
  assert.equal(prepareRuntimeValues(effective, runtime, binding).payloadValue, "payload");
  const animated = resolveRuntimeDocumentAnimationValues(runtime, {
    schemaVersion: 2,
    tracks: [{ fieldId: "property-01", targetId: "", keyframes: [
      { frame: 0, value: "payload", interpolation: "hold" },
      { frame: 7, value: "animated", interpolation: "hold" },
    ] }],
  }, () => 7);
  assert.equal(prepareRuntimeValues(effective, animated.values, binding).payloadValue, "animated");
  assert.deepEqual(config, { owner: { visualValue: "variant" } });
});

test("explicit falsy values remain authoritative and input documents are isolated", () => {
  for (const value of [false, 0, "", null]) {
    assert.equal(prepareRuntimeValues(config, { payloadValue: value }, binding).payloadValue, value);
  }
  const runtime = { payloadValue: { nested: [1, 2] } };
  const prepared = prepareRuntimeValues(config, runtime, binding);
  (prepared.payloadValue as { nested: number[] }).nested.push(3);
  assert.deepEqual(runtime.payloadValue.nested, [1, 2]);
  assert.throws(() => prepareRuntimeValues(config, { payloadValue: undefined }, binding), /undefined/);
});

test("declared nested boundaries resolve child Variant and Override before definition defaults", () => {
  for (const owner of ["owner-a", "owner-b"]) {
    const catalog = {
      variants: { [`${owner}::variant::v1`]: { style: { tint: "child-variant" } } },
      inputDefaults: { "parent::variant::v1": { runtimeTint: "definition-default" } },
    };
    const config = { slot: { variantReference: `${owner}::variant::v1`, overrides: { style: { tint: "child-override" } } } };
    const bindings = [{
      fieldId: "parent-field", runtimeJsonKey: "runtimeTint", configPath: ["style", "tint"],
      boundaries: [{ fieldId: "slot-01", slotPath: ["slot"], componentType: "fixture" }],
    }];
    assert.equal(prepareComponentInputValues(catalog, "parent::variant::v1", config, bindings, {}).runtimeTint, "child-override");
    assert.equal(prepareRuntimeValues(config, { runtimeTint: null }, bindings, catalog).runtimeTint, null);
    assert.equal(prepareComponentInputValues(catalog, "parent::variant::v1", config, bindings, { runtimeTint: "runtime" }).runtimeTint, "runtime");
  }
});

test("missing declared Runtime values do not recover from a Variant", () => {
  assert.throws(() => prepareRuntimeValues(config, {
    inputs: [{ id: "property-01", jsonKey: "payloadValue", source: "runtime" }],
  }, binding), /no prepared value/);
  assert.throws(() => prepareRuntimeValues({}, {}, binding), /no configured value/);
  assert.throws(() => prepareRuntimeValues(config, {}, [...binding, ...binding]), /duplicate/);
  assert.throws(() => prepareRuntimeValues(config, {
    inputs: [{ id: "property-01", jsonKey: "wrong-storage-key" }], payloadValue: "value",
  }, binding), /Ambiguous/);
});

test("explicit links can use unrelated storage keys and retain empty values", () => {
  const links = [{ sourceFieldId: "source-01", sourceJsonKey: "a", targetFieldId: "target-04", targetJsonKey: "b" }];
  assert.deepEqual(projectRuntimeValues({ a: null, b: "unrelated" }, links), { b: null });
  assert.deepEqual(projectRuntimeValues({ b: "same name is not a link" }, links), {});
  assert.throws(() => projectRuntimeValues({ a: true }, [...links, ...links]), /duplicate/);
});

test("definition values use the exact Variant identity without a class default", () => {
  const catalog = { inputDefaults: { "class-01::variant::v1": { a: true, b: 1 } } };
  assert.deepEqual(prepareComponentInputValues(catalog, "class-01::variant::v1", {}, [], { a: false }), { a: false, b: 1 });
  assert.throws(() => prepareComponentInputValues(catalog, "class-01::variant::missing", {}, [], {}));
});

function forward(config: object, runtime: object) {
  const prepared = applyRuntimeInputForwarding({
    configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(runtime),
    instanceJson: "{}",
  } as DesignPreviewPayload);
  return JSON.parse(prepared.configJson);
}

test("List and Notifications accept declared item fields without a resolver whitelist", () => {
  for (const type of ["list", "notifications"]) {
    const fixture = committedComponentFixture(type);
    const runtime = JSON.parse(fixture.designPreviewJson);
    const collection = runtime.collections.find((entry: { jsonKey: string }) => entry.jsonKey === "items");
    collection.fields.push({ id: "extra-field-id", jsonKey: "extraAuthoredValue", valueKind: "StringSingleLine" });
    for (const item of runtime.items) item.extraAuthoredValue = "authored";
    const prepare = () => prepareRuntimePreviewPayload({ ...fixture, designPreviewJson: JSON.stringify(runtime) });
    const resolve = type === "list" ? resolveListComponent : resolveNotificationsComponent;
    assert.doesNotThrow(() => resolve(prepare()));
    assert.equal(JSON.parse(prepare().designPreviewJson).items[0].extraAuthoredValue, "authored");
    runtime.items[0].undeclaredValue = "invalid";
    assert.throws(prepare, /contains undeclared fields: undeclaredValue/);
  }
});

test("collection preparation admits only declared resolved, action and boundary keys", () => {
  const definition = {
    id: "collection-01", jsonKey: "rows",
    fields: [{ id: "owner-id", jsonKey: "ownerRef", resolvedJsonKey: "resolvedOwner" }],
    itemActions: [{ id: "action-01", playInputId: "playing", timeJsonKey: "elapsed", targetFromJsonKey: "origin" }],
    fixedComponentBoundary: { variantReferenceJsonKey: "componentRef", overridesJsonKey: "localOverrides" },
    itemRuntimeContractJsonKey: "childRuntime", uiParentItemIdJsonKey: "parentId",
  };
  const item = { id: "item-01", ownerRef: "owner-01", resolvedOwner: {}, playing: true, elapsed: 5,
    origin: false, componentRef: "class::variant::variant-01", localOverrides: {}, childRuntime: {}, parentId: "parent-01" };
  assert.doesNotThrow(() => forward({}, { collections: [definition], rows: [item] }));
  for (const key of ["itemActions", "fixedComponentBoundary"] as const) {
    const removed = { ...definition, [key]: undefined };
    assert.throws(() => forward({}, { collections: [removed], rows: [item] }), /undeclared fields/);
  }
  assert.throws(() => forward({}, {
    collections: [{ ...definition, fields: [{ id: "owner-id", jsonKey: "ownerRef" }] }], rows: [item],
  }), /undeclared fields: resolvedOwner/);
});

test("collection preparation validates nested structured and embedded owner items", () => {
  const childDefinition = { id: "children-id", jsonKey: "children", fields: [{ id: "value-id", jsonKey: "value" }] };
  for (const embedded of [false, true]) {
    const definition = { id: "rows-id", jsonKey: "rows", fields: embedded ? [] : [
      { id: "children-field", jsonKey: "children", structuredCollection: childDefinition },
    ], ...(embedded ? { componentItems: { inputsJsonKey: "childInputs", overridesJsonKey: "overrides", variantReferenceJsonKey: "variant" } } : {}) };
    const child = { id: "child-01", value: false };
    const runtime = { collections: [definition], rows: [{ id: "parent-01", ...(embedded
      ? { childInputs: { collections: [childDefinition], children: [child] }, overrides: {}, variant: "class::variant::v1" }
      : { children: [child] }) }] };
    assert.doesNotThrow(() => forward({}, runtime));
    Object.assign(child, { unknown: true });
    assert.throws(() => forward({}, runtime), /item 'child-01' contains undeclared fields: unknown/);
  }
});

test("current composed collection fixtures cross the shared preparation boundary", () => {
  for (const type of ["collectionStack", "iconRow", "listItem", "bubble", "incomingCallNotification", "contentRow"]) {
    assert.doesNotThrow(() => prepareRuntimePreviewPayload(committedComponentFixture(type)), type);
  }
});

test("forwarding follows declared field and item identities after reordering", () => {
  const definition = { id: "field-01", jsonKey: "value" };
  const owner = { children: [
    { id: "item-a", output: "variant-a", $forwardedInputs: { output: definition } },
    { id: "item-b", output: "variant-b", $forwardedInputs: { output: definition } },
  ] };
  const runtime = {
    collections: [{ id: "collection-01", jsonKey: "rows", fields: [definition] }],
    rows: [{ id: "item-b", value: false }, { id: "item-a", value: null }],
    unrelated: { id: "item-a", value: "must never be scanned" },
    value: "must never be a root fallback",
  };
  const result = forward(owner, runtime);
  assert.equal(result.children[0].output, null);
  assert.equal(result.children[1].output, false);
});

test("forwarded values use the current animated frame without replacing the authored temporal envelope", () => {
  const definition = { id: "field-01", jsonKey: "value", valueKind: "StringSingleLine" };
  const runtime = { inputs: [definition], value: "authored" };
  const payload = {
    kind: "componentClass", localFrame: 7, frameRate: 25,
    configJson: JSON.stringify({ output: "override", $forwardedInputs: { output: definition } }),
    designPreviewJson: JSON.stringify(runtime), runtimeContractJson: JSON.stringify(runtime),
    themeTokensJson: "{}",
    instanceJson: JSON.stringify({ animation: { schemaVersion: 2, tracks: [
      { fieldId: definition.id, targetId: "", keyframes: [
        { frame: 0, value: "first", interpolation: "hold" },
        { frame: 7, value: "animated", interpolation: "hold" },
      ] },
    ] } }),
  } as DesignPreviewPayload;
  for (const [frame, expected] of [[0, "first"], [6, "first"], [7, "animated"], [20, "animated"]] as const) {
    const prepared = applyRuntimeInputForwarding({ ...payload, localFrame: frame });
    assert.equal(JSON.parse(prepared.configJson).output, expected);
    assert.equal(prepared.designPreviewJson, payload.designPreviewJson);
    assert.equal(prepared.runtimeContractJson, payload.runtimeContractJson);
  }
});

test("forwarding rejects ambiguous declared owners and incomplete Runtime values", () => {
  const definition = { id: "field-01", jsonKey: "value" };
  const config = { id: "item-a", $forwardedInputs: { output: definition } };
  assert.throws(() => forward(config, { inputs: [definition] }), /Missing forwarded runtime value/);
  assert.throws(() => forward(config, {
    inputs: [definition], value: "root",
    collections: [{ id: "collection-01", jsonKey: "rows", fields: [definition] }],
    rows: [{ id: "item-a", value: "item" }],
  }), /Ambiguous/);
});

test("preparing a child cannot mutate its shared Variant or local Overrides", () => {
  const variant = { owner: { array: [1], value: "base" } };
  const overrides = { owner: { value: "local" } };
  const catalog = { variants: { "class-01::variant::v1": variant } };
  const prepared = prepareComponentConfiguration(catalog, "fixture", "class-01::variant::v1", overrides);
  (prepared.owner as { array: number[] }).array.push(2);
  (prepared.owner as { value: string }).value = "modified";
  assert.deepEqual(variant, { owner: { array: [1], value: "base" } });
  assert.deepEqual(overrides, { owner: { value: "local" } });
});

test("Button and Label consume the same prepared color precedence across embedded boundaries", () => {
  const fixture = committedComponentFixture("button");
  const config = JSON.parse(fixture.configJson);
  const bases = JSON.parse(fixture.componentBaseConfigsJson);
  const preview = JSON.parse(fixture.designPreviewJson);
  config.button.contentMode = "iconText";
  config.button.appearance.iconColorToken = "theme.icons.variant";
  config.button.appearance.labelSlot.overrides = { label: { textColorToken: "theme.text.override" } };
  preview.sampleText = "Example";
  preview.inputs = preview.inputs.filter((field: { id: string }) => !["iconColorToken", "textColorToken"].includes(field.id));
  delete preview.iconColorToken;
  delete preview.textColorToken;
  const configured = resolveButtonComponentFromRecords(config, preview, bases, "button-01");
  assert.equal(configured.appearance.iconColorToken, "theme.icons.variant");
  assert.equal(configured.appearance.label?.textColorToken, "theme.text.override");
  const runtime = { ...preview, iconColorToken: "theme.icons.runtime", textColorToken: "theme.text.runtime" };
  const button = resolveButtonComponentFromRecords(config, runtime, bases, "button-01");
  assert.equal(button.appearance.iconColorToken, "theme.icons.runtime");
  assert.equal(button.appearance.label?.textColorToken, "theme.text.runtime");
  const animated = resolveRuntimeDocumentAnimationValues({
    ...runtime,
    inputs: [
      { id: "iconColorToken", jsonKey: "iconColorToken" },
      { id: "textColorToken", jsonKey: "textColorToken" },
    ],
  }, { schemaVersion: 2, tracks: ["iconColorToken", "textColorToken"].map((fieldId) => ({
    fieldId, targetId: "", keyframes: [{ frame: 4, value: "theme.colors.animated", interpolation: "hold" }],
  })) }, () => 4);
  const animatedButton = resolveButtonComponentFromRecords(config, animated.values, bases, "button-01");
  assert.equal(animatedButton.appearance.iconColorToken, "theme.colors.animated");
  assert.equal(animatedButton.appearance.label?.textColorToken, "theme.colors.animated");
  const label = committedComponentFixture("label");
  const resolvedLabel = resolveLabelComponentFromRecords(JSON.parse(label.configJson), {
    ...literalLabelPreview("Example"), textColorToken: runtime.textColorToken,
  }, bases, "label-01", staticLabelFrameContext);
  assert.equal(resolvedLabel.textColorToken, button.appearance.label?.textColorToken);
});
