import assert from "node:assert/strict";
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  writeFileSync,
} from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import Database from "better-sqlite3";

import {
  createModuleScaffoldPlan,
  loadModuleScaffoldInventory,
  moduleScaffoldTemplate,
  ModuleScaffoldValidationError,
  parseModuleScaffoldSpec,
  type ModuleScaffoldSpec,
} from "../../src/development-scaffolding/moduleScaffold.js";
import {
  integrateModuleScaffold,
  materializeModuleScaffold,
  verifyModuleScaffoldImplementation,
} from "../../src/development-scaffolding/moduleScaffoldWorkspace.js";

const repositoryRoot = process.cwd();

test("Module scaffold template is deliberately incomplete", () => {
  const spec = moduleScaffoldTemplate();
  assert.throws(
    () => createModuleScaffoldPlan(
      spec,
      loadModuleScaffoldInventory(repositoryRoot),
      repositoryRoot,
    ),
    (error: unknown) =>
      error instanceof ModuleScaffoldValidationError
      && error.message.includes("placeholders must be replaced"),
  );
});

test("Module scaffold derives one exact child Runtime contract", () => {
  const spec = validSpec();
  const plan = createModuleScaffoldPlan(
    spec,
    loadModuleScaffoldInventory(repositoryRoot),
    repositoryRoot,
  );
  assert.equal(plan.persistedDefinition.row.design_preview_json.componentType, spec.module.recordClassId);
  assert.deepEqual(
    (plan.persistedDefinition.row.design_preview_json.inputs as Array<{ id: string }>)
      .map((input) => input.id),
    ["itemWidth", "itemHeight"],
  );
  assert.deepEqual(
    (plan.persistedDefinition.row.design_preview_json.collections as Array<{ id: string }>)
      .map((collection) => collection.id),
    ["items"],
  );
  assert.deepEqual(
    plan.persistedDefinition.row.design_preview_json.animationTimeline,
    { durationPolicy: "calculated" },
  );
});

test("Module scaffold rejects Runtime source drift and invalid duration", () => {
  const drifted = validSpec();
  drifted.runtimeContract.source.inputIds = ["itemWidth"];
  assert.throws(
    () => createModuleScaffoldPlan(
      drifted,
      loadModuleScaffoldInventory(repositoryRoot),
      repositoryRoot,
    ),
    /Runtime source input ids differ/,
  );

  const invalidDuration = validSpec();
  invalidDuration.runtimeContract.durationPolicy = "explicit";
  invalidDuration.runtimeContract.defaultDurationFrames = 0;
  assert.throws(
    () => createModuleScaffoldPlan(
      invalidDuration,
      loadModuleScaffoldInventory(repositoryRoot),
      repositoryRoot,
    ),
    /positive defaultDurationFrames/,
  );
});

test("Module scaffold parser rejects hidden and incomplete envelopes", () => {
  const missing = structuredClone(validSpec()) as unknown as Record<string, unknown>;
  delete missing.runtimeContract;
  assert.throws(() => parseModuleScaffoldSpec(missing), /must contain exactly/);

  const extra = structuredClone(validSpec()) as unknown as Record<string, unknown>;
  extra.compatibility = {};
  assert.throws(() => parseModuleScaffoldSpec(extra), /must contain exactly/);
});

test("Module materialization is no-overwrite and integration is transactional", () => {
  const fixture = integrationFixture();
  const spec = validSpec();
  const inventory = loadModuleScaffoldInventory(fixture.root, fixture.database);
  const plan = createModuleScaffoldPlan(spec, inventory, fixture.root);
  const materialized = materializeModuleScaffold(spec, plan, fixture.root);
  assert.equal(materialized.status, "materialized-unregistered");
  assert.throws(
    () => materializeModuleScaffold(spec, plan, fixture.root),
    /will not overwrite/,
  );
  assert.throws(
    () => integrateModuleScaffold(spec, fixture.root, fixture.database),
    /still requires semantics/,
  );
  const database = new Database(fixture.database, { readonly: true });
  try {
    assert.equal(
      (database.prepare("SELECT COUNT(*) AS count FROM modules WHERE id = ?")
        .get(spec.module.moduleId) as { count: number }).count,
      0,
    );
  } finally {
    database.close();
  }

  for (const owner of plan.creates) {
    writeFileSync(
      path.join(fixture.root, owner.path),
      owner.requiredExport
        ? `export const ${owner.requiredExport} = true;\n`
        : `${spec.owners.resolverExport}\n`,
      "utf8",
    );
  }
  const integrated = integrateModuleScaffold(spec, fixture.root, fixture.database);
  assert.equal(integrated.status, "integrated");
  assert.ok(existsSync(path.join(fixture.root, integrated.specPath)));
  assert.equal(
    verifyModuleScaffoldImplementation(spec, fixture.root, fixture.database).status,
    "integrated-contract-verified",
  );
});

function validSpec(): ModuleScaffoldSpec {
  const config = {
    appearanceMode: "inherit",
    chatList: {
      listSlot: {
        variantReference: "component_project_foqn_s2_list::variant::chats",
        overrides: {},
      },
    },
  };
  return {
    schemaVersion: 1,
    intent: {
      responsibility: "Own one reusable chat-list Screen.",
      visualBoundary: "Place one exact List boundary inside the Screen.",
      runtimeBehavior: "Expose the exact List Runtime contract.",
      forwarding: "Promote List Runtime ids unchanged.",
      temporalOwnership: "Own Screen time while List owns its collection.",
      productionContext: "Require exact Screen to Shot context.",
    },
    module: {
      moduleId: "module_project_foqn_s2_scaffold_test",
      appId: "app_core_chat",
      projectId: "project_foqn_s2",
      recordClassId: "module.core.scaffoldTest",
      name: "Scaffold Test",
      notes: "Test-only Module contract.",
      sortOrder: 9,
    },
    manifest: {
      label: "Scaffold Test",
      contract: "./scaffoldTestModuleContract",
      resolver: "./scaffoldTestModuleResolver",
      renderable: "./scaffoldTestModuleRenderable",
      embeds: ["list"],
    },
    owners: {
      contractExport: "ScaffoldTestModuleContract",
      resolverExport: "resolveScaffoldTestModule",
      renderableExport: "scaffoldTestModuleToRenderable",
      focusedTest: "tests/animation/scaffoldTestModule.test.ts",
    },
    config,
    defaultVariant: {
      id: "default",
      name: "Default",
      protected: true,
      locked: true,
      config: structuredClone(config),
    },
    additionalVariants: [],
    runtimeContract: {
      source: {
        componentType: "list",
        variantReference: "component_project_foqn_s2_list::variant::chats",
        inputIds: ["itemWidth", "itemHeight"],
        collectionIds: ["items"],
      },
      durationPolicy: "calculated",
      defaultDurationFrames: null,
    },
    metadata: { note: "Test-only scaffold." },
    dictionaryFields: [
      {
        id: "module.core.scaffoldTest.list",
        label: "List",
        valueKind: "ComponentVariantSlot",
        jsonPath: ["chatList", "listSlot"],
        defaultValue: JSON.stringify(config.chatList.listSlot),
        isEditable: true,
        options: [],
        optionsSource: "",
        pairLabels: null,
        number: null,
        componentVariantType: "list",
        runtimeInputComponentVariantFieldId: "",
        runtimeCollectionComponentVariantFieldId: "",
        componentInputBindingsSource: "",
        unit: "",
        embeddedSlot: {
          componentType: "list",
          label: "List",
          recordClassId: "component.list",
        },
      },
    ],
    editorLayout: {
      cards: [
        {
          id: "general",
          label: "General",
          subtitle: "Identity",
          icon: "general",
          order: 10,
          visible: true,
          defaultOpen: false,
          groups: [
            {
              id: "identity",
              label: "Identity",
              order: 10,
              visible: true,
              fields: [
                { id: "core.name", order: 10, visible: true },
                { id: "module.core.scaffoldTest.list", order: 20, visible: true },
              ],
            },
          ],
        },
      ],
    },
    assets: [],
  };
}

function integrationFixture() {
  const root = mkdtempSync(path.join(os.tmpdir(), "mockups-module-scaffold-"));
  for (const directory of [
    "data",
    "src/desktop-preview",
    "spikes/desktop-editor-shell/EditorShell",
    "spikes/desktop-editor-shell/Data",
    "tests/animation",
    "scaffolding",
  ]) {
    mkdirSync(path.join(root, directory), { recursive: true });
  }
  const database = path.join(root, "data/desktop-editor-spike.sqlite");
  copyFileSync(path.join(repositoryRoot, "data/desktop-editor-spike.sqlite"), database);
  for (const relativePath of [
    "src/desktop-preview/desktopPreviewManifest.json",
    "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs",
  ]) {
    copyFileSync(path.join(repositoryRoot, relativePath), path.join(root, relativePath));
  }
  for (const relativePath of [
    "src/desktop-preview/generatedModuleScaffoldRegistry.ts",
    "spikes/desktop-editor-shell/EditorShell/GeneratedModuleScaffoldFieldCatalog.cs",
    "spikes/desktop-editor-shell/Data/GeneratedModuleScaffoldConfigRegistry.cs",
    "spikes/desktop-editor-shell/EditorShell/GeneratedModuleScaffoldEmbeddedSlots.cs",
  ]) {
    writeFileSync(path.join(root, relativePath), readFileSync(path.join(repositoryRoot, relativePath)));
  }
  return { root, database };
}
