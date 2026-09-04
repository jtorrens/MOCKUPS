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
    const preview = JSON.parse(row.design_preview_json) as {
      participants: Array<Record<string, unknown>>;
      videoCallHeaderRows: Array<Record<string, unknown>>;
      videoCallFooterRows: Array<Record<string, unknown>>;
    };
    const actor = (JSON.parse(base.designPreviewJson) as Record<string, unknown>).actor;
    for (const participant of preview.participants) participant.actor = actor;
    for (const runtimeRow of [...preview.videoCallHeaderRows, ...preview.videoCallFooterRows]) {
      for (const slot of [1, 2, 3, 4, 5]) runtimeRow[`slot${slot}Actor`] = actor;
    }
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

test("Video Call permits simultaneous participants with the same role", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 2);
  for (const participant of preview.participants) participant.role = "main";
  const duplicateMain = { ...source, designPreviewJson: JSON.stringify(preview) };
  const call = resolveVideoCallModule(duplicateMain);
  assert.deepEqual(call.participants.map(({ role }) => role), ["main", "main"]);
  const node = videoCallModuleToRenderable(duplicateMain);
  assert.equal(node.children?.filter(child => child.id === "component.callParticipant").length, 2);
});

test("Video Call grid rows fill every row without reserving empty columns", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 3);
  for (const participant of preview.participants) participant.role = "grid";
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  config.videoCall.gridRows = 2;

  const node = videoCallModuleToRenderable({
    ...source,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(preview),
  });
  const participants = node.children?.filter(child => child.id === "component.callParticipant") ?? [];
  assert.equal(participants.length, 3);
  assert.ok(participants.every(participant => participant.box));
  assert.equal(participants[0]!.box!.y, participants[1]!.box!.y);
  assert.equal(participants[0]!.box!.height, participants[2]!.box!.height);
  assert.ok(participants[2]!.box!.y > participants[0]!.box!.y);
  assert.ok(participants[2]!.box!.width > participants[0]!.box!.width * 1.9);
});

test("Video Call master switches remove every optional section", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  for (const key of ["showStatusBar", "showHeader", "showFooter", "showMainVideo", "showPip", "showGridParticipants", "showNavigationBar"]) config.videoCall[key] = false;
  const hidden = { ...source, configJson: JSON.stringify(config) };
  const call = resolveVideoCallModule(hidden);
  assert.equal(call.showHeader, false);
  assert.equal(call.showFooter, false);
  const node = videoCallModuleToRenderable(hidden);
  assert.deepEqual(node.children?.map(({ id }) => id), ["module.core.videoCall.background"]);
});

test("Video Call centers row blocks and presents participants as one collection", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as {
    collections: Array<Record<string, unknown>>;
  };
  const participants = preview.collections.find(collection => collection.id === "participants");
  assert.ok(participants);
  assert.equal("uiPresentation" in participants, false);
  assert.equal("canEditStructure" in participants, false);

  const node = videoCallModuleToRenderable(source);
  const header = node.children?.find(child => child.id === "module.core.videoCall.header");
  assert.ok(header?.box);
  const rows = header.children?.filter(child => child.id === "module.core.videoCall.header.row1" || child.id === "module.core.videoCall.header.row2") ?? [];
  assert.equal(rows.length, 2);
  assert.ok(rows[0]?.box && rows[1]?.box);
  const rowsCenter = (rows[0]!.box!.y + rows[1]!.box!.y + rows[1]!.box!.height) * 0.5;
  const headerCenter = header.box.y + header.box.height * 0.5;
  assert.ok(Math.abs(rowsCenter - headerCenter) < 0.001);
});
