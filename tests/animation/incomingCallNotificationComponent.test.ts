import assert from "node:assert/strict";
import test from "node:test";

import { incomingCallNotificationComponentToRenderable } from "../../src/desktop-preview/incomingCallNotificationComponentRenderable.js";
import { resolveIncomingCallNotificationComponent } from "../../src/desktop-preview/incomingCallNotificationComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

const fixture = (variantId = "default") =>
  committedComponentFixture("incomingCallNotification", variantId);

test("Incoming Call Notification resolves exact iOS and Android child Variant boundaries", () => {
  const iosSource = fixture();
  const androidSource = fixture("android");
  assert.equal(iosSource.designPreviewJson, androidSource.designPreviewJson);
  const runtime = JSON.parse(iosSource.designPreviewJson) as {
    iconRowRuntime: Array<{
      runtimeInputs: {
        buttonInputs: Array<Record<string, unknown>>;
      };
    }>;
  };
  assert.ok(runtime.iconRowRuntime[0]!.runtimeInputs.buttonInputs.every((button) =>
    !Object.hasOwn(button, "contentMode")));

  const ios = resolveIncomingCallNotificationComponent(iosSource);
  const android = resolveIncomingCallNotificationComponent(androidSource);

  assert.equal(ios.layout, "compact");
  assert.deepEqual(ios.size, { width: 360, height: 88 });
  assert.equal(ios.iconRow.items[0]?.button.contentMode, "icon");
  assert.deepEqual(
    ios.iconRow.items.map((item) => item.button.size),
    [{ width: 52, height: 52 }, { width: 52, height: 52 }],
  );
  assert.deepEqual(
    ios.iconRow.items.map((item) => item.button.stateStyle.surface.backgroundColorToken),
    ["theme.colors.negative", "theme.colors.positive"],
  );
  assert.equal(android.layout, "stackedActions");
  assert.deepEqual(android.size, { width: 360, height: 152 });
  assert.equal(android.iconRow.items[0]?.button.contentMode, "iconText");
  assert.ok(android.iconRow.items.every((item) =>
    item.button.dimensionMode === "content"));
  assert.deepEqual(
    android.iconRow.items.map((item) => item.id),
    ["decline", "answer"],
  );
});

test("Incoming Call Notification consumes exact Avatar, Label and Icon Row Runtime contracts", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as {
    labelRuntime: Array<{ runtimeInputs: { sampleText: string; sampleSubtext: string } }>;
    iconRowRuntime: Array<{
      runtimeInputs: { buttonInputs: Array<{ id: string; iconToken: string }> };
    }>;
  };
  preview.labelRuntime[0]!.runtimeInputs.sampleText = "Diana";
  preview.labelRuntime[0]!.runtimeInputs.sampleSubtext = "WhatsApp audio";
  preview.iconRowRuntime[0]!.runtimeInputs.buttonInputs[1]!.iconToken = "phone_in_talk";
  source.designPreviewJson = JSON.stringify(preview);

  const resolved = resolveIncomingCallNotificationComponent(source);
  assert.equal(resolved.label.text, "Diana");
  assert.equal(resolved.label.subtext, "WhatsApp audio");
  assert.equal(resolved.iconRow.items[1]?.button.iconToken, "phone_in_talk");
});

test("Incoming Call Notification rejects missing or manufactured child Runtime values", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as {
    avatarRuntime: unknown[];
    iconRowRuntime: Array<{
      runtimeInputs: { buttonInputs: unknown[] };
    }>;
  };
  preview.avatarRuntime = [];
  source.designPreviewJson = JSON.stringify(preview);
  assert.throws(
    () => resolveIncomingCallNotificationComponent(source),
    /requires exactly one item/,
  );

  const actionSource = fixture();
  const actionPreview = JSON.parse(actionSource.designPreviewJson) as {
    iconRowRuntime: Array<{
      runtimeInputs: { buttonInputs: unknown[] };
    }>;
  };
  actionPreview.iconRowRuntime[0]!.runtimeInputs.buttonInputs =
    actionPreview.iconRowRuntime[0]!.runtimeInputs.buttonInputs.slice(0, 1);
  actionSource.designPreviewJson = JSON.stringify(actionPreview);
  assert.throws(
    () => resolveIncomingCallNotificationComponent(actionSource),
    /Button Runtime values must match the Variant items exactly/,
  );
});

test("Incoming Call Notification renders compact and stacked-actions layouts without overlap", () => {
  const iosSource = fixture();
  const ios = incomingCallNotificationComponentToRenderable(
    iosSource,
    resolveIncomingCallNotificationComponent(iosSource),
  );
  const androidSource = fixture("android");
  const androidPreview = JSON.parse(androidSource.designPreviewJson) as {
    iconRowRuntime: Array<{
      runtimeInputs: { buttonInputs: Array<Record<string, unknown>> };
    }>;
  };
  for (const button of androidPreview.iconRowRuntime[0]!.runtimeInputs.buttonInputs) {
    button.contentMode = "iconText";
  }
  androidSource.designPreviewJson = JSON.stringify(androidPreview);
  const android = incomingCallNotificationComponentToRenderable(
    androidSource,
    resolveIncomingCallNotificationComponent(androidSource),
  );

  assert.equal(ios.children?.length, 4);
  assert.equal(android.children?.length, 4);
  const iosAvatar = ios.children?.[1]?.box!;
  const iosLabel = ios.children?.[2]?.box!;
  const iosActions = ios.children?.[3]?.box!;
  assert.ok(iosAvatar.x + iosAvatar.width <= iosLabel.x);
  assert.ok(iosLabel.x + iosLabel.width <= iosActions.x);
  const androidLabel = android.children?.[2]?.box!;
  const androidActions = android.children?.[3]?.box!;
  assert.ok(androidLabel.y + androidLabel.height <= androidActions.y);
});

test("Incoming Call Notification applies reusable boundary Motion to presence", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as {
    present: boolean;
    presenceTransition: boolean;
    presenceElapsedMs: number;
  };
  preview.present = false;
  preview.presenceTransition = true;
  preview.presenceElapsedMs = 120;
  source.designPreviewJson = JSON.stringify(preview);

  const resolved = resolveIncomingCallNotificationComponent(source);
  const node = incomingCallNotificationComponentToRenderable(source, resolved);
  assert.equal(node.id, "component.incomingCallNotification.exit-motion");
  assert.equal(node.children?.[0]?.id, "component.incomingCallNotification");
});
