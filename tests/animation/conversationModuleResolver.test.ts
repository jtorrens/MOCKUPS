import assert from "node:assert/strict";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { resolveConversationModuleFrame } from "../../src/desktop-preview/conversationModuleResolver.js";

function payload(
  localFrame: number,
  tracks: Array<Record<string, unknown>>,
  messages: Array<Record<string, unknown>>,
): DesignPreviewPayload {
  return {
    kind: "moduleInstance",
    componentType: "conversation",
    configJson: "{}",
    designPreviewJson: JSON.stringify({ headerSubtitle: "base header", messages }),
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
  // m1 starts at 2. Its sequence ends at 5; m2 starts at 9.
  assert.equal((resolveConversationModuleFrame(payload(1, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!.text, "base 1");
  assert.equal((resolveConversationModuleFrame(payload(2, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!.text, "first");
  assert.equal((resolveConversationModuleFrame(payload(8, tracks, messages)).messages as Array<Record<string, unknown>>)[1]!.text, "base 2");
  assert.equal((resolveConversationModuleFrame(payload(9, tracks, messages)).messages as Array<Record<string, unknown>>)[1]!.text, "second start");
  assert.equal((resolveConversationModuleFrame(payload(11, tracks, messages)).messages as Array<Record<string, unknown>>)[1]!.text, "second end");
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
