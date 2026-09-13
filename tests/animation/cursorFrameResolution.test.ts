import assert from "node:assert/strict";
import test from "node:test";

import { cursorComponentToRenderable } from "../../src/desktop-preview/cursorComponentRenderable.js";
import { resolveCursorComponent } from "../../src/desktop-preview/cursorComponentResolver.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { resolveTextBoxComponent } from "../../src/desktop-preview/textBoxComponentResolver.js";
import { resolveTextInputBarComponent } from "../../src/desktop-preview/textInputBarComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("Cursor resolves its continuous fade completely from its owner frame", () => {
  const source = withInputDefaults(committedComponentFixture("cursor"));
  const config = JSON.parse(source.configJson) as {
    cursor: { fadeDurationMs: number; minimumFade: number };
  };
  const fadedFrame = (config.cursor.fadeDurationMs / 1000) * source.frameRate;
  const visible = resolveCursorComponent({ ...source, localFrame: 0 });
  const faded = resolveCursorComponent({ ...source, localFrame: fadedFrame });
  const visibleAgain = resolveCursorComponent({
    ...source,
    localFrame: fadedFrame * 2,
  });

  assert.equal(visible.opacity, 1);
  assertClose(faded.opacity, config.cursor.minimumFade);
  assert.equal(visibleAgain.opacity, 1);
  assertClose(
    cursorComponentToRenderable(source, faded).style?.opacity,
    config.cursor.minimumFade,
  );
});

test("Text Box forwards its frame to its owned Cursor boundary", () => {
  const source = withInputDefaults(committedComponentFixture("textBox"));
  const cursorConfig = embeddedCursorConfig(source);
  const fadedFrame = (cursorConfig.fadeDurationMs / 1000) * source.frameRate;

  assert.equal(resolveTextBoxComponent({ ...source, localFrame: 0 }).cursor.opacity, 1);
  assertClose(
    resolveTextBoxComponent({ ...source, localFrame: fadedFrame }).cursor.opacity,
    cursorConfig.minimumFade,
  );
});

test("Text Input Bar preserves the frame through Text Box into Cursor", () => {
  const source = withInputDefaults(committedComponentFixture("textInputBar"));
  const cursorConfig = embeddedCursorConfig(source);
  const fadedFrame = (cursorConfig.fadeDurationMs / 1000) * source.frameRate;

  assert.equal(
    resolveTextInputBarComponent({ ...source, localFrame: 0 }).textBox.cursor.opacity,
    1,
  );
  assertClose(
    resolveTextInputBarComponent({
      ...source,
      localFrame: fadedFrame,
    }).textBox.cursor.opacity,
    cursorConfig.minimumFade,
  );
});

function assertClose(actual: unknown, expected: number) {
  assert.equal(typeof actual, "number");
  assert.ok(Math.abs(actual - expected) < 1e-9);
}

function embeddedCursorConfig(source: DesignPreviewPayload) {
  const variants = JSON.parse(source.componentBaseConfigsJson) as {
    variants: Record<string, { cursor?: { fadeDurationMs: number; minimumFade: number } }>;
  };
  const config = Object.values(variants.variants)
    .find((entry) => entry.cursor)?.cursor;
  assert.ok(config);
  return config;
}

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
    preview[input.jsonKey] = input.kind === "number"
      ? Number(input.defaultValue)
      : input.kind === "boolean"
        ? input.defaultValue === true || input.defaultValue === "true"
        : input.defaultValue;
  }
  const serialized = JSON.stringify(preview);
  return {
    ...source,
    designPreviewJson: serialized,
    runtimeContractJson: serialized,
  };
}
