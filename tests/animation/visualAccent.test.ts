import assert from "node:assert/strict";
import test from "node:test";
import { resolveVisualAccent, wrapVisualAccent } from "../../src/desktop-preview/visualAccent.js";
import { committedComponentFixture } from "./committedComponentFixture.js";
import { prepareRuntimePreviewPayload } from "../../src/desktop-preview/runtimePreviewDocumentContract.js";
import { resolveButtonComponent } from "../../src/desktop-preview/buttonComponentResolver.js";
import { buttonComponentToRenderable } from "../../src/desktop-preview/buttonComponentRenderable.js";
import { resolveSurfaceStackComponent } from "../../src/desktop-preview/surfaceStackComponentResolver.js";
import { surfaceStackComponentToRenderable } from "../../src/desktop-preview/surfaceStackComponentRenderable.js";
import { componentClassToRenderable } from "../../src/desktop-preview/componentRenderableBoundary.js";
import { RuntimeOwnerTimeline } from "../../src/desktop-preview/runtimeOwnerTimeline.js";

test("three accents sample the owner clock deterministically and return to neutral when disabled", () => {
  const neutral = { scale: 1, x: 0, rotation: 0 };
  for (const accentType of ["pulse", "vibrate", "rock"]) {
    const values = { accentType, accentActive: true };
    const clock = { localFrame: 3, frameRate: 50 };
    const sample = resolveVisualAccent(values, clock);
    assert.notDeepEqual(sample, neutral);
    assert.deepEqual(sample, resolveVisualAccent(values, { localFrame: 6, frameRate: 100 }));
    assert.deepEqual(resolveVisualAccent({ ...values, accentActive: false }, clock), neutral);
    assert.deepEqual(resolveVisualAccent(values, { localFrame: 60, frameRate: 50 }), neutral);
    assert.deepEqual(sample, resolveVisualAccent(values, clock)); // random access has no playback state
  }
  assert.throws(() => resolveVisualAccent({ accentType: "unknown", accentActive: false }, { localFrame: 0, frameRate: 25 }), /Unsupported visual accent/);
  assert.throws(() => resolveVisualAccent({ accentType: "pulse" }, { localFrame: 0, frameRate: 25 }), /Missing boolean value visual accent/);
});

test("accent transforms preserve existing placement, pressed scale and layout bounds", () => {
  const node = { id: "fixture", type: "group" as const, box: { x: 20, y: 30, width: 40, height: 50 }, transform: { scale: 0.94, x: 12, opacity: 0.36 }, children: [] };
  const frame = resolveVisualAccent({ accentType: "vibrate", accentActive: true }, { localFrame: 3, frameRate: 50 });
  const wrapped = wrapVisualAccent(node, frame, 2);
  assert.equal(wrapped.children?.[0], node);
  assert.deepEqual(wrapped.box, node.box);
  assert.equal(wrapped.transform?.x, frame.x * 2);
  assert.deepEqual(node.transform, { scale: 0.94, x: 12, opacity: 0.36 });
});

for (const type of ["button", "surfaceStack"]) {
  for (const accentType of ["pulse", "vibrate", "rock"]) {
    test(`${type} plays ${accentType} through declared hold activation and stops at neutral`, () => {
      const payload = committedComponentFixture(type);
      const values = JSON.parse(payload.designPreviewJson);
      values.accentType = accentType;
      values.accentActive = false;
      const animation = { schemaVersion: 2, tracks: [{ id: "accent-track", fieldId: "accentActive", targetId: "", keyframes: [
        { id: "off", frame: 0, value: false, interpolation: "hold", enabled: true },
        { id: "on", frame: 3, value: true, interpolation: "hold", enabled: true },
        { id: "stop", frame: 20, value: false, interpolation: "hold", enabled: true },
      ] }] };
      const source = JSON.stringify(values);
      const at = (frame: number) => {
        const prepared = prepareRuntimePreviewPayload({ ...payload, localFrame: frame, frameRate: 25,
          designPreviewJson: source, runtimeContractJson: source, instanceJson: JSON.stringify({ animation }) });
        if (type === "button") {
          const resolved = resolveButtonComponent(prepared);
          return { accent: resolved.accent, node: buttonComponentToRenderable(prepared, resolved) };
        }
        const resolved = resolveSurfaceStackComponent(prepared);
        return { accent: resolved.accent, node: surfaceStackComponentToRenderable(prepared, resolved, componentClassToRenderable) };
      };
      const idle = at(0), running = at(8), stopped = at(21);
      assert.equal(idle.accent.scale, 1);
      assert.notDeepEqual(running.accent, idle.accent);
      assert.deepEqual(stopped.accent, idle.accent);
      for (let frame = 0; frame <= 30; frame++) {
        const sample = at(frame);
        assert.deepEqual(sample.accent, resolveVisualAccent(
          { accentType, accentActive: frame >= 3 && frame < 20 },
          { localFrame: frame, frameRate: 25 },
        ));
        assert.deepEqual(sample.node.box, idle.node.box);
      }
      assert.deepEqual(running.node.box, idle.node.box);
      assert.equal(running.node.children?.[0]?.id, idle.node.id);
      assert.equal(JSON.stringify(values), source);
      // An enabled decorative loop never creates an infinite or longer action.
      const inactive = new RuntimeOwnerTimeline(values, values, { schemaVersion: 2, tracks: [] });
      const active = new RuntimeOwnerTimeline(values, { ...values, accentActive: true }, { schemaVersion: 2, tracks: [] });
      assert.equal(inactive.durationFrames, active.durationFrames);
    });
  }
}
