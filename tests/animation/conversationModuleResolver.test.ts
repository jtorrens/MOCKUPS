import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { resolveConversationModuleFrame } from "../../src/desktop-preview/conversationModuleResolver.js";
import { conversationMessageActorIdentityVisible } from "../../src/desktop-preview/conversationModuleRenderable.js";

function payload(
  localFrame: number,
  tracks: Array<Record<string, unknown>>,
  messages: Array<Record<string, unknown>>,
): DesignPreviewPayload {
  const completeMessages = messages.map((message) => {
    const { writeOnDurationFrames = 0, ...authoredMessage } = message;
    return {
      delayAfterPreviousFrames: 0,
      writeOnTiming: {
        mode: "fixed",
        fixedFrames: writeOnDurationFrames,
        paceToken: "theme.motion.naturalPace.normal",
      },
      postWriteOnHoldFrames: 0,
      isPlaying: false,
      ...authoredMessage,
    };
  });
  const fields = [
    { id: "text", jsonKey: "text", animationTimeline: { origin: { kind: "ownerStart" }, completion: { baseDurationFieldId: "writeOn", minimumEnabledKeyframes: 2 } } },
    { id: "delay", jsonKey: "delayAfterPreviousFrames" },
    {
      id: "writeOn",
      jsonKey: "writeOnTiming",
      valueKind: "BehaviorTiming",
      naturalTiming: { sourceFieldId: "text", unit: "grapheme", baseFramesPerUnit: 7 },
    },
    { id: "postWriteOnHold", jsonKey: "postWriteOnHoldFrames" },
    { id: "statusVisible", jsonKey: "statusVisible", animationTimeline: { origin: { kind: "fieldCompletion", fieldId: "text", offsetFrames: 0 }, extendsOwnerDuration: false } },
    { id: "status", jsonKey: "statusState", animationTimeline: { origin: { kind: "fieldCompletion", fieldId: "text", offsetFrames: 0 }, extendsOwnerDuration: false } },
    { id: "statusText", jsonKey: "statusText", animationTimeline: { origin: { kind: "fieldCompletion", fieldId: "text", offsetFrames: 0 }, extendsOwnerDuration: false } },
    { id: "isPlaying", jsonKey: "isPlaying", animationTimeline: { origin: { kind: "fieldCompletion", fieldId: "text", offsetFrames: 0 } } },
    { id: "playDuration", jsonKey: "playDurationFrames" },
    { id: "fullScreen", jsonKey: "isFullScreen", animationTimeline: { origin: { kind: "fieldCompletion", fieldId: "text", offsetFrames: 0 } } },
  ];
  return {
    kind: "moduleInstance",
    componentType: "conversation",
    configJson: "{}",
    designPreviewJson: JSON.stringify({
      headerSubtitle: "base header",
      messages: completeMessages,
      inputs: [{ id: "headerSubtitle", jsonKey: "headerSubtitle", animationTimeline: { origin: { kind: "ownerStart" } } }],
      collections: [{
        id: "messages",
        jsonKey: "messages",
        sourceCollectionJsonKey: "messages",
        animationTimeline: {
          sequence: "serial",
          preDurationFieldIds: ["delay"],
          postDurationFieldIds: ["postWriteOnHold"],
        },
        fields,
        itemActions: [{
          id: "play",
          extendsModuleDuration: true,
          playInputId: "isPlaying",
          durationInputId: "playDuration",
          durationEnabledInputId: "isPlaying",
        }],
      }],
    }),
    instanceJson: JSON.stringify({
      context: { localFrame },
      animation: { schemaVersion: 2, tracks },
    }),
    previewFrame: { canvasWidth: 1, canvasHeight: 1, screenX: 0, screenY: 0, screenWidth: 1, screenHeight: 1 },
    themeMode: "dark",
    themeTokensJson: "{}",
  };
}

function track(fieldId: string, targetId: string, keyframes: Array<Record<string, unknown>>) {
  return { id: `${fieldId}-${targetId || "screen"}`, fieldId, targetId, keyframes };
}

test("only group incoming messages expose per-message Actor identity", () => {
  assert.equal(conversationMessageActorIdentityVisible("group", "incoming"), true);
  assert.equal(conversationMessageActorIdentityVisible("group", "outgoing"), false);
  assert.equal(conversationMessageActorIdentityVisible("group", "system"), false);
  assert.equal(conversationMessageActorIdentityVisible("individual", "incoming"), false);
});

test("Conversation requires its current messages collection", () => {
  const current = payload(0, [], []);
  const preview = JSON.parse(current.designPreviewJson) as Record<string, unknown>;
  delete preview.messages;
  assert.throws(
    () => resolveConversationModuleFrame({
      ...current,
      designPreviewJson: JSON.stringify(preview),
    }),
    /module\.conversation runtime 'messages' is required/,
  );
});

test("Conversation rejects an unsupported message direction", () => {
  assert.throws(
    () => resolveConversationModuleFrame(payload(0, [], [{
      id: "m1",
      direction: "plausible",
      text: "hello",
    }])),
    /unsupported direction 'plausible'/,
  );
});

test("Screen-owned header animation uses the Screen frame", () => {
  const resolved = resolveConversationModuleFrame(payload(3, [
    track("headerSubtitle", "", [
      { id: "h0", frame: 0, value: "zero", interpolation: "hold" },
      { id: "h3", frame: 3, value: "three", interpolation: "hold" },
    ]),
  ], []));
  assert.equal(resolved.headerSubtitle, "three");
});

test("message tracks use each message start as local frame zero", () => {
  const messages = [
    { id: "m1", direction: "incoming", text: "base 1", delayAfterPreviousFrames: 2, writeOnDurationFrames: 3 },
    { id: "m2", direction: "outgoing", text: "base 2", delayAfterPreviousFrames: 4, writeOnDurationFrames: 2, postWriteOnHoldFrames: 1 },
  ];
  const tracks = [
    track("text", "m1", [{ id: "m10", frame: 0, value: "first", interpolation: "hold" }]),
    track("text", "m2", [
      { id: "m20", frame: 0, value: "second start", interpolation: "hold" },
      { id: "m21", frame: 2, value: "second end", interpolation: "hold" },
    ]),
  ];
  // A lone origin keyframe keeps the base write-on: m1 ends at 5 and m2 starts at 9.
  assert.equal((resolveConversationModuleFrame(payload(1, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!.text, "base 1");
  assert.equal((resolveConversationModuleFrame(payload(2, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!.text, "first");
  assert.equal((resolveConversationModuleFrame(payload(8, tracks, messages)).messages as Array<Record<string, unknown>>)[1]!.text, "base 2");
  assert.equal((resolveConversationModuleFrame(payload(9, tracks, messages)).messages as Array<Record<string, unknown>>)[1]!.text, "second start");
  assert.equal((resolveConversationModuleFrame(payload(11, tracks, messages)).messages as Array<Record<string, unknown>>)[1]!.text, "second end");
});

test("reordering or inserting messages moves the origin without changing local keyframes", () => {
  const before = { id: "before", direction: "incoming", text: "before", delayAfterPreviousFrames: 2, writeOnDurationFrames: 3 };
  const inserted = { id: "inserted", direction: "incoming", text: "inserted", delayAfterPreviousFrames: 1, writeOnDurationFrames: 4 };
  const target = { id: "target", direction: "incoming", text: "base", delayAfterPreviousFrames: 4, writeOnDurationFrames: 2 };
  const tracks = [track("text", "target", [
    { id: "t0", frame: 0, value: "local zero", interpolation: "hold" },
    { id: "t2", frame: 2, value: "local two", interpolation: "hold" },
  ])];
  const targetText = (frame: number, messages: Array<Record<string, unknown>>) =>
    (resolveConversationModuleFrame(payload(frame, tracks, messages)).messages as Array<Record<string, unknown>>)
      .find((message) => message.id === "target")!.text;

  // Original origin: 2 + 3 + 4 = 9.
  assert.equal(targetText(10, [before, target]), "local zero");
  assert.equal(targetText(11, [before, target]), "local two");
  // Insertion contributes five frames, moving the origin to 14 without rewriting frame 2.
  assert.equal(targetText(15, [before, inserted, target]), "local zero");
  assert.equal(targetText(16, [before, inserted, target]), "local two");
  // Reordering target first moves its origin to 4; the authored frame remains 2.
  assert.equal(targetText(5, [target, before]), "local zero");
  assert.equal(targetText(6, [target, before]), "local two");
});

test("changing a message delay shifts its animation without rewriting local keyframes", () => {
  const target = {
    id: "target",
    direction: "incoming",
    text: "base",
    delayAfterPreviousFrames: 2,
    writeOnDurationFrames: 2,
  };
  const tracks = [track("text", "target", [
    { id: "t0", frame: 0, value: "local zero", interpolation: "hold" },
    { id: "t2", frame: 2, value: "local two", interpolation: "hold" },
  ])];
  const textAt = (frame: number, delay: number) =>
    (resolveConversationModuleFrame(payload(frame, tracks, [{
      ...target,
      delayAfterPreviousFrames: delay,
    }])).messages as Array<Record<string, unknown>>)[0]!.text;

  assert.equal(textAt(2, 2), "local zero");
  assert.equal(textAt(4, 2), "local two");
  assert.equal(textAt(5, 5), "local zero");
  assert.equal(textAt(7, 5), "local two");
});

test("an animated text track replaces the base write-on duration", () => {
  const messages = [
    { id: "first", direction: "incoming", text: "base", delayAfterPreviousFrames: 0, writeOnDurationFrames: 10 },
    { id: "second", direction: "incoming", text: "second base", delayAfterPreviousFrames: 0, writeOnDurationFrames: 1 },
  ];
  const tracks = [
    track("text", "first", [
      { id: "f0", frame: 0, value: "start", interpolation: "hold" },
      { id: "f2", frame: 2, value: "finish", interpolation: "writeOn" },
    ]),
    track("text", "second", [{ id: "s0", frame: 0, value: "second start", interpolation: "hold" }]),
  ];

  const resolved = resolveConversationModuleFrame(payload(3, tracks, messages));
  const resolvedMessages = resolved.messages as Array<Record<string, unknown>>;
  assert.equal(resolvedMessages[0]!.writeOnDurationFrames, 0);
  assert.equal(
    Number(resolvedMessages[0]!.timelineEndFrame) - Number(resolvedMessages[0]!.timelineStartFrame),
    3,
  );
  assert.equal(resolvedMessages[1]!.text, "second start");
});

test("a prior message extends the chain by its last keyframe or finite media end", () => {
  const first = {
    id: "first",
    direction: "incoming",
    text: "first",
    delayAfterPreviousFrames: 0,
    writeOnDurationFrames: 2,
    postWriteOnHoldFrames: 1,
    isPlaying: false,
    playDurationFrames: 5,
  };
  const second = {
    id: "second",
    direction: "incoming",
    text: "second base",
    delayAfterPreviousFrames: 3,
    writeOnDurationFrames: 1,
  };
  const secondTrack = track("text", "second", [{ id: "s0", frame: 0, value: "second start" }]);
  const textExtended = [
    track("text", "first", [
      { id: "f0", frame: 0, value: "first" },
      { id: "f5", frame: 5, value: "late" },
    ]),
    secondTrack,
  ];
  const mediaExtended = [
    track("isPlaying", "first", [
      { id: "p0", frame: 0, value: false },
      { id: "p1", frame: 1, value: true },
    ]),
    secondTrack,
  ];
  const resolvedText = resolveConversationModuleFrame(payload(10, textExtended, [first, second])).messages as Array<Record<string, unknown>>;
  const resolvedMedia = resolveConversationModuleFrame(payload(12, mediaExtended, [first, second])).messages as Array<Record<string, unknown>>;
  // max(write 2, local end 6) + hold 1 + second delay 3 = Screen frame 10.
  assert.equal(resolvedText[1]!.text, "second start");
  // Media is relative to text completion: text end 2 + local keyframe 1 + five finite frames,
  // then hold 1 and the next message delay 3 place the next origin at Screen frame 12.
  assert.equal(resolvedMedia[1]!.text, "second start");
});

test("actor and direction are never rewritten by animation tracks", () => {
  const messages = [{ id: "m1", actorId: "sam", direction: "incoming", text: "hello" }];
  const tracks = [
    track("actorId", "m1", [{ id: "a0", frame: 0, value: "alex" }]),
    track("direction", "m1", [{ id: "d0", frame: 0, value: "outgoing" }]),
  ];
  const message = (resolveConversationModuleFrame(payload(0, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!;
  assert.equal(message.actorId, "sam");
  assert.equal(message.direction, "incoming");
});

test("delivery, status and full-screen fields resolve independently", () => {
  const messages = [{
    id: "m1",
    direction: "incoming",
    text: "hello",
    statusVisible: false,
    statusState: "sent",
    statusText: "old",
    isFullScreen: false,
  }];
  const tracks = [
    track("statusVisible", "m1", [{ id: "v0", frame: 0, value: true }]),
    track("status", "m1", [{ id: "s0", frame: 0, value: "read" }]),
    track("statusText", "m1", [{ id: "t0", frame: 0, value: "new" }]),
    track("fullScreen", "m1", [{ id: "f0", frame: 0, value: true }]),
  ];
  const message = (resolveConversationModuleFrame(payload(0, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!;
  assert.equal(message.statusVisible, true);
  assert.equal(message.statusState, "read");
  assert.equal(message.statusText, "new");
  assert.equal(message.isFullScreen, true);
});

test("non-extending delivery keyframes overlap later messages without pushing their start", () => {
  const messages = [
    { id: "first", direction: "incoming", text: "first", writeOnDurationFrames: 2, statusVisible: false },
    { id: "second", direction: "incoming", text: "second base", delayAfterPreviousFrames: 3, writeOnDurationFrames: 1 },
  ];
  const tracks = [
    track("statusVisible", "first", [
      { id: "status0", frame: 0, value: false },
      { id: "status30", frame: 30, value: true },
    ]),
    track("text", "second", [{ id: "second0", frame: 0, value: "second start" }]),
  ];
  const atFive = resolveConversationModuleFrame(payload(5, tracks, messages)).messages as Array<Record<string, unknown>>;
  const atThirtyTwo = resolveConversationModuleFrame(payload(32, tracks, messages)).messages as Array<Record<string, unknown>>;
  assert.equal(atFive[1]!.text, "second start");
  assert.equal(atFive[0]!.statusVisible, false);
  assert.equal(atThirtyTwo[0]!.statusVisible, true);
});

test("animated media playing is always finite", () => {
  const messages = [{ id: "m1", direction: "incoming", text: "hello", isPlaying: false, playDurationFrames: 3 }];
  const tracks = [track("isPlaying", "m1", [
    { id: "p0", frame: 1, value: true, interpolation: "hold" },
  ])];
  const at = (frame: number) => (resolveConversationModuleFrame(payload(frame, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!;
  assert.equal(at(0).isPlaying, false);
  assert.equal(at(1).isPlaying, true);
  assert.equal(at(3).isPlaying, true);
  assert.equal(at(4).isPlaying, false);
  assert.equal(at(99).isPlaying, false);
  assert.equal(at(99).playbackFrame, 3);
});
