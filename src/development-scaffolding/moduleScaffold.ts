import { existsSync, readFileSync, realpathSync } from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";

import type { JsonObject, JsonValue } from "./componentScaffold.js";

export type ModuleDurationPolicy = "calculated" | "explicit";

export interface ModuleScaffoldField {
  id: string;
  label: string;
  valueKind: string;
  jsonPath: string[];
  defaultValue: string;
  isEditable: boolean;
  options: JsonObject[];
  optionsSource: string;
  pairLabels: { first: string; second: string } | null;
  number: {
    minimum: number | null;
    maximum: number | null;
    increment: number;
    decimalPlaces: number;
    useSlider: boolean;
  } | null;
  componentVariantType: string;
  runtimeInputComponentVariantFieldId: string;
  runtimeCollectionComponentVariantFieldId: string;
  componentInputBindingsSource: string;
  structuredCollectionSource: string;
  unit: string;
  embeddedSlot: {
    componentType: string;
    label: string;
    recordClassId: string;
  } | null;
}

export interface ModuleScaffoldSpec {
  schemaVersion: 1;
  intent: {
    responsibility: string;
    visualBoundary: string;
    runtimeBehavior: string;
    forwarding: string;
    temporalOwnership: string;
    productionContext: string;
  };
  module: {
    moduleId: string;
    appId: string;
    projectId: string;
    recordClassId: string;
    name: string;
    notes: string;
    sortOrder: number;
  };
  manifest: {
    label: string;
    contract: string;
    resolver: string;
    renderable: string;
    embeds: string[];
  };
  owners: {
    contractExport: string;
    resolverExport: string;
    renderableExport: string;
    focusedTest: string;
  };
  config: JsonObject;
  defaultVariant: {
    id: "default";
    name: string;
    protected: true;
    locked: true;
    config: JsonObject;
  };
  additionalVariants: Array<{
    id: string;
    name: string;
    protected: false;
    locked: boolean;
    config: JsonObject;
  }>;
  runtimeContract: {
    source: {
      componentType: string;
      variantReference: string;
      inputIds: string[];
      collectionIds: string[];
    };
    durationPolicy: ModuleDurationPolicy;
    defaultDurationFrames: number | null;
  };
  metadata: JsonObject;
  dictionaryFields: ModuleScaffoldField[];
  editorLayout: JsonObject;
  assets: string[];
}

interface ModuleInventoryRow {
  id: string;
  appId: string;
  projectId: string;
  recordClassId: string;
  name: string;
}

interface AppInventoryRow {
  id: string;
  projectId: string;
  name: string;
}

interface RuntimeSourceRow {
  componentClassId: string;
  componentType: string;
  projectId: string;
  configJson: string;
  designPreviewJson: string;
  metadataJson: string;
}

export interface ModuleScaffoldInventory {
  moduleClasses: ReadonlySet<string>;
  componentTypes: ReadonlySet<string>;
  projectIds: ReadonlySet<string>;
  recordClassIds: ReadonlySet<string>;
  apps: readonly AppInventoryRow[];
  modules: readonly ModuleInventoryRow[];
  valueKinds: ReadonlySet<string>;
  runtimeSources: readonly RuntimeSourceRow[];
}

export interface ResolvedModuleScaffoldContract {
  designPreview: JsonObject;
  metadata: JsonObject;
}

export interface ModuleScaffoldPlan {
  schemaVersion: 1;
  mode: "dry-run";
  status: "contract-ready-for-owner-implementation";
  intent: ModuleScaffoldSpec["intent"];
  module: ModuleScaffoldSpec["module"];
  creates: Array<{
    role: "contract" | "resolver" | "renderable" | "desktopConfigContract" | "focusedTest";
    path: string;
    requiredExport?: string;
  }>;
  updates: Array<{
    owner: "manifest" | "registry" | "dictionary" | "persistence";
    path: string;
    description: string;
  }>;
  manifestEntry: ModuleScaffoldSpec["manifest"];
  persistedDefinition: {
    table: "modules";
    row: {
      id: string;
      app_id: string;
      record_class_id: string;
      name: string;
      notes: string;
      sort_order: number;
      config_json: JsonObject;
      design_preview_json: JsonObject;
      metadata_json: JsonObject;
    };
  };
  editorLayout: {
    table: "editor_layouts";
    row: {
      record_class_id: string;
      layout_json: JsonObject;
    };
  };
  dictionaryFields: ModuleScaffoldField[];
  assets: Array<{ path: string; status: "existing" | "required-create" }>;
  validationCommands: string[];
}

export class ModuleScaffoldValidationError extends Error {
  public readonly violations: readonly string[];

  public constructor(violations: readonly string[]) {
    super([
      "Module scaffold contract is invalid:",
      ...violations.map((violation, index) => `${index + 1}. ${violation}`),
    ].join("\n"));
    this.name = "ModuleScaffoldValidationError";
    this.violations = violations;
  }
}

export function parseModuleScaffoldSpec(value: unknown): ModuleScaffoldSpec {
  const root = requiredObject(value, "Module scaffold");
  requireExactKeys(root, [
    "schemaVersion",
    "intent",
    "module",
    "manifest",
    "owners",
    "config",
    "defaultVariant",
    "additionalVariants",
    "runtimeContract",
    "metadata",
    "dictionaryFields",
    "editorLayout",
    "assets",
  ], "Module scaffold");
  if (requiredInteger(root.schemaVersion, "Module scaffold schemaVersion") !== 1) {
    throw new Error("Module scaffold schemaVersion must be exactly 1.");
  }

  const intent = requiredObject(root.intent, "Module scaffold intent");
  requireExactKeys(intent, [
    "responsibility",
    "visualBoundary",
    "runtimeBehavior",
    "forwarding",
    "temporalOwnership",
    "productionContext",
  ], "Module scaffold intent");

  const module = requiredObject(root.module, "Module scaffold module");
  requireExactKeys(module, [
    "moduleId",
    "appId",
    "projectId",
    "recordClassId",
    "name",
    "notes",
    "sortOrder",
  ], "Module scaffold module");

  const manifest = requiredObject(root.manifest, "Module scaffold manifest");
  requireExactKeys(
    manifest,
    ["label", "contract", "resolver", "renderable", "embeds"],
    "Module scaffold manifest",
  );
  const owners = requiredObject(root.owners, "Module scaffold owners");
  requireExactKeys(
    owners,
    ["contractExport", "resolverExport", "renderableExport", "focusedTest"],
    "Module scaffold owners",
  );

  const defaultVariant = requiredObject(root.defaultVariant, "Module scaffold defaultVariant");
  requireExactKeys(
    defaultVariant,
    ["id", "name", "protected", "locked", "config"],
    "Module scaffold defaultVariant",
  );
  if (requiredString(defaultVariant.id, "Module scaffold defaultVariant id") !== "default") {
    throw new Error("Module scaffold defaultVariant id must be exactly 'default'.");
  }
  if (!requiredBoolean(defaultVariant.protected, "Module scaffold defaultVariant protected")) {
    throw new Error("Module scaffold defaultVariant must be protected.");
  }
  if (!requiredBoolean(defaultVariant.locked, "Module scaffold defaultVariant locked")) {
    throw new Error("Module scaffold defaultVariant must be locked.");
  }

  const runtimeContract = requiredObject(
    root.runtimeContract,
    "Module scaffold runtimeContract",
  );
  requireExactKeys(
    runtimeContract,
    ["source", "durationPolicy", "defaultDurationFrames"],
    "Module scaffold runtimeContract",
  );
  const source = requiredObject(runtimeContract.source, "Module scaffold runtimeContract source");
  requireExactKeys(
    source,
    ["componentType", "variantReference", "inputIds", "collectionIds"],
    "Module scaffold runtimeContract source",
  );
  const durationPolicy = requiredString(
    runtimeContract.durationPolicy,
    "Module scaffold runtimeContract durationPolicy",
  );
  if (durationPolicy !== "calculated" && durationPolicy !== "explicit") {
    throw new Error(`Unsupported Module duration policy '${durationPolicy}'.`);
  }
  const defaultDurationFrames = optionalInteger(
    runtimeContract.defaultDurationFrames,
    "Module scaffold runtimeContract defaultDurationFrames",
  );

  return {
    schemaVersion: 1,
    intent: {
      responsibility: requiredString(intent.responsibility, "Module scaffold intent responsibility"),
      visualBoundary: requiredString(intent.visualBoundary, "Module scaffold intent visualBoundary"),
      runtimeBehavior: requiredString(intent.runtimeBehavior, "Module scaffold intent runtimeBehavior"),
      forwarding: requiredString(intent.forwarding, "Module scaffold intent forwarding"),
      temporalOwnership: requiredString(intent.temporalOwnership, "Module scaffold intent temporalOwnership"),
      productionContext: requiredString(intent.productionContext, "Module scaffold intent productionContext"),
    },
    module: {
      moduleId: requiredString(module.moduleId, "Module scaffold moduleId"),
      appId: requiredString(module.appId, "Module scaffold appId"),
      projectId: requiredString(module.projectId, "Module scaffold projectId"),
      recordClassId: requiredString(module.recordClassId, "Module scaffold recordClassId"),
      name: requiredString(module.name, "Module scaffold name"),
      notes: requiredString(module.notes, "Module scaffold notes", true),
      sortOrder: requiredInteger(module.sortOrder, "Module scaffold sortOrder"),
    },
    manifest: {
      label: requiredString(manifest.label, "Module scaffold manifest label"),
      contract: requiredString(manifest.contract, "Module scaffold manifest contract"),
      resolver: requiredString(manifest.resolver, "Module scaffold manifest resolver"),
      renderable: requiredString(manifest.renderable, "Module scaffold manifest renderable"),
      embeds: stringArray(manifest.embeds, "Module scaffold manifest embeds"),
    },
    owners: {
      contractExport: requiredString(owners.contractExport, "Module scaffold contract export"),
      resolverExport: requiredString(owners.resolverExport, "Module scaffold resolver export"),
      renderableExport: requiredString(owners.renderableExport, "Module scaffold renderable export"),
      focusedTest: requiredString(owners.focusedTest, "Module scaffold focused test"),
    },
    config: requiredJsonObject(root.config, "Module scaffold config"),
    defaultVariant: {
      id: "default",
      name: requiredString(defaultVariant.name, "Module scaffold defaultVariant name"),
      protected: true,
      locked: true,
      config: requiredJsonObject(defaultVariant.config, "Module scaffold defaultVariant config"),
    },
    additionalVariants: requiredArray(
      root.additionalVariants,
      "Module scaffold additionalVariants",
    ).map((candidate, index) => {
      const owner = `Module scaffold additionalVariants[${index}]`;
      const variant = requiredObject(candidate, owner);
      requireExactKeys(variant, ["id", "name", "protected", "locked", "config"], owner);
      if (requiredBoolean(variant.protected, `${owner} protected`)) {
        throw new Error(`${owner} must not be protected.`);
      }
      return {
        id: requiredString(variant.id, `${owner} id`),
        name: requiredString(variant.name, `${owner} name`),
        protected: false as const,
        locked: requiredBoolean(variant.locked, `${owner} locked`),
        config: requiredJsonObject(variant.config, `${owner} config`),
      };
    }),
    runtimeContract: {
      source: {
        componentType: requiredString(source.componentType, "Module runtime source componentType"),
        variantReference: requiredString(source.variantReference, "Module runtime source variantReference"),
        inputIds: stringArray(source.inputIds, "Module runtime source inputIds"),
        collectionIds: stringArray(source.collectionIds, "Module runtime source collectionIds"),
      },
      durationPolicy,
      defaultDurationFrames,
    },
    metadata: requiredJsonObject(root.metadata, "Module scaffold metadata"),
    dictionaryFields: requiredArray(
      root.dictionaryFields,
      "Module scaffold dictionaryFields",
    ).map((field, index) => parseField(field, `Module scaffold dictionaryFields[${index}]`)),
    editorLayout: requiredJsonObject(root.editorLayout, "Module scaffold editorLayout"),
    assets: stringArray(root.assets, "Module scaffold assets"),
  };
}

export function loadModuleScaffoldInventory(
  repositoryRoot: string,
  databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite"),
): ModuleScaffoldInventory {
  const manifest = requiredObject(
    JSON.parse(readFileSync(
      repositoryPath(repositoryRoot, "src/desktop-preview/desktopPreviewManifest.json"),
      "utf8",
    )) as unknown,
    "Desktop Preview manifest",
  );
  const modules = requiredObject(manifest.modules, "Desktop Preview manifest modules");
  const components = requiredObject(manifest.components, "Desktop Preview manifest components");
  const valueKinds = parseValueKinds(readFileSync(
    repositoryPath(repositoryRoot, "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs"),
    "utf8",
  ));
  const database = new Database(databasePath, { fileMustExist: true, readonly: true });
  try {
    return {
      moduleClasses: new Set(Object.keys(modules)),
      componentTypes: new Set(Object.keys(components)),
      projectIds: new Set(
        (database.prepare("SELECT id FROM projects").all() as Array<{ id: string }>)
          .map((row) => row.id),
      ),
      recordClassIds: new Set(
        (database.prepare("SELECT record_class_id AS id FROM editor_layouts").all() as Array<{ id: string }>)
          .map((row) => row.id),
      ),
      apps: database.prepare(`
        SELECT id, project_id AS projectId, name
        FROM apps
      `).all() as AppInventoryRow[],
      modules: database.prepare(`
        SELECT m.id,
               m.app_id AS appId,
               a.project_id AS projectId,
               m.record_class_id AS recordClassId,
               m.name
        FROM modules m
        JOIN apps a ON a.id = m.app_id
      `).all() as ModuleInventoryRow[],
      valueKinds,
      runtimeSources: database.prepare(`
        SELECT id AS componentClassId,
               component_type AS componentType,
               project_id AS projectId,
               config_json AS configJson,
               design_preview_json AS designPreviewJson,
               metadata_json AS metadataJson
        FROM component_classes
      `).all() as RuntimeSourceRow[],
    };
  } finally {
    database.close();
  }
}

export function resolveModuleScaffoldSpecPath(repositoryRoot: string, suppliedPath: string) {
  const resolved = path.resolve(suppliedPath);
  const archiveRoot = path.resolve(repositoryRoot, "docs", "old");
  requireOutsideArchive(resolved, archiveRoot);
  const canonical = realpathSync(resolved);
  requireOutsideArchive(canonical, archiveRoot);
  return canonical;
}

export function createModuleScaffoldPlan(
  spec: ModuleScaffoldSpec,
  inventory: ModuleScaffoldInventory,
  repositoryRoot: string,
  ownerMode: "mustBeAbsent" | "mustExist" = "mustBeAbsent",
): ModuleScaffoldPlan {
  const violations: string[] = [];
  const identityText = [...Object.values(spec.intent), spec.module.name, spec.module.notes];
  if (identityText.some((value) => /\breplace (?:with|every|this)\b/i.test(value))) {
    violations.push("Printed scaffold template placeholders must be replaced.");
  }
  validateIdentity(spec.module.moduleId, /^[a-z][a-z0-9_]*$/, "moduleId", violations);
  validateIdentity(spec.module.appId, /^[a-z][a-z0-9_]*$/, "appId", violations);
  validateIdentity(spec.module.projectId, /^[a-z][a-z0-9_]*$/, "projectId", violations);
  validateIdentity(
    spec.module.recordClassId,
    /^module\.[A-Za-z][A-Za-z0-9_.]*$/,
    "recordClassId",
    violations,
  );
  if (!spec.module.name.trim()) violations.push("Module name must not be blank.");
  if (!spec.manifest.label.trim()) violations.push("Module manifest label must not be blank.");
  if (spec.module.sortOrder < 0) violations.push("Module sortOrder must not be negative.");

  const app = inventory.apps.find((candidate) => candidate.id === spec.module.appId);
  if (!app) {
    violations.push(`App '${spec.module.appId}' does not exist.`);
  } else if (app.projectId !== spec.module.projectId) {
    violations.push(
      `App '${spec.module.appId}' belongs to Project '${app.projectId}', not '${spec.module.projectId}'.`,
    );
  }
  if (!inventory.projectIds.has(spec.module.projectId)) {
    violations.push(`Project '${spec.module.projectId}' does not exist.`);
  }
  if (inventory.moduleClasses.has(spec.module.recordClassId)) {
    violations.push(`Module class '${spec.module.recordClassId}' already exists in the manifest.`);
  }
  if (inventory.modules.some((candidate) => candidate.id === spec.module.moduleId)) {
    violations.push(`Module id '${spec.module.moduleId}' already exists.`);
  }
  if (inventory.recordClassIds.has(spec.module.recordClassId)
      || inventory.modules.some((candidate) => candidate.recordClassId === spec.module.recordClassId)) {
    violations.push(`Record class id '${spec.module.recordClassId}' already exists.`);
  }
  if (inventory.modules.some((candidate) =>
    candidate.appId === spec.module.appId && candidate.name === spec.module.name)) {
    violations.push(
      `Module name '${spec.module.name}' already exists in App '${spec.module.appId}'.`,
    );
  }

  const embeds = new Set<string>();
  for (const embed of spec.manifest.embeds) {
    if (!inventory.componentTypes.has(embed)) {
      violations.push(`Embedded Component '${embed}' is not declared in the current manifest.`);
    }
    if (embeds.has(embed)) violations.push(`Embedded Component '${embed}' is duplicated.`);
    embeds.add(embed);
  }

  const source = resolveRuntimeSource(spec, inventory, violations);
  const resolved = source
    ? resolveRuntimeContract(spec, source, violations)
    : { designPreview: {}, metadata: {} };

  if (canonicalJson(spec.config) !== canonicalJson(spec.defaultVariant.config)) {
    violations.push(
      "Current config and protected Default Variant config must be the same complete snapshot.",
    );
  }
  if (Object.hasOwn(spec.metadata, "variants")) {
    violations.push("Scaffold metadata must not duplicate variants.");
  }
  const variantIds = new Set(["default"]);
  for (const variant of spec.additionalVariants) {
    validateIdentity(variant.id, /^[a-z][A-Za-z0-9_]*$/, `Variant id '${variant.id}'`, violations);
    if (variantIds.has(variant.id)) violations.push(`Variant id '${variant.id}' is duplicated.`);
    variantIds.add(variant.id);
  }

  const ownerFiles = ownerTargets(spec).map((owner) => ({
    role: owner.role,
    path: owner.path,
    requiredExport: owner.requiredTerm,
  }));
  for (const owner of ownerFiles) {
    let resolvedPath: string | undefined;
    try {
      resolvedPath = repositoryPath(repositoryRoot, owner.path);
    } catch (error) {
      violations.push(error instanceof Error ? error.message : `Invalid owner path '${owner.path}'.`);
    }
    if (owner.role === "focusedTest") {
      if (!owner.path.startsWith("tests/animation/") || !owner.path.endsWith(".test.ts")) {
        violations.push(`Focused test '${owner.path}' must be under tests/animation.`);
      }
    } else if (owner.role === "desktopConfigContract") {
      if (!owner.path.startsWith("spikes/desktop-editor-shell/Data/")
          || !owner.path.endsWith("ModuleConfigContract.cs")) {
        violations.push(`Desktop config owner '${owner.path}' has an invalid route.`);
      }
    } else if (!owner.path.startsWith("src/desktop-preview/") || !owner.path.endsWith(".ts")) {
      violations.push(`${owner.role} owner '${owner.path}' has an invalid route.`);
    }
    if (resolvedPath && ownerMode === "mustBeAbsent" && existsSync(resolvedPath)) {
      violations.push(`Owner target '${owner.path}' already exists and will not be overwritten.`);
    }
    if (resolvedPath && ownerMode === "mustExist" && !existsSync(resolvedPath)) {
      violations.push(`Owner target '${owner.path}' must exist before integration.`);
    }
  }
  for (const [kind, route, suffix] of [
    ["contract", spec.manifest.contract, "ModuleContract"],
    ["resolver", spec.manifest.resolver, "ModuleResolver"],
    ["renderable", spec.manifest.renderable, "ModuleRenderable"],
  ] as const) {
    if (!new RegExp(`^\\./[A-Za-z][A-Za-z0-9/]*${suffix}$`).test(route)) {
      violations.push(`Manifest ${kind} route '${route}' is invalid.`);
    }
  }
  for (const [label, exportName] of [
    ["contract", spec.owners.contractExport],
    ["resolver", spec.owners.resolverExport],
    ["renderable", spec.owners.renderableExport],
  ] as const) {
    validateIdentity(exportName, /^[A-Za-z_$][A-Za-z0-9_$]*$/, `${label} export`, violations);
  }

  validateFields(spec, inventory, violations);
  validateEditorLayout(spec, violations);
  const assets = spec.assets.map((asset) => {
    let exists = false;
    try {
      exists = existsSync(repositoryPath(repositoryRoot, asset));
    } catch (error) {
      violations.push(error instanceof Error ? error.message : `Invalid asset '${asset}'.`);
    }
    return { path: asset, status: exists ? "existing" as const : "required-create" as const };
  });
  if (violations.length > 0) throw new ModuleScaffoldValidationError(violations);

  return {
    schemaVersion: 1,
    mode: "dry-run",
    status: "contract-ready-for-owner-implementation",
    intent: spec.intent,
    module: spec.module,
    creates: ownerFiles,
    updates: [
      {
        owner: "manifest",
        path: "src/desktop-preview/desktopPreviewManifest.json",
        description: `Add exact Module route '${spec.module.recordClassId}'.`,
      },
      {
        owner: "registry",
        path: "src/desktop-preview/moduleRenderableRegistry.ts",
        description: `Route '${spec.module.recordClassId}' through generated registration.`,
      },
      {
        owner: "dictionary",
        path: "spikes/desktop-editor-shell/EditorShell/GeneratedModuleScaffoldFieldCatalog.cs",
        description: `Register ${spec.dictionaryFields.length} Module dictionary fields.`,
      },
      {
        owner: "persistence",
        path: "data/desktop-editor-spike.sqlite",
        description: "Insert the current Module and editor layout in one maintenance transaction.",
      },
    ],
    manifestEntry: spec.manifest,
    persistedDefinition: {
      table: "modules",
      row: {
        id: spec.module.moduleId,
        app_id: spec.module.appId,
        record_class_id: spec.module.recordClassId,
        name: spec.module.name,
        notes: spec.module.notes,
        sort_order: spec.module.sortOrder,
        config_json: spec.config,
        design_preview_json: resolved.designPreview,
        metadata_json: resolved.metadata,
      },
    },
    editorLayout: {
      table: "editor_layouts",
      row: {
        record_class_id: spec.module.recordClassId,
        layout_json: spec.editorLayout,
      },
    },
    dictionaryFields: spec.dictionaryFields,
    assets,
    validationCommands: [
      "npm run test:scaffolding",
      "npm run scaffold:verify",
      "npm run typecheck",
      "npm run animation:test:preview",
      "npm run animation:test:desktop",
      "npm run check:architecture",
      "npm run desktop:build",
      "npm run desktop:db:validate",
      "git diff --check",
    ],
  };
}

export function moduleScaffoldTemplate(): ModuleScaffoldSpec {
  const config = {
    appearanceMode: "inherit",
    replaceMe: {
      componentSlot: {
        variantReference: "component_project_foqn_s2_list::variant::chats",
        overrides: {},
      },
    },
  } satisfies JsonObject;
  return {
    schemaVersion: 1,
    intent: {
      responsibility: "Replace with the Module's single Screen responsibility.",
      visualBoundary: "Replace with the exact Screen composition boundary.",
      runtimeBehavior: "Replace with observable Runtime behavior.",
      forwarding: "Replace with the exact child Runtime contract forwarding.",
      temporalOwnership: "Replace with Module and child temporal ownership.",
      productionContext: "Use exact Screen → Shot → Actor → Theme/Device context.",
    },
    module: {
      moduleId: "module_project_foqn_s2_replace_me",
      appId: "app_core_chat",
      projectId: "project_foqn_s2",
      recordClassId: "module.core.replaceMe",
      name: "Replace Me",
      notes: "Replace every example identity before implementation.",
      sortOrder: 1,
    },
    manifest: {
      label: "Replace Me",
      contract: "./replaceMeModuleContract",
      resolver: "./replaceMeModuleResolver",
      renderable: "./replaceMeModuleRenderable",
      embeds: ["list"],
    },
    owners: {
      contractExport: "ReplaceMeModuleContract",
      resolverExport: "resolveReplaceMeModule",
      renderableExport: "replaceMeModuleToRenderable",
      focusedTest: "tests/animation/replaceMeModule.test.ts",
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
    metadata: { note: "Replace with the current Module note." },
    dictionaryFields: [],
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
                { id: "module.recordClassId", order: 20, visible: false },
                { id: "module.sortOrder", order: 30, visible: true },
                { id: "module.appearanceMode", order: 40, visible: true },
                { id: "core.notes", order: 50, visible: true },
              ],
            },
          ],
        },
      ],
    },
    assets: [],
  };
}

export function resolveModuleScaffoldContract(
  spec: ModuleScaffoldSpec,
  inventory: ModuleScaffoldInventory,
): ResolvedModuleScaffoldContract {
  const violations: string[] = [];
  const source = resolveRuntimeSource(spec, inventory, violations);
  const resolved = source
    ? resolveRuntimeContract(spec, source, violations)
    : { designPreview: {}, metadata: {} };
  if (violations.length > 0) throw new ModuleScaffoldValidationError(violations);
  return resolved;
}

export function moduleOwnerTargets(spec: ModuleScaffoldSpec) {
  const typeName = pascalCase(spec.module.recordClassId.split(".").at(-1) ?? "");
  return [
    {
      role: "contract" as const,
      label: "contract",
      path: manifestOwnerPath(spec.manifest.contract),
      requiredTerm: spec.owners.contractExport,
    },
    {
      role: "resolver" as const,
      label: "resolver",
      path: manifestOwnerPath(spec.manifest.resolver),
      requiredTerm: spec.owners.resolverExport,
    },
    {
      role: "renderable" as const,
      label: "renderable",
      path: manifestOwnerPath(spec.manifest.renderable),
      requiredTerm: spec.owners.renderableExport,
    },
    {
      role: "desktopConfigContract" as const,
      label: "desktop config contract",
      path: `spikes/desktop-editor-shell/Data/${typeName}ModuleConfigContract.cs`,
      requiredTerm: `${typeName}ModuleConfigContract`,
    },
    {
      role: "focusedTest" as const,
      label: "focused test",
      path: spec.owners.focusedTest,
      requiredTerm: spec.owners.resolverExport,
    },
  ];
}

function resolveRuntimeSource(
  spec: ModuleScaffoldSpec,
  inventory: ModuleScaffoldInventory,
  violations: string[],
) {
  const sourceSpec = spec.runtimeContract.source;
  if (!inventory.componentTypes.has(sourceSpec.componentType)) {
    violations.push(
      `Runtime source Component '${sourceSpec.componentType}' is not in the manifest.`,
    );
  }
  if (!spec.manifest.embeds.includes(sourceSpec.componentType)) {
    violations.push(
      `Runtime source Component '${sourceSpec.componentType}' must be a declared Module embed.`,
    );
  }
  const matches = inventory.runtimeSources.filter((candidate) =>
    candidate.projectId === spec.module.projectId
    && candidate.componentType === sourceSpec.componentType);
  if (matches.length !== 1) {
    violations.push(
      `Runtime source '${sourceSpec.componentType}' requires exactly one same-Project Component Class; found ${matches.length}.`,
    );
    return undefined;
  }
  const [classId, marker, variantId] = sourceSpec.variantReference.split("::");
  if (marker !== "variant" || !classId || !variantId || classId !== matches[0]!.componentClassId) {
    violations.push(
      `Runtime source Variant '${sourceSpec.variantReference}' is not a full reference owned by '${matches[0]!.componentClassId}'.`,
    );
    return undefined;
  }
  const metadata = requiredJsonObject(
    JSON.parse(matches[0]!.metadataJson) as unknown,
    `Runtime source '${sourceSpec.componentType}' metadata`,
  );
  const variants = Array.isArray(metadata.variants) ? metadata.variants : [];
  if (!variants.some((candidate) =>
    isPlainObject(candidate) && candidate.id === variantId && isPlainObject(candidate.config))) {
    violations.push(`Runtime source Variant '${sourceSpec.variantReference}' does not exist.`);
  }
  return matches[0];
}

function resolveRuntimeContract(
  spec: ModuleScaffoldSpec,
  source: RuntimeSourceRow,
  violations: string[],
): ResolvedModuleScaffoldContract {
  const preview = requiredJsonObject(
    JSON.parse(source.designPreviewJson) as unknown,
    `Runtime source '${source.componentType}' Design Preview`,
  );
  const inputs = objectArray(preview.inputs, "Runtime source inputs");
  const collections = objectArray(preview.collections, "Runtime source collections");
  const inputIds = inputs.map((input) => requiredString(input.id, "Runtime source input id"));
  const collectionIds = collections.map((collection) =>
    requiredString(collection.id, "Runtime source collection id"));
  if (canonicalJson(inputIds) !== canonicalJson(spec.runtimeContract.source.inputIds)) {
    violations.push(
      `Runtime source input ids differ: expected [${spec.runtimeContract.source.inputIds.join(", ")}], found [${inputIds.join(", ")}].`,
    );
  }
  if (canonicalJson(collectionIds) !== canonicalJson(spec.runtimeContract.source.collectionIds)) {
    violations.push(
      `Runtime source collection ids differ: expected [${spec.runtimeContract.source.collectionIds.join(", ")}], found [${collectionIds.join(", ")}].`,
    );
  }
  const designPreview = structuredClone(preview);
  designPreview.componentType = spec.module.recordClassId;
  designPreview.animationTimeline = spec.runtimeContract.durationPolicy === "explicit"
    ? {
      durationPolicy: "explicit",
      defaultDurationFrames: spec.runtimeContract.defaultDurationFrames!,
    }
    : { durationPolicy: "calculated" };
  if (spec.runtimeContract.durationPolicy === "explicit") {
    if (!spec.runtimeContract.defaultDurationFrames
        || spec.runtimeContract.defaultDurationFrames <= 0) {
      violations.push("Explicit Module duration requires positive defaultDurationFrames.");
    }
  } else if (spec.runtimeContract.defaultDurationFrames !== null) {
    violations.push("Calculated Module duration must use null defaultDurationFrames.");
  }
  return {
    designPreview,
    metadata: {
      ...spec.metadata,
      variants: [spec.defaultVariant, ...spec.additionalVariants],
    },
  };
}

function validateFields(
  spec: ModuleScaffoldSpec,
  inventory: ModuleScaffoldInventory,
  violations: string[],
) {
  const ids = new Set<string>();
  for (const field of spec.dictionaryFields) {
    if (!field.id.startsWith(`${spec.module.recordClassId}.`)) {
      violations.push(
        `Dictionary field '${field.id}' must be namespaced by '${spec.module.recordClassId}.'.`,
      );
    }
    if (ids.has(field.id)) violations.push(`Dictionary field '${field.id}' is duplicated.`);
    ids.add(field.id);
    if (!inventory.valueKinds.has(field.valueKind)) {
      violations.push(`Dictionary field '${field.id}' uses unknown ValueKind '${field.valueKind}'.`);
    }
    if (field.jsonPath.length === 0) {
      violations.push(`Dictionary field '${field.id}' requires a config JSON path.`);
    } else if (!jsonPathExists(spec.config, field.jsonPath)) {
      violations.push(
        `Dictionary field '${field.id}' path '${field.jsonPath.join(".")}' is absent from current config.`,
      );
    }
    if (field.componentVariantType
        && !spec.manifest.embeds.includes(field.componentVariantType)) {
      violations.push(
        `Dictionary field '${field.id}' Component type '${field.componentVariantType}' is not a Module embed.`,
      );
    }
    if (field.embeddedSlot) {
      if (!field.componentVariantType
          || field.componentVariantType !== field.embeddedSlot.componentType) {
        violations.push(
          `Embedded slot field '${field.id}' must use the same componentVariantType.`,
        );
      }
      if (!field.embeddedSlot.recordClassId.startsWith("component.")) {
        violations.push(`Embedded slot field '${field.id}' has an invalid recordClassId.`);
      }
    }
  }
}

function validateEditorLayout(spec: ModuleScaffoldSpec, violations: string[]) {
  const cards = objectArray(spec.editorLayout.cards, "Module editor layout cards");
  const referenced = new Set<string>();
  for (const card of cards) {
    for (const group of objectArray(card.groups, "Module editor layout groups")) {
      for (const field of objectArray(group.fields, "Module editor layout fields")) {
        const id = requiredString(field.id, "Module editor layout field id");
        if (referenced.has(id)) {
          violations.push(`Editor layout field '${id}' is duplicated.`);
        }
        referenced.add(id);
      }
    }
  }
  for (const field of spec.dictionaryFields) {
    if (!referenced.has(field.id)) {
      violations.push(`Dictionary field '${field.id}' is missing from editor layout.`);
    }
  }
}

function parseField(value: unknown, owner: string): ModuleScaffoldField {
  const field = requiredObject(value, owner);
  requireExactKeys(field, [
    "id",
    "label",
    "valueKind",
    "jsonPath",
    "defaultValue",
    "isEditable",
    "options",
    "optionsSource",
    "pairLabels",
    "number",
    "componentVariantType",
    "runtimeInputComponentVariantFieldId",
    "runtimeCollectionComponentVariantFieldId",
    "componentInputBindingsSource",
    "structuredCollectionSource",
    "unit",
    "embeddedSlot",
  ], owner);
  const pair = field.pairLabels === null
    ? null
    : requiredObject(field.pairLabels, `${owner} pairLabels`);
  const number = field.number === null
    ? null
    : requiredObject(field.number, `${owner} number`);
  const embedded = field.embeddedSlot === null
    ? null
    : requiredObject(field.embeddedSlot, `${owner} embeddedSlot`);
  if (pair) requireExactKeys(pair, ["first", "second"], `${owner} pairLabels`);
  if (number) {
    requireExactKeys(
      number,
      ["minimum", "maximum", "increment", "decimalPlaces", "useSlider"],
      `${owner} number`,
    );
  }
  if (embedded) {
    requireExactKeys(
      embedded,
      ["componentType", "label", "recordClassId"],
      `${owner} embeddedSlot`,
    );
  }
  return {
    id: requiredString(field.id, `${owner} id`),
    label: requiredString(field.label, `${owner} label`),
    valueKind: requiredString(field.valueKind, `${owner} valueKind`),
    jsonPath: stringArray(field.jsonPath, `${owner} jsonPath`),
    defaultValue: requiredString(field.defaultValue, `${owner} defaultValue`, true),
    isEditable: requiredBoolean(field.isEditable, `${owner} isEditable`),
    options: objectArray(field.options, `${owner} options`) as JsonObject[],
    optionsSource: requiredString(field.optionsSource, `${owner} optionsSource`, true),
    pairLabels: pair
      ? {
        first: requiredString(pair.first, `${owner} pairLabels first`),
        second: requiredString(pair.second, `${owner} pairLabels second`),
      }
      : null,
    number: number
      ? {
        minimum: optionalNumber(number.minimum, `${owner} number minimum`),
        maximum: optionalNumber(number.maximum, `${owner} number maximum`),
        increment: requiredNumber(number.increment, `${owner} number increment`),
        decimalPlaces: requiredInteger(number.decimalPlaces, `${owner} number decimalPlaces`),
        useSlider: requiredBoolean(number.useSlider, `${owner} number useSlider`),
      }
      : null,
    componentVariantType: requiredString(
      field.componentVariantType,
      `${owner} componentVariantType`,
      true,
    ),
    runtimeInputComponentVariantFieldId: requiredString(
      field.runtimeInputComponentVariantFieldId,
      `${owner} runtimeInputComponentVariantFieldId`,
      true,
    ),
    runtimeCollectionComponentVariantFieldId: requiredString(
      field.runtimeCollectionComponentVariantFieldId,
      `${owner} runtimeCollectionComponentVariantFieldId`,
      true,
    ),
    componentInputBindingsSource: requiredString(
      field.componentInputBindingsSource,
      `${owner} componentInputBindingsSource`,
      true,
    ),
    structuredCollectionSource: requiredString(
      field.structuredCollectionSource,
      `${owner} structuredCollectionSource`,
      true,
    ),
    unit: requiredString(field.unit, `${owner} unit`, true),
    embeddedSlot: embedded
      ? {
        componentType: requiredString(embedded.componentType, `${owner} embeddedSlot componentType`),
        label: requiredString(embedded.label, `${owner} embeddedSlot label`),
        recordClassId: requiredString(embedded.recordClassId, `${owner} embeddedSlot recordClassId`),
      }
      : null,
  };
}

function ownerTargets(spec: ModuleScaffoldSpec) {
  return moduleOwnerTargets(spec);
}

function manifestOwnerPath(route: string) {
  return `src/desktop-preview/${route.slice(2)}.ts`;
}

function repositoryPath(repositoryRoot: string, relativePath: string) {
  if (path.isAbsolute(relativePath) || path.win32.isAbsolute(relativePath)) {
    throw new Error(`Absolute scaffold paths are prohibited: ${relativePath}`);
  }
  const normalized = relativePath.replaceAll("\\", "/");
  if (normalized !== path.posix.normalize(normalized)
      || normalized === ".."
      || normalized.startsWith("../")) {
    throw new Error(`Scaffold path escapes are prohibited: ${relativePath}`);
  }
  if (normalized === "docs/old" || normalized.startsWith("docs/old/")) {
    throw new Error("Historical archive scaffold paths are prohibited.");
  }
  const resolved = path.resolve(repositoryRoot, normalized);
  const root = path.resolve(repositoryRoot);
  if (resolved !== root && !resolved.startsWith(`${root}${path.sep}`)) {
    throw new Error(`Scaffold path escapes are prohibited: ${relativePath}`);
  }
  return resolved;
}

function requireOutsideArchive(candidate: string, archive: string) {
  if (candidate !== archive && !candidate.startsWith(`${archive}${path.sep}`)) return;
  throw new Error("Historical archive scaffold specifications are prohibited.");
}

function parseValueKinds(source: string) {
  const match = /internal enum ValueKind\s*\{([\s\S]*?)\}/.exec(source);
  if (!match) throw new Error("Unable to read current ValueKind declarations.");
  return new Set(
    match[1]!.split(",").map((entry) => entry.trim()).filter(Boolean),
  );
}

function validateIdentity(
  value: string,
  pattern: RegExp,
  label: string,
  violations: string[],
) {
  if (!pattern.test(value)) violations.push(`${label} '${value}' has an invalid stable identity.`);
}

function jsonPathExists(root: JsonObject, segments: readonly string[]) {
  let current: JsonValue = root;
  for (const segment of segments) {
    if (!isPlainObject(current) || !Object.hasOwn(current, segment)) return false;
    current = current[segment]!;
  }
  return true;
}

function canonicalJson(value: unknown): string {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(",")}]`;
  const record = value as Record<string, unknown>;
  return `{${Object.keys(record).sort().map((key) =>
    `${JSON.stringify(key)}:${canonicalJson(record[key])}`).join(",")}}`;
}

function pascalCase(value: string) {
  return value.length === 0 ? value : `${value[0]!.toUpperCase()}${value.slice(1)}`;
}

function requiredObject(value: unknown, owner: string): Record<string, unknown> {
  if (!isPlainObject(value)) throw new Error(`${owner} must be an object.`);
  return value;
}

function requiredJsonObject(value: unknown, owner: string): JsonObject {
  return requiredObject(value, owner) as JsonObject;
}

function requiredArray(value: unknown, owner: string): unknown[] {
  if (!Array.isArray(value)) throw new Error(`${owner} must be an array.`);
  return value;
}

function objectArray(value: unknown, owner: string) {
  return requiredArray(value, owner).map((item, index) =>
    requiredObject(item, `${owner}[${index}]`));
}

function stringArray(value: unknown, owner: string) {
  return requiredArray(value, owner).map((item, index) =>
    requiredString(item, `${owner}[${index}]`));
}

function requiredString(value: unknown, owner: string, allowEmpty = false) {
  if (typeof value !== "string" || (!allowEmpty && !value.trim())) {
    throw new Error(`${owner} must be ${allowEmpty ? "a string" : "a non-empty string"}.`);
  }
  return value;
}

function requiredBoolean(value: unknown, owner: string) {
  if (typeof value !== "boolean") throw new Error(`${owner} must be a boolean.`);
  return value;
}

function requiredNumber(value: unknown, owner: string) {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new Error(`${owner} must be a finite number.`);
  }
  return value;
}

function optionalNumber(value: unknown, owner: string) {
  return value === null ? null : requiredNumber(value, owner);
}

function requiredInteger(value: unknown, owner: string) {
  const result = requiredNumber(value, owner);
  if (!Number.isInteger(result)) throw new Error(`${owner} must be an integer.`);
  return result;
}

function optionalInteger(value: unknown, owner: string) {
  return value === null ? null : requiredInteger(value, owner);
}

function requireExactKeys(
  value: Record<string, unknown>,
  expected: readonly string[],
  owner: string,
) {
  const actual = Object.keys(value).sort();
  const required = [...expected].sort();
  if (canonicalJson(actual) !== canonicalJson(required)) {
    throw new Error(`${owner} must contain exactly: ${expected.join(", ")}.`);
  }
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
