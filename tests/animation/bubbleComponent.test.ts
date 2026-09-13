import assert from "node:assert/strict";
import test from "node:test";

import { resolveBubbleComponent } from "../../src/desktop-preview/bubbleComponentResolver.js";
import { bubbleComponentToRenderable } from "../../src/desktop-preview/bubbleComponentRenderable.js";
import type { RenderableNode } from "../../src/visual/renderable/types.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Bubble maps resolved alignment to tail side and visibility independently of message state", () => {
  const source = committedComponentFixture("bubble", "default_copy");
  const config = JSON.parse(source.configJson) as {
    bubble: {
      incomingAlignment: string;
      surfaceSlot: { overrides: Record<string, unknown> };
    };
  };
  const overrides = config.bubble.surfaceSlot.overrides as {
    surface?: { tail?: Record<string, unknown> };
  };
  config.bubble.surfaceSlot.overrides = {
    ...overrides,
    surface: {
      ...overrides.surface,
      tail: {
        ...overrides.surface?.tail,
        enabled: true,
      },
    },
  };
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  preview.state = "incoming";

  for (const expected of [
    { alignment: "left", enabled: true, side: "left" },
    { alignment: "center", enabled: false, side: "left" },
    { alignment: "right", enabled: true, side: "right" },
  ] as const) {
    config.bubble.incomingAlignment = expected.alignment;
    source.configJson = JSON.stringify(config);
    source.designPreviewJson = JSON.stringify(preview);
    const resolved = resolveBubbleComponent(source);
    assert.equal(resolved.alignment, expected.alignment);
    assert.equal(resolved.surface.tail.enabled, expected.enabled);
    assert.equal(resolved.surface.tail.side, expected.side);
  }
});

test("Bubble preserves the resolved Surface tail enabled value for every message direction", () => {
  const source = committedComponentFixture("bubble");
  const config = JSON.parse(source.configJson) as {
    bubble: { surfaceSlot: { overrides: Record<string, unknown> } };
  };
  config.bubble.surfaceSlot.overrides = {
    surface: { tail: { enabled: false } },
  };
  source.configJson = JSON.stringify(config);

  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  for (const state of ["incoming", "outgoing", "system"]) {
    preview.state = state;
    source.designPreviewJson = JSON.stringify(preview);
    assert.equal(resolveBubbleComponent(source).surface.tail.enabled, false, state);
  }
});

test("Bubble strictly validates every state alignment", () => {
  const missing = committedComponentFixture("bubble");
  const missingConfig = JSON.parse(missing.configJson) as {
    bubble: Record<string, unknown>;
  };
  delete missingConfig.bubble.systemAlignment;
  missing.configJson = JSON.stringify(missingConfig);
  assert.throws(
    () => resolveBubbleComponent(missing),
    /component\.bubble\.systemAlignment/,
  );

  const invalid = committedComponentFixture("bubble");
  const invalidConfig = JSON.parse(invalid.configJson) as {
    bubble: Record<string, unknown>;
  };
  invalidConfig.bubble.outgoingAlignment = "leading";
  invalid.configJson = JSON.stringify(invalidConfig);
  assert.throws(
    () => resolveBubbleComponent(invalid),
    /Unsupported bubble alignment leading/,
  );
});

test("Bubble requires the prepared Runtime max width", () => {
  const source = committedComponentFixture("bubble");
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  delete preview.maxWidth;
  source.designPreviewJson = JSON.stringify(preview);
  assert.throws(
    () => resolveBubbleComponent(source),
    /component\.bubble\.input\.maxWidth/,
  );
});

test("Bubble does not recover a missing Runtime media type from Variant config", () => {
  const source = committedComponentFixture("bubble");
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  delete preview.mediaType;
  source.designPreviewJson = JSON.stringify(preview);
  assert.throws(
    () => resolveBubbleComponent(source),
    /component\.bubble\.input\.mediaType/,
  );
});

test("Bubble requires every field in its complete prepared Runtime snapshot", () => {
  const requiredKeys = [
    "state",
    "sampleText",
    "maxWidth",
    "writeOnDurationFrames",
    "writeOnTrigger",
    "writeOnFrame",
    "actorId",
    "actorName",
    "statusText",
    "statusState",
    "mediaType",
    "mediaSource",
    "viewportSize",
    "mediaScale",
    "mediaOffset",
    "isPlaying",
    "currentTimeSeconds",
    "durationSeconds",
    "isFullScreen",
    "fullScreenTransition",
    "fullframeOrientation",
    "controlsElapsedMs",
    "motionElapsedMs",
    "showIconRow",
    "iconRowRuntime",
  ];
  const source = committedComponentFixture("bubble");
  const complete = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  for (const key of requiredKeys) {
    const preview = { ...complete };
    delete preview[key];
    source.designPreviewJson = JSON.stringify(preview);
    assert.throws(
      () => resolveBubbleComponent(source),
      new RegExp(`component\\.bubble\\.input\\.${key}`),
      key,
    );
  }
});

test("Bubble write-on Cursor never changes the measured Bubble width", () => {
  const source = committedComponentFixture("bubble");
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  Object.assign(preview, {
    sampleText: "Cursor measurement remains stable",
    mediaType: "none",
    statusState: "none",
    statusText: "",
    writeOnTrigger: true,
    writeOnDurationFrames: 20,
    writeOnFrame: 4,
  });
  source.designPreviewJson = JSON.stringify(preview);

  const withCursor = resolveBubbleComponent(source);
  assert.equal(withCursor.textBox.cursorVisible, true);
  const withoutCursor = {
    ...withCursor,
    textBox: {
      ...withCursor.textBox,
      cursorVisible: false,
    },
  };

  assert.equal(
    bubbleComponentToRenderable(source, withCursor).box?.width,
    bubbleComponentToRenderable(source, withoutCursor).box?.width,
  );
});

test("Bubble write-on Cursor stays on the final visible glyph line while text is erased", () => {
  const source = committedComponentFixture("bubble");
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  Object.assign(preview, {
    sampleText: "N\n",
    mediaType: "none",
    statusState: "none",
    statusText: "",
    writeOnTrigger: false,
    writeOnDurationFrames: 0,
    writeOnFrame: 0,
    keepCursorAfterWrite: true,
  });
  source.designPreviewJson = JSON.stringify(preview);

  const bubble = resolveBubbleComponent(source);
  const renderable = bubbleComponentToRenderable(source, bubble);
  const nodes: RenderableNode[] = [];
  const visit = (node: RenderableNode) => {
    nodes.push(node);
    node.children?.forEach(visit);
  };
  visit(renderable);

  const finalVisibleText = nodes.find((node) =>
    node.id === `${bubble.textBox.id}.text.0`
  );
  const cursor = nodes.find((node) => node.id === bubble.textBox.cursor.id);
  assert.notEqual(finalVisibleText?.box, undefined);
  assert.notEqual(cursor?.box, undefined);
  assert.equal(cursor!.box!.y, finalVisibleText!.box!.y);
});

test("Bubble text paint clip extends past measured content without changing layout width", () => {
  const source = committedComponentFixture("bubble");
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  Object.assign(preview, {
    sampleText: "No",
    mediaType: "none",
    statusState: "none",
    statusText: "",
    writeOnTrigger: false,
    writeOnDurationFrames: 0,
    writeOnFrame: 0,
    keepCursorAfterWrite: true,
  });
  source.designPreviewJson = JSON.stringify(preview);

  const bubble = resolveBubbleComponent(source);
  const renderable = bubbleComponentToRenderable(source, bubble);
  const nodes: RenderableNode[] = [];
  const visit = (node: RenderableNode) => {
    nodes.push(node);
    node.children?.forEach(visit);
  };
  visit(renderable);

  const textClip = nodes.find((node) => node.id === `${bubble.textBox.id}.textClip`);
  const textLine = nodes.find((node) => node.id === `${bubble.textBox.id}.text.0`);
  assert.notEqual(textClip?.box, undefined);
  assert.notEqual(textLine?.box, undefined);
  assert.ok(textClip!.box!.width > textLine!.box!.width);
  assert.ok(textClip!.box!.x + textClip!.box!.width <= renderable.box!.x + renderable.box!.width);
});

test("Bubble remeasures wrapped lines from the current resolved text", () => {
  const renderedAt = (
    sampleText: string,
    writeOnTrigger: boolean,
    writeOnFrame: number,
  ) => {
    const source = committedComponentFixture("bubble");
    const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
    Object.assign(preview, {
      sampleText,
      maxWidth: 40,
      mediaType: "none",
      statusState: "none",
      statusText: "",
      writeOnTrigger,
      writeOnDurationFrames: 20,
      writeOnFrame,
    });
    source.designPreviewJson = JSON.stringify(preview);
    const resolved = resolveBubbleComponent(source);
    const width = bubbleComponentToRenderable(source, resolved).box?.width;
    assert.notEqual(width, undefined);
    return {
      text: resolved.textBox.text,
      width: width!,
      maximumWidth: resolved.maxWidth,
    };
  };

  const beforeWrap = renderedAt("A broadword broadword", true, 16);
  const afterWrap = renderedAt("A broadword broadword", true, 17);
  assert.equal(beforeWrap.text, "A broadword broad");
  assert.equal(afterWrap.text, "A broadword broadw");
  assert.ok(afterWrap.width < beforeWrap.width);
  assert.ok(afterWrap.width < afterWrap.maximumWidth);

  const animatedValue = renderedAt(afterWrap.text, false, 0);
  assert.equal(animatedValue.width, afterWrap.width);
});

test("Bubble reserves the Actor Label frame depth at its top edge", () => {
  const renderedAt = (
    mode: "center" | "insideEdge" | "outsideEdge",
    offsetY: number,
  ) => {
    const payload = committedComponentFixture("bubble", "default_copy");
    const config = JSON.parse(payload.configJson) as {
      bubble: {
        actorLabelSlot: Record<string, unknown>;
        avatarSlot: Record<string, unknown>;
      };
    };
    config.bubble.actorLabelSlot.showLabel = true;
    config.bubble.actorLabelSlot.placement = {
      mode,
      alignX: 0,
      alignY: 0,
      offsetX: 32,
      offsetY,
    };
    config.bubble.avatarSlot.showAvatar = true;
    config.bubble.avatarSlot.reserveTextSpace = true;
    config.bubble.avatarSlot.placement = {
      mode: "insideEdge",
      alignX: 0,
      alignY: 0.5,
      offsetX: 8,
      offsetY: 0,
    };
    payload.configJson = JSON.stringify(config);

    const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
    return {
      surface: requiredNode(rendered, "component.bubble.surface").box!,
      label: requiredNode(rendered, "component.bubble.actorLabel").box!,
      avatar: requiredNode(rendered, "component.bubble.avatar").box!,
      text: requiredNode(rendered, "component.bubble.textBox").box!,
    };
  };

  const baseline = renderedAt("outsideEdge", 0);
  for (const expected of [
    { mode: "outsideEdge" as const, offsetY: 4, frameDepth: 4 },
    { mode: "center" as const, offsetY: 4, frameDepthFromHeight: 0.5 },
    { mode: "insideEdge" as const, offsetY: 4, frameDepthFromHeight: 1 },
  ]) {
    const layout = renderedAt(expected.mode, expected.offsetY);
    const frameDepth = expected.frameDepth
      ?? layout.label.height * expected.frameDepthFromHeight! + expected.offsetY;
    approximatelyEqual(
      layout.label.y + layout.label.height - layout.surface.y,
      frameDepth,
    );
    approximatelyEqual(layout.surface.height - baseline.surface.height, frameDepth);
    approximatelyEqual(
      layout.text.y - layout.surface.y - (baseline.text.y - baseline.surface.y),
      frameDepth,
    );
    approximatelyEqual(
      layout.avatar.y - layout.surface.y - (baseline.avatar.y - baseline.surface.y),
      frameDepth / 2,
    );
  }
});

test("Bubble Avatar vertical offset translates without changing its centered edge reservation", () => {
  const renderedAt = (offsetY: number) => {
    const payload = committedComponentFixture("bubble", "default_copy");
    const config = JSON.parse(payload.configJson) as {
      bubble: {
        actorLabelSlot: Record<string, unknown>;
        avatarSlot: Record<string, unknown>;
      };
    };
    config.bubble.actorLabelSlot.showLabel = false;
    config.bubble.avatarSlot.showAvatar = true;
    config.bubble.avatarSlot.reserveTextSpace = true;
    config.bubble.avatarSlot.placement = {
      mode: "insideEdge",
      alignX: 0,
      alignY: 0.5,
      offsetX: 8,
      offsetY,
    };
    payload.configJson = JSON.stringify(config);

    const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
    return {
      surface: requiredNode(rendered, "component.bubble.surface").box!,
      avatar: requiredNode(rendered, "component.bubble.avatar").box!,
      text: requiredNode(rendered, "component.bubble.textBox").box!,
    };
  };

  const centered = renderedAt(0);
  for (const offsetY of [-1, 1]) {
    const shifted = renderedAt(offsetY);
    approximatelyEqual(shifted.surface.width, centered.surface.width);
    approximatelyEqual(shifted.surface.height, centered.surface.height);
    approximatelyEqual(
      shifted.text.y - shifted.surface.y,
      centered.text.y - centered.surface.y,
    );
    approximatelyEqual(
      shifted.avatar.y - shifted.surface.y,
      centered.avatar.y - centered.surface.y + offsetY,
    );
  }
});

test("Bubble reserves message text for an Avatar anchored to any Bubble edge", () => {
  const placements = [
    {
      name: "left",
      placement: { mode: "insideEdge" as const, alignX: 0, alignY: 0.5, offsetX: 8, offsetY: 0 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.x >= avatar.box!.x + avatar.box!.width),
    },
    {
      name: "top",
      placement: { mode: "insideEdge" as const, alignX: 0.5, alignY: 0, offsetX: 0, offsetY: 8 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.y >= avatar.box!.y + avatar.box!.height),
    },
    {
      name: "right",
      placement: { mode: "insideEdge" as const, alignX: 1, alignY: 0.5, offsetX: -8, offsetY: 0 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.x + text.box!.width <= avatar.box!.x),
    },
    {
      name: "bottom",
      placement: { mode: "insideEdge" as const, alignX: 0.5, alignY: 1, offsetX: 0, offsetY: -8 },
      assertReserved: (text: RenderableNode, avatar: RenderableNode) =>
        assert.ok(text.box!.y + text.box!.height <= avatar.box!.y),
    },
  ];

  for (const { placement, assertReserved } of placements) {
    const payload = committedComponentFixture("bubble", "default_copy");
    const config = JSON.parse(payload.configJson) as {
      bubble: { avatarSlot: Record<string, unknown> };
    };
    config.bubble.avatarSlot.reserveTextSpace = true;
    config.bubble.avatarSlot.placement = placement;
    payload.configJson = JSON.stringify(config);

    const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
    const text = requiredNode(rendered, "component.bubble.textBox");
    const avatar = requiredNode(rendered, "component.bubble.avatar");
    assertReserved(text, avatar);
  }
});

test("Bubble minimum height contains a reserved interior Avatar without message text", () => {
  const payload = committedComponentFixture("bubble", "default_copy");
  const config = JSON.parse(payload.configJson) as {
    bubble: { avatarSlot: { overrides: Record<string, unknown>; reserveTextSpace?: boolean } };
  };
  config.bubble.avatarSlot.reserveTextSpace = true;
  config.bubble.avatarSlot.overrides = {
    avatar: { defaultSize: 96 },
  };
  payload.configJson = JSON.stringify(config);
  const preview = JSON.parse(payload.designPreviewJson) as Record<string, unknown>;
  preview.sampleText = "";
  payload.designPreviewJson = JSON.stringify(preview);

  const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
  const surface = requiredNode(rendered, "component.bubble.surface");
  const avatar = requiredNode(rendered, "component.bubble.avatar");
  assert.ok(surface.box!.height >= avatar.box!.height);
});

test("Bubble requires an explicit Avatar text-space reservation choice", () => {
  const payload = committedComponentFixture("bubble", "default_copy");
  const config = JSON.parse(payload.configJson) as {
    bubble: { avatarSlot: Record<string, unknown> };
  };
  delete config.bubble.avatarSlot.reserveTextSpace;
  payload.configJson = JSON.stringify(config);

  assert.throws(
    () => resolveBubbleComponent(payload),
    /component\.bubble\.avatar\.reserveTextSpace/,
  );
});

test("Bubble centers its active Icon Row after content and before Status", () => {
  for (const mediaPosition of ["top", "bottom", "left", "right"] as const) {
    const payload = committedComponentFixture("bubble", "default_copy");
    const config = JSON.parse(payload.configJson) as {
      bubble: { mediaPosition: string };
    };
    config.bubble.mediaPosition = mediaPosition;
    payload.configJson = JSON.stringify(config);
    const preview = JSON.parse(payload.designPreviewJson) as Record<string, unknown>;
    Object.assign(preview, {
      showIconRow: true,
      mediaType: "image",
      statusState: "sent",
      statusText: "10:42",
      writeOnTrigger: false,
      keepCursorAfterWrite: false,
    });
    payload.designPreviewJson = JSON.stringify(preview);

    const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
    const surface = requiredNode(rendered, "component.bubble.surface").box!;
    const text = requiredNode(rendered, "component.bubble.textBox").box!;
    const media = requiredNode(rendered, "component.bubble.image").box!;
    const iconRow = requiredNode(rendered, "component.bubble.iconRow").box!;
    const status = requiredNode(rendered, "component.bubble.status").box!;

    assert.ok(iconRow.y >= Math.max(text.y + text.height, media.y + media.height));
    approximatelyEqual(
      iconRow.x + iconRow.width / 2,
      surface.x + surface.width / 2,
    );
    assert.ok(status.y >= iconRow.y + iconRow.height);
  }
});

test("Bubble applies symmetric Variant-owned vertical padding only around a visible Icon Row", () => {
  const renderedAt = (verticalPaddingToken: string) => {
    const payload = committedComponentFixture("bubble", "default_copy");
    const config = JSON.parse(payload.configJson) as {
      bubble: { iconRowVerticalPaddingToken: string };
    };
    config.bubble.iconRowVerticalPaddingToken = verticalPaddingToken;
    payload.configJson = JSON.stringify(config);
    const preview = JSON.parse(payload.designPreviewJson) as Record<string, unknown>;
    Object.assign(preview, {
      showIconRow: true,
      mediaType: "none",
      statusState: "sent",
      statusText: "10:42",
      writeOnTrigger: false,
      keepCursorAfterWrite: false,
    });
    payload.designPreviewJson = JSON.stringify(preview);
    const resolved = resolveBubbleComponent(payload);
    const rendered = bubbleComponentToRenderable(payload, resolved);
    return {
      token: resolved.iconRowSlot.verticalPaddingToken,
      surface: requiredNode(rendered, "component.bubble.surface").box!,
      text: requiredNode(rendered, "component.bubble.textBox").box!,
      iconRow: requiredNode(rendered, "component.bubble.iconRow").box!,
      status: requiredNode(rendered, "component.bubble.status").box!,
    };
  };

  const none = renderedAt("theme.spacing.none");
  const padded = renderedAt("theme.spacing.m");
  assert.equal(none.token, "theme.spacing.none");
  assert.equal(padded.token, "theme.spacing.m");
  const topPadding =
    (padded.iconRow.y - padded.text.y - padded.text.height)
    - (none.iconRow.y - none.text.y - none.text.height);
  const bottomPadding =
    (padded.status.y - padded.iconRow.y - padded.iconRow.height)
    - (none.status.y - none.iconRow.y - none.iconRow.height);
  assert.ok(topPadding > 0);
  approximatelyEqual(bottomPadding, topPadding);
  approximatelyEqual(padded.surface.height - none.surface.height, topPadding * 2);
});

test("Bubble places its active Icon Row directly below text when media is absent", () => {
  const payload = committedComponentFixture("bubble");
  const preview = JSON.parse(payload.designPreviewJson) as Record<string, unknown>;
  Object.assign(preview, {
    showIconRow: true,
    mediaType: "none",
    statusState: "none",
    statusText: "",
    writeOnTrigger: false,
    keepCursorAfterWrite: false,
  });
  payload.designPreviewJson = JSON.stringify(preview);

  const rendered = bubbleComponentToRenderable(payload, resolveBubbleComponent(payload));
  const text = requiredNode(rendered, "component.bubble.textBox").box!;
  const iconRow = requiredNode(rendered, "component.bubble.iconRow").box!;
  assert.ok(iconRow.y >= text.y + text.height);
});

function requiredNode(node: RenderableNode, id: string): RenderableNode {
  if (node.id === id) return node;
  for (const child of node.children ?? []) {
    const found = findNode(child, id);
    if (found) return found;
  }
  throw new Error(`Missing renderable node '${id}'.`);
}

function findNode(node: RenderableNode, id: string): RenderableNode | undefined {
  if (node.id === id) return node;
  for (const child of node.children ?? []) {
    const found = findNode(child, id);
    if (found) return found;
  }
  return undefined;
}

function approximatelyEqual(actual: number, expected: number) {
  assert.ok(
    Math.abs(actual - expected) < 0.0001,
    `Expected ${actual} to approximately equal ${expected}.`,
  );
}
