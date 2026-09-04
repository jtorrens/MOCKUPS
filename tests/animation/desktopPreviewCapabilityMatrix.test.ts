import assert from "node:assert/strict";
import test from "node:test";
import { existsSync } from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";

import {
  desktopPreviewComponentCapabilities,
  desktopPreviewModuleCapabilities,
} from "../../src/desktop-preview/desktopPreviewCapabilityMatrix.js";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentClass,
} from "../../src/desktop-preview/desktopPreviewComponents.js";
import { desktopPreviewModules } from "../../src/desktop-preview/desktopPreviewModules.js";
import { parityDatabasePath } from "../../src/development-scaffolding/parityDatabasePath.js";

type Action = {
  id: string;
};

type Collection = {
  jsonKey: string;
  itemActions?: Action[];
};

type RuntimeOwnerDocument = {
  actions?: Action[];
  collections?: Collection[];
};

const databasePath = parityDatabasePath();

const focusedCharacterizations = {
  label: "tests/animation/calculatedText.test.ts",
  cursor: "tests/animation/cursorFrameResolution.test.ts",
  textBox: "tests/animation/cursorFrameResolution.test.ts",
  componentStack: "tests/animation/componentStackComponent.test.ts",
  collectionStack: "tests/animation/collectionStackComponent.test.ts",
  notification: "tests/animation/atomicTemporalComponents.test.ts",
  notifications: "tests/animation/notificationsComponent.test.ts",
  list: "tests/animation/listComponent.test.ts",
  button: "tests/animation/atomicTemporalComponents.test.ts",
  audio: "tests/animation/atomicTemporalComponents.test.ts",
  textInputBar: "tests/animation/cursorFrameResolution.test.ts",
  keyboard: "tests/animation/atomicTemporalComponents.test.ts",
  keypad: "tests/animation/atomicTemporalComponents.test.ts",
  fingerprint: "tests/animation/atomicTemporalComponents.test.ts",
  faceRecognition: "tests/animation/atomicTemporalComponents.test.ts",
  drawPassword: "tests/animation/atomicTemporalComponents.test.ts",
  password: "tests/animation/passwordComponentResolver.test.ts",
  media: "tests/animation/atomicTemporalComponents.test.ts",
  bubble: "tests/animation/atomicTemporalComponents.test.ts",
  incomingCallNotification:
    "tests/animation/incomingCallNotificationComponent.test.ts",
  callParticipant: "tests/animation/callParticipantComponent.test.ts",
  "module.core.chat": "tests/animation/conversationModuleResolver.test.ts",
  "module.core.lockScreen":
    "tests/Mockups.Desktop.Tests/Program.cs",
  "module.core.chatList": "tests/animation/chatListModule.test.ts",
  "module.core.socialPost": "tests/animation/socialPostModule.test.ts",
  "module.core.videoCall": "tests/animation/videoCallModule.test.ts",
} as const;

test("the capability matrix covers every manifest owner exactly once", () => {
  assert.deepEqual(
    Object.keys(desktopPreviewComponentCapabilities).sort(),
    Object.keys(desktopPreviewComponents).sort(),
  );
  assert.deepEqual(
    Object.keys(desktopPreviewModuleCapabilities).sort(),
    Object.keys(desktopPreviewModules).sort(),
  );
});

test("every frame-owned behavior has an explicit focused characterization", () => {
  const temporalOwners = [
    ...Object.entries(desktopPreviewComponentCapabilities),
    ...Object.entries(desktopPreviewModuleCapabilities),
  ]
    .filter(([, capabilities]) => capabilities.frameCapabilities.length > 0)
    .map(([owner]) => owner)
    .sort();
  assert.deepEqual(
    Object.keys(focusedCharacterizations).sort(),
    temporalOwners,
  );
  for (const [owner, relativePath] of Object.entries(focusedCharacterizations)) {
    assert.ok(
      existsSync(path.join(process.cwd(), relativePath)),
      `${owner} focused characterization is missing: ${relativePath}`,
    );
  }
});

test("the capability matrix declares every persisted component action exactly", () => {
  const database = new Database(databasePath, {
    readonly: true,
    fileMustExist: true,
  });
  try {
    const rows = database.prepare(`
      SELECT component_type, design_preview_json
      FROM component_classes
      ORDER BY component_type, id
    `).all() as Array<{
      component_type: DesktopPreviewComponentClass;
      design_preview_json: string;
    }>;
    const covered = new Set<DesktopPreviewComponentClass>();
    for (const row of rows) {
      assert.ok(Object.hasOwn(desktopPreviewComponents, row.component_type));
      const expected = desktopPreviewComponentCapabilities[row.component_type];
      assert.ok(expected);
      assertActionCoverage(
        JSON.parse(row.design_preview_json) as RuntimeOwnerDocument,
        expected,
        `Component ${row.component_type}`,
      );
      covered.add(row.component_type);
    }
    assert.deepEqual(
      [...covered].sort(),
      Object.keys(desktopPreviewComponents).sort(),
    );
  } finally {
    database.close();
  }
});

test("the capability matrix declares every persisted Module action exactly", () => {
  const database = new Database(databasePath, {
    readonly: true,
    fileMustExist: true,
  });
  try {
    const rows = database.prepare(`
      SELECT record_class_id, design_preview_json
      FROM modules
      ORDER BY record_class_id, id
    `).all() as Array<{
      record_class_id: keyof typeof desktopPreviewModuleCapabilities;
      design_preview_json: string;
    }>;
    const covered = new Set<keyof typeof desktopPreviewModuleCapabilities>();
    for (const row of rows) {
      assert.ok(Object.hasOwn(desktopPreviewModules, row.record_class_id));
      const expected = desktopPreviewModuleCapabilities[row.record_class_id];
      assert.ok(expected);
      assertActionCoverage(
        JSON.parse(row.design_preview_json) as RuntimeOwnerDocument,
        expected,
        `Module ${row.record_class_id}`,
      );
      covered.add(row.record_class_id);
    }
    assert.deepEqual(
      [...covered].sort(),
      Object.keys(desktopPreviewModules).sort(),
    );
  } finally {
    database.close();
  }
});

function assertActionCoverage(
  document: RuntimeOwnerDocument,
  expected: {
    readonly runtimeActions: readonly string[];
    readonly collectionActions: readonly string[];
  },
  owner: string,
) {
  const runtimeActions = (document.actions ?? []).map((action) => action.id);
  const collectionActions = (document.collections ?? []).flatMap((collection) =>
    (collection.itemActions ?? []).map((action) => `${collection.jsonKey}.${action.id}`),
  );
  assert.equal(
    new Set(runtimeActions).size,
    runtimeActions.length,
    `${owner} repeats a runtime action id`,
  );
  assert.equal(
    new Set(collectionActions).size,
    collectionActions.length,
    `${owner} repeats a collection action id`,
  );
  assert.deepEqual(
    runtimeActions.sort(),
    [...expected.runtimeActions].sort(),
    `${owner} runtime actions differ from its audited capabilities`,
  );
  assert.deepEqual(
    collectionActions.sort(),
    [...expected.collectionActions].sort(),
    `${owner} collection actions differ from its audited capabilities`,
  );
}
