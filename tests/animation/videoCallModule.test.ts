import assert from "node:assert/strict";
import test from "node:test";
import Database from "better-sqlite3";

import { parityDatabasePath } from "../../src/development-scaffolding/parityDatabasePath.js";
import { videoCallModuleToRenderable } from "../../src/desktop-preview/videoCallModuleRenderable.js";
import { resolveVideoCallModule } from "../../src/desktop-preview/videoCallModuleResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

function fixture() {
  const base = committedComponentFixture("callParticipant", "default");
  const database = new Database(parityDatabasePath(), { readonly: true, fileMustExist: true });
  try {
    const row = database.prepare("SELECT design_preview_json, metadata_json FROM modules WHERE record_class_id = 'module.core.videoCall'").get() as { design_preview_json: string; metadata_json: string };
    assert.ok(row);
    const metadata = JSON.parse(row.metadata_json) as { variants: Array<{ id: string; config: Record<string, unknown> }> };
    const variant = metadata.variants.find(item => item.id === "default");
    assert.ok(variant);
    const preview = JSON.parse(row.design_preview_json) as { participants: Array<Record<string, unknown>> };
    const actor = (JSON.parse(base.designPreviewJson) as Record<string, unknown>).actor;
    for (const participant of preview.participants) participant.actor = actor;
    return { ...base, kind: "module" as const, componentType: "module.core.videoCall", configJson: JSON.stringify(variant.config), designPreviewJson: JSON.stringify(preview), runtimeContractJson: JSON.stringify(preview) };
  } finally { database.close(); }
}

test("Video Call resolves group participants including connecting and connection-loss states", () => {
  const source = fixture();
  const call = resolveVideoCallModule(source);
  assert.equal(call.participants.length, 4);
  assert.equal(call.participants.find(item => item.id === "participant_sam")?.participant.statusLabel.text, "Connecting…");
  assert.equal(call.participants.find(item => item.id === "participant_jon")?.participant.statusLabel.text, "Connection lost");
  const node = videoCallModuleToRenderable(source);
  assert.equal(node.id, "module.core.videoCall");
  assert.ok((node.children?.length ?? 0) > 4);
});

test("Video Call automatic layout expands one remote participant and keeps self view optional", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { selfParticipantId: string; participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 2);
  preview.selfParticipantId = String(preview.participants[1]!.id);
  const oneToOne = { ...source, designPreviewJson: JSON.stringify(preview) };
  const node = videoCallModuleToRenderable(oneToOne);
  assert.ok(node.children?.some(child => child.id === "component.callParticipant"));
});

test("Video Call master switches remove header, controls, self view and system chrome", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  for (const key of ["showStatusBar", "showHeader", "showSelfView", "showControls", "showNavigationBar"]) config.videoCall[key] = false;
  const hidden = { ...source, configJson: JSON.stringify(config) };
  const call = resolveVideoCallModule(hidden);
  assert.equal(call.visibility.showHeader, false);
  const node = videoCallModuleToRenderable(hidden);
  assert.ok(!node.children?.some(child => child.id.includes("statusBar") || child.id.includes("navigationBar")));
});
