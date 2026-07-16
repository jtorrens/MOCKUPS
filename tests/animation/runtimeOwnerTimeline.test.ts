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

