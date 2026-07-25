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

  assert.deepEqual(ios.size, { width: 360, height: 88 });
  assert.deepEqual(ios.avatarPlacement, {
    mode: "insideEdge",
    alignX: 0,
    alignY: 0.5,
    offsetX: 0,
    offsetY: 0,
  });
  assert.deepEqual(ios.iconRowPlacement, {
    mode: "insideEdge",
    alignX: 1,
    alignY: 0.5,
    offsetX: 0,
    offsetY: 0,
  });
  assert.equal(ios.avatar.labelSlot.showLabel, true);
  assert.equal(ios.avatar.labelSlot.showSubtext, true);
  assert.equal(ios.iconRow.items[0]?.button.contentMode, "icon");
  assert.deepEqual(
    ios.iconRow.items.map((item) => item.button.size),
    [{ width: 52, height: 52 }, { width: 52, height: 52 }],
  );
  assert.deepEqual(
    ios.iconRow.items.map((item) => item.button.stateStyle.surface.backgroundColorToken),
    ["theme.colors.negative", "theme.colors.positive"],
  );
  assert.deepEqual(android.size, { width: 360, height: 152 });
  assert.equal(android.avatarPlacement.alignY, 0);
  assert.equal(android.iconRowPlacement.alignY, 1);
  assert.equal(android.iconRow.items[0]?.button.contentMode, "iconText");
  assert.ok(android.iconRow.items.every((item) =>
    item.button.dimensionMode === "content"));
  assert.deepEqual(
    android.iconRow.items.map((item) => item.id),
    ["decline", "answer"],
  );
});

test("Incoming Call Notification consumes exact Avatar and Icon Row Runtime contracts", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as {
    avatarRuntime: Array<{
      runtimeInputs: { sampleSubtext: string };
    }>;
    iconRowRuntime: Array<{
      runtimeInputs: {
        buttonInputs: Array<{ id: string; iconToken: string; state: string }>;
      };
    }>;
  };
  preview.avatarRuntime[0]!.runtimeInputs.sampleSubtext = "WhatsApp audio";
  preview.iconRowRuntime[0]!.runtimeInputs.buttonInputs[1]!.iconToken = "phone_in_talk";
  preview.iconRowRuntime[0]!.runtimeInputs.buttonInputs[1]!.state = "pushed";
  source.designPreviewJson = JSON.stringify(preview);

  const resolved = resolveIncomingCallNotificationComponent(source);
  assert.equal(resolved.avatar.actor.displayName, "Alex");
  assert.equal(resolved.avatar.labelSlot.label?.text, "Alex");
  assert.equal(resolved.avatar.labelSlot.label?.subtext, "WhatsApp audio");
  assert.equal(resolved.iconRow.items[1]?.button.iconToken, "phone_in_talk");
  assert.equal(resolved.iconRow.items[1]?.button.state, "pushed");
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

test("Incoming Call Notification places Avatar and Icon Row independently inside its Surface frame", () => {
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

  assert.equal(ios.children?.length, 3);
  assert.equal(android.children?.length, 3);
  assert.deepEqual(ios.children?.[0]?.children?.[0]?.box, ios.box);
  assert.deepEqual(android.children?.[0]?.children?.[0]?.box, android.box);
  const iosAvatar = ios.children?.[1]?.children?.[0]?.box!;
  const iosActions = ios.children?.[2]?.box!;
  assert.ok(iosAvatar.x < iosActions.x);
  assert.equal(
    Math.round(iosAvatar.y + iosAvatar.height / 2),
    Math.round(iosActions.y + iosActions.height / 2),
  );
  const androidAvatar = android.children?.[1]?.children?.[0]?.box!;
  const androidActions = android.children?.[2]?.box!;
  assert.ok(androidAvatar.y < androidActions.y);
  assert.ok(androidActions.x + androidActions.width <= android.box!.x + android.box!.width);
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
