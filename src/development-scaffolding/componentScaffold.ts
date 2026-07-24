import { existsSync, readFileSync, realpathSync } from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";

export type ComponentScaffoldCategory = "atom" | "component" | "system";
export type ComponentRegistryMode =
  | "simple"
  | "assignedBox"
  | "children"
  | "assignedBoxAndChildren";

export type JsonValue =
  | null
  | boolean
  | number
  | string
  | JsonValue[]
  | { [key: string]: JsonValue };

export type JsonObject = { [key: string]: JsonValue };

export interface ComponentScaffoldField {
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
  componentInputBindings: JsonObject[] | null;
  structuredCollection: JsonObject | null;
  componentVariantType: string;
  runtimeInputComponentVariantFieldId: string;
  unit: string;
}

export interface ComponentScaffoldSpec {
  schemaVersion: 1;
  intent: {
    responsibility: string;
    visualBoundary: string;
    runtimeBehavior: string;
    forwarding: string;
    temporalOwnership: string;
  };
  component: {
    componentType: string;
    category: ComponentScaffoldCategory;
    componentClassId: string;
    projectId: string;
    recordClassId: string;
    name: string;
    notes: string;
  };
  manifest: {
    contract: string;
    resolver: string;
    renderable: string;
    embeds: string[];
  };
  owners: {
    contractExport: string;
    resolverExport: string;
    renderableExport: string;
    registryMode: ComponentRegistryMode;
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
  designPreview: JsonObject;
  metadata: JsonObject;
  dictionaryFields: ComponentScaffoldField[];
  editorLayout: JsonObject;
  assets: string[];
}

interface ComponentInventoryRow {
  id: string;
  projectId: string;
  componentType: string;
  recordClassId: string;
  name: string;
}

export interface ComponentScaffoldInventory {
  componentTypes: ReadonlySet<string>;
  projectIds: ReadonlySet<string>;
  recordClassIds: ReadonlySet<string>;
  componentClasses: readonly ComponentInventoryRow[];
  valueKinds: ReadonlySet<string>;
}

export interface ComponentScaffoldPlan {
  schemaVersion: 1;
  mode: "dry-run";
  status: "contract-ready-for-owner-implementation";
  intent: ComponentScaffoldSpec["intent"];
  component: ComponentScaffoldSpec["component"];
  creates: Array<{
    role:
      | "contract"
      | "resolver"
      | "renderable"
      | "desktopConfigContract"
      | "focusedTest";
    path: string;
    requiredExport?: string;
  }>;
  updates: Array<{
    owner: "manifest" | "registry" | "dictionary" | "persistence";
    path: string;
    description: string;
  }>;
  manifestEntry: ComponentScaffoldSpec["manifest"] & {
    category: ComponentScaffoldCategory;
  };
  registryRoute: {
    componentType: string;
    resolverExport: string;
    renderableExport: string;
    mode: ComponentRegistryMode;
  };
  persistedDefinition: {
    table: "component_classes";
    row: {
      id: string;
      project_id: string;
      component_type: string;
      record_class_id: string;
      name: string;
      notes: string;
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
  dictionaryFields: ComponentScaffoldField[];
  assets: Array<{
    path: string;
    status: "existing" | "required-create";
  }>;
  validationCommands: string[];
}

export class ComponentScaffoldValidationError extends Error {
  public readonly violations: readonly string[];

  public constructor(violations: readonly string[]) {
    super([
      "Component scaffold contract is invalid:",
      ...violations.map((violation, index) => `${index + 1}. ${violation}`),
    ].join("\n"));
    this.name = "ComponentScaffoldValidationError";
    this.violations = violations;
  }
}

const componentCategories = new Set<ComponentScaffoldCategory>([
  "atom",
  "component",
  "system",
]);
const registryModes = new Set<ComponentRegistryMode>([
  "simple",
  "assignedBox",
  "children",
  "assignedBoxAndChildren",
]);
const identityFieldIds = [
  "core.name",
  "component.type",
  "core.notes",
] as const;
const runtimeInputBaseKeys = [
  "id",
  "label",
  "jsonKey",
  "kind",
  "valueKind",
  "defaultValue",
  "minimum",
  "maximum",
  "increment",
  "tableId",
  "resolvedJsonKey",
  "componentType",
  "options",
  "pairFirstLabel",
  "pairSecondLabel",
  "source",
  "uiOrigin",
  "uiGroupId",
  "uiGroupLabel",
  "uiParentGroupId",
  "uiOrder",
  "visibleWhenPath",
  "visibleWhenValue",
  "transition",
  "unit",
] as const;

export function parseComponentScaffoldSpec(value: unknown): ComponentScaffoldSpec {
  const root = requiredObject(value, "Component scaffold");
  requireExactKeys(root, [
    "schemaVersion",
    "intent",
    "component",
    "manifest",
    "owners",
    "config",
    "defaultVariant",
    "additionalVariants",
    "designPreview",
    "metadata",
    "dictionaryFields",
    "editorLayout",
    "assets",
  ], "Component scaffold");

  if (requiredInteger(root.schemaVersion, "Component scaffold schemaVersion") !== 1) {
    throw new Error("Component scaffold schemaVersion must be exactly 1.");
  }

  const intent = requiredObject(root.intent, "Component scaffold intent");
  requireExactKeys(intent, [
    "responsibility",
    "visualBoundary",
    "runtimeBehavior",
    "forwarding",
    "temporalOwnership",
  ], "Component scaffold intent");

  const component = requiredObject(root.component, "Component scaffold component");
  requireExactKeys(component, [
    "componentType",
    "category",
    "componentClassId",
    "projectId",
    "recordClassId",
    "name",
    "notes",
  ], "Component scaffold component");
  const category = requiredString(component.category, "Component scaffold component category");
  if (!componentCategories.has(category as ComponentScaffoldCategory)) {
    throw new Error(`Component scaffold category '${category}' is not supported.`);
  }

  const manifest = requiredObject(root.manifest, "Component scaffold manifest");
  requireExactKeys(
    manifest,
    ["contract", "resolver", "renderable", "embeds"],
    "Component scaffold manifest",
  );

  const owners = requiredObject(root.owners, "Component scaffold owners");
  requireExactKeys(owners, [
    "contractExport",
    "resolverExport",
    "renderableExport",
    "registryMode",
    "focusedTest",
  ], "Component scaffold owners");
  const registryMode = requiredString(
    owners.registryMode,
    "Component scaffold owners registryMode",
  );
  if (!registryModes.has(registryMode as ComponentRegistryMode)) {
    throw new Error(`Component registry mode '${registryMode}' is not supported.`);
  }

  const defaultVariant = requiredObject(
    root.defaultVariant,
    "Component scaffold defaultVariant",
  );
  requireExactKeys(defaultVariant, [
    "id",
    "name",
    "protected",
    "locked",
    "config",
  ], "Component scaffold defaultVariant");
  const defaultVariantId = requiredString(
    defaultVariant.id,
    "Component scaffold defaultVariant id",
  );
  if (defaultVariantId !== "default") {
    throw new Error("Component scaffold defaultVariant id must be exactly 'default'.");
  }
  if (requiredBoolean(
    defaultVariant.protected,
    "Component scaffold defaultVariant protected",
  ) !== true) {
    throw new Error("Component scaffold defaultVariant must be protected.");
  }
  if (requiredBoolean(
    defaultVariant.locked,
    "Component scaffold defaultVariant locked",
  ) !== true) {
    throw new Error("Component scaffold defaultVariant must be locked.");
  }
  const additionalVariants = requiredArray(
    root.additionalVariants,
    "Component scaffold additionalVariants",
  ).map((value, index) => {
    const owner = `Component scaffold additionalVariants[${index}]`;
    const variant = requiredObject(value, owner);
    requireExactKeys(
      variant,
      ["id", "name", "protected", "locked", "config"],
      owner,
    );
    if (requiredBoolean(variant.protected, `${owner} protected`) !== false) {
      throw new Error(`${owner} must not be protected.`);
    }
    return {
      id: requiredString(variant.id, `${owner} id`),
      name: requiredString(variant.name, `${owner} name`),
      protected: false as const,
      locked: requiredBoolean(variant.locked, `${owner} locked`),
      config: requiredJsonObject(variant.config, `${owner} config`),
    };
  });

  const fields = requiredArray(
    root.dictionaryFields,
    "Component scaffold dictionaryFields",
  ).map((field, index) => parseDictionaryField(
    field,
    `Component scaffold dictionaryFields[${index}]`,
  ));

  return {
    schemaVersion: 1,
    intent: {
      responsibility: requiredString(
        intent.responsibility,
        "Component scaffold intent responsibility",
      ),
      visualBoundary: requiredString(
        intent.visualBoundary,
        "Component scaffold intent visualBoundary",
      ),
      runtimeBehavior: requiredString(
        intent.runtimeBehavior,
        "Component scaffold intent runtimeBehavior",
      ),
      forwarding: requiredString(
        intent.forwarding,
        "Component scaffold intent forwarding",
      ),
      temporalOwnership: requiredString(
        intent.temporalOwnership,
        "Component scaffold intent temporalOwnership",
      ),
    },
    component: {
      componentType: requiredString(
        component.componentType,
        "Component scaffold componentType",
      ),
      category: category as ComponentScaffoldCategory,
      componentClassId: requiredString(
        component.componentClassId,
        "Component scaffold componentClassId",
      ),
      projectId: requiredString(component.projectId, "Component scaffold projectId"),
      recordClassId: requiredString(
        component.recordClassId,
        "Component scaffold recordClassId",
      ),
      name: requiredString(component.name, "Component scaffold name"),
      notes: requiredString(component.notes, "Component scaffold notes", true),
    },
    manifest: {
      contract: requiredString(manifest.contract, "Component scaffold manifest contract"),
      resolver: requiredString(manifest.resolver, "Component scaffold manifest resolver"),
      renderable: requiredString(
        manifest.renderable,
        "Component scaffold manifest renderable",
      ),
      embeds: requiredArray(
        manifest.embeds,
        "Component scaffold manifest embeds",
      ).map((embed, index) => requiredString(
        embed,
        `Component scaffold manifest embeds[${index}]`,
      )),
    },
    owners: {
      contractExport: requiredString(
        owners.contractExport,
        "Component scaffold owners contractExport",
      ),
      resolverExport: requiredString(
        owners.resolverExport,
        "Component scaffold owners resolverExport",
      ),
      renderableExport: requiredString(
        owners.renderableExport,
        "Component scaffold owners renderableExport",
      ),
      registryMode: registryMode as ComponentRegistryMode,
      focusedTest: requiredString(
        owners.focusedTest,
        "Component scaffold owners focusedTest",
      ),
    },
    config: requiredJsonObject(root.config, "Component scaffold config"),
    defaultVariant: {
      id: "default",
      name: requiredString(
        defaultVariant.name,
        "Component scaffold defaultVariant name",
      ),
      protected: true,
      locked: true,
      config: requiredJsonObject(
        defaultVariant.config,
        "Component scaffold defaultVariant config",
      ),
    },
    additionalVariants,
    designPreview: requiredJsonObject(
      root.designPreview,
      "Component scaffold designPreview",
    ),
    metadata: requiredJsonObject(root.metadata, "Component scaffold metadata"),
    dictionaryFields: fields,
    editorLayout: requiredJsonObject(
      root.editorLayout,
      "Component scaffold editorLayout",
    ),
    assets: requiredArray(root.assets, "Component scaffold assets")
      .map((asset, index) => requiredString(asset, `Component scaffold assets[${index}]`)),
  };
}

export function loadComponentScaffoldInventory(
  repositoryRoot: string,
  databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite"),
): ComponentScaffoldInventory {
  const manifestPath = resolveRepositoryPath(
    repositoryRoot,
    "src/desktop-preview/desktopPreviewManifest.json",
  );
  const manifest = requiredObject(
    JSON.parse(readFileSync(manifestPath, "utf8")) as unknown,
    "Desktop Preview manifest",
  );
  const components = requiredObject(
    manifest.components,
    "Desktop Preview manifest components",
  );

  const valueKindPath = resolveRepositoryPath(
    repositoryRoot,
    "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs",
  );
  const valueKinds = parseValueKinds(readFileSync(valueKindPath, "utf8"));

  const database = new Database(databasePath, {
    fileMustExist: true,
    readonly: true,
  });
  try {
    const projectIds = new Set(
      (database.prepare("SELECT id FROM projects").all() as Array<{ id: string }>)
        .map((row) => row.id),
    );
    const componentClasses = database.prepare(`
      SELECT id,
             project_id AS projectId,
             component_type AS componentType,
             record_class_id AS recordClassId,
             name
      FROM component_classes
    `).all() as ComponentInventoryRow[];
    const recordClassIds = new Set(
      (database.prepare("SELECT record_class_id AS id FROM editor_layouts").all() as Array<{ id: string }>)
        .map((row) => row.id),
    );
    return {
      componentTypes: new Set(Object.keys(components)),
      projectIds,
      recordClassIds,
      componentClasses,
      valueKinds,
    };
  } finally {
    database.close();
  }
}

export function resolveComponentScaffoldSpecPath(
  repositoryRoot: string,
  suppliedPath: string,
) {
  const resolved = path.resolve(suppliedPath);
  const archiveRoot = path.resolve(repositoryRoot, "docs", "old");
  requireOutsideHistoricalArchive(resolved, archiveRoot);
  const canonical = realpathSync(resolved);
  requireOutsideHistoricalArchive(canonical, archiveRoot);
  return canonical;

  function requireOutsideHistoricalArchive(candidate: string, archive: string) {
    if (candidate !== archive && !candidate.startsWith(`${archive}${path.sep}`)) return;
    throw new Error("Historical archive scaffold specifications are prohibited.");
  }
}

export function createComponentScaffoldPlan(
  spec: ComponentScaffoldSpec,
  inventory: ComponentScaffoldInventory,
  repositoryRoot: string,
): ComponentScaffoldPlan {
  const violations: string[] = [];
  const {
    componentType,
    componentClassId,
    projectId,
    recordClassId,
    name,
  } = spec.component;

  const authoringText = [
    ...Object.values(spec.intent),
    spec.component.name,
    spec.component.notes,
  ];
  if (componentType === "replaceMe"
      || authoringText.some((value) => /\breplace (?:with|every|this)\b/i.test(value))) {
    violations.push(
      "Printed scaffold template placeholders must be replaced by an explicitly authored Component contract.",
    );
  }

  validateIdentity(componentType, /^[a-z][A-Za-z0-9_]*$/, "componentType", violations);
  validateIdentity(componentClassId, /^[a-z][a-z0-9_]*$/, "componentClassId", violations);
  validateIdentity(projectId, /^[a-z][a-z0-9_]*$/, "projectId", violations);
  validateIdentity(
    recordClassId,
    /^component\.[A-Za-z][A-Za-z0-9_]*$/,
    "recordClassId",
    violations,
  );
  if (!name.trim()) violations.push("Component name must not be blank.");

  if (!inventory.projectIds.has(projectId)) {
    violations.push(`Project '${projectId}' does not exist in the current database.`);
  }
  if (inventory.componentTypes.has(componentType)) {
    violations.push(`Component type '${componentType}' already exists in the manifest.`);
  }
  if (inventory.componentClasses.some((row) => row.id === componentClassId)) {
    violations.push(`Component class id '${componentClassId}' already exists.`);
  }
  if (inventory.recordClassIds.has(recordClassId)
      || inventory.componentClasses.some((row) => row.recordClassId === recordClassId)) {
    violations.push(`Record class id '${recordClassId}' already exists.`);
  }
  if (inventory.componentClasses.some((row) =>
    row.projectId === projectId
    && row.componentType === componentType
    && row.name === name)) {
    violations.push(
      `Component name '${name}' already exists for type '${componentType}' in Project '${projectId}'.`,
    );
  }

  const embeds = new Set<string>();
  for (const embed of spec.manifest.embeds) {
    if (embed === componentType) {
      violations.push(`Component '${componentType}' cannot embed itself.`);
    } else if (!inventory.componentTypes.has(embed)) {
      violations.push(`Embedded Component '${embed}' is not declared in the current manifest.`);
    }
    if (embeds.has(embed)) {
      violations.push(`Embedded Component '${embed}' is declared more than once.`);
    }
    embeds.add(embed);
  }

  const ownerFiles = [
    ownerFile("contract", spec.manifest.contract, spec.owners.contractExport),
    ownerFile("resolver", spec.manifest.resolver, spec.owners.resolverExport),
    ownerFile("renderable", spec.manifest.renderable, spec.owners.renderableExport),
    {
      role: "desktopConfigContract" as const,
      path: `spikes/desktop-editor-shell/Data/${pascalCase(componentType)}ComponentConfigContract.cs`,
      requiredExport: `${pascalCase(componentType)}ComponentConfigContract`,
    },
    {
      role: "focusedTest" as const,
      path: spec.owners.focusedTest,
    },
  ];
  const ownerPaths = new Set<string>();
  for (const owner of ownerFiles) {
    let resolved: string | undefined;
    let normalized = owner.path;
    try {
      normalized = normalizeRepositoryPath(owner.path);
      if (normalized !== owner.path) {
        violations.push(`Owner target '${owner.path}' must use its normalized repository path.`);
      }
      resolved = resolveRepositoryPath(repositoryRoot, owner.path);
    } catch (error) {
      violations.push(error instanceof Error ? error.message : `Invalid owner path '${owner.path}'.`);
    }
    if (owner.role === "focusedTest") {
      if (!normalized.startsWith("tests/animation/") || !normalized.endsWith(".test.ts")) {
        violations.push(
          `Focused test '${owner.path}' must be an executable tests/animation/*.test.ts owner.`,
        );
      }
    } else if (owner.role === "desktopConfigContract") {
      if (!normalized.startsWith("spikes/desktop-editor-shell/Data/")
          || !normalized.endsWith("ComponentConfigContract.cs")) {
        violations.push(
          `desktop config owner '${owner.path}' must be a ComponentConfigContract.cs file under the desktop Data owner.`,
        );
      }
    } else if (!normalized.startsWith("src/desktop-preview/") || !normalized.endsWith(".ts")) {
      violations.push(
        `${owner.role} owner '${owner.path}' must be a TypeScript file under src/desktop-preview.`,
      );
    }
    if (ownerPaths.has(normalized)) {
      violations.push(`Owner target '${owner.path}' is declared more than once.`);
    }
    ownerPaths.add(normalized);
    if (resolved && existsSync(resolved)) {
      violations.push(`Owner target '${owner.path}' already exists and will not be overwritten.`);
    }
  }

  for (const [role, route, suffix] of [
    ["contract", spec.manifest.contract, "ComponentContract"],
    ["resolver", spec.manifest.resolver, "ComponentResolver"],
    ["renderable", spec.manifest.renderable, "ComponentRenderable"],
  ] as const) {
    if (!new RegExp(`^\\./[A-Za-z][A-Za-z0-9/]*${suffix}$`).test(route)) {
      violations.push(
        `Manifest ${role} route '${route}' must be an explicit extensionless ./${suffix} route.`,
      );
    }
  }
  for (const [label, exportName] of [
    ["contract", spec.owners.contractExport],
    ["resolver", spec.owners.resolverExport],
    ["renderable", spec.owners.renderableExport],
  ] as const) {
    validateIdentity(exportName, /^[A-Za-z_$][A-Za-z0-9_$]*$/, `${label} export`, violations);
  }

  if (canonicalJson(spec.config) !== canonicalJson(spec.defaultVariant.config)) {
    violations.push(
      "Current config and protected Default Variant config must be the same complete snapshot.",
    );
  }
  if (Object.hasOwn(spec.metadata, "variants")) {
    violations.push(
      "Scaffold metadata must not duplicate variants; Default and additionalVariants are the Variant sources.",
    );
  }
  const variantIds = new Set(["default"]);
  for (const variant of spec.additionalVariants) {
    validateIdentity(
      variant.id,
      /^[a-z][A-Za-z0-9_]*$/,
      `additional Variant id '${variant.id}'`,
      violations,
    );
    if (variantIds.has(variant.id)) {
      violations.push(`Variant id '${variant.id}' is duplicated.`);
    }
    variantIds.add(variant.id);
    if (!variant.name.trim()) {
      violations.push(`Additional Variant '${variant.id}' name must not be blank.`);
    }
  }

  validateDictionaryFields(spec, inventory, violations);
  validateDesignPreview(spec, inventory, violations);
  validateEditorLayout(spec, violations);
  const assets = validateAssets(spec.assets, repositoryRoot, violations);

  if (violations.length > 0) {
    throw new ComponentScaffoldValidationError(violations);
  }

  const metadata = {
    ...spec.metadata,
    variants: [spec.defaultVariant, ...spec.additionalVariants],
  } satisfies JsonObject;

  return {
    schemaVersion: 1,
    mode: "dry-run",
    status: "contract-ready-for-owner-implementation",
    intent: spec.intent,
    component: spec.component,
    creates: ownerFiles,
    updates: [
      {
        owner: "manifest",
        path: "src/desktop-preview/desktopPreviewManifest.json",
        description: `Add exact Component route '${componentType}' and its declared embeds.`,
      },
      {
        owner: "registry",
        path: "src/desktop-preview/componentClassRenderableRegistry.ts",
        description: `Route '${componentType}' through its declared resolver and renderable exports.`,
      },
      {
        owner: "dictionary",
        path: "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
        description: `Register ${spec.dictionaryFields.length} explicit dictionary field descriptors.`,
      },
      {
        owner: "persistence",
        path: "data/desktop-editor-spike.sqlite",
        description: "Apply one explicit maintenance revision for the current row and editor layout.",
      },
    ],
    manifestEntry: {
      category: spec.component.category,
      ...spec.manifest,
    },
    registryRoute: {
      componentType,
      resolverExport: spec.owners.resolverExport,
      renderableExport: spec.owners.renderableExport,
      mode: spec.owners.registryMode,
    },
    persistedDefinition: {
      table: "component_classes",
      row: {
        id: componentClassId,
        project_id: projectId,
        component_type: componentType,
        record_class_id: recordClassId,
        name: spec.component.name,
        notes: spec.component.notes,
        config_json: spec.config,
        design_preview_json: spec.designPreview,
        metadata_json: metadata,
      },
    },
    editorLayout: {
      table: "editor_layouts",
      row: {
        record_class_id: recordClassId,
        layout_json: spec.editorLayout,
      },
    },
    dictionaryFields: spec.dictionaryFields,
    assets,
    validationCommands: [
      "npm run test:scaffolding",
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

export function componentScaffoldTemplate(): ComponentScaffoldSpec {
  const config = {
    replaceMe: {
      size: 100,
    },
  } satisfies JsonObject;
  return {
    schemaVersion: 1,
    intent: {
      responsibility: "Replace with the Component's single semantic responsibility.",
      visualBoundary: "Replace with the exact geometry and child-composition boundary.",
      runtimeBehavior: "Replace with observable Runtime Input behavior and states.",
      forwarding: "No forwarding. Replace this sentence when explicit forwarding is required.",
      temporalOwnership: "Static owner with no temporal state. Replace when timing is required.",
    },
    component: {
      componentType: "replaceMe",
      category: "atom",
      componentClassId: "component_project_foqn_s2_replace_me",
      projectId: "project_foqn_s2",
      recordClassId: "component.replaceMe",
      name: "Replace Me",
      notes: "Replace every example identity and supply the real owner semantics before implementation.",
    },
    manifest: {
      contract: "./replaceMeComponentContract",
      resolver: "./replaceMeComponentResolver",
      renderable: "./replaceMeComponentRenderable",
      embeds: [],
    },
    owners: {
      contractExport: "ReplaceMeDesignContract",
      resolverExport: "resolveReplaceMeComponent",
      renderableExport: "replaceMeComponentToRenderable",
      registryMode: "simple",
      focusedTest: "tests/animation/replaceMeComponent.test.ts",
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
    designPreview: {
      componentType: "replaceMe",
      sampleSize: 256,
      inputs: [
        {
          id: "value",
          label: "Value",
          jsonKey: "value",
          kind: "number",
          valueKind: "Integer",
          defaultValue: "1",
          minimum: 0,
          maximum: 100,
          increment: 1,
          tableId: "",
          resolvedJsonKey: "",
          componentType: "",
          options: [],
          pairFirstLabel: "W",
          pairSecondLabel: "H",
          source: "runtime",
          uiOrigin: "self",
          uiGroupId: "content",
          uiGroupLabel: "Content",
          uiParentGroupId: "",
          uiOrder: 10,
          visibleWhenPath: "",
          visibleWhenValue: "",
          transition: null,
          unit: "",
        },
      ],
      collections: [],
      actions: [],
      value: 1,
    },
    metadata: {
      note: "Replace with the current definition note.",
    },
    dictionaryFields: [
      {
        id: "component.replaceMe.size",
        label: "Size",
        valueKind: "Integer",
        jsonPath: ["replaceMe", "size"],
        defaultValue: "100",
        isEditable: true,
        options: [],
        optionsSource: "",
        pairLabels: null,
        number: {
          minimum: 1,
          maximum: 1000,
          increment: 1,
          decimalPlaces: 0,
          useSlider: false,
        },
        componentInputBindings: null,
        structuredCollection: null,
        componentVariantType: "",
        runtimeInputComponentVariantFieldId: "",
        unit: "",
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
                { id: "component.type", order: 20, visible: true },
                { id: "core.notes", order: 30, visible: true },
              ],
            },
          ],
        },
        {
          id: "layout",
          label: "Layout",
          subtitle: "Replace with the real owner fields",
          icon: "layout",
          order: 20,
          visible: true,
          defaultOpen: false,
          groups: [
            {
              id: "layout",
              label: "Layout",
              order: 10,
              visible: true,
              fields: [
                { id: "component.replaceMe.size", order: 10, visible: true },
              ],
            },
          ],
        },
      ],
    },
    assets: [],
  };
}

function parseDictionaryField(value: unknown, owner: string): ComponentScaffoldField {
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
    "componentInputBindings",
    "structuredCollection",
    "componentVariantType",
    "runtimeInputComponentVariantFieldId",
    "unit",
  ], owner);
  const pairLabels = field.pairLabels === null
    ? null
    : requiredObject(field.pairLabels, `${owner} pairLabels`);
  if (pairLabels) {
    requireExactKeys(pairLabels, ["first", "second"], `${owner} pairLabels`);
  }
  const number = field.number === null
    ? null
    : requiredObject(field.number, `${owner} number`);
  if (number) {
    requireExactKeys(
      number,
      ["minimum", "maximum", "increment", "decimalPlaces", "useSlider"],
      `${owner} number`,
    );
  }
  const componentInputBindings = field.componentInputBindings === null
    ? null
    : requiredArray(field.componentInputBindings, `${owner} componentInputBindings`)
      .map((item, index) => requiredJsonObject(
        item,
        `${owner} componentInputBindings[${index}]`,
      ));
  return {
    id: requiredString(field.id, `${owner} id`),
    label: requiredString(field.label, `${owner} label`),
    valueKind: requiredString(field.valueKind, `${owner} valueKind`),
    jsonPath: requiredArray(field.jsonPath, `${owner} jsonPath`)
      .map((segment, index) => requiredString(segment, `${owner} jsonPath[${index}]`)),
    defaultValue: requiredString(field.defaultValue, `${owner} defaultValue`, true),
    isEditable: requiredBoolean(field.isEditable, `${owner} isEditable`),
    options: requiredArray(field.options, `${owner} options`)
      .map((option, index) => requiredJsonObject(option, `${owner} options[${index}]`)),
    optionsSource: requiredString(
      field.optionsSource,
      `${owner} optionsSource`,
      true,
    ),
    pairLabels: pairLabels
      ? {
          first: requiredString(pairLabels.first, `${owner} pairLabels first`),
          second: requiredString(pairLabels.second, `${owner} pairLabels second`),
        }
      : null,
    number: number
      ? {
          minimum: optionalNumber(number.minimum, `${owner} number minimum`),
          maximum: optionalNumber(number.maximum, `${owner} number maximum`),
          increment: requiredNumber(number.increment, `${owner} number increment`),
          decimalPlaces: requiredInteger(
            number.decimalPlaces,
            `${owner} number decimalPlaces`,
          ),
          useSlider: requiredBoolean(number.useSlider, `${owner} number useSlider`),
        }
      : null,
    componentInputBindings,
    structuredCollection: field.structuredCollection === null
      ? null
      : requiredJsonObject(field.structuredCollection, `${owner} structuredCollection`),
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
    unit: requiredString(field.unit, `${owner} unit`, true),
  };
}

function validateDictionaryFields(
  spec: ComponentScaffoldSpec,
  inventory: ComponentScaffoldInventory,
  violations: string[],
) {
  const ids = new Set<string>();
  const paths = new Set<string>();
  for (const field of spec.dictionaryFields) {
    if (!field.id.startsWith(`${spec.component.recordClassId}.`)) {
      violations.push(
        `Dictionary field '${field.id}' must use record class prefix '${spec.component.recordClassId}.'.`,
      );
    }
    if (ids.has(field.id)) {
      violations.push(`Dictionary field id '${field.id}' is duplicated.`);
    }
    ids.add(field.id);
    if (!inventory.valueKinds.has(field.valueKind)) {
      violations.push(
        `Dictionary field '${field.id}' uses unknown current ValueKind '${field.valueKind}'.`,
      );
    }
    if (field.jsonPath.length === 0) {
      violations.push(`Dictionary field '${field.id}' requires a non-empty JSON path.`);
    } else if (!jsonPathExists(spec.config, field.jsonPath)) {
      violations.push(
        `Dictionary field '${field.id}' JSON path '${field.jsonPath.join(".")}' is missing from current config.`,
      );
    }
    const storagePath = field.jsonPath.join(".");
    if (paths.has(storagePath)) {
      violations.push(`Dictionary JSON path '${storagePath}' is declared more than once.`);
    }
    paths.add(storagePath);
    if (field.number) {
      if (field.number.increment <= 0) {
        violations.push(`Dictionary field '${field.id}' number increment must be positive.`);
      }
      if (field.number.decimalPlaces < 0) {
        violations.push(`Dictionary field '${field.id}' decimalPlaces cannot be negative.`);
      }
      if (field.number.minimum !== null
          && field.number.maximum !== null
          && field.number.minimum > field.number.maximum) {
        violations.push(`Dictionary field '${field.id}' number minimum exceeds its maximum.`);
      }
    }
  }
}

function validateDesignPreview(
  spec: ComponentScaffoldSpec,
  inventory: ComponentScaffoldInventory,
  violations: string[],
) {
  if (spec.designPreview.componentType !== spec.component.componentType) {
    violations.push(
      `Design Preview componentType must be exactly '${spec.component.componentType}'.`,
    );
  }
  const sampleSize = spec.designPreview.sampleSize;
  if (typeof sampleSize !== "number" || !Number.isFinite(sampleSize) || sampleSize <= 0) {
    violations.push("Design Preview sampleSize must be a positive finite number.");
  }
  const inputs = spec.designPreview.inputs;
  if (!Array.isArray(inputs)) {
    violations.push("Design Preview must declare an inputs array, including an explicit empty array.");
    return;
  }
  if (!Array.isArray(spec.designPreview.collections)) {
    violations.push(
      "Design Preview must declare a collections array, including an explicit empty array.",
    );
  }
  if (!Array.isArray(spec.designPreview.actions)) {
    violations.push(
      "Design Preview must declare an actions array, including an explicit empty array.",
    );
  }
  validateStableObjectIds(spec.designPreview.collections, "collections", violations);
  validateStableObjectIds(spec.designPreview.actions, "actions", violations);
  const ids = new Set<string>();
  const keys = new Set<string>();
  for (let index = 0; index < inputs.length; index += 1) {
    const input = inputs[index];
    if (!isPlainObject(input)) {
      violations.push(`Design Preview inputs[${index}] must be an object.`);
      continue;
    }
    for (const key of runtimeInputBaseKeys) {
      if (!Object.hasOwn(input, key)) {
        violations.push(`Design Preview inputs[${index}] requires explicit '${key}'.`);
      }
    }
    const id = typeof input.id === "string" ? input.id : "";
    const jsonKey = typeof input.jsonKey === "string" ? input.jsonKey : "";
    const label = typeof input.label === "string" ? input.label : "";
    const kind = typeof input.kind === "string" ? input.kind : "";
    const valueKind = typeof input.valueKind === "string" ? input.valueKind : "";
    const source = typeof input.source === "string" ? input.source : "";
    const uiOrigin = typeof input.uiOrigin === "string" ? input.uiOrigin : "";
    if (!id || !label || !jsonKey || !kind) {
      violations.push(
        `Design Preview inputs[${index}] requires stable id, label, jsonKey and kind strings.`,
      );
    }
    if (ids.has(id)) violations.push(`Design Preview Runtime Input id '${id}' is duplicated.`);
    if (keys.has(jsonKey)) {
      violations.push(`Design Preview Runtime Input jsonKey '${jsonKey}' is duplicated.`);
    }
    ids.add(id);
    keys.add(jsonKey);
    if (!inventory.valueKinds.has(valueKind)) {
      violations.push(
        `Design Preview Runtime Input '${id || index}' uses unknown current ValueKind '${valueKind}'.`,
      );
    }
    if (!["runtime", "variant", "calculated"].includes(source)) {
      violations.push(`Design Preview Runtime Input '${id || index}' has invalid source '${source}'.`);
    }
    if (!["self", "embedded"].includes(uiOrigin)) {
      violations.push(
        `Design Preview Runtime Input '${id || index}' has invalid uiOrigin '${uiOrigin}'.`,
      );
    }
    if (typeof input.defaultValue !== "string") {
      violations.push(
        `Design Preview Runtime Input '${id || index}' requires a string defaultValue.`,
      );
    }
    if (source === "runtime"
        && (!jsonKey || !Object.hasOwn(spec.designPreview, jsonKey)
          || spec.designPreview[jsonKey] === null)) {
      violations.push(
        `Design Preview Runtime Input '${id || index}' requires an explicit current sample value at '${jsonKey}'.`,
      );
    }
  }
}

function validateStableObjectIds(
  value: JsonValue | undefined,
  label: string,
  violations: string[],
) {
  if (!Array.isArray(value)) return;
  const ids = new Set<string>();
  for (let index = 0; index < value.length; index += 1) {
    const item = value[index];
    const id = isPlainObject(item) && typeof item.id === "string" ? item.id : "";
    if (!id) {
      violations.push(`Design Preview ${label}[${index}] requires a stable id.`);
      continue;
    }
    if (ids.has(id)) {
      violations.push(`Design Preview ${label} id '${id}' is duplicated.`);
    }
    ids.add(id);
  }
}

function validateAssets(
  assets: readonly string[],
  repositoryRoot: string,
  violations: string[],
) {
  const seen = new Set<string>();
  const result: ComponentScaffoldPlan["assets"] = [];
  for (const asset of assets) {
    let normalized = asset;
    let resolved: string | undefined;
    try {
      normalized = normalizeRepositoryPath(asset);
      resolved = resolveRepositoryPath(repositoryRoot, asset);
    } catch (error) {
      violations.push(error instanceof Error ? error.message : `Invalid asset path '${asset}'.`);
    }
    if (normalized !== asset) {
      violations.push(`Asset '${asset}' must use its normalized repository path.`);
    }
    if (!normalized.startsWith("assets/")) {
      violations.push(`Asset '${asset}' must be an explicit repository path under assets/.`);
    }
    if (seen.has(normalized)) {
      violations.push(`Asset '${asset}' is declared more than once.`);
    }
    seen.add(normalized);
    result.push({
      path: normalized,
      status: resolved && existsSync(resolved) ? "existing" : "required-create",
    });
  }
  return result;
}

function validateEditorLayout(
  spec: ComponentScaffoldSpec,
  violations: string[],
) {
  const cards = spec.editorLayout.cards;
  if (!Array.isArray(cards) || cards.length === 0) {
    violations.push("Editor layout must declare at least one card.");
    return;
  }
  const cardIds = new Set<string>();
  const fieldCounts = new Map<string, number>();
  for (let cardIndex = 0; cardIndex < cards.length; cardIndex += 1) {
    const card = cards[cardIndex];
    if (!isPlainObject(card)) {
      violations.push(`Editor layout cards[${cardIndex}] must be an object.`);
      continue;
    }
    const cardId = typeof card.id === "string" ? card.id : "";
    if (!cardId) violations.push(`Editor layout cards[${cardIndex}] requires a stable id.`);
    if (cardIds.has(cardId)) violations.push(`Editor layout card id '${cardId}' is duplicated.`);
    cardIds.add(cardId);
    if (!Array.isArray(card.groups) || card.groups.length === 0) {
      violations.push(`Editor layout card '${cardId || cardIndex}' requires at least one group.`);
      continue;
    }
    const groupIds = new Set<string>();
    for (let groupIndex = 0; groupIndex < card.groups.length; groupIndex += 1) {
      const group = card.groups[groupIndex];
      if (!isPlainObject(group)) {
        violations.push(
          `Editor layout card '${cardId || cardIndex}' groups[${groupIndex}] must be an object.`,
        );
        continue;
      }
      const groupId = typeof group.id === "string" ? group.id : "";
      if (!groupId) {
        violations.push(
          `Editor layout card '${cardId || cardIndex}' groups[${groupIndex}] requires a stable id.`,
        );
      }
      if (groupIds.has(groupId)) {
        violations.push(
          `Editor layout card '${cardId || cardIndex}' group id '${groupId}' is duplicated.`,
        );
      }
      groupIds.add(groupId);
      if (!Array.isArray(group.fields)) {
        violations.push(
          `Editor layout card '${cardId || cardIndex}' group '${groupId || groupIndex}' requires fields.`,
        );
        continue;
      }
      for (let fieldIndex = 0; fieldIndex < group.fields.length; fieldIndex += 1) {
        const field = group.fields[fieldIndex];
        const fieldId = isPlainObject(field) && typeof field.id === "string" ? field.id : "";
        if (!fieldId) {
          violations.push(
            `Editor layout field at ${cardId || cardIndex}/${groupId || groupIndex}/${fieldIndex} requires an id.`,
          );
          continue;
        }
        fieldCounts.set(fieldId, (fieldCounts.get(fieldId) ?? 0) + 1);
      }
    }
  }

  const requiredFields = new Set([
    ...identityFieldIds,
    ...spec.dictionaryFields.map((field) => field.id),
  ]);
  for (const fieldId of requiredFields) {
    const count = fieldCounts.get(fieldId) ?? 0;
    if (count !== 1) {
      violations.push(
        `Editor layout must expose required field '${fieldId}' exactly once; found ${count}.`,
      );
    }
  }
  for (const fieldId of fieldCounts.keys()) {
    if (!requiredFields.has(fieldId)) {
      violations.push(`Editor layout references undeclared field '${fieldId}'.`);
    }
  }
}

function ownerFile(
  role: "contract" | "resolver" | "renderable",
  route: string,
  requiredExport: string,
) {
  return {
    role,
    path: `src/desktop-preview/${route.replace(/^\.\//, "")}.ts`,
    requiredExport,
  } as const;
}

function parseValueKinds(source: string) {
  const match = source.match(/internal\s+enum\s+ValueKind\s*\{([\s\S]*?)\}/);
  if (!match) throw new Error("Could not derive current ValueKind names from FieldDefinition.cs.");
  const values = (match[1] ?? "")
    .split(",")
    .map((value) => value.trim())
    .filter(Boolean);
  if (values.length === 0) {
    throw new Error("Current ValueKind enum is empty.");
  }
  return new Set(values);
}

function resolveRepositoryPath(repositoryRoot: string, relativePath: string) {
  const normalized = normalizeRepositoryPath(relativePath);
  const absoluteRoot = path.resolve(repositoryRoot);
  const fullPath = path.resolve(absoluteRoot, ...normalized.split("/"));
  if (fullPath !== absoluteRoot && !fullPath.startsWith(`${absoluteRoot}${path.sep}`)) {
    throw new Error(`Scaffold target path escapes are prohibited: ${relativePath}`);
  }
  return fullPath;
}

function normalizeRepositoryPath(relativePath: string) {
  if (path.isAbsolute(relativePath) || path.win32.isAbsolute(relativePath)) {
    throw new Error(`Absolute scaffold target paths are prohibited: ${relativePath}`);
  }
  const normalized = path.posix.normalize(relativePath.replace(/\\/g, "/"))
    .replace(/^(?:\.\/)+/, "");
  if (!normalized || normalized === ".." || normalized.startsWith("../")) {
    throw new Error(`Scaffold target path escapes are prohibited: ${relativePath}`);
  }
  if (normalized === "docs/old" || normalized.startsWith("docs/old/")) {
    throw new Error(`Historical archive scaffold targets are prohibited: ${relativePath}`);
  }
  return normalized;
}

function validateIdentity(
  value: string,
  pattern: RegExp,
  label: string,
  violations: string[],
) {
  if (!pattern.test(value)) {
    violations.push(`${label} '${value}' is not a valid explicit stable identity.`);
  }
}

function canonicalJson(value: JsonValue): string {
  if (Array.isArray(value)) {
    return `[${value.map(canonicalJson).join(",")}]`;
  }
  if (isPlainObject(value)) {
    return `{${Object.keys(value).sort().map((key) =>
      `${JSON.stringify(key)}:${canonicalJson(value[key] as JsonValue)}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

function jsonPathExists(root: JsonObject, segments: readonly string[]) {
  let current: JsonValue = root;
  for (const segment of segments) {
    if (!isPlainObject(current) || !Object.hasOwn(current, segment)) return false;
    current = current[segment] as JsonValue;
  }
  return current !== null;
}

function pascalCase(value: string) {
  return value.length === 0
    ? value
    : `${value[0]!.toUpperCase()}${value.slice(1)}`;
}

function requiredObject(value: unknown, owner: string): Record<string, unknown> {
  if (!isPlainObject(value)) throw new Error(`${owner} must be an object.`);
  return value;
}

function requiredJsonObject(value: unknown, owner: string): JsonObject {
  const object = requiredObject(value, owner);
  assertJsonValue(object, owner);
  return object as JsonObject;
}

function requiredArray(value: unknown, owner: string): unknown[] {
  if (!Array.isArray(value)) throw new Error(`${owner} must be an array.`);
  return value;
}

function requiredString(value: unknown, owner: string, allowEmpty = false): string {
  if (typeof value !== "string" || (!allowEmpty && !value.trim())) {
    throw new Error(`${owner} must be ${allowEmpty ? "a string" : "a non-empty string"}.`);
  }
  return value;
}

function requiredBoolean(value: unknown, owner: string): boolean {
  if (typeof value !== "boolean") throw new Error(`${owner} must be a boolean.`);
  return value;
}

function requiredNumber(value: unknown, owner: string): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new Error(`${owner} must be a finite number.`);
  }
  return value;
}

function optionalNumber(value: unknown, owner: string): number | null {
  return value === null ? null : requiredNumber(value, owner);
}

function requiredInteger(value: unknown, owner: string): number {
  const number = requiredNumber(value, owner);
  if (!Number.isInteger(number)) throw new Error(`${owner} must be an integer.`);
  return number;
}

function requireExactKeys(
  object: Record<string, unknown>,
  keys: readonly string[],
  owner: string,
) {
  const expected = new Set(keys);
  const missing = keys.filter((key) => !Object.hasOwn(object, key));
  const unknown = Object.keys(object).filter((key) => !expected.has(key));
  if (missing.length > 0 || unknown.length > 0) {
    throw new Error([
      `${owner} has an invalid shape.`,
      missing.length > 0 ? `Missing: ${missing.join(", ")}.` : "",
      unknown.length > 0 ? `Unknown: ${unknown.join(", ")}.` : "",
    ].filter(Boolean).join(" "));
  }
}

function assertJsonValue(value: unknown, owner: string): asserts value is JsonValue {
  if (value === null
      || typeof value === "string"
      || typeof value === "boolean"
      || (typeof value === "number" && Number.isFinite(value))) {
    return;
  }
  if (Array.isArray(value)) {
    value.forEach((item, index) => assertJsonValue(item, `${owner}[${index}]`));
    return;
  }
  if (isPlainObject(value)) {
    for (const [key, child] of Object.entries(value)) {
      assertJsonValue(child, `${owner}.${key}`);
    }
    return;
  }
  throw new Error(`${owner} contains a non-JSON value.`);
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object"
    && value !== null
    && !Array.isArray(value)
    && Object.getPrototypeOf(value) === Object.prototype;
}
