import assert from "node:assert/strict";
import test from "node:test";
import { RuntimeOwnerTimeline } from "../../src/desktop-preview/runtimeOwnerTimeline.js";

const contract = {
  collections: [
    {
      jsonKey: "slots",
      animationTimeline: { sequenceItems: false },
      fields: [{
        id: "state",
        jsonKey: "runtimeStateId",
        animationTimeline: { extendsOwnerDuration: false },
      }],
    },
    {
      jsonKey: "states",
      animationTimeline: {
        sequenceItems: false,
        ownerOrigin: {
          kind: "firstMatchingValue",
          sourceCollectionJsonKey: "slots",
          sourceTargetIdJsonKey: "slotId",
          sourceFieldId: "state",
          sourceValueJsonKey: "runtimeStateId",
          matchValueJsonKey: "id",
        },
      },
      fields: [
        { id: "slotId", jsonKey: "slotId" },
        { id: "text", jsonKey: "text" },
      ],
    },
  ],
};

const runtime = {
  slots: [{ id: "slot-1", runtimeStateId: "state-clock" }],
  states: [
    { id: "state-password", slotId: "slot-1", text: "Password" },
    { id: "state-clock", slotId: "slot-1", text: "Clock" },
  ],
};

const animation = {
  schemaVersion: 2,
  tracks: [
    {
      id: "selector",
      fieldId: "state",
      targetId: "slot-1",
      keyframes: [
        { id: "selector-0", frame: 0, value: "state-clock", enabled: true },
        { id: "selector-10", frame: 10, value: "state-password", enabled: true },
        { id: "selector-30", frame: 30, value: "state-clock", enabled: true },
        { id: "selector-40", frame: 40, value: "state-password", enabled: true },
      ],
    },
    {
      id: "password-text",
      fieldId: "text",
      targetId: "state-password",
      keyframes: [
        { id: "password-text-0", frame: 0, value: "Password", enabled: true },
        { id: "password-text-5", frame: 5, value: "Ready", enabled: true },
      ],
    },
  ],
};

test("entity-owned keyframes use first appearance and do not restart on re-entry", () => {
  const timeline = new RuntimeOwnerTimeline(contract, runtime, animation);
  assert.equal(timeline.screenFrame("text", "state-clock", 0), 0);
  assert.equal(timeline.screenFrame("text", "state-password", 0), 10);
  assert.equal(timeline.screenFrame("text", "state-password", 5), 15);
  assert.equal(timeline.localFrame("text", "state-password", 15), 5);
  assert.equal(timeline.localFrame("text", "state-password", 40), 30);
});

test("finite runtime action durations require positive JSON numbers", () => {
  const finiteContract = {
    collections: [{
      jsonKey: "items",
      animationTimeline: { sequenceItems: false },
      fields: [{ id: "play", jsonKey: "isPlaying" }],
      itemActions: [{
        extendsModuleDuration: true,
        playInputId: "play",
        durationInputId: "durationFrames",
        durationEnabledInputId: "isPlaying",
      }],
    }],
  };
  const finiteAnimation = {
    tracks: [{
      fieldId: "play",
      targetId: "item",
      keyframes: [{ frame: 0, value: false }, { frame: 2, value: true }],
    }],
  };

  assert.equal(new RuntimeOwnerTimeline(
    finiteContract,
    { items: [{ id: "item", isPlaying: false, durationFrames: 4 }] },
    finiteAnimation,
  ).durationFrames, 6);
  assert.throws(() => new RuntimeOwnerTimeline(
    finiteContract,
    { items: [{ id: "item", isPlaying: false, durationFrames: "4" }] },
    finiteAnimation,
  ));
  assert.throws(() => new RuntimeOwnerTimeline(
    finiteContract,
    { items: [{ id: "item", isPlaying: false, durationFrames: 0 }] },
    finiteAnimation,
  ));
});

test("runtime owner timeline rejects filtered contract envelopes", () => {
  assert.doesNotThrow(() => new RuntimeOwnerTimeline({}, {}, {}));
  assert.doesNotThrow(() => new RuntimeOwnerTimeline({}, {}, {
    tracks: [{ fieldId: "screenField", targetId: "", keyframes: [] }],
  }));

  const invalidAnimations: Array<Record<string, unknown>> = [
    { tracks: null },
    { tracks: [4] },
    { tracks: [{ fieldId: "" }] },
    { tracks: [{ fieldId: "field", targetId: 4 }] },
    { tracks: [{ fieldId: "field", keyframes: {} }] },
    { tracks: [{ fieldId: "field", keyframes: [null] }] },
    { tracks: [{ fieldId: "field", keyframes: [{ frame: "0" }] }] },
    { tracks: [{ fieldId: "field", keyframes: [{ frame: 0.5 }] }] },
    { tracks: [{ fieldId: "field", keyframes: [{ frame: 0, enabled: "true" }] }] },
    { retime: null },
    { retime: [] },
    { retime: { targetDurationFrames: 0 } },
    { retime: { targetDurationFrames: "4" } },
    { retime: { targets: [] } },
    { retime: { targets: { item: null } } },
    { retime: { targets: { item: { targetDurationFrames: 0 } } } },
  ];
  for (const invalidAnimation of invalidAnimations) {
    assert.throws(() => new RuntimeOwnerTimeline({}, {}, invalidAnimation));
  }

  const invalidCases: Array<[Record<string, unknown>, Record<string, unknown>]> = [
    [{ collections: null }, {}],
    [{ collections: [4] }, {}],
    [{ inputs: {} }, {}],
    [{ actions: [null] }, {}],
    [{ collections: [{}] }, {}],
    [{ collections: [{ jsonKey: 4 }] }, {}],
    [{ collections: [{ storageCollectionJsonKey: "", jsonKey: "items" }] }, {}],
    [{ collections: [{ sourceCollectionJsonKey: 4, jsonKey: "items" }] }, {}],
    [{ collections: [{ jsonKey: "items" }, { storageCollectionJsonKey: "items" }] }, {}],
    [{ collections: [{ jsonKey: "first" }, { jsonKey: "second" }] }, {
      first: [{ id: "item" }],
      second: [{ id: "item" }],
    }],
    [{ inputs: [{ id: "value" }, { id: "value" }] }, {}],
    [{ collections: [{ jsonKey: "items", fields: [{ id: "value" }, { id: "value" }] }] }, {}],
    [{ collections: [{ jsonKey: "items" }] }, { items: {} }],
    [{ collections: [{ jsonKey: "items" }] }, { items: [null] }],
    [{ collections: [{ jsonKey: "items" }] }, { items: [{ id: "" }] }],
    [{ collections: [{ jsonKey: "items", fields: {} }] }, { items: [{ id: "item" }] }],
    [{ collections: [{ jsonKey: "items", itemActions: [null] }] }, { items: [{ id: "item" }] }],
    [{ collections: [{ jsonKey: "items", animationTimeline: null }] }, { items: [{ id: "item" }] }],
    [{
      collections: [{ jsonKey: "items", itemRuntimeContractJsonKey: "runtimeContract" }],
    }, { items: [{ id: "item" }] }],
    [{
      collections: [{ jsonKey: "items", itemRuntimeContractJsonKey: "runtimeContract" }],
    }, { items: [{ id: "item", runtimeContract: { inputs: null } }] }],
    [{
      collections: [{
        jsonKey: "items",
        componentItems: { inputsJsonKey: "inputs" },
      }],
    }, { items: [{ id: "item" }] }],
    [{
      collections: [{
        jsonKey: "items",
        animationTimeline: { postDurationFieldIds: ["hold", 4] },
        fields: [{ id: "hold", jsonKey: "hold" }],
      }],
    }, { items: [{ id: "item", hold: 0 }] }],
  ];

  for (const [invalidContract, invalidRuntime] of invalidCases) {
    assert.throws(() => new RuntimeOwnerTimeline(invalidContract, invalidRuntime, {}));
  }

  const invalidTimelineContracts: Array<Record<string, unknown>> = [
    { collections: [{ jsonKey: "items", animationTimeline: { sequence: "parallel" } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { sequenceItems: "false" } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { ownerOrigin: null } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { ownerOrigin: { kind: "ownerStart" } } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { ownerOrigin: { kind: "firstMatchingValue" } } }] },
    { inputs: [{ id: "field", animationTimeline: { extendsOwnerDuration: "false" } }] },
    { inputs: [{ id: "field", animationTimeline: { origin: null } }] },
    { inputs: [{ id: "field", animationTimeline: { origin: { kind: "unknown" } } }] },
    { inputs: [{ id: "field", animationTimeline: { origin: { kind: "fieldCompletion", fieldId: "source" } } }] },
    { inputs: [{ id: "field", animationTimeline: { origin: { kind: "fieldCompletion", fieldId: "source", offsetFrames: -1 } } }] },
    { inputs: [{ id: "field", animationTimeline: { completion: null } }] },
    { inputs: [{ id: "field", animationTimeline: { completion: {} } }] },
    { inputs: [{ id: "field", animationTimeline: { completion: { baseDurationFieldId: "duration", trackOverride: "first" } } }] },
    { inputs: [{ id: "field", animationTimeline: { completion: { baseDurationFieldId: "duration", minimumEnabledKeyframes: 1 } } }] },
  ];
  for (const invalidContract of invalidTimelineContracts) {
    assert.throws(() => new RuntimeOwnerTimeline(invalidContract, {}, {}));
  }

  const missingDurationField = {
    collections: [{
      jsonKey: "items",
      fields: [{
        id: "text",
        jsonKey: "text",
        animationTimeline: { completion: { baseDurationFieldId: "missing", minimumEnabledKeyframes: 2 } },
      }],
    }],
  };
  assert.throws(() => new RuntimeOwnerTimeline(
    missingDurationField,
    { items: [{ id: "item", text: "value" }] },
    {},
  ));

  const missingPreDurationValue = {
    collections: [{
      jsonKey: "items",
      animationTimeline: { preDurationFieldIds: ["delay"] },
      fields: [{ id: "delay", jsonKey: "delay" }],
    }],
  };
  assert.throws(() => new RuntimeOwnerTimeline(
    missingPreDurationValue,
    { items: [{ id: "item" }] },
    {},
  ));
  assert.throws(() => new RuntimeOwnerTimeline(
    missingPreDurationValue,
    { items: [{ id: "item", delay: "2" }] },
    {},
  ));
});
