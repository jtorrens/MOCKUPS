import assert from "node:assert/strict";
import test from "node:test";

import { incomingCallNotificationComponentToRenderable } from "../../src/desktop-preview/incomingCallNotificationComponentRenderable.js";
import { resolveIncomingCallNotificationComponent } from "../../src/desktop-preview/incomingCallNotificationComponentResolver.js";
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "../../src/desktop-preview/iconRowComponentRenderable.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

const fixture = (variantId = "default") =>
  committedComponentFixture("incomingCallNotification", variantId);

test("Incoming Call Notification resolves current authored values through exact child Variant boundaries", () => {
  const iosSource = fixture();
  const androidSource = fixture("android");
  assert.equal(iosSource.designPreviewJson, androidSource.designPreviewJson);

  const ios = resolveIncomingCallNotificationComponent(iosSource);
  const android = resolveIncomingCallNotificationComponent(androidSource);

  for (const [source, resolved] of [
    [iosSource, ios],
    [androidSource, android],
  ] as const) {
    const authored = incomingCallAuthoredValues(source.configJson);
    assert.deepEqual(resolved.size, authored.size);
    assert.deepEqual(resolved.avatarPlacement, authored.avatarPlacement);
    assert.deepEqual(resolved.iconRowPlacement, authored.iconRowPlacement);
    assert.equal(resolved.avatar.id, "component.incomingCallNotification.avatar");
    assert.equal(resolved.iconRow.id, "component.incomingCallNotification.iconRow");
  }
});

function incomingCallAuthoredValues(configJson: string) {
  const config = JSON.parse(configJson) as {
    incomingCallNotification: {
      size: string;
      avatarPlacement: Record<string, unknown>;
      iconRowPlacement: Record<string, unknown>;
    };
  };
  const [width, height, extra] = config.incomingCallNotification.size
    .split("|")
    .map(Number);
  assert.equal(extra, undefined);
  assert.ok(Number.isFinite(width));
  assert.ok(Number.isFinite(height));
  return {
    size: { width: width!, height: height! },
    avatarPlacement: config.incomingCallNotification.avatarPlacement,
    iconRowPlacement: config.incomingCallNotification.iconRowPlacement,
  };
}

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

test("Incoming Call Notification preserves an exact nested Button Surface selection", () => {
  const source = fixture("android");
  const config = JSON.parse(source.configJson) as {
    incomingCallNotification: {
      iconRowSlot: {
        variantReference: string;
        overrides: Record<string, unknown>;
      };
    };
  };
  const bases = JSON.parse(source.componentBaseConfigsJson) as {
    variants: Record<string, {
      iconRow: {
        items: Array<Record<string, unknown>>;
      };
    }>;
  };
  const iconRowSlot = config.incomingCallNotification.iconRowSlot;
  const structuralItems = structuredClone(
    bases.variants[iconRowSlot.variantReference]!.iconRow.items,
  );
  structuralItems[0]!.buttonOverrides = {
    button: {
      states: {
        normal: {
          surfaceSlot: {
            variantReference: "component_project_foqn_s2_surface::variant::default",
            overrides: {
              style: { cornerRadiusToken: "theme.radii.m" },
              surface: {
                backgroundColorToken: "theme.colors.surface",
                backgroundAlpha: 0.7,
              },
            },
          },
        },
      },
    },
  };
  iconRowSlot.overrides = { iconRow: { items: structuralItems } };
  source.configJson = JSON.stringify(config);

  const resolved = resolveIncomingCallNotificationComponent(source);
  assert.equal(
    resolved.iconRow.items[0]?.button.stateStyle.surface.backgroundColorToken,
    "theme.colors.surface",
  );
  assert.equal(resolved.iconRow.items[0]?.button.stateStyle.surface.backgroundAlpha, 0.7);
  assert.equal(
    resolved.iconRow.items[0]?.button.stateStyle.surface.surface.cornerRadiusToken,
    "theme.radii.m",
  );
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

  assert.equal(ios.children?.length, 2);
  assert.equal(android.children?.length, 2);
  assert.deepEqual(ios.children?.[0]?.children?.[0]?.box, ios.box);
  assert.deepEqual(android.children?.[0]?.children?.[0]?.box, android.box);
  const iosContent = ios.children?.[1];
  const androidContent = android.children?.[1];
  assert.equal(iosContent?.style?.overflow, "hidden");
  assert.equal(androidContent?.style?.overflow, "hidden");
  const iosAvatar = iosContent?.children?.[0]?.children?.[0]?.box!;
  const iosActions = iosContent?.children?.[1]?.box!;
  assert.ok(iosAvatar.x < iosActions.x);
  assert.equal(
    Math.round(iosAvatar.y + iosAvatar.height / 2),
    Math.round(iosActions.y + iosActions.height / 2),
  );
  const androidAvatar = androidContent?.children?.[0]?.children?.[0]?.box!;
  const androidActions = androidContent?.children?.[1]?.box!;
  assert.ok(androidAvatar.y < androidActions.y);
  assert.ok(androidActions.x + androidActions.width <= android.box!.x + android.box!.width);
  const leftInset = androidActions.x - android.box!.x;
  const rightInset =
    android.box!.x + android.box!.width - androidActions.x - androidActions.width;
  assert.ok(leftInset > 0);
  assert.equal(leftInset, rightInset);
  const androidButtons = androidContent?.children?.[1]?.children ?? [];
  assert.equal(androidButtons.length, 2);
  assert.equal(androidButtons[0]!.box!.width, androidButtons[1]!.box!.width);
});

test("Incoming Call Notification clips fixed children instead of rejecting a smaller assigned frame", () => {
  const source = fixture();
  const assigned = { x: 10, y: 20, width: 24, height: 24 };
  const node = incomingCallNotificationComponentToRenderable(
    source,
    resolveIncomingCallNotificationComponent(source),
    assigned,
  );

  assert.deepEqual(node.box, assigned);
  assert.equal(node.style?.overflow, "visible");
  assert.equal(node.children?.length, 2);
  assert.equal(node.children?.[1]?.style?.overflow, "hidden");
});

test("Icon Row Fill parent distributes only its orientation axis", () => {
  const source = fixture("android");
  const row = resolveIncomingCallNotificationComponent(source).iconRow;
  const horizontalIntrinsic = measureIconRowComponent(source, row);
  const horizontalBox = { x: 10, y: 20, width: 300, height: horizontalIntrinsic.height };
  const horizontal = iconRowComponentToRenderableAt(source, row, horizontalBox);
  const horizontalButtons = horizontal.children ?? [];
  assert.equal(horizontalButtons.length, 2);
  assert.equal(
    horizontalButtons[0]!.box!.width,
    (horizontalBox.width - horizontalIntrinsic.gap) / 2,
  );
  assert.equal(
    horizontalButtons[0]!.box!.height,
    horizontalIntrinsic.sizes[0]!.height,
  );
  const clipped = iconRowComponentToRenderableAt(
    source,
    row,
    { x: 10, y: 20, width: 1, height: horizontalIntrinsic.height },
  );
  assert.equal(clipped.style?.overflow, "hidden");
  assert.deepEqual(
    clipped.children?.map((button) => button.box?.width),
    [0, 0],
  );

  const verticalRow = { ...row, orientation: "vertical" as const };
  const verticalIntrinsic = measureIconRowComponent(source, verticalRow);
  const verticalBox = { x: 10, y: 20, width: verticalIntrinsic.width, height: 300 };
  const vertical = iconRowComponentToRenderableAt(source, verticalRow, verticalBox);
  const verticalButtons = vertical.children ?? [];
  assert.equal(verticalButtons.length, 2);
  assert.equal(
    verticalButtons[0]!.box!.height,
    (verticalBox.height - verticalIntrinsic.gap) / 2,
  );
  assert.equal(
    verticalButtons[0]!.box!.width,
    verticalIntrinsic.sizes[0]!.width,
  );
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
