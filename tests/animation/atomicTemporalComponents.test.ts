import assert from "node:assert/strict";
import test from "node:test";

import { resolveAudioComponent } from "../../src/desktop-preview/audioComponentResolver.js";
import { resolveBubbleComponent } from "../../src/desktop-preview/bubbleComponentResolver.js";
import { resolveButtonComponent } from "../../src/desktop-preview/buttonComponentResolver.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { resolveDrawPasswordComponent } from "../../src/desktop-preview/drawPasswordComponentResolver.js";
import { resolveFaceRecognitionComponent } from "../../src/desktop-preview/faceRecognitionComponentResolver.js";
import { resolveFingerprintComponent } from "../../src/desktop-preview/fingerprintComponentResolver.js";
import { resolveKeyboardComponent } from "../../src/desktop-preview/keyboardComponentResolver.js";
import { resolveKeypadComponent } from "../../src/desktop-preview/keypadComponentResolver.js";
import { resolveMediaComponent } from "../../src/desktop-preview/mediaComponentResolver.js";
import { mediaComponentToRenderableAt } from "../../src/desktop-preview/mediaComponentRenderable.js";
import { resolveNotificationComponent } from "../../src/desktop-preview/notificationComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Button push resolves the pushed state without render-time interpretation", () => {
  const source = withInputDefaults(committedComponentFixture("button"));
  assert.notEqual(resolveButtonComponent(source).state, "pushed");
  assert.equal(
    resolveButtonComponent(withValues(source, {
      pushTrigger: true,
      pushElapsedMs: 50,
    })).state,
    "pushed",
  );
});

test("Audio playback resolves clamped and looped progress", () => {
  const source = withInputDefaults(committedComponentFixture("audio"));
  assert.equal(resolveAudioComponent(withValues(source, {
    currentTimeSeconds: 2,
    durationSeconds: 5,
  })).playback.progress, 0.4);
  assert.equal(resolveAudioComponent(withValues(source, {
    currentTimeSeconds: 8,
    durationSeconds: 5,
    playbackMode: "once",
  })).playback.currentTimeSeconds, 5);
  assert.equal(resolveAudioComponent(withValues(source, {
    currentTimeSeconds: 8,
    durationSeconds: 5,
    playbackMode: "loop",
  })).playback.currentTimeSeconds, 3);
});

test("Notification display changes resolve their reflow state and source label", () => {
  const source = withInputDefaults(committedComponentFixture("notification"));
  const theme = JSON.parse(source.themeTokensJson) as {
    motion: { reflowDurationMs: number };
  };
  const resolved = resolveNotificationComponent(withValues(source, {
    displayMode: "detail",
    displayModeFrom: "summary",
    displayModeTransition: true,
    displayModeElapsedMs: theme.motion.reflowDurationMs / 2,
  }));
  assert.ok(resolved.reflow);
  assert.ok(resolved.reflow.progress > 0 && resolved.reflow.progress < 1);
  assert.notEqual(resolved.reflow.fromLabel.subtext, resolved.label.subtext);
});

test("Keyboard resolves the pressed grapheme and entrance Motion from runtime state", () => {
  const source = withInputDefaults(committedComponentFixture("keyboard"));
  const resolved = resolveKeyboardComponent(withValues(source, {
    text: "A",
    currentCharacter: 1,
    trigger: true,
    motionElapsedMs: 150,
  }));
  assert.equal(resolved.pressedKey, "A");
  assert.equal(resolved.mode, "shift");
  assert.equal(resolved.motionFrame.active, true);
  assert.ok(resolved.motionFrame.progress > 0);
});

test("Keypad resolves the exact pushed key and preserves all other key states", () => {
  const source = withInputDefaults(committedComponentFixture("keypad"));
  const resolved = resolveKeypadComponent(withValues(source, {
    pushedKey: "5",
    pushTrigger: true,
    pushElapsedMs: 50,
  }));
  assert.equal(resolved.keys.find((key) => key.value === "5")?.state, "pushed");
  assert.equal(resolved.keys.find((key) => key.value === "4")?.state, "normal");
});

test("Media resolves playback, controls fade and full-screen Motion before painting", () => {
  const source = withInputDefaults(committedComponentFixture("media"));
  const resolved = resolveMediaComponent(withValues(source, {
    isPlaying: true,
    currentTimeSeconds: 2,
    durationSeconds: 12,
    controlsElapsedMs: 100000,
    isFullScreen: true,
    fullScreenTransition: true,
    motionElapsedMs: 100,
  }));
  assert.equal(resolved.playbackState, "playing");
  assert.equal(resolved.currentTimeSeconds, 2);
  assert.equal(resolved.controlsOpacity, 0);
  assert.equal(resolved.displayState, "fullframe");
  assert.equal(resolved.motionFrame.active, true);
  assert.ok(resolved.motionFrame.progress > 0);
});

test("Media full-screen uses the complete root Screen frame", () => {
  const source = withInputDefaults(committedComponentFixture("media"));
  const rootPreviewFrame = {
    ...source.previewFrame,
    screenX: 10,
    screenY: 20,
    screenWidth: 300,
    screenHeight: 600,
  };
  const fullScreen = resolveMediaComponent(withValues(source, {
    isFullScreen: true,
    fullScreenTransition: false,
  }));
  const node = mediaComponentToRenderableAt(
    { ...source, rootPreviewFrame },
    fullScreen,
    { x: 80, y: 140, width: 120, height: 80 },
  );
  assert.deepEqual(node.box, {
    x: 10,
    y: 20,
    width: 300,
    height: 600,
  });
  assert.deepEqual(
    node.children?.find((child) => child.id.endsWith(".visualClip"))?.box,
    node.box,
  );
  assert.equal(node.style?.rootOverlay, true);
  assert.equal(node.style?.rootOverlayTranslationFactor, 0);
});

test("Bubble resolves write-on and embedded video state from the same owner frame", () => {
  const source = withInputDefaults(committedComponentFixture("bubble"));
  const text = "Resolver owned";
  const writeOn = resolveBubbleComponent(withValues(source, {
    sampleText: text,
    writeOnTrigger: true,
    writeOnFrame: 2,
    writeOnDurationFrames: 20,
  }));
  assert.ok(writeOn.textBox.text.length > 0);
  assert.ok(writeOn.textBox.text.length < text.length);
  assert.equal(writeOn.textBox.cursorVisible, true);

  const video = resolveBubbleComponent(withValues(source, {
    mediaType: "video",
    isPlaying: true,
    currentTimeSeconds: 2,
    durationSeconds: 12,
    isFullScreen: true,
    fullScreenTransition: true,
    motionElapsedMs: 100,
  }));
  assert.equal(video.mediaSlot.media?.playbackState, "playing");
  assert.equal(video.mediaSlot.media?.displayState, "fullframe");
  assert.equal(video.mediaSlot.media?.motionFrame.active, true);
});

test("Authentication primitives expose exact bounded progress and visible pattern state", () => {
  const fingerprint = withInputDefaults(committedComponentFixture("fingerprint"));
  assert.equal(resolveFingerprintComponent(withValues(fingerprint, {
    state: "active",
    progress: 0.25,
  })).progress, 0.25);

  const face = withInputDefaults(committedComponentFixture("faceRecognition"));
  assert.equal(resolveFaceRecognitionComponent(withValues(face, {
    state: "correct",
    progress: 1,
  })).state, "correct");

  const draw = withInputDefaults(committedComponentFixture("drawPassword"));
  const resolvedDraw = resolveDrawPasswordComponent(withValues(draw, {
    state: "active",
    pattern: "1236",
    visibleCount: 2,
  }));
  assert.deepEqual(resolvedDraw.pattern, [1, 2, 3, 6]);
  assert.equal(resolvedDraw.visibleCount, 2);
});

function withInputDefaults(source: DesignPreviewPayload): DesignPreviewPayload {
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown> & {
    inputs?: Array<{
      jsonKey: string;
      kind: string;
      defaultValue: unknown;
    }>;
  };
  for (const input of preview.inputs ?? []) {
    if (Object.hasOwn(preview, input.jsonKey)) continue;
    preview[input.jsonKey] = parsedDefault(input.kind, input.defaultValue);
  }
  return withDocument(source, preview);
}

function parsedDefault(kind: string, value: unknown) {
  if (kind === "number") return Number(value);
  if (kind === "boolean") return value === true || value === "true";
  if (kind === "behaviorTiming" && typeof value === "string") return JSON.parse(value);
  return value;
}

function withValues(
  source: DesignPreviewPayload,
  values: Record<string, unknown>,
) {
  const document = {
    ...JSON.parse(source.designPreviewJson) as Record<string, unknown>,
    ...values,
  };
  return withDocument(source, document);
}

function withDocument(
  source: DesignPreviewPayload,
  document: Record<string, unknown>,
) {
  const serialized = JSON.stringify(document);
  return {
    ...source,
    designPreviewJson: serialized,
    runtimeContractJson: serialized,
  };
}
