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

test("serial collection offsets may overlap the preceding item", () => {
  const serialContract = {
    collections: [{
      jsonKey: "items",
      animationTimeline: {
        sequenceItems: true,
        preDurationFieldIds: ["delay"],
        sequenceCompletionFieldIds: ["text"],
      },
      fields: [
        { id: "delay", jsonKey: "delay" },
        {
          id: "text",
          jsonKey: "text",
          animationTimeline: {
            completion: { baseDurationFieldId: "duration" },
          },
        },
        { id: "duration", jsonKey: "duration" },
      ],
    }],
  };
  const timeline = new RuntimeOwnerTimeline(
    serialContract,
    { items: [
      { id: "first", delay: 2, text: "One", duration: 6 },
      { id: "second", delay: -2, text: "Two", duration: 3 },
    ] },
    {},
  );
  assert.equal(timeline.itemStartFrame("first"), 2);
  assert.equal(timeline.itemEndFrame("first"), 8);
  assert.equal(timeline.itemStartFrame("second"), 6);
});

test("collection presence duration is independent from serial completion", () => {
  const presenceContract = {
    collections: [{
      jsonKey: "items",
      animationTimeline: {
        sequenceItems: true,
        preDurationFieldIds: ["delay"],
        sequenceCompletionFieldIds: ["text"],
        presenceDurationFieldId: "visibleDuration",
      },
      fields: [
        { id: "delay", jsonKey: "delay" },
        { id: "visibleDuration", jsonKey: "visibleDurationFrames" },
        { id: "text", jsonKey: "text", animationTimeline: { completion: { baseDurationFieldId: "write" } } },
        { id: "write", jsonKey: "writeFrames" },
      ],
    }],
  };
  const timeline = new RuntimeOwnerTimeline(
    presenceContract,
    { items: [
      { id: "first", delay: 2, visibleDurationFrames: 0, text: "One", writeFrames: 6 },
      { id: "second", delay: 3, visibleDurationFrames: 20, text: "Two", writeFrames: 4 },
    ] },
    {},
  );
  assert.equal(timeline.itemStartFrame("first"), 2);
  assert.equal(timeline.itemEndFrame("first"), 8);
  assert.equal(timeline.itemPresenceEndFrame("first", 100), 100);
  assert.equal(timeline.itemStartFrame("second"), 11);
  assert.equal(timeline.itemPresenceEndFrame("second", 100), 31);
  assert.equal(timeline.itemHasExplicitPresenceEnd("first"), false);
  assert.equal(timeline.itemHasExplicitPresenceEnd("second"), true);
});

test("a declared owner motion phase precedes every relative item field and serial successor", () => {
  const timeline = new RuntimeOwnerTimeline(
    {
      collections: [{
        jsonKey: "items",
        animationTimeline: {
          sequenceItems: true,
          ownerPhase: {
            kind: "resolvedMotion",
            motion: {
              transition: "slide",
              direction: "bottom",
              bounds: "parent",
              fade: false,
              translate: true,
              scale: false,
            },
          },
          sequenceCompletionFieldIds: ["text"],
        },
        fields: [
          { id: "text", jsonKey: "text", animationTimeline: { completion: { baseDurationFieldId: "write" } } },
          { id: "write", jsonKey: "write" },
        ],
      }],
    },
    { items: [
      { id: "first", text: "One", write: 4 },
      { id: "second", text: "Two", write: 2 },
    ] },
    {},
    {
      motion: {
        transitions: {
          slide: { delayMs: 0, durationMs: 200, easing: "linear", intensity: 1 },
        },
      },
    },
    0,
    25,
  );
  assert.equal(timeline.screenFrame("text", "first", 0), 5);
  assert.equal(timeline.itemStartFrame("second"), 9);
  assert.equal(timeline.screenFrame("text", "second", 0), 14);
});

test("finite runtime action durations require positive JSON numbers", () => {
  const finiteContract = {
    collections: [{
      jsonKey: "items",
      animationTimeline: { sequenceItems: false },
      fields: [
        { id: "play", jsonKey: "isPlaying" },
        { id: "duration", jsonKey: "durationFrames" },
      ],
      itemActions: [{
        id: "play",
        extendsModuleDuration: true,
        playInputId: "play",
        durationInputId: "duration",
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
  assert.throws(() => new RuntimeOwnerTimeline(
    {
      collections: [{
        ...finiteContract.collections[0],
        itemActions: [{
          ...finiteContract.collections[0].itemActions[0],
          durationInputId: "durationFrames",
        }],
      }],
    },
    { items: [{ id: "item", isPlaying: false, durationFrames: 4 }] },
    finiteAnimation,
  ));
});

test("explicit collection sequence completion fields do not delay later items with independent actions", () => {
  const sequenceContract = {
    collections: [{
      jsonKey: "messages",
      animationTimeline: {
        sequence: "serial",
        sequenceCompletionFieldIds: ["text"],
        postDurationFieldIds: ["hold"],
      },
      fields: [
        {
          id: "text",
          jsonKey: "text",
          animationTimeline: { completion: { baseDurationFieldId: "write", minimumEnabledKeyframes: 2 } },
        },
        { id: "write", jsonKey: "write" },
        { id: "hold", jsonKey: "hold" },
        { id: "play", jsonKey: "isPlaying" },
        { id: "duration", jsonKey: "durationFrames" },
      ],
      itemActions: [{
        id: "play",
        extendsModuleDuration: true,
        playInputId: "play",
        durationInputId: "duration",
        durationEnabledInputId: "isPlaying",
      }],
    }],
  };
  const sequenceRuntime = {
    messages: [
      { id: "first", text: "first", write: 2, hold: 1, isPlaying: false, durationFrames: 10 },
      { id: "second", text: "second", write: 1, hold: 0, isPlaying: false, durationFrames: 1 },
    ],
  };
  const sequenceAnimation = {
    tracks: [{
      fieldId: "play",
      targetId: "first",
      keyframes: [{ frame: 0, value: false }, { frame: 1, value: true }],
    }],
  };
  const timeline = new RuntimeOwnerTimeline(
    sequenceContract,
    sequenceRuntime,
    sequenceAnimation,
  );

  assert.equal(timeline.itemStartFrame("second"), 3);
  assert.equal(timeline.durationFrames, 11);
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
    { tracks: [
      { fieldId: "field", targetId: "item", keyframes: [] },
      { fieldId: "field", targetId: "item", keyframes: [] },
    ] },
    { tracks: [
      { fieldId: "field", keyframes: [] },
      { fieldId: "field", targetId: "", keyframes: [] },
    ] },
    { tracks: [{ fieldId: "field", keyframes: [{ frame: 0 }, { frame: 0 }] }] },
    { tracks: [{ fieldId: "field", keyframes: [{ frame: 2 }, { frame: 1 }] }] },
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

  const invalidActionCases: Array<[Record<string, unknown>, Record<string, unknown>]> = [
    [{ actions: [{ definesModuleDuration: "true" }] }, {}],
    [{ actions: [{ definesModuleDuration: true, durationBaseFrames: 1 }] }, {}],
    [{ actions: [{ id: "duration", definesModuleDuration: true, durationBaseFrames: "1" }] }, {}],
    [{ collections: [{ jsonKey: "items", itemActions: [{ extendsModuleDuration: "true" }] }] }, {
      items: [{ id: "item", enabled: false }],
    }],
    [{ collections: [{
      jsonKey: "items",
      fields: [
        { id: "play" },
        { id: "duration", jsonKey: "durationFrames" },
      ],
      itemActions: [{ id: "play", extendsModuleDuration: true, playInputId: "play", durationInputId: "duration" }],
    }] }, { items: [{ id: "item", enabled: false }] }],
    [{ collections: [{
      jsonKey: "items",
      fields: [{ id: "play" }],
      itemActions: [{
        id: "play", extendsModuleDuration: true, playInputId: "play", playFieldId: "",
        durationInputId: "duration", durationEnabledInputId: "enabled",
      }],
    }] }, { items: [{ id: "item", enabled: false }] }],
    [{ collections: [{
      jsonKey: "items",
      fields: [{ id: "other" }],
      itemActions: [{
        id: "play", extendsModuleDuration: true, playInputId: "play",
        durationInputId: "duration", durationEnabledInputId: "enabled",
      }],
    }] }, { items: [{ id: "item", enabled: false }] }],
  ];
  for (const [invalidContract, invalidRuntime] of invalidActionCases) {
    assert.throws(() => new RuntimeOwnerTimeline(invalidContract, invalidRuntime, {}));
  }

  const finiteActionContract = {
    collections: [{
      jsonKey: "items",
      fields: [
        { id: "play" },
        { id: "duration", jsonKey: "durationFrames" },
      ],
      itemActions: [{
        id: "play", extendsModuleDuration: true, playInputId: "play",
        durationInputId: "duration", durationEnabledInputId: "enabled",
      }],
    }],
  };
  assert.throws(() => new RuntimeOwnerTimeline(
    finiteActionContract,
    { items: [{ id: "item", durationFrames: 4 }] },
    {},
  ));
  assert.throws(() => new RuntimeOwnerTimeline(
    finiteActionContract,
    { items: [{ id: "item", enabled: "false", durationFrames: 4 }] },
    {},
  ));
  assert.throws(() => new RuntimeOwnerTimeline(
    finiteActionContract,
    { items: [{ id: "item", enabled: false, durationFrames: 4 }] },
    { tracks: [{ fieldId: "play", targetId: "item", keyframes: [{ frame: 0, value: "true" }] }] },
  ));
  assert.equal(new RuntimeOwnerTimeline(
    finiteActionContract,
    { items: [{ id: "item", enabled: false }] },
    {},
  ).durationFrames, 1);

  const invalidTimelineContracts: Array<Record<string, unknown>> = [
    { collections: [{ jsonKey: "items", animationTimeline: { sequence: "parallel" } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { sequenceItems: "false" } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { sequenceCompletionFieldIds: "text" } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { sequenceCompletionFieldIds: ["missing"] } }] },
    { collections: [{ jsonKey: "items", animationTimeline: { sequenceCompletionFieldIds: ["text", "text"] }, fields: [{ id: "text" }] }] },
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
  for (const [index, invalidContract] of invalidTimelineContracts.entries()) {
    assert.throws(
      () => new RuntimeOwnerTimeline(invalidContract, {}, {}),
      `invalid timeline contract ${index}`,
    );
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
