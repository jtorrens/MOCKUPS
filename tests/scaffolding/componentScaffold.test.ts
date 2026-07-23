import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import os from "node:os";

import Database from "better-sqlite3";

import {
  ComponentScaffoldValidationError,
  componentScaffoldTemplate,
  createComponentScaffoldPlan,
  loadComponentScaffoldInventory,
  parseComponentScaffoldSpec,
  resolveComponentScaffoldSpecPath,
  type ComponentScaffoldInventory,
  type ComponentScaffoldSpec,
} from "../../src/development-scaffolding/componentScaffold.js";

const repositoryRoot = process.cwd();

function inventory(
  overrides: Partial<ComponentScaffoldInventory> = {},
): ComponentScaffoldInventory {
  return {
    componentTypes: new Set(["label", "surface"]),
    projectIds: new Set(["project_foqn_s2"]),
    recordClassIds: new Set(["component.label", "component.surface"]),
    componentClasses: [
      {
        id: "component_project_foqn_s2_label",
        projectId: "project_foqn_s2",
        componentType: "label",
        recordClassId: "component.label",
        name: "Label",
      },
    ],
    valueKinds: new Set([
      "Boolean",
      "Decimal",
      "Integer",
      "StringSingleLine",
      "ThemeToken",
    ]),
    ...overrides,
  };
}

function cloneTemplate(): ComponentScaffoldSpec {
  return structuredClone(componentScaffoldTemplate());
}

function validSpec(): ComponentScaffoldSpec {
  const spec = cloneTemplate();
  spec.intent = {
    responsibility: "Own an isolated numeric fixture used to characterize scaffolding.",
    visualBoundary: "Own one centered generic group inside the assigned Preview frame.",
    runtimeBehavior: "Expose one integer Runtime Input and render its resolved value.",
    forwarding: "No forwarding is declared.",
    temporalOwnership: "Static owner with no temporal state.",
  };
  spec.component = {
    componentType: "scaffoldFixture",
    category: "atom",
    componentClassId: "component_project_foqn_s2_scaffold_fixture",
    projectId: "project_foqn_s2",
    recordClassId: "component.scaffoldFixture",
    name: "Scaffold Fixture",
    notes: "Disposable contract used only by scaffolding tests.",
  };
  spec.manifest = {
    contract: "./scaffoldFixtureComponentContract",
    resolver: "./scaffoldFixtureComponentResolver",
    renderable: "./scaffoldFixtureComponentRenderable",
    embeds: [],
  };
  spec.owners = {
    contractExport: "ScaffoldFixtureDesignContract",
    resolverExport: "resolveScaffoldFixtureComponent",
    renderableExport: "scaffoldFixtureComponentToRenderable",
    registryMode: "simple",
    focusedTest: "tests/animation/scaffoldFixtureComponent.test.ts",
  };
  spec.config = { scaffoldFixture: { size: 100 } };
  spec.defaultVariant.config = structuredClone(spec.config);
  spec.designPreview.componentType = "scaffoldFixture";
  spec.metadata = { note: "Complete disposable scaffolding fixture." };
  spec.dictionaryFields[0]!.id = "component.scaffoldFixture.size";
  spec.dictionaryFields[0]!.jsonPath = ["scaffoldFixture", "size"];
  const cards = spec.editorLayout.cards as Array<Record<string, unknown>>;
  const groups = cards[1]!.groups as Array<Record<string, unknown>>;
  const fields = groups[0]!.fields as Array<Record<string, unknown>>;
  fields[0]!.id = "component.scaffoldFixture.size";
  return spec;
}

function expectInvalid(spec: ComponentScaffoldSpec, expected: string) {
  assert.throws(
    () => createComponentScaffoldPlan(spec, inventory(), repositoryRoot),
    (error: unknown) =>
      error instanceof ComponentScaffoldValidationError
      && error.message.includes(expected),
  );
}

test("Authored Component scaffold contract produces a deterministic read-only owner plan", () => {
  const spec = parseComponentScaffoldSpec(JSON.parse(JSON.stringify(validSpec())) as unknown);
  const first = createComponentScaffoldPlan(spec, inventory(), repositoryRoot);
  const second = createComponentScaffoldPlan(spec, inventory(), repositoryRoot);

  assert.deepEqual(first, second);
  assert.equal(first.mode, "dry-run");
  assert.equal(first.status, "contract-ready-for-owner-implementation");
  assert.deepEqual(
    first.creates.map((owner) => owner.role),
    ["contract", "resolver", "renderable", "focusedTest"],
  );
  assert.deepEqual(first.manifestEntry.embeds, []);
  assert.equal(first.registryRoute.mode, "simple");
  assert.equal(
    first.persistedDefinition.row.metadata_json.variants?.[0]
      && (first.persistedDefinition.row.metadata_json.variants[0] as { id?: unknown }).id,
    "default",
  );
  assert.equal(
    (first.persistedDefinition.row.metadata_json.variants as unknown[]).length,
    1,
  );
  assert.equal(first.editorLayout.row.record_class_id, "component.scaffoldFixture");
  assert.equal(first.dictionaryFields.length, 1);
  assert.deepEqual(first.assets, []);
  assert.ok(first.validationCommands.includes("npm run check:architecture"));
  assert.ok(first.validationCommands.includes("npm run desktop:db:validate"));
});

test("Component scaffold inventory opens the database read-only", () => {
  const temporaryDirectory = mkdtempSync(path.join(os.tmpdir(), "mockups-component-scaffold-"));
  const databasePath = path.join(temporaryDirectory, "inventory.sqlite");
  try {
    const database = new Database(databasePath);
    database.exec(`
      CREATE TABLE projects (id TEXT PRIMARY KEY);
      CREATE TABLE component_classes (
        id TEXT PRIMARY KEY,
        project_id TEXT NOT NULL,
        component_type TEXT NOT NULL,
        record_class_id TEXT NOT NULL,
        name TEXT NOT NULL
      );
      CREATE TABLE editor_layouts (record_class_id TEXT PRIMARY KEY);
      INSERT INTO projects VALUES ('project_fixture');
      INSERT INTO component_classes VALUES (
        'component_project_fixture_label',
        'project_fixture',
        'label',
        'component.label',
        'Label'
      );
      INSERT INTO editor_layouts VALUES ('component.label');
    `);
    database.close();
    const before = sha256(databasePath);

    const loaded = loadComponentScaffoldInventory(repositoryRoot, databasePath);

    assert.equal(sha256(databasePath), before);
    assert.ok(loaded.projectIds.has("project_fixture"));
    assert.ok(loaded.componentTypes.has("label"));
    assert.ok(loaded.recordClassIds.has("component.label"));
    assert.ok(loaded.valueKinds.has("ComponentVariantSlot"));
  } finally {
    rmSync(temporaryDirectory, { force: true, recursive: true });
  }
});

test("Component scaffold CLI produces a dry-run without changing parity data", () => {
  const temporaryDirectory = mkdtempSync(path.join(os.tmpdir(), "mockups-component-cli-"));
  const specPath = path.join(temporaryDirectory, "component.json");
  const databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite");
  try {
    writeFileSync(specPath, JSON.stringify(validSpec()), "utf8");
    const before = sha256(databasePath);
    const result = spawnSync(
      process.execPath,
      [
        "--import",
        "tsx",
        "scripts/scaffoldComponent.ts",
        "--spec",
        specPath,
        "--database",
        databasePath,
        "--dry-run",
      ],
      {
        cwd: repositoryRoot,
        encoding: "utf8",
      },
    );

    assert.equal(result.status, 0, result.stderr);
    assert.equal(sha256(databasePath), before);
    const plan = JSON.parse(result.stdout) as { mode?: unknown; status?: unknown };
    assert.equal(plan.mode, "dry-run");
    assert.equal(plan.status, "contract-ready-for-owner-implementation");

    const apply = spawnSync(
      process.execPath,
      ["--import", "tsx", "scripts/scaffoldComponent.ts", "--apply"],
      {
        cwd: repositoryRoot,
        encoding: "utf8",
      },
    );
    assert.notEqual(apply.status, 0);
    assert.match(apply.stderr, /Unknown option '--apply'/);
  } finally {
    rmSync(temporaryDirectory, { force: true, recursive: true });
  }
});

test("Component scaffold rejects current identity collisions", () => {
  const spec = validSpec();
  spec.component.componentType = "label";
  expectInvalid(spec, "already exists in the manifest");

  const duplicateClass = validSpec();
  duplicateClass.component.componentClassId = "component_project_foqn_s2_label";
  expectInvalid(duplicateClass, "already exists");

  const duplicateRecordClass = validSpec();
  duplicateRecordClass.component.recordClassId = "component.label";
  expectInvalid(duplicateRecordClass, "Record class id 'component.label' already exists");
});

test("Component scaffold rejects incomplete ownership and unsafe targets", () => {
  const missingEmbed = validSpec();
  missingEmbed.manifest.embeds = ["missing"];
  expectInvalid(missingEmbed, "is not declared in the current manifest");

  const unsafeTest = validSpec();
  unsafeTest.owners.focusedTest = "../escape.test.ts";
  expectInvalid(unsafeTest, "Scaffold target path escapes are prohibited");

  const disguisedTest = validSpec();
  disguisedTest.owners.focusedTest = "tests/animation/../../src/disguised.test.ts";
  expectInvalid(disguisedTest, "must use its normalized repository path");

  const existingOwner = validSpec();
  existingOwner.manifest.contract = "./badgeComponentContract";
  expectInvalid(existingOwner, "already exists and will not be overwritten");

  const unsafeAsset = validSpec();
  unsafeAsset.assets = ["../outside.bin"];
  expectInvalid(unsafeAsset, "Scaffold target path escapes are prohibited");
});

test("Component scaffold rejects historical specifications before reading them", () => {
  assert.throws(
    () => resolveComponentScaffoldSpecPath(
      repositoryRoot,
      path.join(repositoryRoot, "docs", "old", "sealed.json"),
    ),
    /Historical archive scaffold specifications are prohibited/,
  );
});

test("Component scaffold requires one complete protected Default Variant and complete additional Variants", () => {
  const mismatched = validSpec();
  mismatched.defaultVariant.config = { scaffoldFixture: { size: 101 } };
  expectInvalid(mismatched, "same complete snapshot");

  const duplicatedEnvelope = validSpec();
  duplicatedEnvelope.metadata.variants = [];
  expectInvalid(duplicatedEnvelope, "must not duplicate variants");

  const withVariants = validSpec();
  withVariants.additionalVariants = [
    {
      id: "calls",
      name: "Calls",
      protected: false,
      locked: false,
      config: { scaffoldFixture: { size: 88 } },
    },
    {
      id: "chats",
      name: "Chats",
      protected: false,
      locked: false,
      config: { scaffoldFixture: { size: 96 } },
    },
  ];
  const plan = createComponentScaffoldPlan(withVariants, inventory(), repositoryRoot);
  assert.deepEqual(
    (plan.persistedDefinition.row.metadata_json.variants as Array<{ id: string }>)
      .map((variant) => variant.id),
    ["default", "calls", "chats"],
  );

  const duplicateVariant = validSpec();
  duplicateVariant.additionalVariants = [
    {
      id: "default",
      name: "Duplicate",
      protected: false,
      locked: false,
      config: { scaffoldFixture: { size: 90 } },
    },
  ];
  expectInvalid(duplicateVariant, "Variant id 'default' is duplicated");

  const malformed = JSON.parse(JSON.stringify(componentScaffoldTemplate())) as Record<string, unknown>;
  const defaultVariant = malformed.defaultVariant as Record<string, unknown>;
  defaultVariant.protected = false;
  assert.throws(
    () => parseComponentScaffoldSpec(malformed),
    /must be protected/,
  );

  const protectedAdditional = JSON.parse(JSON.stringify(validSpec())) as Record<string, unknown>;
  protectedAdditional.additionalVariants = [{
    id: "calls",
    name: "Calls",
    protected: true,
    locked: false,
    config: { scaffoldFixture: { size: 88 } },
  }];
  assert.throws(
    () => parseComponentScaffoldSpec(protectedAdditional),
    /must not be protected/,
  );
});

test("Component scaffold validates current dictionary and Runtime Input contracts", () => {
  const unknownFieldKind = validSpec();
  unknownFieldKind.dictionaryFields[0]!.valueKind = "Unknown";
  expectInvalid(unknownFieldKind, "unknown current ValueKind");

  const duplicateStorage = validSpec();
  duplicateStorage.dictionaryFields.push({
    ...structuredClone(duplicateStorage.dictionaryFields[0]!),
    id: "component.scaffoldFixture.otherSize",
  });
  expectInvalid(duplicateStorage, "JSON path 'scaffoldFixture.size' is declared more than once");

  const missingStorage = validSpec();
  missingStorage.dictionaryFields[0]!.jsonPath = ["scaffoldFixture", "missing"];
  expectInvalid(missingStorage, "is missing from current config");

  const unknownInputKind = validSpec();
  (unknownInputKind.designPreview.inputs as Array<Record<string, unknown>>)[0]!.valueKind = "Unknown";
  expectInvalid(unknownInputKind, "unknown current ValueKind");

  const missingInputShape = validSpec();
  delete (missingInputShape.designPreview.inputs as Array<Record<string, unknown>>)[0]!.unit;
  expectInvalid(missingInputShape, "requires explicit 'unit'");

  const missingCollections = validSpec();
  delete missingCollections.designPreview.collections;
  expectInvalid(missingCollections, "must declare a collections array");

  const missingActions = validSpec();
  delete missingActions.designPreview.actions;
  expectInvalid(missingActions, "must declare an actions array");

  const missingRuntimeValue = validSpec();
  delete missingRuntimeValue.designPreview.value;
  expectInvalid(missingRuntimeValue, "requires an explicit current sample value");
});

test("Component scaffold requires every dictionary field in the editor layout exactly once", () => {
  const missing = validSpec();
  const cards = missing.editorLayout.cards as Array<Record<string, unknown>>;
  const layoutCard = cards[1]!;
  const groups = layoutCard.groups as Array<Record<string, unknown>>;
  groups[0]!.fields = [];
  expectInvalid(missing, "must expose required field 'component.scaffoldFixture.size' exactly once");

  const unknown = validSpec();
  const unknownCards = unknown.editorLayout.cards as Array<Record<string, unknown>>;
  const unknownGroups = unknownCards[1]!.groups as Array<Record<string, unknown>>;
  const fields = unknownGroups[0]!.fields as Array<Record<string, unknown>>;
  fields.push({ id: "component.scaffoldFixture.undeclared", order: 20, visible: true });
  expectInvalid(unknown, "references undeclared field");
});

test("Printed Component scaffold template cannot be treated as an authored contract", () => {
  expectInvalid(cloneTemplate(), "template placeholders must be replaced");
});

test("Component scaffold parser rejects unknown top-level data", () => {
  const value = JSON.parse(JSON.stringify(componentScaffoldTemplate())) as Record<string, unknown>;
  value.fallbackDefaults = {};
  assert.throws(
    () => parseComponentScaffoldSpec(value),
    /Unknown: fallbackDefaults/,
  );
});

function sha256(filePath: string) {
  return createHash("sha256").update(readFileSync(filePath)).digest("hex");
}
