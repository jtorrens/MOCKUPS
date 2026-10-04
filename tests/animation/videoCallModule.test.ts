import assert from "node:assert/strict";
import test from "node:test";
import Database from "better-sqlite3";

import { parityDatabasePath } from "../../src/development-scaffolding/parityDatabasePath.js";
import { videoCallModuleToRenderable } from "../../src/desktop-preview/videoCallModuleRenderable.js";
import { resolveVideoCallModule } from "../../src/desktop-preview/videoCallModuleResolver.js";
import { numberToken, renderScale } from "../../src/desktop-preview/componentRenderableCommon.js";
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
      videoCallMainRows: Array<Record<string, unknown>>;
    };
    const actor = (JSON.parse(base.designPreviewJson) as Record<string, unknown>).actor;
    for (const participant of preview.participants) participant.actor = actor;
    for (const runtimeRow of [
      ...preview.videoCallHeaderRows,
      ...preview.videoCallFooterRows,
      ...preview.videoCallMainRows,
    ]) {
      const slots = runtimeRow.slotInputs as Array<Record<string, unknown>>;
      for (const slot of slots) slot.actor = actor;
    }
    return { ...base, kind: "module" as const, componentType: "module.core.videoCall", configJson: JSON.stringify(variant.config), designPreviewJson: JSON.stringify(preview), runtimeContractJson: JSON.stringify(preview) };
  } finally { database.close(); }
}

test("Video Call resolves free-form and empty participant connection text", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants[0]!.connectionText = "Sin señal";
  const edited = { ...source, designPreviewJson: JSON.stringify(preview) };
  const call = resolveVideoCallModule(edited);
  assert.equal(call.participants.length, 4);
  assert.equal(call.participants[0]?.statusLabel.text, "Sin señal");
  assert.equal(call.participants.find(item => item.id === "participant_sam")?.statusLabel.text, "");
  assert.equal(call.participants.find(item => item.id === "participant_jon")?.statusLabel.text, "Connection lost");
  const node = videoCallModuleToRenderable(edited);
  assert.equal(node.id, "module.core.videoCall");
  assert.ok((node.children?.length ?? 0) > 4);
});

test("Video Call derives each visible participant name from its Actor", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants[0]!.showActorName = true;
  preview.participants[1]!.showActorName = false;
  const selectedActor = preview.participants[0]!.actor as { displayName: string };
  const edited = { ...source, designPreviewJson: JSON.stringify(preview) };
  const call = resolveVideoCallModule(edited);
  assert.equal(call.participants[0]?.nameLabel?.text, selectedActor.displayName);
  assert.equal(call.participants[0]?.showActorName, true);
  assert.equal(call.participants[1]?.showActorName, false);
  const node = videoCallModuleToRenderable(edited);
  const visible = node.children?.find(child => child.id === "participant_alex");
  const hidden = node.children?.find(child => child.id === "participant_asia");
  const name = visible?.children?.find(child => child.id.includes(".name"));
  assert.ok(visible?.box && name?.box);
  assert.ok(Math.abs(
    name.box.x + name.box.width * 0.5
      - (visible.box.x + visible.box.width * 0.5),
  ) < 0.001);
  assert.equal(hidden?.children?.some(child => child.id.includes(".name")), false);
});

test("Video Call uses exactly one configured gap between percentage regions", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 2);
  preview.participants[0]!.role = "main";
  preview.participants[1]!.role = "grid";
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  config.videoCall.gridHeightMode = "percent";
  config.videoCall.gridHeightPercent = 50;
  config.videoCall.participantOuterPadding = "theme.spacing.m|theme.spacing.m";
  config.videoCall.gridGapToken = "theme.spacing.s";
  config.videoCall.showPip = false;

  const node = videoCallModuleToRenderable({
    ...source,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(preview),
  });
  const main = node.children?.find(child => child.id === "participant_alex");
  const grid = node.children?.find(child => child.id === "participant_asia");
  assert.ok(main?.box && grid?.box);
  assert.equal(
    grid.box.y - (main.box.y + main.box.height),
    numberToken(source, "theme.spacing.s") * renderScale(source),
  );
});

test("Video Call linearly animates participant Media scale and offset", () => {
  const source = fixture();
  source.localFrame = 5;
  source.instanceJson = JSON.stringify({
    context: { screenFrame: 5 },
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          fieldId: "mediaScale",
          targetId: "participant_alex",
          keyframes: [
            { frame: 0, value: 1, interpolation: "hold" },
            { frame: 10, value: 2, interpolation: "linear" },
          ],
        },
        {
          fieldId: "mediaOffset",
          targetId: "participant_alex",
          keyframes: [
            { frame: 0, value: "0|0", interpolation: "hold" },
            { frame: 10, value: "20|-10", interpolation: "linear" },
          ],
        },
      ],
    },
  });
  const participant = resolveVideoCallModule(source).participants
    .find(item => item.id === "participant_alex");
  assert.equal(participant?.media.viewport.scale, 1.5);
  assert.equal(participant?.media.viewport.offsetX, 10);
  assert.equal(participant?.media.viewport.offsetY, -5);
});

test("Video Call enters a participant and reflows stable cards from their prior geometry", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 2);
  preview.participants[0]!.role = "main";
  preview.participants[1]!.role = "grid";
  preview.participants[1]!.present = false;
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  config.videoCall.showPip = false;
  const at = (frame: number) => {
    const payload = {
      ...source,
      localFrame: frame,
      configJson: JSON.stringify(config),
      designPreviewJson: JSON.stringify(preview),
      runtimeContractJson: JSON.stringify(preview),
      instanceJson: JSON.stringify({
        context: { screenFrame: frame },
        animation: {
          schemaVersion: 2,
          tracks: [{
            fieldId: "present",
            targetId: "participant_asia",
            keyframes: [{ frame: 10, value: true, interpolation: "hold" }],
          }],
        },
      }),
    };
    const renderable = videoCallModuleToRenderable(payload);
    const enteringNode = renderable.children
      ?.find(child => child.id === "participant_asia.motion");
    return {
      contract: resolveVideoCallModule(payload),
      main: renderable.children
        ?.find(child => child.id === "participant_alex"),
      enteringBox: enteringNode?.children?.[0]?.box,
    };
  };

  const before = at(9);
  const start = at(10);
  const middle = at(12);
  const after = at(20);
  const entering = start.contract.participants.find(item => item.id === "participant_asia");
  assert.equal(entering?.presenceMotionKind, "enter");
  assert.equal(start.contract.participantReflow?.progress, 0);
  assert.ok(middle.contract.participantReflow?.progress);
  assert.equal(after.contract.participantReflow, undefined);
  assert.ok(before.main?.box && start.main?.box && middle.main?.box && after.main?.box
    && start.enteringBox && middle.enteringBox);
  assert.equal(start.main.box.height, before.main.box.height);
  assert.ok(start.main.box.y + start.main.box.height <= start.enteringBox.y);
  assert.ok(middle.main.box.height < start.main.box.height);
  assert.ok(middle.main.box.y + middle.main.box.height <= middle.enteringBox.y);
  assert.equal(after.main.box.height < middle.main.box.height, true);
});

test("Video Call keeps an initially absent participant out of Preview", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 2);
  preview.participants[1]!.present = false;
  const targetId = String(preview.participants[1]!.id);
  const payload = {
    ...source,
    localFrame: 0,
    designPreviewJson: JSON.stringify(preview),
    runtimeContractJson: JSON.stringify(preview),
    instanceJson: JSON.stringify({
      context: { screenFrame: 0 },
      animation: {
        schemaVersion: 2,
        tracks: [{
          fieldId: "present",
          targetId,
          keyframes: [{ frame: 0, value: false, interpolation: "hold" }],
        }],
      },
    }),
  };

  assert.equal(resolveVideoCallModule(payload).participants.some(({ id }) => id === targetId), false);
  assert.equal(videoCallModuleToRenderable(payload).children?.some(({ id }) => id === targetId), false);
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
  assert.equal(node.children?.filter(child => child.id.startsWith("participant_")).length, 2);
});

test("Video Call grid rows fill every row without reserving empty columns", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 3);
  for (const participant of preview.participants) participant.role = "grid";
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  config.videoCall.gridHeightMode = "fill";
  config.videoCall.gridRows = 2;

  const node = videoCallModuleToRenderable({
    ...source,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(preview),
  });
  const participants = node.children?.filter(child => child.id.startsWith("participant_")) ?? [];
  assert.equal(participants.length, 3);
  assert.ok(participants.every(participant => participant.box));
  assert.equal(participants[0]!.box!.y, participants[1]!.box!.y);
  assert.equal(participants[0]!.box!.height, participants[2]!.box!.height);
  assert.ok(participants[2]!.box!.y > participants[0]!.box!.y);
  assert.ok(participants[2]!.box!.width > participants[0]!.box!.width * 1.9);
});

test("Video Call percentage grid uses the stacked available body", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 3);
  preview.participants[0]!.role = "main";
  preview.participants[1]!.role = "grid";
  preview.participants[2]!.role = "grid";
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  config.videoCall.gridHeightMode = "percent";
  config.videoCall.gridHeightPercent = 50;
  config.videoCall.gridRows = 1;
  config.videoCall.participantOuterPadding = "theme.spacing.none|theme.spacing.none";
  config.videoCall.gridGapToken = "theme.spacing.none";
  config.videoCall.showPip = false;

  const node = videoCallModuleToRenderable({ ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(preview) });
  const participants = node.children?.filter(child => child.id.startsWith("participant_")) ?? [];
  const footer = node.children?.find(child => child.id === "module.core.videoCall.footer");
  assert.equal(participants.length, 3);
  assert.ok(participants.every(participant => participant.box) && footer?.box);
  assert.ok(participants[1]!.box!.y >= participants[0]!.box!.y + participants[0]!.box!.height);
  assert.equal(participants[1]!.box!.y, participants[2]!.box!.y);
  assert.equal(participants[1]!.box!.y + participants[1]!.box!.height, footer!.box!.y);
  assert.equal(participants[1]!.box!.height, participants[0]!.box!.height);
});

test("Video Call percentage main expands through the complete body when there is no grid", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 1);
  preview.participants[0]!.role = "main";
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  config.videoCall.gridHeightMode = "percent";
  config.videoCall.participantOuterPadding = "theme.spacing.none|theme.spacing.none";
  for (const key of ["showStatusBar", "showHeader", "showFooter", "showPip", "showNavigationBar"]) config.videoCall[key] = false;

  const node = videoCallModuleToRenderable({ ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(preview) });
  const background = node.children?.find(child => child.id === "module.core.videoCall.background");
  const participant = node.children?.find(child => child.id.startsWith("participant_"));
  assert.deepEqual(participant?.box, background?.box);
});

test("Video Call fill mode gives main and grid roles the same tile geometry", () => {
  const source = fixture();
  const preview = JSON.parse(source.designPreviewJson) as { participants: Array<Record<string, unknown>> };
  preview.participants = preview.participants.slice(0, 3);
  preview.participants[0]!.role = "main";
  preview.participants[1]!.role = "grid";
  preview.participants[2]!.role = "grid";
  const config = JSON.parse(source.configJson) as { videoCall: Record<string, unknown> };
  config.videoCall.gridHeightMode = "fill";
  config.videoCall.gridRows = 2;
  config.videoCall.participantOuterPadding = "theme.spacing.none|theme.spacing.none";
  config.videoCall.gridGapToken = "theme.spacing.none";
  for (const key of ["showStatusBar", "showHeader", "showFooter", "showPip", "showNavigationBar"]) config.videoCall[key] = false;

  const render = (participants: Array<Record<string, unknown>>) => videoCallModuleToRenderable({
    ...source,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify({ ...preview, participants }),
  }).children?.filter(child => child.id.startsWith("participant_")).map(child => child.box);
  const before = render(preview.participants);
  preview.participants[0]!.role = "grid";
  preview.participants[1]!.role = "main";
  const after = render(preview.participants);
  assert.deepEqual(after, before);
  assert.equal(before?.[0]?.width, before?.[1]?.width);
  assert.equal(before?.[0]?.height, before?.[1]?.height);
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
  assert.equal(rows.length, 1);
  assert.ok(rows[0]?.box);
  const rowsCenter = rows[0]!.box!.y + rows[0]!.box!.height * 0.5;
  const headerCenter = header.box.y + header.box.height * 0.5;
  assert.ok(Math.abs(rowsCenter - headerCenter) < 0.001);
});

test("Video Call excludes invisible rows while preserving the remaining row placement", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as {
    videoCall: { headerRows: Array<{ visible: boolean }> };
  };
  config.videoCall.headerRows[1]!.visible = false;
  const hidden = { ...source, configJson: JSON.stringify(config) };
  const contract = resolveVideoCallModule(hidden);
  assert.equal(contract.headerRows[0].visible, true);
  assert.equal(contract.headerRows[1].visible, false);

  const node = videoCallModuleToRenderable(hidden);
  const header = node.children?.find(child => child.id === "module.core.videoCall.header");
  const row1 = header?.children?.find(child => child.id === "module.core.videoCall.header.row1");
  const row2 = header?.children?.find(child => child.id === "module.core.videoCall.header.row2");
  assert.ok(header?.box && row1?.box);
  assert.equal(row2, undefined);
  assert.equal(
    row1.box.y + row1.box.height * 0.5,
    header.box.y + header.box.height * 0.5,
  );
});
