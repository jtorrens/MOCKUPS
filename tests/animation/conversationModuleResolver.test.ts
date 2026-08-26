import assert from "node:assert/strict";
import test from "node:test";
import Database from "better-sqlite3";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  numberToken,
  renderableVisualBounds,
  renderScale,
} from "../../src/desktop-preview/componentRenderableCommon.js";
import { conversationModuleToRenderable } from "../../src/desktop-preview/conversationModuleRenderable.js";
import {
  conversationMessageActorIdentityVisible,
  resolveConversationModule,
  resolveConversationModuleFrame,
} from "../../src/desktop-preview/conversationModuleResolver.js";
import { parityDatabasePath } from "../../src/development-scaffolding/parityDatabasePath.js";
import type { RenderableNode } from "../../src/visual/renderable/types.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

function payload(
  localFrame: number,
  tracks: Array<Record<string, unknown>>,
  messages: Array<Record<string, unknown>>,
): DesignPreviewPayload {
  const completeMessages = messages.map((message) => {
    const { writeOnDurationFrames = 0, ...authoredMessage } = message;
    return {
      actorId: "actor_test",
      actor: {
        id: "actor_test",
        displayName: "Test Actor",
        shortName: "Test",
        avatar: {
          imageUri: "",
          backgroundColor: "#ffffff",
          textColor: "#000000",
          scale: 1,
          offsetX: 0,
          offsetY: 0,
          baseSize: 640,
        },
      },
      delayAfterPreviousFrames: 0,
      statusVisible: false,
      statusState: "none",
      statusText: "",
      mediaType: "none",
      mediaSource: "",
      viewportSize: "240|160",
      mediaScale: 1,
      mediaOffset: "0|0",
      isPlaying: false,
      currentTimeSeconds: 0,
      durationSeconds: 12,
      playbackMode: "once",
      playDurationFrames: 72,
      isFullScreen: false,
      fullScreenTransition: false,
      fullframeOrientation: "portrait",
      controlsElapsedMs: 0,
      visibleDurationFrames: 0,
      writeOnTiming: {
        mode: "fixed",
        fixedFrames: writeOnDurationFrames,
        paceToken: "theme.motion.naturalPace.normal",
      },
      postWriteOnHoldFrames: 0,
      visibleDurationFrames: 0,
      isPlaying: false,
      ...authoredMessage,
    };
  });
  const fields = [
    { id: "actor", jsonKey: "actorId" },
    { id: "direction", jsonKey: "direction", animationTimeline: { origin: { kind: "ownerStart" } } },
    { id: "text", jsonKey: "text", animationTimeline: { origin: { kind: "ownerStart" }, completion: { baseDurationFieldId: "writeOn", minimumEnabledKeyframes: 2 } } },
    { id: "delay", jsonKey: "delayAfterPreviousFrames" },
    {
      id: "writeOn",
      jsonKey: "writeOnTiming",
      valueKind: "BehaviorTiming",
      naturalTiming: { sourceFieldId: "text", unit: "grapheme", baseFramesPerUnit: 7 },
    },
    { id: "postWriteOnHold", jsonKey: "postWriteOnHoldFrames" },
    { id: "visibleDuration", jsonKey: "visibleDurationFrames" },
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
    frameRate: 30,
    localFrame,
    configJson: "{}",
    designPreviewJson: JSON.stringify({
      headerSubtitle: "base header",
      bubbleRevealMode: "afterWriteOn",
      incomingRevealMode: "typingIndicator",
      textInputVisible: true,
      keyboardVisible: true,
      typingIndicatorText: "•••",
      typingIndicatorSizeToken: "theme.typography.sizes.m",
      typingIndicatorAnimation: "pulsating",
      messages: completeMessages,
      inputs: [
        { id: "actor", jsonKey: "actorId", animationTimeline: { origin: { kind: "ownerStart" } } },
        { id: "headerSubtitle", jsonKey: "headerSubtitle", animationTimeline: { origin: { kind: "ownerStart" } } },
      ],
      collections: [{
        id: "messages",
        jsonKey: "messages",
        sourceCollectionJsonKey: "messages",
        animationTimeline: {
          sequence: "serial",
          sequenceCompletionFieldIds: ["text"],
          preDurationFieldIds: ["delay"],
          postDurationFieldIds: ["postWriteOnHold"],
          presenceDurationFieldId: "visibleDuration",
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
      context: { screenFrame: localFrame },
      animation: { schemaVersion: 2, tracks },
    }),
    previewFrame: {
      canvasWidth: 1,
      canvasHeight: 1,
      screenX: 0,
      screenY: 0,
      screenWidth: 1,
      screenHeight: 1,
      moduleTransparency: { enabled: false, mode: "fixed", paletteColor: "gray_000", backgroundOpacity: 1, fixedStart: 0, minimumOpaqueExtent: 0, gradientHeight: 1, variableOffset: 0 },
    },
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

test("a message owner phase freezes incoming content at its initial write state", () => {
  const current = payload(2, [], [{
    id: "incoming",
    direction: "incoming",
    text: "hello",
    mediaType: "image",
    mediaSource: "media/image.png",
    writeOnDurationFrames: 8,
  }]);
  const motion = {
    transition: "slide",
    direction: "bottom",
    bounds: "parent",
    fade: true,
    translate: true,
    scale: false,
  };
  const preview = JSON.parse(current.designPreviewJson) as Record<string, unknown>;
  preview.conversationType = "individual";
  preview.incomingRevealMode = "typingIndicator";
  const collections = preview.collections as Array<Record<string, unknown>>;
  collections[0]!.animationTimeline = {
    ...(collections[0]!.animationTimeline as Record<string, unknown>),
    ownerPhase: {
      kind: "resolvedMotion",
      motion,
    },
  };
  const resolved = resolveConversationModule({
    ...current,
    configJson: JSON.stringify({
      conversation: {
        showKeyboard: false,
        showTextInputBar: false,
        messageMotion: motion,
        messageReflowTiming: { durationMs: 240, easing: "linear" },
      },
    }),
    designPreviewJson: JSON.stringify(preview),
    runtimeContractJson: JSON.stringify(preview),
    themeTokensJson: JSON.stringify({
      motion: { transitions: { slide: { delayMs: 0, durationMs: 200, easing: "linear", intensity: 1 } } },
    }),
  });
  const message = resolved.visibleMessages[0]!;
  assert.equal(message.isTypingIndicator, true);
  assert.equal(message.mediaType, "none");
  assert.equal(message.text, "•••");
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
    { id: "first", direction: "outgoing", text: "base", delayAfterPreviousFrames: 0, writeOnDurationFrames: 10 },
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
  assert.equal(resolvedMessages[0]!.composerWriteOnDurationFrames, 0);
  assert.equal(resolvedMessages[0]!.composerWriteOnFrame, 3);
  assert.equal(
    Number(resolvedMessages[0]!.timelineEndFrame) - Number(resolvedMessages[0]!.timelineStartFrame),
    3,
  );
  assert.equal(resolvedMessages[1]!.text, "second start");
});

test("message arrival follows text completion while finite media continues independently", () => {
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
  const beforeMediaSequence = resolveConversationModuleFrame(payload(5, mediaExtended, [first, second])).messages as Array<Record<string, unknown>>;
  const resolvedMedia = resolveConversationModuleFrame(payload(6, mediaExtended, [first, second])).messages as Array<Record<string, unknown>>;
  // max(write 2, local end 6) + hold 1 + second delay 3 = Screen frame 10.
  assert.equal(resolvedText[1]!.text, "second start");
  // Media remains active on the first message timeline, while text end 2 + hold 1
  // and the next message delay 3 place the next origin at Screen frame 6.
  assert.equal(beforeMediaSequence[1]!.text, "second base");
  assert.equal(resolvedMedia[1]!.text, "second start");
});

test("message direction resolves hold keyframes without replacing its Actor", () => {
  const messages = [{ id: "m1", actorId: "sam", direction: "incoming", text: "hello" }];
  const tracks = [track("direction", "m1", [
    { id: "d0", frame: 0, value: "incoming", interpolation: "hold" },
    { id: "d1", frame: 5, value: "outgoing", interpolation: "hold" },
  ])];
  const before = (resolveConversationModuleFrame(payload(4, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!;
  const after = (resolveConversationModuleFrame(payload(5, tracks, messages)).messages as Array<Record<string, unknown>>)[0]!;
  assert.equal(before.actorId, "sam");
  assert.equal(before.direction, "incoming");
  assert.equal(after.actorId, "sam");
  assert.equal(after.direction, "outgoing");
});

test("chat Actor resolves hold keyframes through the prepared record catalog", () => {
  const source = payload(5, [track("actor", "", [
    { id: "a0", frame: 0, value: "actor_alex", interpolation: "hold" },
    { id: "a1", frame: 5, value: "actor_sam", interpolation: "hold" },
  ])], []);
  source.designPreviewJson = JSON.stringify({
    ...JSON.parse(source.designPreviewJson),
    actorId: "actor_alex",
    actor: { id: "actor_alex", displayName: "Alex" },
  });
  source.runtimeRecordReferencesJson = JSON.stringify({
    actors: {
      actor_alex: { id: "actor_alex", displayName: "Alex" },
      actor_sam: { id: "actor_sam", displayName: "Sam" },
    },
  });
  const resolved = resolveConversationModuleFrame(source);
  assert.equal(resolved.actorId, "actor_sam");
  assert.deepEqual(resolved.actor, { id: "actor_sam", displayName: "Sam" });
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

test("hold full-screen keyframes derive the owning Media Motion clock in both directions", () => {
  const messages = [{
    id: "m1",
    direction: "incoming",
    text: "image",
    mediaType: "image",
    isFullScreen: false,
    fullScreenTransition: false,
  }];
  const tracks = [track("fullScreen", "m1", [
    { id: "f0", frame: 0, value: false, interpolation: "hold" },
    { id: "f1", frame: 10, value: true, interpolation: "hold" },
    { id: "f2", frame: 20, value: false, interpolation: "hold" },
  ])];
  const at = (frame: number) => {
    const resolved = resolveConversationModuleFrame(
      payload(frame, tracks, messages),
    ).messages as Array<Record<string, unknown>>;
    return resolved[0]!;
  };

  assert.equal(at(9).fullScreenTransition, false);
  assert.equal(at(10).isFullScreen, true);
  assert.equal(at(10).fullScreenTransition, true);
  assert.equal(at(10).motionElapsedMs, 0);
  assert.equal(at(11).motionElapsedMs, 1000 / 30);
  assert.equal(at(20).isFullScreen, false);
  assert.equal(at(20).fullScreenTransition, true);
  assert.equal(at(20).motionElapsedMs, 0);
  assert.equal(at(21).motionElapsedMs, 1000 / 30);
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

test("Conversation positions each message from the resolved Bubble alignment", () => {
  const source = committedConversationPayload(true);
  const config = JSON.parse(source.configJson) as {
    conversation: {
      bubbleSlot: { overrides: Record<string, unknown> };
      screenGutter: string;
    };
  };
  config.conversation.bubbleSlot.overrides = {
    ...config.conversation.bubbleSlot.overrides,
    bubble: {
      incomingAlignment: "right",
      systemAlignment: "left",
      outgoingAlignment: "center",
    },
  };
  source.configJson = JSON.stringify(config);
  setConversationFrame(source, 239);

  const bubbles = findNodes(
    conversationModuleToRenderable(source),
    "component.bubble",
  );
  assert.equal(bubbles.length, 3);
  const [incoming, outgoing, system] = bubbles.map(renderableVisualBounds);
  const [gutterToken] = config.conversation.screenGutter.split("|");
  const gutter = numberToken(source, gutterToken!) * renderScale(source);
  const screenLeft = source.previewFrame.screenX;
  const screenRight = screenLeft + source.previewFrame.screenWidth;
  const screenCenter = screenLeft + source.previewFrame.screenWidth / 2;

  assert.ok(Math.abs(system!.x - (screenLeft + gutter)) < 0.001);
  assert.ok(Math.abs((incoming!.x + incoming!.width) - (screenRight - gutter)) < 0.001);
  assert.ok(Math.abs((outgoing!.x + outgoing!.width / 2) - screenCenter) < 0.001);
});

test("Conversation reserves outgoing Bubble final height throughout write-on", () => {
  const source = committedConversationPayload(true);
  const config = JSON.parse(source.configJson) as {
    conversation: {
      bubbleSlot: { overrides: Record<string, unknown> };
      showHeader: boolean;
      showStatusBar: boolean;
    };
  };
  config.conversation.bubbleSlot.overrides = {
    ...config.conversation.bubbleSlot.overrides,
    bubble: {
      outgoingAlignment: "center",
    },
  };
  config.conversation.showHeader = false;
  config.conversation.showStatusBar = false;
  source.configJson = JSON.stringify(config);
  source.previewFrame = {
    ...source.previewFrame,
    canvasHeight: 70,
    screenHeight: 70,
  };
  const runtime = JSON.parse(source.designPreviewJson) as {
    messages: Array<Record<string, unknown>>;
  };
  const outgoing = runtime.messages.find(({ direction }) => direction === "outgoing")!;
  runtime.messages = [
    {
      ...outgoing,
      id: "write-on-outgoing",
      text: "A long outgoing message that wraps across several lines while it is written.",
      delayAfterPreviousFrames: 0,
      postWriteOnHoldFrames: 0,
      writeOnTiming: {
        mode: "fixed",
        fixedFrames: 40,
        paceToken: "theme.motion.naturalPace.normal",
      },
    },
  ];
  source.designPreviewJson = JSON.stringify(runtime);

  setConversationFrame(source, 12);
  const during = findNodes(
    conversationModuleToRenderable(source),
    "component.bubble",
  ).map(renderableVisualBounds);
  setConversationFrame(source, 40);
  const complete = findNodes(
    conversationModuleToRenderable(source),
    "component.bubble",
  ).map(renderableVisualBounds);

  assert.equal(during.length, 1);
  assert.equal(complete.length, 1);
  assert.ok(during[0]!.height < complete[0]!.height);
  assert.ok(Math.abs(during[0]!.y - complete[0]!.y) < 0.001);
});

test("Conversation writes outgoing text in Bubble when its composer slots are absent", () => {
  const source = committedConversationPayload(true);
  const config = JSON.parse(source.configJson) as {
    conversation: Record<string, unknown>;
  };
  config.conversation.showKeyboard = false;
  config.conversation.showTextInputBar = false;
  config.conversation.bubbleRevealMode = "afterWriteOn";
  source.configJson = JSON.stringify(config);
  const runtime = JSON.parse(source.designPreviewJson) as {
    messages: Array<Record<string, unknown>>;
  };
  const outgoing = runtime.messages.find(({ direction }) => direction === "outgoing")!;
  runtime.messages = [{
    ...outgoing,
    id: "outgoing",
    direction: "outgoing",
    text: "Writing in Bubble",
    delayAfterPreviousFrames: 0,
    postWriteOnHoldFrames: 0,
    visibleDurationFrames: 0,
    isPlaying: false,
    writeOnTiming: {
      mode: "fixed",
      fixedFrames: 12,
      paceToken: "theme.motion.naturalPace.normal",
    },
  }];
  source.designPreviewJson = JSON.stringify(runtime);
  const instance = JSON.parse(source.instanceJson) as Record<string, unknown>;
  instance.context = { screenFrame: 10 };
  source.instanceJson = JSON.stringify(instance);
  source.localFrame = 10;

  const resolved = resolveConversationModule(source);
  assert.equal(resolved.composer.keyboardVisible, false);
  assert.equal(resolved.composer.textInputVisible, false);
  assert.equal(resolved.visibleMessages[0]?.text, "Writing in Bubble");
  assert.equal(resolved.visibleMessages[0]?.writeOnTrigger, true);
});

test("Conversation rejects the retired incoming instant reveal mode", () => {
  const source = committedConversationPayload();
  const config = JSON.parse(source.configJson) as {
    conversation: Record<string, unknown>;
  };
  config.conversation.incomingRevealMode = "instant";
  source.configJson = JSON.stringify(config);
  const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  runtime.incomingRevealMode = "instant";
  source.designPreviewJson = JSON.stringify(runtime);
  assert.throws(
    () => resolveConversationModule(source),
    /Unsupported Conversation incoming reveal mode instant/,
  );
});

test("Conversation timing requires one complete prepared Runtime document", () => {
  for (const key of [
    "bubbleRevealMode",
    "incomingRevealMode",
    "textInputVisible",
    "keyboardVisible",
    "typingIndicatorText",
    "typingIndicatorSizeToken",
    "typingIndicatorAnimation",
  ]) {
    const source = committedConversationPayload();
    const config = JSON.parse(source.configJson) as {
      conversation: Record<string, unknown>;
    };
    config.conversation[key] = key.endsWith("Visible") ? true : "legacy-value";
    source.configJson = JSON.stringify(config);
    const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
    delete runtime[key];
    source.designPreviewJson = JSON.stringify(runtime);
    assert.throws(
      () => resolveConversationModule(source),
      new RegExp(`module\\.core\\.chat\\.input\\.${key}`),
      key,
    );
  }
});

test("Conversation rejects unsupported prepared Runtime timing options", () => {
  for (const [key, expected] of [
    ["bubbleRevealMode", /Unsupported Conversation bubble reveal mode legacy/],
    ["typingIndicatorAnimation", /Unsupported Conversation typing indicator animation legacy/],
  ] as const) {
    const source = committedConversationPayload();
    const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
    runtime[key] = "legacy";
    source.designPreviewJson = JSON.stringify(runtime);
    assert.throws(() => resolveConversationModule(source), expected, key);
  }
});

test("Conversation requires every authored field in each prepared Runtime message", () => {
  const requiredKeys = [
    "direction",
    "text",
    "delayAfterPreviousFrames",
    "writeOnTiming",
    "postWriteOnHoldFrames",
    "statusVisible",
    "statusState",
    "statusText",
    "mediaType",
    "mediaSource",
    "viewportSize",
    "mediaScale",
    "mediaOffset",
    "isPlaying",
    "currentTimeSeconds",
    "durationSeconds",
    "playbackMode",
    "playDurationFrames",
    "isFullScreen",
    "fullScreenTransition",
    "fullframeOrientation",
    "controlsElapsedMs",
    "visibleDurationFrames",
  ];
  const source = committedConversationPayload(true);
  const complete = JSON.parse(source.designPreviewJson) as {
    messages: Array<Record<string, unknown>>;
  };
  const first = complete.messages[0]!;
  for (const key of requiredKeys) {
    const runtime = structuredClone(complete);
    runtime.messages[0] = { ...first };
    delete runtime.messages[0]![key];
    source.designPreviewJson = JSON.stringify(runtime);
    assert.throws(
      () => resolveConversationModule(source),
      new RegExp(`module\\.core\\.chat\\.messages\\[0\\].*${key}`),
      key,
    );
  }
});

test("Conversation Header keeps its upward bleed and can use the resolved Actor color", () => {
  const source = committedConversationPayload();
  const config = JSON.parse(source.configJson) as {
    conversation: {
      headerUseActorColor: boolean;
      showHeaderSeparator: boolean;
    };
  };
  const actorColor = (
    JSON.parse(source.designPreviewJson) as {
      actor: { avatar: { backgroundColor: string } };
    }
  ).actor.avatar.backgroundColor;

  config.conversation.headerUseActorColor = false;
  source.configJson = JSON.stringify(config);
  const surfaceColorTree = conversationModuleToRenderable(source);
  const surfaceColorNode = findNode(
    surfaceColorTree,
    "module.core.chat.header.surface",
  );
  const headerNode = findNode(surfaceColorTree, "module.core.chat.header");
  const statusNode = findNode(surfaceColorTree, "status_bar");
  assert.ok(surfaceColorNode);
  assert.ok(headerNode?.box);
  assert.ok(statusNode?.box);
  assert.deepEqual(surfaceColorNode.box, {
    x: 0,
    y: 0,
    width: 360,
    height: statusNode.box.height + 40,
  });
  assert.equal(headerNode.box.y, statusNode.box.height);
  assert.notEqual(surfaceColorNode.style?.background, actorColor);

  config.conversation.showHeaderSeparator = false;
  source.configJson = JSON.stringify(config);
  assert.equal(
    findNode(
      conversationModuleToRenderable(source),
      "module.core.chat.header.separator",
    ),
    undefined,
  );

  config.conversation.headerUseActorColor = true;
  config.conversation.showHeaderSeparator = true;
  source.configJson = JSON.stringify(config);
  const actorColorNode = findNode(
    conversationModuleToRenderable(source),
    "module.core.chat.header.surface",
  );
  assert.ok(actorColorNode);
  assert.equal(actorColorNode.style?.background, actorColor);
  assert.equal(actorColorNode.style?.borderRadius, 0);

  source.authoringOwnerId = "module_core_chat::variant::default";
  source.authoringRecordClassId = "module.core.chat";
  const authoringNode = findNode(
    conversationModuleToRenderable(source),
    "module.core.chat.header.surface",
  );
  assert.deepEqual(authoringNode?.metadata?.authoringTarget, {
    focusFieldId: "component.surface.backgroundColorToken",
    ownerId: "module_core_chat::variant::default",
    slotFieldIds: ["module.core.chat.headerSurface.editor"],
  });
  const authoringAvatar = findNode(
    conversationModuleToRenderable(source),
    "module.core.chat.header.avatar",
  );
  assert.deepEqual(authoringAvatar?.metadata?.authoringTarget, {
    focusFieldId: "component.avatar.defaultSize",
    ownerId: "module_core_chat::variant::default",
    slotFieldIds: ["module.core.chat.headerAvatar.editor"],
  });
  const headerVideoButton = findNode(
    conversationModuleToRenderable(source),
    "module.core.chat.header.right.button_001",
  );
  assert.deepEqual(headerVideoButton?.metadata?.authoringTarget, {
    focusFieldId: "component.iconRow.items",
    focusItemId: "button_001",
    ownerId: "module_core_chat::variant::default",
    slotFieldIds: ["module.core.chat.headerRightIconRow.editor"],
  });
});

test("Conversation message presence ends at its explicit resolved Out", () => {
  const source = committedConversationPayload(true);
  const runtime = JSON.parse(source.designPreviewJson) as {
    messages: Array<Record<string, unknown>>;
  };
  const original = runtime.messages[0]!;
  runtime.messages = [{
    ...original,
    id: "presence-message",
    direction: "incoming",
    delayAfterPreviousFrames: 0,
    postWriteOnHoldFrames: 0,
    visibleDurationFrames: 10,
    writeOnTiming: {
      mode: "fixed",
      fixedFrames: 0,
      paceToken: "theme.motion.naturalPace.normal",
    },
  }];
  source.designPreviewJson = JSON.stringify(runtime);
  source.screenTiming = {
    screenFrame: 0,
    transitionFrameCount: 0,
    actionDelayFrames: 0,
    actionDurationFrames: 100,
    actionStartFrame: 0,
  };
  const at = (frame: number) => {
    const instance = JSON.parse(source.instanceJson) as Record<string, unknown>;
    instance.context = { screenFrame: frame };
    source.instanceJson = JSON.stringify(instance);
    source.localFrame = frame;
    source.screenTiming = { ...source.screenTiming!, screenFrame: frame };
    return resolveConversationModule(source).visibleMessages;
  };
  assert.equal(at(0)[0]?.presenceMotionKind, "enter");
  assert.equal(at(9)[0]?.presenceMotionKind, "exit");
  assert.equal(at(10).length, 0);

  runtime.messages[0]!.visibleDurationFrames = 0;
  source.designPreviewJson = JSON.stringify(runtime);
  assert.equal(at(99).length, 1);
  assert.equal(at(99)[0]?.presenceMotionKind, undefined);
  assert.equal(at(100).length, 0);
});

test("Conversation uses one reflow timing when a message appears", () => {
  const source = committedConversationPayload(true);
  const runtime = JSON.parse(source.designPreviewJson) as {
    messages: Array<Record<string, unknown>>;
  };
  const original = runtime.messages[0]!;
  const message = (id: string, delayAfterPreviousFrames: number) => ({
    ...original,
    id,
    direction: "incoming",
    delayAfterPreviousFrames,
    postWriteOnHoldFrames: 0,
    visibleDurationFrames: 0,
    writeOnTiming: {
      mode: "fixed",
      fixedFrames: 0,
      paceToken: "theme.motion.naturalPace.normal",
    },
  });
  runtime.messages = [message("first", 0), message("second", 10)];
  source.designPreviewJson = JSON.stringify(runtime);

  const appearanceFrame = Array.from({ length: 30 }, (_, frame) => frame)
    .find((frame) => {
      setConversationFrame(source, frame);
      return resolveConversationModule(source).visibleMessages.length === 2;
    });
  assert.notEqual(appearanceFrame, undefined);
  setConversationFrame(source, appearanceFrame!);
  const started = resolveConversationModule(source);
  assert.deepEqual(started.visibleMessages.map(({ id }) => id), ["first", "second"]);
  assert.deepEqual(started.messageReflow?.fromMessages.map(({ id }) => id), ["first"]);
  assert.equal(started.messageReflow?.progress, 0);

  setConversationFrame(source, appearanceFrame! + 4);
  const moving = resolveConversationModule(source);
  assert.ok((moving.messageReflow?.progress ?? 0) > 0);
  assert.ok((moving.messageReflow?.progress ?? 1) < 1);
});

test("Conversation uses the same reflow timing after a message Out completes", () => {
  const source = committedConversationPayload(true);
  const runtime = JSON.parse(source.designPreviewJson) as {
    messages: Array<Record<string, unknown>>;
  };
  const original = runtime.messages[0]!;
  const message = (id: string, visibleDurationFrames: number) => ({
    ...original,
    id,
    direction: "incoming",
    delayAfterPreviousFrames: 0,
    postWriteOnHoldFrames: 0,
    visibleDurationFrames,
    writeOnTiming: {
      mode: "fixed",
      fixedFrames: 0,
      paceToken: "theme.motion.naturalPace.normal",
    },
  });
  runtime.messages = [message("leaving", 10), message("remaining", 0)];
  source.designPreviewJson = JSON.stringify(runtime);

  setConversationFrame(source, 10);
  const started = resolveConversationModule(source);
  assert.deepEqual(started.visibleMessages.map(({ id }) => id), ["remaining"]);
  assert.deepEqual(
    started.messageReflow?.fromMessages.map(({ id }) => id),
    ["leaving", "remaining"],
  );
  assert.equal(started.messageReflow?.progress, 0);

  const remainingY = (frame: number) => {
    setConversationFrame(source, frame);
    const bubble = findNodes(
      conversationModuleToRenderable(source),
      "component.bubble",
    )[0];
    assert.ok(bubble);
    return renderableVisualBounds(bubble).y;
  };
  const startY = remainingY(10);
  const middleY = remainingY(14);
  const finalY = remainingY(18);
  assert.ok(startY > middleY);
  assert.ok(middleY > finalY);

  setConversationFrame(source, 14);
  const moving = resolveConversationModule(source);
  assert.ok((moving.messageReflow?.progress ?? 0) > 0);
  assert.ok((moving.messageReflow?.progress ?? 1) < 1);
});

test("a nested full-screen Media keeps the exact root Screen coordinates", () => {
  const source = committedConversationPayload();
  const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  runtime.messages = [{
    id: "fullscreen-image",
    actorId: runtime.actorId,
    actor: runtime.actor,
    direction: "outgoing",
    text: "Image",
    delayAfterPreviousFrames: 0,
    postWriteOnHoldFrames: 0,
    statusVisible: false,
    statusState: "none",
    statusText: "",
    mediaType: "image",
    mediaSource: "",
    viewportSize: "240|160",
    mediaScale: 1,
    mediaOffset: "0|0",
    isPlaying: false,
    currentTimeSeconds: 0,
    durationSeconds: 12,
    playbackMode: "once",
    playDurationFrames: 72,
    isFullScreen: true,
    fullScreenTransition: false,
    fullframeOrientation: "portrait",
    controlsElapsedMs: 0,
    visibleDurationFrames: 0,
    writeOnTiming: {
      mode: "fixed",
      fixedFrames: 0,
      paceToken: "theme.motion.naturalPace.normal",
    },
  }];
  source.designPreviewJson = JSON.stringify(runtime);
  const tree = conversationModuleToRenderable(source);
  const overlay = findRootOverlay(tree);
  assert.ok(overlay);
  assert.deepEqual(overlay.box, {
    x: source.previewFrame.screenX,
    y: source.previewFrame.screenY,
    width: source.previewFrame.screenWidth,
    height: source.previewFrame.screenHeight,
  });
});

function committedConversationPayload(keepMessages = false): DesignPreviewPayload {
  const source = committedComponentFixture("avatar", "avatar_chat_header");
  const database = new Database(
    parityDatabasePath(),
    { readonly: true, fileMustExist: true },
  );
  try {
    const module = database.prepare(`
      SELECT design_preview_json, metadata_json
      FROM modules
      WHERE id = 'module_core_chat'
    `).get() as {
      design_preview_json: string;
      metadata_json: string;
    } | undefined;
    assert.ok(module);
    const metadata = JSON.parse(module.metadata_json) as {
      variants: Array<{ id: string; config: Record<string, unknown> }>;
    };
    const defaultVariant = metadata.variants.find(({ id }) => id === "default");
    assert.ok(defaultVariant);
    const runtime = JSON.parse(module.design_preview_json) as Record<string, unknown>;
    runtime.actor = {
      id: "actor_header_test",
      displayName: "Header Actor",
      shortName: "Actor",
      initials: "HA",
      avatar: {
        imageUri: "",
        backgroundColor: "#123456",
        textColor: "#ffffff",
        scale: 1,
        offsetX: 0,
        offsetY: 0,
        baseSize: 640,
      },
    };
    if (!keepMessages) runtime.messages = [];
    runtime.keyboardVisible = false;
    runtime.textInputVisible = false;
    const config = structuredClone(defaultVariant.config) as {
      conversation: Record<string, unknown>;
    };
    config.conversation.useAppWallpaper = false;
    config.conversation.showNavigationBar = false;
    config.conversation.showKeyboard = false;
    config.conversation.showTextInputBar = false;
    return {
      ...source,
      kind: "module",
      componentType: "module.core.chat",
      configJson: JSON.stringify(config),
      designPreviewJson: JSON.stringify(runtime),
      runtimeContractJson: JSON.stringify(runtime),
      themeStatusBarVariantReference:
        "component_project_foqn_s2_status_bar::variant::default",
    };
  } finally {
    database.close();
  }
}

function setConversationFrame(source: DesignPreviewPayload, frame: number) {
  const instance = JSON.parse(source.instanceJson) as Record<string, unknown>;
  instance.context = { screenFrame: frame };
  source.instanceJson = JSON.stringify(instance);
  source.localFrame = frame;
  source.screenTiming = {
    screenFrame: frame,
    transitionFrameCount: 0,
    actionDelayFrames: 0,
    actionDurationFrames: 240,
    actionStartFrame: 0,
  };
}

function findNode(root: RenderableNode, id: string): RenderableNode | undefined {
  if (root.id === id) return root;
  for (const child of root.children ?? []) {
    const match = findNode(child, id);
    if (match) return match;
  }
  return undefined;
}

function findNodes(root: RenderableNode, id: string): RenderableNode[] {
  return [
    ...(root.id === id ? [root] : []),
    ...(root.children ?? []).flatMap((child) => findNodes(child, id)),
  ];
}

function findRootOverlay(root: RenderableNode): RenderableNode | undefined {
  if (root.style?.rootOverlay === true) return root;
  for (const child of root.children ?? []) {
    const match = findRootOverlay(child);
    if (match) return match;
  }
  return undefined;
}
