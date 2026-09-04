import assert from "node:assert/strict";
import test from "node:test";

import { callParticipantComponentToRenderable } from "../../src/desktop-preview/callParticipantComponentRenderable.js";
import { resolveCallParticipantComponent } from "../../src/desktop-preview/callParticipantComponentResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

const fixture = () => committedComponentFixture("callParticipant", "default");

test("Call Participant uses the Variant status while a track is connecting", () => {
  const source = fixture();
  const participant = resolveCallParticipantComponent(source);
  assert.equal(participant.videoPresent, false);
  assert.equal(participant.statusLabel.text, "Connecting…");
  assert.equal(participant.avatar.actor.displayName, "Alex");
  const node = callParticipantComponentToRenderable(source, participant, { x: 0, y: 0, width: 360, height: 360 });
  assert.ok(node.children?.some(child => child.id === "component.callParticipant.avatar"));
  assert.ok(node.children?.some(child => child.id === "component.callParticipant.status"));
});

test("Call Participant accepts a Runtime connection-loss message and restores video", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  preview.statusTextOverride = "Connection lost";
  preview.connectionState = "lost";
  const lost = resolveCallParticipantComponent({ ...source, designPreviewJson: JSON.stringify(preview) });
  assert.equal(lost.statusLabel.text, "Connection lost");
  assert.equal(lost.connectionIconToken, "media_video_off");
  preview.videoPresent = true;
  preview.connectionState = "connected";
  const restoredSource = { ...source, designPreviewJson: JSON.stringify(preview) };
  const restored = resolveCallParticipantComponent(restoredSource);
  const node = callParticipantComponentToRenderable(restoredSource, restored, { x: 0, y: 0, width: 360, height: 360 });
  assert.equal(restored.connectionIconToken, undefined);
  assert.ok(node.children?.some(child => child.id === "component.callParticipant.media"));
  assert.ok(!node.children?.some(child => child.id === "component.callParticipant.avatar"));
});

test("Call Participant visual switches independently remove optional blocks", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as { callParticipant: Record<string, unknown> };
  for (const key of ["showBackground", "showMedia", "showAvatarWhenVideoAbsent", "showStatusWhenVideoAbsent", "showName", "showMicrophoneStatus", "showConnectionStatus", "showActiveSpeakerIndicator"])
    config.callParticipant[key] = false;
  const hiddenSource = { ...source, configJson: JSON.stringify(config) };
  const node = callParticipantComponentToRenderable(hiddenSource, resolveCallParticipantComponent(hiddenSource), { x: 0, y: 0, width: 360, height: 360 });
  assert.deepEqual(node.children, []);
});
