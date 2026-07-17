import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentManifestEntry,
} from "../src/desktop-preview/desktopPreviewComponents.js";
import { componentRenderableFactories } from "../src/desktop-preview/componentClassRenderableRegistry.js";
import {
  desktopPreviewModules,
  type DesktopPreviewModuleManifestEntry,
} from "../src/desktop-preview/desktopPreviewModules.js";
import { moduleRenderableFactories } from "../src/desktop-preview/moduleRenderableRegistry.js";
import {
  approximateTextWidth,
  approximateWrappedTextLines,
} from "../src/desktop-preview/previewTextHelpers.js";
import { renderableNodeTypes } from "../src/visual/renderable/types.js";

const root = process.cwd();
const previewRoot = path.join(root, "src", "desktop-preview");

const violations: string[] = [];
const retiredTimeFields = [
  "fadeFrames",
  "cursorBlinkFrames",
  "blinkFrames",
  "motionTimeSeconds",
  "textAnimationTimeSeconds",
  "composerTransitionTimeSeconds",
];
const desktopPreviewPaintNodeTypes = new Set([
  "group",
  "icon",
  "image",
  "path",
  "surface",
  "text",
]);
const forbiddenDesktopPreviewNodeTypes = new Set([
  "status_bar",
  "status_bar_zone",
  "status_bar_item",
  "navigation_bar",
  "navigation_bar_zone",
  "navigation_bar_item",
  "navigation_bar_gesture",
  "component_preview_unsupported",
  "design_preview_surface",
  "icon_token",
  "waveform_bar",
]);

function relative(filePath: string) {
  return path.relative(root, filePath).replace(/\\/g, "/");
}

function readText(relativePath: string) {
  return readFileSync(path.join(root, relativePath), "utf8");
}

function addViolation(filePath: string, message: string) {
  violations.push(`${filePath}: ${message}`);
}

function walkFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const fullPath = path.join(directory, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) return walkFiles(fullPath);
    return /\.(ts|tsx)$/.test(entry) ? [fullPath] : [];
  });
}

function walkFilesByExtension(directory: string, extensions: readonly string[]): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const fullPath = path.join(directory, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) return walkFilesByExtension(fullPath, extensions);
    return extensions.some((extension) => entry.endsWith(extension)) ? [fullPath] : [];
  });
}

for (const directory of ["src/desktop-preview", "spikes/desktop-editor-shell/Common", "spikes/desktop-editor-shell/Data", "spikes/desktop-editor-shell/EditorShell"]) {
  for (const filePath of walkFilesByExtension(path.join(root, directory), [".ts", ".tsx", ".cs"])) {
    const source = readFileSync(filePath, "utf8");
    for (const retired of retiredTimeFields) {
      if (source.includes(retired)) addViolation(relative(filePath), `retired time field remains: ${retired}`);
    }
  }
}

function importTargets(source: string) {
  const targets: string[] = [];
  const importPattern = /import\s+(?:type\s+)?(?:[\s\S]*?)\s+from\s+["']([^"']+)["']/g;
  let match: RegExpExecArray | null;
  while ((match = importPattern.exec(source)) !== null) {
    targets.push(match[1] ?? "");
  }
  return targets;
}

function assertNoTerms(relativePath: string, terms: string[]) {
  const source = readText(relativePath);
  for (const term of terms) {
    if (source.includes(term)) {
      addViolation(relativePath, `forbidden central preview term "${term}"`);
    }
  }
}

function assertContains(relativePath: string, term: string, message: string) {
  const source = readText(relativePath);
  if (!source.includes(term)) {
    addViolation(relativePath, message);
  }
}

function assertSourceContains(sourceLabel: string, source: string, term: string, message: string) {
  if (!source.includes(term)) {
    addViolation(sourceLabel, message);
  }
}

function assertAnyContains(relativePaths: string[], term: string, message: string) {
  const found = relativePaths.some((relativePath) => {
    const fullPath = path.join(root, relativePath);
    return existsSync(fullPath) && readText(relativePath).includes(term);
  });
  if (!found) {
    addViolation(relativePaths.join(", "), message);
  }
}

function assertDoesNotContain(relativePath: string, term: string, message: string) {
  const source = readText(relativePath);
  if (source.includes(term)) {
    addViolation(relativePath, message);
  }
}

function assertPackageScriptDoesNotContain(scriptName: string, term: string, message: string) {
  const packageJson = JSON.parse(readText("package.json")) as {
    scripts?: Record<string, string>;
  };
  const script = packageJson.scripts?.[scriptName] ?? "";
  if (script.includes(term)) {
    addViolation("package.json", message);
  }
}

function assertFilesDoNotContain(files: readonly string[], term: string, message: string) {
  for (const file of files) {
    const relativePath = relative(file);
    assertDoesNotContain(relativePath, term, message);
  }
}

const currentRepositoryFiles = walkFilesByExtension(
  path.join(root, "spikes", "desktop-editor-shell", "Data"),
  [".cs"],
);
assertFilesDoNotContain(
  currentRepositoryFiles,
  "EnsurePresetArray",
  "current Component Variants must never synthesize a missing Variant array",
);
assertFilesDoNotContain(
  currentRepositoryFiles,
  "ComponentPresetIsLocked",
  "current Component Variants must not infer lock state from an id",
);
assertFilesDoNotContain(
  currentRepositoryFiles,
  "ParseJsonObject(string.IsNullOrWhiteSpace",
  "current persisted JSON roots must reject blank documents instead of parsing an empty object",
);
assertFilesDoNotContain(
  currentRepositoryFiles,
  "preset.ConfigJson == \"{}\"",
  "a selected Component Variant config must not fall back to class config",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/35_current_json_and_variant_contract.md",
  "AGENTS must require the current JSON and Variant contract",
);
assertContains(
  "docs/architecture/README.md",
  "35_current_json_and_variant_contract.md",
  "the architecture index must include contract 35",
);
const jsonRootInventorySource = readText("spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs");
const executableJsonRoots = new Map(
  [...jsonRootInventorySource.matchAll(/\("([a-z_]+)", "([a-z_]+)", "(object|array)"\)/g)]
    .map((match) => [`${match[1]}.${match[2]}`, match[3]]),
);
const documentedJsonRoots = new Map<string, string>();
let documentedRootKind = "";
for (const line of readText("docs/architecture/35_current_json_and_variant_contract.md").split(/\r?\n/)) {
  if (line === "object" || line === "array") {
    documentedRootKind = line;
    continue;
  }
  const entry = /^  ([a-z_]+\.[a-z_]+)$/.exec(line)?.[1];
  if (entry && documentedRootKind) documentedJsonRoots.set(entry, documentedRootKind);
}
for (const [entry, rootKind] of executableJsonRoots) {
  if (documentedJsonRoots.get(entry) !== rootKind) {
    addViolation(
      "docs/architecture/35_current_json_and_variant_contract.md",
      `${entry} must document its executable ${rootKind} root`,
    );
  }
}
for (const [entry, rootKind] of documentedJsonRoots) {
  if (executableJsonRoots.get(entry) !== rootKind) {
    addViolation(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
      `${entry} must have the documented ${rootKind} startup root`,
    );
  }
}
function assertDesktopJsonRootsAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    for (const [entry, rootKind] of executableJsonRoots) {
      const [table, column] = entry.split(".");
      const rows = database.prepare(`SELECT rowid AS owner_rowid, ${column} AS json FROM ${table}`).all() as {
        owner_rowid: number;
        json: string;
      }[];
      for (const row of rows) {
        let parsed: unknown;
        try {
          parsed = JSON.parse(row.json);
        } catch {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${entry} row ${row.owner_rowid} contains malformed JSON`,
          );
          continue;
        }
        const matches = rootKind === "array"
          ? Array.isArray(parsed)
          : typeof parsed === "object" && parsed !== null && !Array.isArray(parsed);
        if (!matches) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${entry} row ${row.owner_rowid} must have a ${rootKind} root`,
          );
        }
      }
    }
  } finally {
    database.close();
  }
}
assertDesktopJsonRootsAreCanonical();
assertContains(
  "spikes/desktop-editor-shell/Common/VariantEnvelopeContract.cs",
  "RequiredBoolean",
  "the shared Variant envelope must require explicit boolean flags",
);
assertContains(
  "spikes/desktop-editor-shell/Common/VariantEnvelopeContract.cs",
  "must contain the stable Default Variant id 'default'",
  "the shared Variant envelope must require the stable Default id",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProductionPreviewRuntimeResolver.cs",
  "catch",
  "Production payload parsing must reject invalid current JSON instead of returning an empty object",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewTestValues.cs",
  "return new JsonObject();",
  "Design Preview Test Values must not hide invalid current JSON behind an empty object",
);

function assertSharedEditorSurfacesHaveNoConcreteEditors() {
  const sharedSurfaces = [
    "spikes/desktop-editor-shell/MainWindow.axaml.cs",
    "spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorBreadcrumbBar.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorCardHostController.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorContextStrip.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorGroupBlock.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  ];
  const concreteEditorTypes = [
    "IconThemeTokensCollectionEditor",
    "NavigationBarItemsCollectionEditor",
    "RuntimeInputsCollectionEditor",
    "ShotModuleInstancesCollectionEditor",
    "StatusBarItemsCollectionEditor",
  ];
  const concreteComponentLiterals = [
    '"audio"',
    '"avatar"',
    '"bubble"',
    '"button"',
    '"label"',
    '"navigation_bar"',
    '"status_bar"',
    '"text_input"',
    '"video"',
  ];

  for (const relativePath of sharedSurfaces) {
    for (const term of [...concreteEditorTypes, ...concreteComponentLiterals]) {
      assertDoesNotContain(
        relativePath,
        term,
        `shared editor surface contains concrete editor/component knowledge: ${term}`,
      );
    }
  }
}

function assertMatches(relativePath: string, pattern: RegExp, message: string) {
  const source = readText(relativePath);
  if (!pattern.test(source)) {
    addViolation(relativePath, message);
  }
}

function assertSourceMatches(sourceLabel: string, source: string, pattern: RegExp, message: string) {
  if (!pattern.test(source)) {
    addViolation(sourceLabel, message);
  }
}

function quotedStringsFromBlock(relativePath: string, pattern: RegExp, message: string) {
  const source = readText(relativePath);
  const match = pattern.exec(source);
  if (!match) {
    addViolation(relativePath, message);
    return new Set<string>();
  }
  return new Set([...match[1].matchAll(/"([^"]+)"/g)].map((item) => item[1] ?? ""));
}

function assertStringSetEquals(
  relativePath: string,
  actual: Set<string>,
  expected: Set<string>,
  label: string,
) {
  for (const value of expected) {
    if (!actual.has(value)) {
      addViolation(relativePath, `${label} is missing primitive "${value}"`);
    }
  }
  for (const value of actual) {
    if (!expected.has(value)) {
      addViolation(relativePath, `${label} contains non-allowlisted primitive "${value}"`);
    }
  }
}

function assertPropertyBlockDoesNotContain(
  relativePath: string,
  propertyName: string,
  term: string,
  message: string,
) {
  const source = readText(relativePath);
  const match = new RegExp(`public bool ${propertyName} =>[\\s\\S]*?;`).exec(source);
  if (!match) {
    addViolation(relativePath, `could not find ${propertyName} permission block`);
    return;
  }
  if (match[0].includes(term)) {
    addViolation(relativePath, message);
  }
}

function assertPropertyBlockContainsKind(
  relativePath: string,
  propertyName: string,
  kind: string,
  expected: boolean,
  message: string,
) {
  const source = readText(relativePath);
  const match = new RegExp(`public bool ${propertyName} =>[\\s\\S]*?;`).exec(source);
  if (!match) {
    addViolation(relativePath, `could not find ${propertyName} permission block`);
    return;
  }
  const contains = new RegExp(`ProjectTreeNodeKind\\.${kind}(?![A-Za-z0-9_])`).test(match[0]);
  if (contains !== expected) addViolation(relativePath, message);
}

function assertDesktopDatabaseTableIsEmpty(tableName: string, message: string) {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) {
    return;
  }

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const table = database
      .prepare("SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?")
      .get(tableName) as { name: string } | undefined;
    if (!table) {
      return;
    }

    const row = database.prepare(`SELECT COUNT(*) AS count FROM ${tableName}`).get() as { count: number };
    if (row.count > 0) {
      addViolation("data/desktop-editor-spike.sqlite", `${message}; found ${row.count} row(s) in ${tableName}`);
    }
  } finally {
    database.close();
  }
}

function assertDesktopComponentPresetReferencesAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, project_id, component_type, config_json, metadata_json FROM component_classes")
      .all() as {
        id: string;
        project_id: string;
        component_type: string;
        config_json: string;
        metadata_json: string;
      }[];
    const rowsById = new Map(rows.map((row) => [row.id, row]));
    const variantsByClassId = new Map<string, Set<string>>();
    const variantsByClassIdAndMetadata = new Map<string, Record<string, unknown>[]>();
    for (const row of rows) {
      const metadata = parseRequiredJsonObject(row.metadata_json, `component ${row.id}.metadata_json`);
      const variants = completeVariantEnvelopes(metadata.presets, `component ${row.id}.metadata_json.presets`);
      variantsByClassIdAndMetadata.set(row.id, variants);
      variantsByClassId.set(row.id, new Set(variants.map((variant) => variant.id as string)));
    }

    const validateValue = (owner: (typeof rows)[number], value: unknown, pathLabel: string) => {
      if (Array.isArray(value)) {
        value.forEach((item, index) => validateValue(owner, item, `${pathLabel}[${index}]`));
        return;
      }
      if (typeof value !== "object" || value === null) return;

      for (const [key, child] of Object.entries(value)) {
        const childPath = pathLabel ? `${pathLabel}.${key}` : key;
        if (key === "presetId" && typeof child === "string") {
          const match = /^(?<classId>.+)::preset::(?<presetId>.+)$/.exec(child);
          const targetClassId = match?.groups?.classId ?? "";
          const presetId = match?.groups?.presetId ?? "";
          const target = rowsById.get(targetClassId);
          if (!match || !target || target.project_id !== owner.project_id || !variantsByClassId.get(targetClassId)?.has(presetId)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `component ${owner.id} has invalid full variant reference at ${childPath}: ${child}`,
            );
          }
        }
        validateValue(owner, child, childPath);
      }
    };

    for (const row of rows) {
      const classConfig = parseRequiredJsonObject(row.config_json, `component ${row.id}.config_json`);
      validateValue(row, classConfig, "config");
      const variants = variantsByClassIdAndMetadata.get(row.id) ?? [];
      variants.forEach((variant, index) => validateValue(row, variant, `metadata.presets[${index}]`));
      const defaultPreset = variants.find((variant) => variant.id === "default");
      if (!defaultPreset || JSON.stringify(defaultPreset.config) !== JSON.stringify(classConfig)) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `component ${row.id} config_json must mirror its canonical Default variant`,
        );
      }
    }
  } finally {
    database.close();
  }
}

assertDesktopComponentPresetReferencesAreCanonical();

function jsonParse(value: string): unknown {
  return JSON.parse(value) as unknown;
}

function parseRequiredJsonObject(value: string, owner: string): Record<string, unknown> {
  if (!value.trim()) {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} is blank`);
    return {};
  }
  try {
    const parsed = JSON.parse(value) as unknown;
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner} must be a JSON object`);
      return {};
    }
    return parsed as Record<string, unknown>;
  } catch {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} contains malformed JSON`);
    return {};
  }
}

function completeVariantEnvelopes(value: unknown, owner: string): Record<string, unknown>[] {
  if (!Array.isArray(value) || value.length === 0) {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} must be a non-empty Variant array`);
    return [];
  }
  const variants: Record<string, unknown>[] = [];
  const ids = new Set<string>();
  for (const [index, candidate] of value.entries()) {
    if (typeof candidate !== "object" || candidate === null || Array.isArray(candidate)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] must be an object`);
      continue;
    }
    const variant = candidate as Record<string, unknown>;
    const id = typeof variant.id === "string" ? variant.id : "";
    const name = typeof variant.name === "string" ? variant.name : "";
    if (!id.trim() || !/^[\p{L}\p{N}_.-]+$/u.test(id)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has an invalid stable id`);
    } else if (ids.has(id)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner} contains duplicate Variant id ${id}`);
    }
    ids.add(id);
    if (!name.trim()) addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no display name`);
    if (typeof variant.protected !== "boolean") addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no explicit protected flag`);
    if (typeof variant.locked !== "boolean") addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no explicit locked flag`);
    if (typeof variant.config !== "object" || variant.config === null || Array.isArray(variant.config)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no object config snapshot`);
    }
    variants.push(variant);
  }
  const defaults = variants.filter((variant) => variant.id === "default");
  if (defaults.length !== 1 || defaults[0]?.protected !== true) {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} must contain exactly one protected Default Variant`);
  }
  return variants;
}

function assertDesktopModuleVariantEnvelopesAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const modules = database.prepare("SELECT id, metadata_json FROM modules").all() as {
      id: string;
      metadata_json: string;
    }[];
    for (const module of modules) {
      const metadata = parseRequiredJsonObject(module.metadata_json, `module ${module.id}.metadata_json`);
      completeVariantEnvelopes(metadata.variants, `module ${module.id}.metadata_json.variants`);
    }
  } finally {
    database.close();
  }
}

assertDesktopModuleVariantEnvelopesAreCanonical();

function jsonRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function jsonArray(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function assertDesktopRuntimeInputValueKindsAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const valueKindSource = readText("spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs");
  const valueKindBlock = /internal enum ValueKind\s*\{([\s\S]*?)\}/.exec(valueKindSource);
  const valueKinds = new Set(
    (valueKindBlock?.[1] ?? "")
      .split(",")
      .map((value) => value.trim())
      .filter(Boolean),
  );
  const inputKinds = new Set([
    "text",
    "number",
    "integerPair",
    "boolean",
    "option",
    "recordReference",
    "componentPreset",
    "themeToken",
    "icon",
    "iconList",
    "multilineText",
    "mediaFilePath",
    "behaviorTiming",
    "collection",
  ]);
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database.prepare(`
      SELECT 'component class' AS owner_type, id, config_json, design_preview_json, metadata_json
      FROM component_classes
      UNION ALL
      SELECT 'module', id, config_json, design_preview_json, metadata_json
      FROM modules
    `).all() as {
      owner_type: string;
      id: string;
      config_json: string;
      design_preview_json: string;
      metadata_json: string;
    }[];
    const visit = (owner: string, value: unknown, pathLabel: string) => {
      if (Array.isArray(value)) {
        value.forEach((item, index) => visit(owner, item, `${pathLabel}[${index}]`));
        return;
      }
      if (typeof value !== "object" || value === null) return;
      const definition = value as Record<string, unknown>;
      if (typeof definition.id === "string"
          && typeof definition.jsonKey === "string"
          && typeof definition.kind === "string") {
        if (!inputKinds.has(definition.kind)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${owner}.${pathLabel} uses unsupported runtime input kind "${definition.kind}"`,
          );
        }
        if (typeof definition.valueKind !== "string" || !valueKinds.has(definition.valueKind)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${owner}.${pathLabel} uses missing or non-canonical valueKind "${String(definition.valueKind ?? "")}"`,
          );
        }
      }
      for (const [key, child] of Object.entries(definition)) {
        visit(owner, child, pathLabel ? `${pathLabel}.${key}` : key);
      }
    };
    for (const row of rows) {
      for (const [column, json] of [
        ["config_json", row.config_json],
        ["design_preview_json", row.design_preview_json],
        ["metadata_json", row.metadata_json],
      ] as const) {
        visit(`${row.owner_type} ${row.id}.${column}`, JSON.parse(json), "$");
      }
    }
  } finally {
    database.close();
  }
}

function walkJson(value: unknown, visit: (value: unknown, pathLabel: string) => void, pathLabel = "") {
  visit(value, pathLabel);
  if (Array.isArray(value)) {
    value.forEach((item, index) => walkJson(item, visit, `${pathLabel}[${index}]`));
    return;
  }
  if (typeof value !== "object" || value === null) return;
  for (const [key, child] of Object.entries(value)) {
    walkJson(child, visit, pathLabel ? `${pathLabel}.${key}` : key);
  }
}

function assertDesktopDatabaseDoesNotContainRetiredTokens() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const retiredRadiusTokens = new Set([
    "theme.radii.control",
    "theme.radii.card",
    "theme.radii.panel",
    "theme.radii.surface",
    "theme.radii.pill",
    "theme.radii.avatar",
  ]);
  const retiredKeyboardKeys = new Set([
    "backgroundColorToken",
    "backgroundAlpha",
    "keyBackgroundColorToken",
    "specialKeyBackgroundColorToken",
    "pressedKeyBackgroundColorToken",
    "keyTextColorToken",
    "keyBorderColorToken",
    "popoverBackgroundColorToken",
    "specialKeyTextScale",
  ]);

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const componentRows = database
      .prepare("SELECT id, component_type, config_json, metadata_json FROM component_classes")
      .all() as { id: string; component_type: string; config_json: string; metadata_json: string }[];
    for (const row of componentRows) {
      for (const [column, json] of [["config_json", row.config_json], ["metadata_json", row.metadata_json]] as const) {
        if (json.includes("theme.typography.fontFamily")) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.${column} still uses retired typography font-family sentinel "theme.typography.fontFamily"; use "theme"`,
          );
        }
        walkJson(jsonParse(json), (value, pathLabel) => {
          if (typeof value === "string" && retiredRadiusTokens.has(value)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.${column}.${pathLabel} still references retired radius token "${value}"`,
            );
          }
        });
      }
      if (row.component_type === "keyboard") {
        walkJson(jsonParse(row.config_json), (_value, pathLabel) => {
          const key = pathLabel.split(".").pop()?.replace(/\[\d+\]$/, "") ?? "";
          if (retiredKeyboardKeys.has(key)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.config_json.${pathLabel} still uses retired keyboard-owned field "${key}"`,
            );
          }
        });
        walkJson(jsonParse(row.metadata_json), (_value, pathLabel) => {
          const key = pathLabel.split(".").pop()?.replace(/\[\d+\]$/, "") ?? "";
          if (retiredKeyboardKeys.has(key)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.metadata_json.${pathLabel} still uses retired keyboard-owned field "${key}"`,
            );
          }
        });
      }
    }

    const themeRows = database
      .prepare("SELECT id, tokens_json FROM themes")
      .all() as { id: string; tokens_json: string }[];
    for (const row of themeRows) {
      const radii = jsonRecord(jsonRecord(jsonParse(row.tokens_json)).radii);
      for (const retired of ["control", "card", "panel", "surface", "pill", "avatar"]) {
        if (retired in radii) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.tokens_json.radii still contains retired radius key "${retired}"`,
          );
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopRuntimeCollectionsAreConsistent() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, design_preview_json FROM modules UNION ALL SELECT id, design_preview_json FROM component_classes")
      .all() as { id: string; design_preview_json: string }[];
    for (const row of rows) {
      const preview = jsonRecord(jsonParse(row.design_preview_json));
      const collections = jsonArray(preview.collections).map(jsonRecord);
      for (const collection of collections) {
        const jsonKey = typeof collection.jsonKey === "string" ? collection.jsonKey : "";
        const sourceKey = typeof collection.sourceCollectionJsonKey === "string"
          ? collection.sourceCollectionJsonKey
          : "";
        if (!sourceKey) continue;
        const sourceExists = Array.isArray(preview[sourceKey]);
        const sourceItems = jsonArray(preview[sourceKey]).map(jsonRecord);
        if (!sourceExists) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json collection "${jsonKey}" declares missing source "${sourceKey}"`,
          );
        }
        const ids = new Set<string>();
        for (const [index, item] of sourceItems.entries()) {
          const id = typeof item.id === "string" ? item.id : "";
          if (!id) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.${sourceKey}[${index}] must have stable id for sourced runtime overrides`,
            );
          } else if (ids.has(id)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.${sourceKey} has duplicate item id "${id}"`,
            );
          }
          ids.add(id);
        }
        const testValues = jsonRecord(preview.testValues);
        for (const overrideItem of jsonArray(testValues[jsonKey]).map(jsonRecord)) {
          const id = typeof overrideItem.id === "string" ? overrideItem.id : "";
          if (id && !ids.has(id)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.testValues.${jsonKey} has stale override for missing source item "${id}"`,
            );
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopPreviewActionsAreDeclarative() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, design_preview_json FROM modules UNION ALL SELECT id, design_preview_json FROM component_classes")
      .all() as { id: string; design_preview_json: string }[];
    const actionInventory = new Set<string>();
    for (const row of rows) {
      const preview = jsonRecord(jsonParse(row.design_preview_json));
      const assertAction = (action: JsonRecord, path: string, isItemAction: boolean) => {
        const id = typeof action.id === "string" ? action.id : "";
        const label = typeof action.label === "string" ? action.label : "";
        const timeJsonKey = typeof action.timeJsonKey === "string" ? action.timeJsonKey : "";
        const hasDuration = typeof action.durationInputId === "string"
          || typeof action.durationBehaviorTimingInputId === "string"
          || typeof action.durationCollectionJsonKey === "string"
          || typeof action.durationSeconds === "number"
          || typeof action.durationThemeToken === "string"
          || typeof action.durationMotionConfigPath === "string"
          || action.durationOwnerTimeline === true;
        if (!id || !label) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} must declare id and label`,
          );
        }
        if (isItemAction && (!timeJsonKey || typeof action.playInputId !== "string" || !hasDuration)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} item action must declare playInputId, a duration source and timeJsonKey`,
          );
        }
        if (!isItemAction && (typeof action.playInputId !== "string" || !timeJsonKey || !hasDuration)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} must declare playInputId, a duration source and timeJsonKey`,
          );
        }
        if ("durationThemeToken" in action
          && (typeof action.durationThemeToken !== "string" || !action.durationThemeToken.startsWith("theme."))) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path}.durationThemeToken must be a theme.* token`,
          );
        }
        if ("durationBehaviorTimingInputId" in action) {
          const inputId = typeof action.durationBehaviorTimingInputId === "string"
            ? action.durationBehaviorTimingInputId
            : "";
          const inputs = Array.isArray(preview.inputs) ? preview.inputs.map(jsonRecord) : [];
          const input = inputs.find((candidate) => candidate.id === inputId);
          if (!inputId || input?.valueKind !== "BehaviorTiming") {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.${path}.durationBehaviorTimingInputId must reference a BehaviorTiming input`,
            );
          }
        }
        if (action.completionBehavior !== "reset"
          && action.completionBehavior !== "holdFinal") {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path}.completionBehavior must be reset or holdFinal`,
          );
        }
        if (("durationCollectionJsonKey" in action || "durationItemNumberKeys" in action) && !timeJsonKey) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} duration action must declare timeJsonKey`,
          );
        }
        if ("durationCollectionJsonKey" in action && !Array.isArray(action.durationItemNumberKeys)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} collection duration action must declare durationItemNumberKeys[]`,
          );
        }
      };
      for (const [index, action] of jsonArray(preview.actions).map(jsonRecord).entries()) {
        assertAction(action, `actions[${index}]`, false);
        actionInventory.add(`root:${String(preview.componentType ?? "module")}:${String(action.id ?? "")}`);
      }
      for (const [collectionIndex, collection] of jsonArray(preview.collections).map(jsonRecord).entries()) {
        for (const [actionIndex, action] of jsonArray(collection.itemActions).map(jsonRecord).entries()) {
          assertAction(action, `collections[${collectionIndex}].itemActions[${actionIndex}]`, true);
          const values = jsonArray(action.visibleWhenItemValues).join(",");
          actionInventory.add(
            `item:${String(collection.jsonKey ?? "")}:${String(action.id ?? "")}:${String(action.visibleWhenItemJsonKey ?? "")}:${values}`,
          );
        }
      }
    }
    for (const required of [
      "root:module:playConversation",
      "root:keyboard:in",
      "root:audio:play",
      "root:media:play",
      "root:media:fullScreen",
      "root:bubble:writeOn",
      "root:bubble:play",
      "root:bubble:fullScreen",
      "item:messages:playVideo:mediaType:video",
      "item:messages:playAudio:mediaType:audio",
    ]) {
      if (!actionInventory.has(required)) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `required declarative Test Values action is missing: ${required}`,
        );
      }
    }
  } finally {
    database.close();
  }
}

function assertComponentEditorLayoutsUseKnownFields() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const catalog = readFileSync(
    path.join(root, "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs"),
    "utf8",
  );
  const knownFields = new Set(
    [...catalog.matchAll(/^\s*\["([^"]+)"\]\s*=/gm)].map((match) => match[1]),
  );
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database.prepare(
      "SELECT record_class_id, layout_json FROM editor_layouts WHERE record_class_id LIKE 'component.%'",
    ).all() as { record_class_id: string; layout_json: string }[];
    for (const row of rows) {
      const layout = jsonRecord(jsonParse(row.layout_json));
      for (const card of jsonArray(layout.cards).map(jsonRecord)) {
        for (const group of jsonArray(card.groups).map(jsonRecord)) {
          for (const field of jsonArray(group.fields).map(jsonRecord)) {
            const fieldId = typeof field.id === "string" ? field.id : "";
            if (fieldId.startsWith("component.") && !knownFields.has(fieldId)) {
              addViolation(
                "data/desktop-editor-spike.sqlite",
                `${row.record_class_id} editor layout references unknown field ${fieldId}`,
              );
            }
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopConversationPreviewDoesNotUseLegacyMessageKeys() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const legacyMessageKeys = new Set([
    "message1Text",
    "message2Text",
    "message3Text",
    "message2StatusState",
    "message2StatusText",
  ]);
  const perMessageTimingKeys = new Set([
    "bubbleRevealMode",
    "textInputVisible",
    "keyboardVisible",
    "textReveal",
    "writeOnDurationFrames",
  ]);
  const requiredMessageTimingKeys = new Set([
    "writeOnTiming",
    "postWriteOnHoldFrames",
  ]);

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, design_preview_json FROM modules UNION ALL SELECT id, design_preview_json FROM component_classes")
      .all() as { id: string; design_preview_json: string }[];
    for (const row of rows) {
      const preview = jsonRecord(jsonParse(row.design_preview_json));
      for (const key of legacyMessageKeys) {
        if (key in preview) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json still uses legacy direct conversation preview key "${key}"`,
          );
        }
      }
      for (const [index, message] of jsonArray(preview.messages).map(jsonRecord).entries()) {
        for (const key of requiredMessageTimingKeys) {
          if (!(key in message)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.messages[${index}] is missing per-message timing key "${key}"`,
            );
          }
        }
        for (const key of perMessageTimingKeys) {
          if (key in message) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.messages[${index}] still uses per-message timing key "${key}"`,
            );
          }
        }
      }
    }

    const instanceRows = database
      .prepare("SELECT id, content_json FROM module_instances")
      .all() as { id: string; content_json: string }[];
    for (const row of instanceRows) {
      const content = jsonRecord(jsonParse(row.content_json));
      for (const [index, message] of jsonArray(content.messages).map(jsonRecord).entries()) {
        for (const key of requiredMessageTimingKeys) {
          if (!(key in message)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.content_json.messages[${index}] is missing per-message timing key "${key}"`,
            );
          }
        }
        for (const key of perMessageTimingKeys) {
          if (key in message) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.content_json.messages[${index}] still uses per-message timing key "${key}"`,
            );
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertGenericTextWrappingIsConservative() {
  const sample = "Mensaje de prueba 😎😍 en dos líneas";
  const fontSize = 18;
  const maxWidth = 250;
  const lines = approximateWrappedTextLines(sample, fontSize, maxWidth);
  if (lines.length < 2) {
    addViolation(
      "src/desktop-preview/previewTextHelpers.ts",
      "generic text wrapping must wrap latin text with emoji before it reaches the frame edge",
    );
    return;
  }

  for (const [index, line] of lines.entries()) {
    if (approximateTextWidth(line, fontSize) > maxWidth) {
      addViolation(
        "src/desktop-preview/previewTextHelpers.ts",
        `generic text wrapping produced over-wide line ${index + 1}: "${line}"`,
      );
    }
  }
}

assertDesktopDatabaseDoesNotContainRetiredTokens();
assertDesktopRuntimeCollectionsAreConsistent();
assertDesktopPreviewActionsAreDeclarative();
assertComponentEditorLayoutsUseKnownFields();
assertDesktopConversationPreviewDoesNotUseLegacyMessageKeys();
assertGenericTextWrappingIsConservative();
for (const filePath of walkFiles(previewRoot)) {
  const file = relative(filePath);
  if (file !== "src/desktop-preview/previewTextHelpers.ts"
      && readFileSync(filePath, "utf8").includes("approximateText")) {
    addViolation(file, "production preview layout must use resolved production-font measurement");
  }
}
assertMatches(
  "src/desktop-preview/conversationModuleRenderable.ts",
  /childRenderable\(\s*payload,[\s\S]*?"keyboard"[\s\S]*?\{\s*text: composer\.text,[\s\S]*?currentCharacter: composer\.currentCharacter,[\s\S]*?motionElapsedMs,/,
  "Conversation must pass shared module motionElapsedMs to Keyboard runtime inputs",
);
for (const legacyConversationKey of [
  "message1Text",
  "message2Text",
  "message3Text",
  "message2StatusState",
  "message2StatusText",
  "instanceMessages",
]) {
  assertDoesNotContain(
    "src/desktop-preview/conversationModuleRenderable.ts",
    legacyConversationKey,
    `Conversation renderer must consume the canonical messages[] runtime collection instead of legacy key "${legacyConversationKey}"`,
  );
}

function componentLayoutFieldIds(source: string) {
  const ids = new Set<string>();
  const pattern = /\{\s*"id"\s*:\s*"(component\.[^"]+)"/g;
  let match: RegExpExecArray | null;
  while ((match = pattern.exec(source)) !== null) {
    ids.add(match[1] ?? "");
  }
  return ids;
}

function componentFieldCatalogIds(source: string) {
  const ids = new Set<string>();
  const pattern = /\["(component\.[^"]+)"\]\s*=/g;
  let match: RegExpExecArray | null;
  while ((match = pattern.exec(source)) !== null) {
    ids.add(match[1] ?? "");
  }
  return ids;
}

if (existsSync(path.join(previewRoot, "webPreviewBridge.ts"))) {
  addViolation(
    "src/desktop-preview/webPreviewBridge.ts",
    "central web preview bridge must not exist",
  );
}

const componentLayoutPath =
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs";
const componentFieldCatalogPath =
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs";
if (
  existsSync(path.join(root, componentLayoutPath)) &&
  existsSync(path.join(root, componentFieldCatalogPath))
) {
  const layoutIds = componentLayoutFieldIds(readText(componentLayoutPath));
  const catalogIds = componentFieldCatalogIds(readText(componentFieldCatalogPath));
  for (const fieldId of layoutIds) {
    if (!catalogIds.has(fieldId)) {
      addViolation(
        componentLayoutPath,
        `layout references component field "${fieldId}" without a ComponentClassFieldCatalog entry`,
      );
    }
  }
}

for (const removedLegacyPath of [
  "src/debug-ui",
  "src/debug-server",
  "src/electron",
  "src/remotion",
  "src/visual/adapters/react",
  "src/visual/renderable/helpers.ts",
  "src/visual/layout",
  "src/visual/modules",
  "src/visual/validation",
  "src/domain",
  "src/persistence",
  "src/icon-themes/importDevelopmentIconTheme.ts",
  "src/desktop-preview/systemBarComponentContract.ts",
  "src/desktop-preview/systemBarPreviewResolver.ts",
  "src/desktop-preview/systemBarRenderables.ts",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassNormalization.cs",
  "index.html",
  "remotion.config.ts",
  "vite.config.ts",
]) {
  if (existsSync(path.join(root, removedLegacyPath))) {
    addViolation(
      removedLegacyPath,
      "legacy React/debug/remotion render route must not be restored in this repository",
    );
  }
}

for (const filePath of walkFiles(previewRoot)) {
  const source = readFileSync(filePath, "utf8");
  for (const legacyImport of ["../domain/", "../persistence/"]) {
    if (source.includes(legacyImport)) {
      addViolation(
        relative(filePath),
        `desktop preview must not import active behavior from historical ${legacyImport} code`,
      );
    }
  }
}

assertNoTerms("src/desktop-preview/renderDesignPreviewHtml.tsx", [
  "label",
  "avatar",
  "buttonIcon",
  "audio",
  "textBox",
  "textInputBar",
  "keyboard",
  "media",
  "video",
  "statusBar",
  "navigationBar",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
  "component_text_box",
  "component_text_input",
  "component_keyboard",
  "component_media",
  "component_video",
  "status_bar",
  "navigation_bar",
]);
assertDoesNotContain(
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
  "../visual/adapters/react/RenderableReactAdapter.js",
  "desktop design preview must use the clean desktop HTML adapter, not the legacy React renderable adapter",
);
for (const legacyScriptTerm of [
  "validate:examples",
  "validate:resolver",
  "validate:sqlite",
  "db:reset",
  "db:seed",
  "audit:current-model",
  "db:normalize-current-model",
]) {
  assertPackageScriptDoesNotContain(
    "test",
    legacyScriptTerm,
    `desktop test path must not call legacy script ${legacyScriptTerm}`,
  );
}
assertNoTerms("src/desktop-preview/DesktopRenderableHtmlAdapter.tsx", [
  "component_preview_unsupported",
  "design_preview_surface",
  "icon_token",
  "message_bubble",
  "audio_message",
  "button_icon",
  "status_bar",
  "status_bar_item",
  "navigation_bar",
  "navigation_bar_item",
  "keyboard_key",
  "text_input_bar_",
  "video_message",
  "status_indicators",
]);
assertDoesNotContain(
  "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
  "./previewColorHelpers.js",
  "desktop HTML adapter must not import token/color resolution helpers",
);
assertDoesNotContain(
  "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
  "iconTokenLabel",
  "desktop HTML adapter icon fallback must not use token-specific naming",
);
assertContains(
  "src/visual/renderable/types.ts",
  "export interface RenderableMetadata",
  "renderable metadata must stay explicitly typed",
);
assertContains(
  "src/visual/renderable/types.ts",
  "export const renderableNodeTypes",
  "renderable primitive list must stay explicit and shared",
);
assertContains(
  "src/visual/renderable/types.ts",
  "export type RenderableNodeType = (typeof renderableNodeTypes)[number];",
  "renderable node type must be derived from the shared primitive list",
);
assertContains(
  "src/visual/renderable/types.ts",
  "type: RenderableNodeType;",
  "renderable node type must not accept arbitrary strings",
);
assertDoesNotContain(
  "src/visual/renderable/types.ts",
  "metadata?: Record<string, unknown>",
  "renderable nodes must not expose arbitrary metadata",
);
assertContains(
  "src/visual/renderable/schema.ts",
  "const RenderableMetadataSchema",
  "renderable schema must validate metadata through an explicit schema",
);
assertDoesNotContain(
  "src/visual/renderable/schema.ts",
  "metadata: z.record(z.string(), z.unknown()).optional()",
  "renderable schema must not accept arbitrary metadata",
);
assertContains(
  "src/visual/renderable/schema.ts",
  "const RenderableNodeTypeSchema = z.enum(renderableNodeTypes);",
  "renderable schema must validate node type through the shared primitive list",
);
assertContains(
  "src/visual/renderable/schema.ts",
  "type: RenderableNodeTypeSchema,",
  "renderable schema must validate node type with the explicit primitive enum",
);
assertStringSetEquals(
  "src/visual/renderable/types.ts",
  quotedStringsFromBlock(
    "src/visual/renderable/types.ts",
    /export const renderableNodeTypes\s*=\s*\[([\s\S]*?)\]\s*as const;/,
    "renderable primitive list must be parseable",
  ),
  desktopPreviewPaintNodeTypes,
  "shared renderable primitive list",
);

assertNoTerms("src/desktop-preview/componentRenderableCommon.ts", [
  "label",
  "avatar",
  "buttonIcon",
  "audio",
  "textBox",
  "textInputBar",
  "keyboard",
  "media",
  "video",
  "statusBar",
  "navigationBar",
  "waveform",
  "badge",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
  "component_text_box",
  "component_text_input",
  "component_keyboard",
  "component_media",
  "component_video",
]);
assertContains(
  "src/desktop-preview/componentResolverCommon.ts",
  "from \"./previewComponentContracts.js\"",
  "component resolver common must re-export shared contracts instead of defining them locally",
);
assertContains(
  "src/desktop-preview/componentResolverCommon.ts",
  "from \"./previewValueHelpers.js\"",
  "component resolver common must re-export shared value helpers instead of defining them locally",
);
assertContains(
  "src/desktop-preview/componentResolverCommon.ts",
  "from \"./previewJsonHelpers.js\"",
  "component resolver common must re-export shared JSON helpers instead of defining them locally",
);

const centralCommonFiles = new Set([
  "src/desktop-preview/componentRenderableCommon.ts",
  "src/desktop-preview/componentResolverCommon.ts",
  "src/desktop-preview/designPreviewPayload.ts",
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
]);

for (const relativePath of centralCommonFiles) {
  const imports = importTargets(readText(relativePath));
  for (const target of imports) {
    if (
      /Component(Resolver|Renderable)\.js$/.test(target) ||
      /systemBar.*\.js$/.test(target)
    ) {
      addViolation(relativePath, `central/common file imports concrete preview module "${target}"`);
    }
  }
}

const registryFiles = new Set([
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "src/desktop-preview/designPreviewRenderableRegistry.ts",
  "src/desktop-preview/moduleRenderableRegistry.ts",
]);

const sharedPreviewHelperFiles = new Set([
  "src/desktop-preview/previewColorHelpers.ts",
  "src/desktop-preview/previewComponentContracts.ts",
  "src/desktop-preview/previewGeometryHelpers.ts",
  "src/desktop-preview/previewJsonHelpers.ts",
  "src/desktop-preview/previewSurfaceHelpers.ts",
  "src/desktop-preview/previewValueHelpers.ts",
]);

const filesystemAllowedPreviewFiles = new Set([
  "src/desktop-preview/previewAssetResolver.ts",
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
  "src/desktop-preview/renderPreviewRasterServer.ts",
]);

const manifestEntries = Object.entries(desktopPreviewComponents);
const moduleManifestEntries = Object.entries(desktopPreviewModules);

function moduleFile(entry: DesktopPreviewComponentManifestEntry, kind: "resolver" | "renderable") {
  return `src/desktop-preview/${entry[kind].replace(/^\.\//, "")}.ts`;
}

function moduleImport(entry: DesktopPreviewComponentManifestEntry, kind: "resolver" | "renderable") {
  return `${entry[kind]}.js`;
}

function moduleEntrypointFile(entry: DesktopPreviewModuleManifestEntry, kind: "resolver" | "renderable") {
  return `src/desktop-preview/${entry[kind].replace(/^\.\//, "")}.ts`;
}

function moduleEntrypointImport(entry: DesktopPreviewModuleManifestEntry, kind: "resolver" | "renderable") {
  return `${entry[kind]}.js`;
}

for (const [componentClass, entry] of manifestEntries) {
  for (const kind of ["contract", "resolver", "renderable"] as const) {
    const filePath = path.join(previewRoot, `${entry[kind].replace(/^\.\//, "")}.ts`);
    if (!existsSync(filePath)) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `manifest entry "${componentClass}" points to missing ${kind} file "${entry[kind]}"`,
      );
    }
  }

  for (const child of entry.embeds) {
    if (!desktopPreviewComponents[child]) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `manifest entry "${componentClass}" embeds unknown component "${child}"`,
      );
    }
  }
}

for (const [moduleClass, entry] of moduleManifestEntries) {
  for (const kind of ["resolver", "renderable"] as const) {
    const filePath = path.join(previewRoot, `${entry[kind].replace(/^\.\//, "")}.ts`);
    if (!existsSync(filePath)) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `module manifest entry "${moduleClass}" points to missing ${kind} file "${entry[kind]}"`,
      );
    }
  }
  for (const child of entry.embeds) {
    if (!desktopPreviewComponents[child]) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `module manifest entry "${moduleClass}" embeds unknown component "${child}"`,
      );
    }
  }
}

for (const [, entry] of manifestEntries) {
  const renderableFile = moduleFile(entry, "renderable");
  assertDoesNotContain(
    renderableFile,
    "componentType:",
    "component renderables must not emit component identity metadata into the final paint tree",
  );
  assertDoesNotContain(
    renderableFile,
    "systemBarType:",
    "system component renderables must not emit system-bar identity metadata into the final paint tree",
  );
}
for (const [, entry] of manifestEntries) {
  const renderableFile = moduleFile(entry, "renderable");
  for (const legacyTerm of [
    "message_bubble",
    "audio_message",
    "button_icon",
    "text_input_bar",
    "keyboard_key",
    "video_message",
  ]) {
    assertDoesNotContain(
      renderableFile,
      legacyTerm,
      `component renderables must not emit legacy desktop/runtime term "${legacyTerm}"`,
    );
  }
}

const routedComponentClasses = new Set(Object.keys(componentRenderableFactories));
for (const [componentClass, entry] of manifestEntries) {
  if (!routedComponentClasses.has(componentClass)) {
    addViolation(
      "src/desktop-preview/componentClassRenderableRegistry.ts",
      `component class "${componentClass}" is missing from component renderable registry`,
    );
  }
}

const routedModuleClasses = new Set(Object.keys(moduleRenderableFactories));
for (const [moduleClass] of moduleManifestEntries) {
  if (!routedModuleClasses.has(moduleClass)) {
    addViolation(
      "src/desktop-preview/moduleRenderableRegistry.ts",
      `module class "${moduleClass}" is missing from module renderable registry`,
    );
  }
}
for (const moduleClass of routedModuleClasses) {
  if (!desktopPreviewModules[moduleClass]) {
    addViolation(
      "src/desktop-preview/moduleRenderableRegistry.ts",
      `module renderable registry contains unknown class "${moduleClass}"`,
    );
  }
}

for (const [componentClass, entry] of manifestEntries) {
  if (!["component", "atom", "system"].includes(entry.category)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `component "${componentClass}" has unsupported category "${entry.category}"`,
    );
  }
  if (!["functional", "structural"].includes(entry.migrationStatus)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `component "${componentClass}" has unsupported migration status "${entry.migrationStatus}"`,
    );
  }
  if (new Set(entry.embeds).size !== entry.embeds.length) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `component "${componentClass}" has duplicate embedded dependencies`,
    );
  }
}
for (const [moduleClass, entry] of moduleManifestEntries) {
  if (!entry.label.trim()) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `module "${moduleClass}" has no label`,
    );
  }
  if (new Set(entry.embeds).size !== entry.embeds.length) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `module "${moduleClass}" has duplicate embedded dependencies`,
    );
  }
}

for (const registryPath of registryFiles) {
  assertNoTerms(registryPath, [
    "applyRuntimeInputForwarding",
    "runtimeContractJson:",
    "designPreviewJson:",
    "backgroundColor:",
    "previewFrame.screen",
  ]);
}
assertContains(
  "src/desktop-preview/componentRenderableBoundary.ts",
  "resolveRenderablePayload(payload)",
  "component payload preparation must happen before component registry routing",
);
assertContains(
  "src/desktop-preview/moduleRenderableBoundary.ts",
  "resolveRenderablePayload(payload)",
  "module payload preparation must happen before module registry routing",
);
assertContains(
  "src/desktop-preview/renderablePayloadBoundary.ts",
  "applyRuntimeInputForwarding(payload)",
  "runtime forwarding must belong to the payload boundary",
);
for (const recursivePayloadPath of [
  "src/desktop-preview/previewPayloadHelpers.ts",
  "src/desktop-preview/lockScreenModuleRenderable.ts",
  "src/desktop-preview/conversationModuleRenderable.ts",
  "src/desktop-preview/notificationsComponentResolver.ts",
]) {
  assertDoesNotContain(
    recursivePayloadPath,
    "runtimeContractJson:",
    "embedded component composition must preserve the complete temporal owner envelope",
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "var runtimePreviewJson = runtimePreview.ToJsonString();",
  "desktop payloads must materialize one explicit effective temporal owner envelope",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "RuntimeContractJson = preview.ToJsonString()",
  "isolated Design Test Values must update the effective temporal owner envelope without persistence",
);
for (const componentClass of routedComponentClasses) {
  if (!desktopPreviewComponents[componentClass]) {
    addViolation(
      "src/desktop-preview/componentClassRenderableRegistry.ts",
      `component renderable registry contains unknown class "${componentClass}"`,
    );
  }
}

const allowedComponentImports: Record<string, Set<string>> = {};
const desktopPreviewPaintTreeSourceFiles = new Set([
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "src/desktop-preview/designPreviewRenderableRegistry.ts",
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
]);
const manifestComponentEntrypointImports = new Set<string>();
for (const [, entry] of manifestEntries) {
  desktopPreviewPaintTreeSourceFiles.add(moduleFile(entry, "renderable"));
  manifestComponentEntrypointImports.add(moduleImport(entry, "resolver"));
  manifestComponentEntrypointImports.add(moduleImport(entry, "renderable"));
  for (const kind of ["resolver", "renderable"] as const) {
    const filePath = moduleFile(entry, kind);
    const allowed = allowedComponentImports[filePath] ?? new Set<string>();
    for (const child of entry.embeds) {
      const childEntry = desktopPreviewComponents[child];
      if (childEntry) {
        allowed.add(moduleImport(childEntry, kind));
      }
    }
    allowedComponentImports[filePath] = allowed;
  }
}

const manifestModuleEntrypointImports = new Set<string>();
const allowedOwningModuleImports: Record<string, Set<string>> = {};
for (const [, entry] of moduleManifestEntries) {
  desktopPreviewPaintTreeSourceFiles.add(moduleEntrypointFile(entry, "renderable"));
  manifestModuleEntrypointImports.add(moduleEntrypointImport(entry, "resolver"));
  manifestModuleEntrypointImports.add(moduleEntrypointImport(entry, "renderable"));
  const ownerFiles = [
    moduleEntrypointFile(entry, "resolver"),
    moduleEntrypointFile(entry, "renderable"),
  ];
  const ownerImports = new Set([
    moduleEntrypointImport(entry, "resolver"),
    moduleEntrypointImport(entry, "renderable"),
  ]);
  for (const ownerFile of ownerFiles) allowedOwningModuleImports[ownerFile] = ownerImports;
  for (const kind of ["resolver", "renderable"] as const) {
    const filePath = moduleEntrypointFile(entry, kind);
    const allowed = allowedComponentImports[filePath] ?? new Set<string>();
    for (const child of entry.embeds) {
      const childEntry = desktopPreviewComponents[child];
      if (childEntry) {
        allowed.add(moduleImport(childEntry, "resolver"));
        allowed.add(moduleImport(childEntry, "renderable"));
      }
    }
    allowedComponentImports[filePath] = allowed;
  }
}

for (const filePath of walkFiles(previewRoot)) {
  const relativePath = relative(filePath);
  const source = readFileSync(filePath, "utf8");
  if (!sharedPreviewHelperFiles.has(relativePath)) {
    for (const helperName of [
      "applyNeutralTint",
      "asRecord",
      "colorForMode",
      "cssColorWithAlpha",
      "numberValue",
      "parseObject",
      "renderScale",
      "requiredAlpha",
      "requiredBoolean",
      "requiredNumber",
      "requiredNumberPair",
      "requiredNumberValue",
      "requiredPlacement",
      "requiredRecord",
      "requiredString",
      "resolvePaletteColor",
      "resolveSurfaceStyle",
      "selectedColor",
      "stringValue",
      "tokenValueForMode",
      "variants",
    ]) {
      if (new RegExp(`function\\s+${helperName}\\s*\\(`).test(source)) {
        addViolation(
          relativePath,
          `shared preview helper "${helperName}" must be imported from common helpers, not redefined locally`,
        );
      }
    }
  }

  if (desktopPreviewPaintTreeSourceFiles.has(relativePath) && /role:\s*["']/.test(source)) {
    addViolation(
      relativePath,
      "desktop preview paint tree nodes must not emit role metadata; use generic node types and marks only",
    );
  }
  if (desktopPreviewPaintTreeSourceFiles.has(relativePath) && /metadata:\s*\{\s*\.\.\./.test(source)) {
    addViolation(
      relativePath,
      "desktop preview paint tree metadata must not spread component contracts into final nodes",
    );
  }
  if (desktopPreviewPaintTreeSourceFiles.has(relativePath)) {
    const metadataPattern = /metadata:\s*\{[\s\S]*?\}/g;
    let metadataMatch: RegExpExecArray | null;
    while ((metadataMatch = metadataPattern.exec(source)) !== null) {
      const metadataSource = metadataMatch[0];
      for (const forbiddenMetadataKey of [
        "charging",
        "componentType",
        "kind",
        "label",
        "order",
        "route",
        "value",
        "zone",
      ]) {
        if (new RegExp(`\\b${forbiddenMetadataKey}\\s*:`).test(metadataSource)) {
          addViolation(
            relativePath,
            `desktop preview paint tree metadata must not contain component semantic key "${forbiddenMetadataKey}"`,
          );
        }
      }
    }
  }

  const nodeTypePattern = /type:\s*["']([^"']+)["']/g;
  let nodeTypeMatch: RegExpExecArray | null;
  while ((nodeTypeMatch = nodeTypePattern.exec(source)) !== null) {
    const nodeType = nodeTypeMatch[1] ?? "";
    if (nodeType.startsWith("component_")) {
      addViolation(
        relativePath,
        `component-specific renderable node type "${nodeType}" must be a generic primitive type`,
      );
    }
    if (forbiddenDesktopPreviewNodeTypes.has(nodeType)) {
      addViolation(
        relativePath,
        `desktop preview semantic node type "${nodeType}" must be emitted as generic primitives`,
      );
    }
    if (
      desktopPreviewPaintTreeSourceFiles.has(relativePath)
      && !desktopPreviewPaintNodeTypes.has(nodeType)
    ) {
      addViolation(
        relativePath,
        `desktop preview paint node type "${nodeType}" is not in the generic primitive allowlist`,
      );
    }
  }
  if (registryFiles.has(relativePath)) continue;

  const imports = importTargets(source);
  for (const target of imports) {
    if (
      (target === "node:fs" || target === "node:fs/promises")
      && !filesystemAllowedPreviewFiles.has(relativePath)
    ) {
      addViolation(
        relativePath,
        `filesystem access "${target}" belongs in preview asset/request boundary helpers only`,
      );
    }
  }

  for (const target of imports) {
    const isConcreteComponentImport = manifestComponentEntrypointImports.has(target);
    if (!isConcreteComponentImport) continue;

    const allowed = allowedComponentImports[relativePath];
    if (!allowed?.has(target)) {
      addViolation(
        relativePath,
        `concrete component import "${target}" is not a declared embedded-component dependency`,
      );
    }
  }

  for (const target of imports) {
    if (!manifestModuleEntrypointImports.has(target)) continue;
    if (relativePath !== "src/desktop-preview/moduleRenderableRegistry.ts"
        && !allowedOwningModuleImports[relativePath]?.has(target)) {
      addViolation(
        relativePath,
        `concrete module entrypoint import "${target}" is allowed only in the module registry`,
      );
    }
  }
}

const payloadSource = readText("src/desktop-preview/designPreviewPayload.ts");
{
  const adapterSource = readText("src/desktop-preview/DesktopRenderableHtmlAdapter.tsx");
  assertStringSetEquals(
    "src/visual/renderable/types.ts",
    new Set(renderableNodeTypes),
    desktopPreviewPaintNodeTypes,
    "runtime renderable primitive list",
  );
  if (!adapterSource.includes("const supportedNodeTypes = new Set(renderableNodeTypes);")) {
    addViolation(
      "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
      "desktop renderer must use the shared renderable primitive list",
    );
  }
}
if (payloadSource.includes("device:")) {
  addViolation(
    "src/desktop-preview/designPreviewPayload.ts",
    "design preview payload must expose previewFrame, not device",
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "device =",
  "web design preview renderer must serialize previewFrame, not a device object",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "previewFrame = new",
  "web design preview renderer must provide previewFrame geometry to the web renderer",
);
for (const componentType of Object.keys(desktopPreviewComponents)) {
  assertDoesNotContain(
    "src/visual/renderable/rasterFramePlan.ts",
    componentType,
    `generic raster frame planning must not contain component-specific knowledge (${componentType})`,
  );
}
assertContains(
  "src/visual/renderable/rasterFramePlan.ts",
  'kind: "hold"',
  "raster frame planning must represent visually unchanged frames without duplicate bitmaps",
);
assertContains(
  "src/desktop-preview/renderDesignPreviewHtmlServer.ts",
  "assets: compact.assets",
  "the persistent renderer must return compact HTML with an incremental asset manifest",
);
assertContains(
  "src/desktop-preview/renderPreviewRasterServer.ts",
  'format: "webp"',
  "the shared Chromium raster boundary must support WebP preview frames",
);
assertContains(
  "src/desktop-preview/renderPreviewRasterServer.ts",
  'type: "png"',
  "the shared Chromium raster boundary must preserve a PNG lossless GFX path",
);
assertContains(
  "src/visual/renderable/rasterFramePlan.ts",
  'kind: "tiles"',
  "raster frame planning must represent localized visual changes as tiles",
);
if (payloadSource.includes('"statusBar"') || payloadSource.includes('"navigationBar"')) {
  addViolation(
    "src/desktop-preview/designPreviewPayload.ts",
    "status/navigation bars must route as componentClass, not top-level preview kinds",
  );
}

const desktopPersistenceDataPaths = readdirSync(path.join(root, "spikes/desktop-editor-shell/Data"))
  .filter((entry) => entry.endsWith(".cs"))
  .map((entry) => `spikes/desktop-editor-shell/Data/${entry}`);
for (const relativePath of desktopPersistenceDataPaths) {
  const source = readText(relativePath);
  if (/\b(?:Ensure|Normalize|Retire|Migrate)\w*\s*\(\s*SqliteConnection\b/.test(source)) {
    addViolation(
      relativePath,
      "normal runtime data code must not retain database-wide compatibility or repair routines",
    );
  }
  if (/\bSeed\w*IfEmpty\s*\(/.test(source)) {
    addViolation(
      relativePath,
      "normal runtime data code must not seed partially missing current data",
    );
  }
}
assertContains(
  "spikes/desktop-editor-shell/Data/SqliteProjectContext.cs",
  "Mode = SqliteOpenMode.ReadOnly",
  "the shared SQLite context must open existing databases read-only until strict current validation succeeds",
);
for (const [contractType, implementationType] of [
  ["IEditorLayoutRepository", "EditorLayoutRepository"],
  ["IProjectEpisodeRepository", "ProjectEpisodeRepository"],
  ["IRenderPresetRepository", "RenderPresetRepository"],
  ["IPaletteRepository", "PaletteRepository"],
  ["IDeviceRepository", "DeviceRepository"],
  ["IActorRepository", "ActorRepository"],
  ["IThemeRepository", "ThemeRepository"],
  ["IProductionFontRepository", "ProductionFontRepository"],
  ["IIconThemeRepository", "IconThemeRepository"],
  ["IAppModuleRepository", "AppModuleRepository"],
  ["IModuleInstanceThemeContextService", "ModuleInstanceThemeContextService"],
  ["IReferenceUsageService", "ReferenceUsageService"],
] as const) {
  assertContains(
    "spikes/desktop-editor-shell/Data/PersistenceContracts.cs",
    `interface ${contractType}`,
    `persistence contract ${contractType} must remain explicit`,
  );
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
    `new ${implementationType}(_context)`,
    `SpikeDatabase must delegate through ${implementationType}`,
  );
}
for (const facadePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.EditorLayouts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectsEpisodes.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RenderPresets.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Palette.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Devices.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Actors.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProductionFonts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemeSearch.cs",
]) {
  for (const forbiddenPersistenceDetail of [".CreateCommand()", "SELECT ", "INSERT INTO ", "UPDATE ", "DELETE FROM "]) {
    assertDoesNotContain(
      facadePath,
      forbiddenPersistenceDetail,
      `${facadePath} must delegate instead of owning extracted repository SQL or row mapping`,
    );
  }
}
for (const [repositoryPath, ownedTable] of [
  ["spikes/desktop-editor-shell/Data/EditorLayoutRepository.cs", "editor_layouts"],
  ["spikes/desktop-editor-shell/Data/ProjectEpisodeRepository.cs", "episodes"],
  ["spikes/desktop-editor-shell/Data/RenderPresetRepository.cs", "render_presets"],
  ["spikes/desktop-editor-shell/Data/PaletteRepository.cs", "palette_colors"],
  ["spikes/desktop-editor-shell/Data/DeviceRepository.cs", "devices"],
  ["spikes/desktop-editor-shell/Data/ActorRepository.cs", "actors"],
  ["spikes/desktop-editor-shell/Data/ThemeRepository.cs", "themes"],
  ["spikes/desktop-editor-shell/Data/ProductionFontRepository.cs", "production_fonts"],
  ["spikes/desktop-editor-shell/Data/IconThemeRepository.cs", "icon_themes"],
  ["spikes/desktop-editor-shell/Data/AppModuleRepository.cs", "apps"],
] as const) {
  assertContains(
    repositoryPath,
    ownedTable,
    `${repositoryPath} must own its declared persistence slice`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "SqliteProjectContext",
  "MainWindow must not receive persistence infrastructure or repositories",
);
for (const ownedResourceTable of ["palette_colors", "devices", "actors", "production_fonts", "icon_themes"]) {
  for (const sqlOperation of ["INSERT INTO", "UPDATE", "DELETE FROM"]) {
    assertDoesNotContain(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
      `${sqlOperation} ${ownedResourceTable}`,
      `tree orchestration must delegate ${ownedResourceTable} lifecycle writes to its repository`,
    );
  }
}
for (const sqlOperation of ["INSERT INTO", "UPDATE", "DELETE FROM"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
    `${sqlOperation} themes`,
    "tree orchestration must delegate Theme lifecycle writes to ThemeRepository",
  );
}
for (const resourceRepositoryPath of [
  "spikes/desktop-editor-shell/Data/PaletteRepository.cs",
  "spikes/desktop-editor-shell/Data/DeviceRepository.cs",
  "spikes/desktop-editor-shell/Data/ActorRepository.cs",
  "spikes/desktop-editor-shell/Data/ThemeRepository.cs",
  "spikes/desktop-editor-shell/Data/ProductionFontRepository.cs",
  "spikes/desktop-editor-shell/Data/IconThemeRepository.cs",
]) {
  assertDoesNotContain(
    resourceRepositoryPath,
    "MainWindow",
    `${resourceRepositoryPath} must not import or construct the desktop shell`,
  );
  assertDoesNotContain(
    resourceRepositoryPath,
    "LIKE $needle",
    `${resourceRepositoryPath} must not copy the broad inferred Usage scanner`,
  );
  assertDoesNotContain(
    resourceRepositoryPath,
    "ReferenceSearchTables",
    `${resourceRepositoryPath} must not infer Usage from text-column discovery`,
  );
}
for (const forbiddenProductionFontRepositoryConcern of [
  "System.IO",
  "File.",
  "Directory.",
  "ProductionFontFace",
  "ProjectTreeNode",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ProductionFontRepository.cs",
    forbiddenProductionFontRepositoryConcern,
    `ProductionFontRepository must not own filesystem, Preview or tree concern ${forbiddenProductionFontRepositoryConcern}`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/42_production_font_persistence_contract.md",
  "AGENTS must require the Production Font persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "42_production_font_persistence_contract.md",
  "the active architecture index must include the Production Font persistence contract",
);
for (const forbiddenIconThemeRepositoryConcern of [
  "System.IO",
  "File.",
  "Directory.",
  "Svg",
  "Process",
  "ProjectTreeNode",
  "IconThemeToken",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/IconThemeRepository.cs",
    forbiddenIconThemeRepositoryConcern,
    `IconThemeRepository must not own asset, Preview or tree concern ${forbiddenIconThemeRepositoryConcern}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "has no explicit SVG file reference",
  "Icon Theme token reads must fail when the current mapping has no explicit file",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "tokenObject[\"file\"] = file",
  "Icon Theme token reads must not repair a missing file reference",
);
assertContains(
  "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
  "modules",
  "AppModuleRepository must own both App and Module definition tables",
);
for (const [definitionFacadePath, forbiddenDefinitionPersistence] of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "UPDATE apps"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "UPDATE modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "FROM apps"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "FROM modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs", "UPDATE modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs", "UPDATE modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "UPDATE apps"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "UPDATE modules"],
] as const) {
  assertDoesNotContain(
    definitionFacadePath,
    forbiddenDefinitionPersistence,
    `${definitionFacadePath} must delegate App/Module definition persistence instead of retaining ${forbiddenDefinitionPersistence}`,
  );
}
for (const forbiddenAppModuleRepositoryConcern of [
  "MainWindow",
  "ProjectTreeNode",
  "RuntimeInputForwardingContract",
  "ModuleInstance",
  "Renderable",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
    forbiddenAppModuleRepositoryConcern,
    `AppModuleRepository must not own UI, Runtime or render concern ${forbiddenAppModuleRepositoryConcern}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
  "has no explicit default Variant",
  "Module definition persistence must reject metadata without the protected current default Variant id",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/44_app_module_definition_persistence_contract.md",
  "AGENTS must require the App and Module definition persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "44_app_module_definition_persistence_contract.md",
  "the active architecture index must include the App and Module definition persistence contract",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/45_editor_session_view_state_contract.md",
  "AGENTS must require the editor session view state contract",
);
assertContains(
  "docs/architecture/README.md",
  "45_editor_session_view_state_contract.md",
  "the active architecture index must include the editor session view state contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "new ComponentClassRepository(_context)",
  "SpikeDatabase must construct the focused Component Class repository",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "_componentClassRepository.UpdateConfigAndMetadata(",
  "Component Class coordinated document writes must delegate to the repository",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs",
  "_componentClassRepository.UpdateMetadata(",
  "Component Variant document writes must delegate prepared metadata to the repository",
);
for (const componentClassFacadePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassReferences.cs",
] as const) {
  for (const forbiddenComponentClassSql of [
    "FROM component_classes",
    "UPDATE component_classes",
    "INSERT INTO component_classes",
    "DELETE FROM component_classes",
  ]) {
    assertDoesNotContain(
      componentClassFacadePath,
      forbiddenComponentClassSql,
      `${componentClassFacadePath} must delegate Component Class persistence instead of retaining ${forbiddenComponentClassSql}`,
    );
  }
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  'ProjectTreeNodeKind.ComponentClass => "component_classes"',
  "tree node writes must delegate Component Classes to their repository",
);
for (const forbiddenComponentClassRepositoryConcern of [
  "MainWindow",
  "ProjectTreeNode",
  "ComponentClassFieldCatalog",
  "EmbeddedComponent",
  "RuntimeInput",
  "Renderable",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ComponentClassRepository.cs",
    forbiddenComponentClassRepositoryConcern,
    `ComponentClassRepository must not own UI, field, composition, Runtime or render concern ${forbiddenComponentClassRepositoryConcern}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/ComponentClassRepository.cs",
  "has no explicit default Variant",
  "Component Class persistence must reject metadata without an explicit current default Variant id",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/46_component_class_definition_persistence_contract.md",
  "AGENTS must require the Component Class definition persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "46_component_class_definition_persistence_contract.md",
  "the active architecture index must include the Component Class definition persistence contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "new ModuleInstanceRepository(_context)",
  "SpikeDatabase must construct the focused Module Instance repository",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "_moduleInstanceRepository.UpdateDuration(",
  "timeline coordination must delegate prepared Module Instance durations",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs",
  "_moduleInstanceRepository.UpdateVariantDocuments(",
  "Module Variant changes must delegate prepared Module Instance documents",
);
for (const moduleInstanceFacadePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
] as const) {
  for (const forbiddenModuleInstanceSql of [
    "FROM module_instances",
    "UPDATE module_instances",
    "INSERT INTO module_instances",
    "DELETE FROM module_instances",
  ]) {
    assertDoesNotContain(
      moduleInstanceFacadePath,
      forbiddenModuleInstanceSql,
      `${moduleInstanceFacadePath} must delegate Module Instance persistence instead of retaining ${forbiddenModuleInstanceSql}`,
    );
  }
}
for (const forbiddenModuleInstanceRepositoryConcern of [
  "MainWindow",
  "ProjectTreeNode",
  "RuntimeInputForwardingContract",
  "RuntimeTimeline",
  "EffectiveModuleInstanceContract",
  "ValueKind",
  "Renderable",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ModuleInstanceRepository.cs",
    forbiddenModuleInstanceRepositoryConcern,
    `ModuleInstanceRepository must not own UI, Runtime, timing or render concern ${forbiddenModuleInstanceRepositoryConcern}`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/47_module_instance_persistence_contract.md",
  "AGENTS must require the Module Instance persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "47_module_instance_persistence_contract.md",
  "the active architecture index must include the Module Instance persistence contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "metadata has no explicit iconSet contract",
  "Icon Theme runtime generation must require explicit current iconSet metadata",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/43_icon_theme_persistence_and_asset_contract.md",
  "AGENTS must require the Icon Theme persistence and asset contract",
);
assertContains(
  "docs/architecture/README.md",
  "43_icon_theme_persistence_and_asset_contract.md",
  "the active architecture index must include the Icon Theme persistence and asset contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
  "has no resolvable Theme context",
  "Module Instance Theme context must fail explicitly when no Theme resolves",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
  "?? \"{}\"",
  "Module Instance Theme context must not return a plausible empty document",
);
for (const inferredThemeContext of ["COALESCE(", "ORDER BY t.name", "JOIN apps a"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
    inferredThemeContext,
    `Module Instance Theme context must not restore inferred fallback ${inferredThemeContext}`,
  );
}
for (const [relativePath, requiredGuard] of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs", "RequireShotContext(connection, shot.Id)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "RequireShotOwnerChange(connection, shotId, value)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Actors.cs", "RequireActorThemeChange(connection, actorId, value)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs", "module instance without explicit Shot owner Theme context"],
] as const) {
  assertContains(
    relativePath,
    requiredGuard,
    `${relativePath} must preserve explicit Shot owner Theme context`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "ORDER BY t.name, t.id",
  "duration synchronization must not infer a Theme from project ordering",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
  "JOIN actors actor ON actor.id = s.owner_actor_id",
  "Module Instance Theme context must resolve through the exact Shot owner Actor",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Schema.cs",
  "owner_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT",
  "Shot owner Actor must remain a required restricted foreign key without an empty default",
);
for (const [relativePath, explicitShotCreationTerm] of [
  ["spikes/desktop-editor-shell/EditorShell/EditorAddChildWorkflow.cs", "new ShotCreationDialog(_owner, _database).Show(parent)"],
  ["spikes/desktop-editor-shell/EditorShell/ShotCreationDialog.cs", "SelectedItem = null"],
  ["spikes/desktop-editor-shell/EditorShell/ShotCreationDialog.cs", "GetRequiredActorOptions(project.Id)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "must be created through AddShot"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "RequireEpisodeActor(connection, episode.Id, actorId)"],
  ["spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs", "GetRequiredActorOptions(settings.ProjectId)"],
] as const) {
  assertContains(
    relativePath,
    explicitShotCreationTerm,
    `${relativePath} must retain explicit non-empty Shot Actor selection`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/40_theme_persistence_and_context_contract.md",
  "AGENTS must require the Theme persistence and context contract",
);
assertContains(
  "docs/architecture/README.md",
  "40_theme_persistence_and_context_contract.md",
  "the architecture index must include contract 40",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/41_explicit_shot_production_context_contract.md",
  "AGENTS must require the explicit Shot Production context contract",
);
assertContains(
  "docs/architecture/README.md",
  "41_explicit_shot_production_context_contract.md",
  "the architecture index must include contract 41",
);
for (const retiredUsageInference of [
  "LIKE $needle",
  " LIKE ",
  "sqlite_master",
  "PRAGMA table_info",
  "ReferenceSearchTables",
  "TextColumns(",
  "LabelColumn(",
  "JsonContainsString",
  "ReferenceKindForSource",
  "IsProductionUsageSource",
  "AddUsageIfContains",
  "ReferenceSearchValue",
]) {
  for (const usagePath of [
    "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsage.cs",
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsageDetails.cs",
  ]) {
    assertDoesNotContain(
      usagePath,
      retiredUsageInference,
      `Usage must not restore inferred reference behavior ${retiredUsageInference}`,
    );
  }
}
for (const explicitUsageContract of [
  "RecordReferenceKinds",
  "ModuleComponentReferencePaths",
  "ThemeColorTokenCatalog.ColorTokens",
  "ComponentClassFieldCatalog.All()",
  "ReferenceUsageScope.Production",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
    explicitUsageContract,
    `Usage must retain explicit typed contract ${explicitUsageContract}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "_referenceUsageService.BuildIndex(connection)",
  "tree Used state must consume the shared explicit Usage edge set",
);
for (const typedUsageDetail of ["ReferenceUsageScope Scope", "usage.Scope,"]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsageDetails.cs",
    typedUsageDetail,
    `Usage details must retain typed edge scope ${typedUsageDetail}`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  "MainWindow",
  "the Usage service must not import or construct the desktop shell",
);
for (const contextualUsageNavigation of [
  "ReferenceUsageScope.Design => EditorWorkspace.Design",
  "ReferenceUsageScope.Production => EditorWorkspace.Production",
  "usage.SourceNodeId",
  "usage.EmbeddedUsage",
]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/EditorReferenceUsageNavigator.cs",
    contextualUsageNavigation,
    `Usage navigation must retain typed context ${contextualUsageNavigation}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "SelectReferenceNodeInWorkspace",
  "the shell must provide generic workspace-aware Usage selection",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "_navigationRenderer.BringNodeIntoView",
  "Usage navigation must reveal its selected tree node",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeCommandController.cs",
  "new EditorReferenceUsageDialog(_owner, _isDark()).Show(node, usages)",
  "blocked deletion must preserve typed Usage links",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ReferenceUsageCollectionEditor.cs",
  "EditorReferenceUsageLink.Create",
  "the Usage card must use the shared contextual link surface",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeCommandController.cs",
  "GetReferenceUsages(node)",
  "blocked deletion must not flatten typed Usage edges into prose",
);
for (const productionDataTreeContract of [
  "productionDataRoot.AddChild(actorsRoot);",
  "productionDataRoot.AddChild(devicesRoot);",
  "productionDataRoot.AddChild(productionFontsRoot);",
  "productionDataRoot.AddChild(renderPresetsRoot);",
  "systemDataRoot.AddChild(themesRoot);",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
    productionDataTreeContract,
    `Production Data tree must retain ${productionDataTreeContract}`,
  );
}
for (const productionWorkspaceKind of [
  "ProjectTreeNodeKind.ProductionDataRoot",
  "or ProjectTreeNodeKind.DevicesRoot or ProjectTreeNodeKind.Device",
  "or ProjectTreeNodeKind.ProductionFontsRoot or ProjectTreeNodeKind.ProductionFont",
  "or ProjectTreeNodeKind.RenderPresetsRoot or ProjectTreeNodeKind.RenderPreset",
]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/EditorNavigationMetadata.cs",
    productionWorkspaceKind,
    `Production workspace metadata must retain ${productionWorkspaceKind}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  'ProjectTreeNodeKind.Actor, "Actor", reader.GetString(1), ReferenceUsageScope.Production',
  "Actor-owned Usage must navigate to the Production workspace",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNavigationRenderer.cs",
  "node.CanAddChild && node.Parent?.Kind == ProjectTreeNodeKind.ProductionDataRoot",
  "grouped Production Data sections must retain their explicit Add actions",
);
for (const resourceNavigationContract of [
  "Actors, Devices, Production Fonts and Render Presets",
  "copy current records | regenerate from current seeds | create empty",
  "never fall back to records from another Project",
]) {
  assertContains(
    "docs/architecture/39_design_production_resource_navigation_contract.md",
    resourceNavigationContract,
    `resource navigation contract must retain ${resourceNavigationContract}`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/39_design_production_resource_navigation_contract.md",
  "AGENTS must require the current Design/Production resource navigation contract",
);
assertContains(
  "docs/architecture/README.md",
  "39_design_production_resource_navigation_contract.md",
  "the architecture index must include contract 39",
);
assertContains(
  "spikes/desktop-editor-shell/Data/CurrentDatabaseMaintenance.cs",
  "File.Copy(sourcePath, outputPath, overwrite: false)",
  "current database provisioning must preserve an already-validated source byte-for-byte",
);
for (const retiredPersistenceCommand of [
  "--migrate-database",
  "--create-schema-v1",
  "--validate-schema-v1",
]) {
  assertDoesNotContain(
    "package.json",
    retiredPersistenceCommand,
    `retired persistence command ${retiredPersistenceCommand} must not return`,
  );
}
const desktopDatabasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
type CurrentComponentClassRow = {
  id: string;
  component_type: string;
  record_class_id: string;
  config_json: string;
  metadata_json: string;
};
type CurrentModuleClassRow = {
  id: string;
  record_class_id: string;
};
let currentComponentClassRows: CurrentComponentClassRow[] = [];
let currentModuleClassRows: CurrentModuleClassRow[] = [];
let editorLayoutSource = "";
let moduleContractSource = "";
const editorLayoutRecordClassIds = new Set<string>();
if (!existsSync(desktopDatabasePath)) {
  addViolation("data/desktop-editor-spike.sqlite", "committed current database is missing");
} else {
  const database = new Database(desktopDatabasePath, { readonly: true, fileMustExist: true });
  try {
    const parityShots = database
      .prepare("SELECT id, episode_id FROM shots ORDER BY id")
      .all() as { id: string; episode_id: string }[];
    if (parityShots.length !== 1
      || parityShots[0]?.id !== "shot_001"
      || parityShots[0]?.episode_id !== "episode_001") {
      addViolation(
        "data/desktop-editor-spike.sqlite",
        "canonical parity data must retain only episode_001 / shot_001",
      );
    }
    const parityModuleInstances = database
      .prepare("SELECT id, shot_id FROM module_instances ORDER BY id")
      .all() as { id: string; shot_id: string }[];
    if (parityModuleInstances.length !== 2
      || parityModuleInstances.some((row) => row.shot_id !== "shot_001")) {
      addViolation(
        "data/desktop-editor-spike.sqlite",
        "canonical parity Module Instances must be the two authored Screens owned by shot_001",
      );
    }
    currentComponentClassRows = database
      .prepare("SELECT id, component_type, record_class_id, config_json, metadata_json FROM component_classes")
      .all() as CurrentComponentClassRow[];
    currentModuleClassRows = database
      .prepare("SELECT id, record_class_id FROM modules")
      .all() as CurrentModuleClassRow[];
    const layouts = database
      .prepare("SELECT record_class_id, layout_json FROM editor_layouts ORDER BY record_class_id")
      .all() as { record_class_id: string; layout_json: string }[];
    for (const layout of layouts) editorLayoutRecordClassIds.add(layout.record_class_id);
    editorLayoutSource = layouts.map((layout) => layout.layout_json).join("\n");
    moduleContractSource = (database
      .prepare("SELECT config_json, design_preview_json, metadata_json FROM modules ORDER BY id")
      .all() as { config_json: string; design_preview_json: string; metadata_json: string }[])
      .flatMap((row) => [row.config_json, row.design_preview_json, row.metadata_json])
      .join("\n");
  } finally {
    database.close();
  }
}
const currentComponentClasses = new Set(
  currentComponentClassRows.map((row) => row.component_type),
);
for (const row of currentComponentClassRows) {
  const componentClass = row.component_type;
  if (!desktopPreviewComponents[componentClass]) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current component class "${componentClass}" is missing from desktop preview manifest`,
    );
  }
  if (!routedComponentClasses.has(componentClass)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current component class "${componentClass}" is missing from desktop preview registry`,
    );
  }
  if (!editorLayoutRecordClassIds.has(row.record_class_id)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current component class "${componentClass}" is missing editor layout "${row.record_class_id}"`,
    );
  }

  if (componentClass === "bubble") {
    const metadata = jsonRecord(jsonParse(row.metadata_json));
    const configs = [jsonParse(row.config_json), ...jsonArray(metadata.presets).map((preset) => jsonRecord(preset).config)];
    configs.forEach((config, index) => {
      const gapToken = jsonRecord(jsonRecord(jsonRecord(config).bubble).status).gapToken;
      if (typeof gapToken !== "string" || !gapToken.startsWith("theme.spacing.")) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `Bubble current config ${index} must declare status.gapToken as a spacing token`,
        );
      }
    });
  }
}
for (const componentClass of Object.keys(desktopPreviewComponents)) {
  if (!currentComponentClasses.has(componentClass)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `manifest component class "${componentClass}" is absent from the committed current database`,
    );
  }
}
const currentModuleClasses = new Set(currentModuleClassRows.map((row) => row.record_class_id));
for (const row of currentModuleClassRows) {
  if (!desktopPreviewModules[row.record_class_id]) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current module "${row.id}" uses class "${row.record_class_id}" missing from the module manifest`,
    );
  }
  if (!routedModuleClasses.has(row.record_class_id)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current module class "${row.record_class_id}" is missing from module registry`,
    );
  }
}
for (const moduleClass of Object.keys(desktopPreviewModules)) {
  if (!currentModuleClasses.has(moduleClass)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `manifest module class "${moduleClass}" is absent from the committed current database`,
    );
  }
}

assertNoTerms("spikes/desktop-editor-shell/MainWindow.axaml.cs", [
  "Current class values",
  "ProjectTreeNodeKind.",
  "ValueKind.",
  "new TextBox",
  "new ComboBox",
  "new CheckBox",
  "IconTheme",
  "IconToken",
  "SvgReplace",
  "ColorPicker",
  "ActorAvatar",
  "actor.avatar",
]);
{
  const mainWindowSource = readText("spikes/desktop-editor-shell/MainWindow.axaml.cs");
  const directDatabaseCalls = [...mainWindowSource.matchAll(/_database\.([A-Za-z0-9_]+)/g)]
    .map((match) => match[1])
    .filter((call): call is string => typeof call === "string");
  const forbiddenDirectDatabaseCalls = directDatabaseCalls.filter((call) => call !== "LoadProjectTree");
  if (forbiddenDirectDatabaseCalls.length > 0) {
    addViolation(
      "spikes/desktop-editor-shell/MainWindow.axaml.cs",
      `MainWindow must remain shell-only; direct database calls are limited to LoadProjectTree, found ${[...new Set(forbiddenDirectDatabaseCalls)].join(", ")}`,
    );
  }
}
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "ProjectTreeNodeKind.ComponentClassesRoot",
  "component class root must not expose Add; parent component classes are internal",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "ProjectTreeNodeKind.StatusBarsRoot",
  "legacy status bar root must not expose Add; system bars are component presets",
);
assertPropertyBlockContainsKind(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "AppsRoot",
  false,
  "Apps root must not expose Add; App definitions are development-owned",
);
for (const propertyName of ["CanDuplicate", "CanDelete"]) {
  for (const kind of ["App", "Module"]) {
    assertPropertyBlockContainsKind(
      "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
      propertyName,
      kind,
      false,
      `${kind} definitions must not expose ${propertyName.replace("Can", "")}`,
    );
  }
}
for (const kind of ["App", "Module", "ModuleVariant"]) {
  assertPropertyBlockContainsKind(
    "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
    "CanRenameDirectly",
    kind,
    true,
    `${kind} must expose Rename without changing its stable id`,
  );
}
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "ProjectTreeNodeKind.NavigationBarsRoot",
  "legacy navigation bar root must not expose Add; system bars are component presets",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDuplicate",
  "ProjectTreeNodeKind.ComponentClass",
  "parent component classes must not expose Duplicate; use presets instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDuplicate",
  "ProjectTreeNodeKind.StatusBar",
  "legacy status bars must not expose Duplicate; use component presets instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDuplicate",
  "ProjectTreeNodeKind.NavigationBar",
  "legacy navigation bars must not expose Duplicate; use component presets instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDelete",
  "ProjectTreeNodeKind.ComponentClass",
  "parent component classes must not expose Delete; they are internal",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDelete",
  "ProjectTreeNodeKind.StatusBar",
  "legacy status bars must not expose Delete; use component presets instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDelete",
  "ProjectTreeNodeKind.NavigationBar",
  "legacy navigation bars must not expose Delete; use component presets instead",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "var statusBars = QueryStatusBarRows(connection);",
  "project tree must not load legacy status bars as navigation nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "var navigationBars = QueryNavigationBarRows(connection);",
  "project tree must not load legacy navigation bars as navigation nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "foreach (var statusBar in statusBars.OrderBy",
  "project tree must not add legacy status bar nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "foreach (var navigationBar in navigationBars.OrderBy",
  "project tree must not add legacy navigation bar nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "if (parent.Kind == ProjectTreeNodeKind.StatusBarsRoot)",
  "legacy status bar add workflow must not remain; use component presets instead",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "if (parent.Kind == ProjectTreeNodeKind.NavigationBarsRoot)",
  "legacy navigation bar add workflow must not remain; use component presets instead",
);
for (const forbiddenLegacyTreeTerm of [
  "StatusBarsRoot",
  "NavigationBarsRoot",
  "ProjectTreeNodeKind.StatusBar",
  "ProjectTreeNodeKind.NavigationBar",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
    forbiddenLegacyTreeTerm,
    `legacy system bar tree term ${forbiddenLegacyTreeTerm} must not return; use component presets`,
  );
}
for (const forbiddenLegacyLayoutTerm of [
  "recordClassId == \"status_bar\"",
  "recordClassId == \"navigation_bar\"",
]) {
  if (editorLayoutSource.includes(forbiddenLegacyLayoutTerm)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `legacy system bar layout term ${forbiddenLegacyLayoutTerm} must not return; use component layouts`,
    );
  }
}
assertMatches(
  "archive/react-legacy/src/domain/fields/themeFields.ts",
  /statusBarId:[\s\S]*?tableId:\s*"component_presets"/,
  "theme status bar field must reference component presets, not legacy status_bars",
);
assertMatches(
  "archive/react-legacy/src/domain/fields/themeFields.ts",
  /navigationBarId:[\s\S]*?tableId:\s*"component_presets"/,
  "theme navigation bar field must reference component presets, not legacy navigation_bars",
);
for (const forbiddenComponentInputControl of [
  "EditorInstantComboBox",
  "new ComboBox",
  "new TextBox",
  "new CheckBox",
  "new ToggleSwitch",
  "new NumericUpDown",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
    forbiddenComponentInputControl,
    `component inputs must use dictionary controls, not local ${forbiddenComponentInputControl}`,
  );
}
for (const collectionEditorPath of [
  "spikes/desktop-editor-shell/EditorShell/StatusBarItemsCollectionEditor.cs",
  "spikes/desktop-editor-shell/EditorShell/NavigationBarItemsCollectionEditor.cs",
]) {
  for (const forbiddenCollectionControl of [
    "EditorInstantComboBox",
    "new ComboBox",
    "new TextBox",
    "new CheckBox",
    "new ToggleSwitch",
    "new NumericUpDown",
  ]) {
    assertDoesNotContain(
      collectionEditorPath,
      forbiddenCollectionControl,
      `${collectionEditorPath} item scalar fields must use dictionary controls, not local ${forbiddenCollectionControl}`,
    );
  }
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
  "\"actor\",",
  "component input seeds must use generic recordReference + tableId, not a special actor input kind",
);
for (const recordReferenceSpecializationPath of [
  "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs",
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
]) {
  for (const forbiddenRecordReferenceSpecialization of [
    "ActorReference",
    "ComponentInputKind.Actor",
    "ValueKind.Actor",
  ]) {
    assertDoesNotContain(
      recordReferenceSpecializationPath,
      forbiddenRecordReferenceSpecialization,
      "record references must stay generic through recordReference + tableId",
    );
  }
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "ProjectTreeNodeKind.StatusBar => fieldId.StartsWith(\"statusBar.\"",
  "legacy status bars must not be exposed through record-class field editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "ProjectTreeNodeKind.NavigationBar => fieldId.StartsWith(\"navigationBar.\"",
  "legacy navigation bars must not be exposed through record-class field editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "UpdateStatusBarField",
  "legacy status bar field writes must not remain in generic record editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "UpdateNavigationBarField",
  "legacy navigation bar field writes must not remain in generic record editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "SeedStatusBarsIfEmpty",
  "desktop database initialization must not seed legacy status_bars rows; use status_bar component presets",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "SeedNavigationBarsIfEmpty",
  "desktop database initialization must not seed legacy navigation_bars rows; use navigation_bar component presets",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Schema.cs",
  "CREATE TABLE IF NOT EXISTS status_bars",
  "desktop schema must not recreate legacy status_bars; use status_bar component variants",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Schema.cs",
  "CREATE TABLE IF NOT EXISTS navigation_bars",
  "desktop schema must not recreate legacy navigation_bars; use navigation_bar component variants",
);
for (const forbiddenSchemaMigrationTerm of [
  "AddColumnIfMissing",
  "EnsureShotColumns",
  "EnsureAppColumns",
  "EnsureModuleColumns",
  "EnsureModuleInstanceColumns",
  "EnsureComponentClassColumns",
  "MigrateScreenInstancesToModuleInstances",
]) {
  assertFilesDoNotContain(
    walkFilesByExtension(path.join(root, "spikes/desktop-editor-shell/Data"), [".cs"]),
    forbiddenSchemaMigrationTerm,
    `schema v1 startup must not keep historical schema migration helper ${forbiddenSchemaMigrationTerm}`,
  );
}
assertDesktopDatabaseTableIsEmpty(
  "status_bars",
  "desktop database must not contain legacy status_bars rows; use status_bar component variants",
);
assertDesktopDatabaseTableIsEmpty(
  "navigation_bars",
  "desktop database must not contain legacy navigation_bars rows; use navigation_bar component variants",
);
for (const legacyTextBoxComponentInput of [
  "\"leftIconRowPresetId\"",
  "\"rightIconRowPresetId\"",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
    legacyTextBoxComponentInput,
    "text box embedded component inputs must use preset slots, not legacy preset id fields",
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Common/DeviceMetricRules.cs",
  "JsonPath.NumberAt(metrics,",
  "device preview metric reads must be strict; defaults belong in seed/import normalization, not preview rendering",
);
for (const forbiddenLegacySystemBarMethod of [
  "GetStatusBarSettings",
  "GetStatusBarFieldValue",
  "GetStatusBarItems",
  "UpdateStatusBarField",
  "UpdateStatusBarItem",
  "QueryStatusBarRows",
  "GetNavigationBarSettings",
  "GetNavigationBarFieldValue",
  "GetNavigationBarItems",
  "UpdateNavigationBarField",
  "UpdateNavigationBarItem",
  "QueryNavigationBarRows",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.StatusNavigationComponents.cs",
    forbiddenLegacySystemBarMethod,
    `legacy system bar database method ${forbiddenLegacySystemBarMethod} must not return; use component class presets`,
  );
}
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "./systemBar",
  "system bars must be declared as explicit status/navigation component modules, not shared manifest entrypoints",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "./systemBar",
  "component registry must route status/navigation through their explicit component modules",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"audio\": {",
  "desktop preview component manifest must use the current audio component type",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"textBox\": {",
  "desktop preview component manifest must route text box as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"textInputBar\": {",
  "desktop preview component manifest must route text input bar as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"keyboard\": {",
  "desktop preview component manifest must route keyboard as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"media\": {",
  "desktop preview component manifest must route media as an owning component module",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "audio: (payload)",
  "component renderable registry must route the current audio component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "textBox: (payload)",
  "component renderable registry must route the current text box component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "textInputBar: (payload)",
  "component renderable registry must route the current text input bar component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "keyboard: (payload)",
  "component renderable registry must route the current keyboard component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "media: (payload)",
  "component renderable registry must route the current media component type",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "audio_message",
  "legacy audio_message component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "button_icon",
  "legacy button_icon component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "text_input_bar",
  "legacy text_input_bar component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "video_message",
  "legacy video_message component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "audio_message",
  "legacy audio_message component type must not return to the preview registry",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "button_icon",
  "legacy button_icon component type must not return to the preview registry",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "text_input_bar",
  "legacy text_input_bar component type must not return to the preview registry",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "video_message",
  "legacy video_message component type must not return to the preview registry",
);
for (const legacyComponentTypeFile of [
  "archive/react-legacy/src/domain/schemas/componentClass.ts",
  "archive/react-legacy/src/domain/fields/componentClassFields.ts",
  "archive/react-legacy/src/domain/repository/fixtures/exampleDataset.ts",
  "archive/react-legacy/src/domain/resolvers/resolveChatScreen.ts",
]) {
  for (const legacyComponentType of [
    "audio_message",
    "button_icon",
    "text_input_bar",
    "video_message",
  ]) {
    assertDoesNotContain(
      legacyComponentTypeFile,
      legacyComponentType,
      `legacy component type ${legacyComponentType} must not return to ${legacyComponentTypeFile}`,
    );
  }
}
for (const legacySeededComponentType of [
  "audio_message",
  "button_icon",
  "text_input_bar",
  "video_message",
]) {
  assertDoesNotContain(
    "archive/react-legacy/src/persistence/sqlite/createDatabase.ts",
    `componentType: "${legacySeededComponentType}"`,
    `legacy component type ${legacySeededComponentType} must not be seeded as componentType`,
  );
  assertDoesNotContain(
    "archive/react-legacy/src/persistence/sqlite/createDatabase.ts",
    `type: "${legacySeededComponentType}"`,
    `legacy component type ${legacySeededComponentType} must not be seeded as component_type`,
  );
}
for (const componentType of Object.keys(desktopPreviewComponents)) {
  assertContains(
    "archive/react-legacy/src/domain/schemas/componentClass.ts",
    `"${componentType}"`,
    `component class schema must include manifest component type ${componentType}`,
  );
  assertContains(
    "archive/react-legacy/src/domain/fields/componentClassFields.ts",
    `"${componentType}"`,
    `component class field options must include manifest component type ${componentType}`,
  );
}
for (const legacyComponentRecordClassId of [
  "component.button_icon",
  "component.text_input_bar",
]) {
  for (const filePath of [
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
    "spikes/desktop-editor-shell/EditorShell/EmbeddedComponentSlotCatalog.cs",
  ]) {
    assertDoesNotContain(
      filePath,
      legacyComponentRecordClassId,
      `legacy component record class id ${legacyComponentRecordClassId} must not return to ${filePath}`,
    );
  }
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs",
  "ComponentPreset",
  "embedded component preset selection must have a dedicated dictionary value kind",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  "ValueKind.ComponentPreset",
  "component preset fields must use their dedicated dictionary control",
);
{
  const fieldDefinitionSource = readText("spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs");
  const valueKindBlock = /internal enum ValueKind\s*\{([\s\S]*?)\}/.exec(fieldDefinitionSource);
  const valueKinds = new Set(
    (valueKindBlock?.[1] ?? "")
      .split(",")
      .map((value) => value.trim())
      .filter(Boolean),
  );
  const registrySource = readText("spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs");
  const registeredKinds = new Set(
    [...registrySource.matchAll(/\[ValueKind\.([A-Za-z0-9_]+)\]\s*=/g)]
      .map((match) => match[1] ?? "")
      .filter(Boolean),
  );
  for (const valueKind of valueKinds) {
    if (!registeredKinds.has(valueKind)) {
      addViolation(
        "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
        `dictionary registry is missing explicit ValueKind.${valueKind}`,
      );
    }
  }
  for (const registeredKind of registeredKinds) {
    if (!valueKinds.has(registeredKind)) {
      addViolation(
        "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
        `dictionary registry contains unknown ValueKind.${registeredKind}`,
      );
    }
  }
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  ": new DictionaryTextControl(definition, value)",
  "dictionary registry must fail for an unregistered ValueKind instead of falling back to text",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  "uses unregistered dictionary value kind",
  "dictionary registry must report an unregistered ValueKind visibly",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "ignoreCase: false",
  "runtime input valueKind parsing must accept only the current canonical enum spelling",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "_ => ComponentInputKind.Text",
  "runtime input kind parsing must not silently fall back to text",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs",
  "DictionaryControlRegistry.Create",
  "dictionary field rows must host controls through the dictionary control registry",
);
assertNoTerms("spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs", [
  "DictionaryPathBrowseButton",
  "ValueKind.DirectoryPath",
  "ValueKind.ImageFilePath",
]);
assertContains(
  "spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj",
  "desktopPreviewManifest.json",
  "desktop app must embed the canonical preview manifest",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "DesktopPreviewManifest.ComponentCategory(componentClass.ComponentType)",
  "component navigation category must come from the canonical preview manifest",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "ComponentClassNavigationGroupFor",
  "component navigation must not infer category from a hard-coded component type switch",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
  "DesktopPreviewManifest.Modules",
  "module class options must come from the canonical preview manifest",
);
for (const retiredGenericModulePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
]) {
  assertDoesNotContain(
    retiredGenericModulePath,
    "module.generic",
    "undeclared generic module fallback must not return",
  );
}
for (const retiredGenericAppPath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
]) {
  assertDoesNotContain(
    retiredGenericAppPath,
    "app.generic",
    "retired generic App fallback must not return",
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "if (parent.Kind == ProjectTreeNodeKind.AppsRoot)",
  "repository must not create Apps through generic Add Child",
);
assertNoTerms("spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs", [
  "\"audio\"",
  "\"avatar\"",
  "\"buttonIcon\"",
  "\"textBox\"",
  "\"navigation_bar\"",
  "\"status_bar\"",
  "\"textInputBar\"",
  "\"keyboard\"",
  "\"media\"",
]);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "index < slots.Count - 1 && overrides is not null",
  "embedded inherited values must apply ancestor overrides only, so reset restores the selected child preset",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassReferences.cs",
  "return GetComponentPresetReferenceOptionsByType(projectId, componentType);",
  "embedded component preset selectors must store full component preset references, not short preset ids",
);
assertContains(
  "src/desktop-preview/componentPreviewDefaults.ts",
  "componentPresetConfig",
  "desktop preview resolvers must resolve embedded child presets through the shared preset helper",
);
assertContains(
  "src/desktop-preview/audioComponentResolver.ts",
  "componentPresetConfig(componentBaseConfigs, \"badge\", badgeSlot.presetId)",
  "audio badge preview must resolve the selected Badge preset",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "ResolveIconTokenAssetPath",
  "icon tokens must never resolve through the first Icon Set in a project",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/IconTokenPickerDialog.cs",
  "GetIconThemeOptions",
  "the generic icon-token picker must use the active Theme Icon Set instead of selecting a concrete set",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/SvgIconPreview.cs",
  "NativeWebView",
  "editor icon thumbnails must use lightweight vector controls instead of one web view per icon",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
  "component.label.textGap\"",
  "Label text separation must use the canonical spacing-token field",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "ValidateComponentPresetReferencesForPreview",
  "design preview payloads must validate full embedded preset references before web rendering",
);
for (const embeddedPresetField of [
  "component.avatar.label.presetId",
  "component.audio.avatar.presetId",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
    `{ "id": "${embeddedPresetField}"`,
    `embedded preset field "${embeddedPresetField}" must not be shown as a separate layout row`,
  );
  assertMatches(
    "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
    new RegExp(`\\["${embeddedPresetField.replaceAll(".", "\\.")}"\\][\\s\\S]*?ValueKind\\.OptionToken`),
    `embedded preset field "${embeddedPresetField}" must keep the slot preset route, not generic recordReference`,
  );
}

assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeSelectionState.cs",
  "private readonly Dictionary<string, string> _lastComponentPresetNodeIds",
  "component preset navigation must remember the last selected preset per component class",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "ResolveSelectionNode",
  "component class navigation must resolve to a concrete preset selection",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "_editorContent.Build(editorNode, node)",
  "component editor layout node and data node must stay separated so presets edit preset config",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "presetSourceNode.Kind != ProjectTreeNodeKind.ComponentPreset",
  "Save preset must only be offered for a concrete selected component preset",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeSelectionState.cs",
  "EndsWith(\"::preset::default\", StringComparison.Ordinal)",
  "first component class selection must prefer the protected Default preset",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs",
  "Component variants can only be saved from an active selected variant.",
  "component variant saving must reject ambiguous parent component class configs",
);
for (const kind of ["ComponentClass", "ComponentPreset"]) {
  assertPropertyBlockContainsKind(
    "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
    "CanRenameDirectly",
    kind,
    true,
    `${kind} must expose direct rename through the standard variant action`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanRenameDirectly => Kind == ProjectTreeNodeKind.ComponentClass\n        || (Kind == ProjectTreeNodeKind.ComponentPreset && !IsProtected)",
  "component preset rename must not be coupled to delete protection",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNavigationRenderer.cs",
  "EditorIcons.Create(EditorIcons.Edit, 14)",
  "component preset rename must use the standard editor rename icon",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "Kind == ProjectTreeNodeKind.ComponentPreset && !IsProtected",
  "protected component presets must not be deletable",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "ProjectTreeNodeKind.ComponentPreset => FromComponentPreset",
  "design preview must route selected component preset nodes",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "database.GetComponentPresetSettings(node)",
  "component preset preview payload must load preset config",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldValueService.cs",
  "ProjectTreeNodeKind.ComponentClass or ProjectTreeNodeKind.ComponentPreset",
  "component field service must support component presets as editable data contexts",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "CreateComponentPresetFieldValue",
  "component preset fields must read from preset config",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "UpdateComponentPresetField",
  "component preset fields must write to preset config",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  "ProjectTreeNodeKind.ComponentPreset, ReadString(reader, 3)",
  "theme status bar references must target concrete Component Variants",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  "ProjectTreeNodeKind.ComponentPreset, ReadString(reader, 4)",
  "theme navigation bar references must target concrete Component Variants",
);
assertAnyContains(
  desktopPersistenceDataPaths,
  "GetComponentPresetReferenceOptionsByType(projectId, \"status_bar\"",
  "theme status bar selector must list component presets",
);
assertAnyContains(
  desktopPersistenceDataPaths,
  "GetComponentPresetReferenceOptionsByType(projectId, \"navigation_bar\"",
  "theme navigation bar selector must list component presets",
);
assertAnyContains(
  desktopPersistenceDataPaths,
  "[\"id\"] = DefaultComponentPresetId",
  "component class normalization must create a Default preset",
);
assertAnyContains(
  desktopPersistenceDataPaths,
  "[\"protected\"] = true",
  "Default component preset must be protected in stored metadata",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/MotionVariantValue.cs",
  "legacyKey",
  "motion parser must not accept legacy transition keys",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "motion.Remove(\"opacity\")",
  "component config normalization must not migrate legacy motion opacity",
);
for (const legacyMediaIconBarSlot of [
  "\"topIconBarSlot\"",
  "\"centerIconBarSlot\"",
  "\"bottomIconBarSlot\"",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
    legacyMediaIconBarSlot,
    `media icon bars must use explicit inline/fullscreen slots, not legacy ${legacyMediaIconBarSlot}`,
  );
}

assertContains(
  "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
  "data-renderable-id={node.id}",
  "desktop preview nodes must expose stable ids for generic incremental WebView updates",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "window.mockupsRegisterPreviewAsset",
  "animation frames must register repeated data assets once instead of transporting them in every body patch",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "_pendingUpdate = nextUpdate;",
  "animation playback must keep the latest pending frame instead of accumulating obsolete frames",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "MaxQueuedAnimationFrames",
  "animation playback must not restore a bounded backlog of obsolete frames",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "Stopwatch.GetElapsedTime(_playbackStartedTimestamp).TotalSeconds",
  "preview playback time must derive from a monotonic elapsed clock instead of counting processed UI ticks",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "PrewarmPersistentRenderer",
  "preview prewarming must not serialize interactive frames through the same renderer process",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "public static IDisposable ReserveFrameCacheCapacity",
  "expanded playback frame caches must be represented by a releasable reservation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "ReleaseFrameCacheReservation();",
  "playback must release its expanded frame-cache reservation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorCollectionCardFactory.cs",
  "ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ComponentPreset or ProjectTreeNodeKind.ModuleInstance",
  "module instances must use the same declarative runtime-input editor as module Test Values",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "payload.Kind is \"componentClass\" or \"module\" or \"moduleInstance\"",
  "module-instance actions must use the generic preview input session",
);
for (const retiredInstanceEditorTerm of [
  "moduleInstance.conversation.bubbleRevealMode",
  "moduleInstance.conversation.incomingRevealMode",
  "ConversationMessagesCollectionEditor",
]) {
  for (const file of [
    "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorCollectionCardFactory.cs",
  ]) {
    assertDoesNotContain(
      file,
      retiredInstanceEditorTerm,
      `module instances must not restore the retired Conversation-specific editor route (${retiredInstanceEditorTerm})`,
    );
  }
  if (editorLayoutSource.includes(retiredInstanceEditorTerm)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `module instances must not restore the retired Conversation-specific editor route (${retiredInstanceEditorTerm})`,
    );
  }
}
assertDoesNotContain(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "parseObject(payload.instanceJson).behavior",
  "module renderables must consume the canonical runtime preview payload instead of a second instance behavior channel",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  "[\"definesModuleDuration\"] = true",
  "a module runtime contract must declare which action defines its finite instance duration",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  "DeclaredBaseDuration(contract)",
  "module-instance duration must be evaluated generically from the declared runtime action",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  'candidate["extendsModuleDuration"]?.GetValue<bool>() == true',
  "finite collection-item actions must be able to extend module duration through their declarative contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "group.Sum((instance) => instance.DurationFrames)",
  "cut-only Shot duration must remain the sum of its ordered module-instance durations",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "ProjectTreeNodeKind.Shot => FromShot",
  "Shot preview must resolve its active ordered module-instance slot",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "boundedFrame - startFrame",
  "Shot preview must translate the requested Shot frame to the active module's local frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  '["moduleInstanceId"] = node.Id',
  "production preview payloads must identify their active module instance",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "RenderProductionContextHistoryItems",
  "production preview history must use its own Shot and module-instance stack",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "public void SetWorkspace(EditorWorkspace workspace)",
  "preview ownership must follow the explicit Design or Production workspace",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "_previewController.SetWorkspaceWithoutRefresh(workspace);",
  "workspace transactions must establish their final selection before refreshing Preview",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorContentController.cs",
  "_cardHost.Replace(cards);",
  "editor content must build a candidate before replacing the visible card host",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNavigationRenderer.cs",
  "var candidate = new StackPanel();",
  "navigation content must build a candidate before replacing the visible host",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "{ Kind: ProjectTreeNodeKind.ModuleInstance } instance => _database.GetModuleInstanceSettings(instance.Id).ShotId",
  "a selected module instance must retain its owning Shot playhead context",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  'new FieldOption("screen", "Screen")',
  "production navigation must not duplicate tree context with a Shot or Screen scope control",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "NavigationFrameRange()",
  "production slider and playback must share the tree-owned navigation range",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "return ScreenFrameRange(shotId, screen.Id);",
  "a selected Screen must present and play its own local range",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  '["localFrame"] = Math.Max(0, localTimelineFrame ?? 0)',
  "production Screen payload identity must expose its resolved local frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  '["moduleInstanceId"]?.GetValue<string>()',
  "runtime Test Values scope must distinguish production Screens by instance id",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  /TimelineButtonGroup\([\s\S]*?_shotAbsoluteStartButton,[\s\S]*?_shotPreviousSlotButton,[\s\S]*?_shotPreviousKeyframeButton,[\s\S]*?_shotPreviousFrameButton,[\s\S]*?_shotPlayButton,[\s\S]*?_shotNextFrameButton,[\s\S]*?_shotNextKeyframeButton,[\s\S]*?_shotNextSlotButton,[\s\S]*?_shotAbsoluteEndButton\)/,
  "production transport must remain symmetric around frame stepping and playback",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  /_shotPreviousKeyframeButton,[\s\S]*?_shotPreviousFrameButton,[\s\S]*?_shotPlayButton,[\s\S]*?_shotNextFrameButton,[\s\S]*?_shotNextKeyframeButton/,
  "production transport must keep frame stepping next to playback and keyframe stepping outside it",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_shotNavigationScope",
  "Production preview context must come from the selected tree node rather than a duplicate scope control",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "navigationRow = new Border",
  "production transport must retain its grouped separator layout",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_shotTimelineControls.DesiredSize.Width",
  "production transport controls must reflow as one measured unit",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "availableWidth < 880",
  "production transport must not use a fixed wrapping breakpoint",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "SynchronizeExplicitScreenSelection",
  "Production scrubbing must move the shared playhead without changing tree selection",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorIcons.cs",
  'TimelineShotStart => "M3 3H6V5H5V19H6V21H3Z',
  "timeline boundary bars must be filled geometry rather than invisible open paths",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "ModuleInstanceLocalFrame(database, node.Id, timelineFrame)",
  "module-instance production preview must translate the global Shot frame to a local frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorTimelineTransport.cs",
  "CreateKeyframeStepIcon(bool next)",
  "Shot keyframe controls must use shared timeline transport chrome",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorTimelineTransport.cs",
  "CreateKeyframeGlyph(",
  "Preview and editor keyframe buttons must share one SVG glyph factory",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "var isOnKeyframe = keyframes.Contains(_shotPreviewFrame)",
  "Preview playback must expose when the playhead is parked on a keyframe",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  /_shotPlayButton\.BorderBrush\s*=\s*isOnKeyframe[\s\S]*?EditorAnimationVisuals\.ActiveTrackBrush/,
  "Preview playback must use the animation amber border at an exact keyframe",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "ModuleInstanceTimeline.ShotKeyframeFrames(_database, shotId)",
  "Shot navigation must aggregate keyframes from every Screen before selecting the current Screen range",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "var showScreenStep = contextNode?.Kind == ProjectTreeNodeKind.Shot",
  "previous and next Screen controls must appear only in Shot context",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "? ScreenFrameRange(shotId, contextNode.Id)",
  "Screen context keyframe navigation must stay inside the selected Screen",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "private ProjectTreeNode? ProductionPayloadNode() => ProductionContextNode();",
  "the selected Production tree node must be the sole preview payload context",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_designContextLockButton.IsVisible = _workspace != EditorWorkspace.Production",
  "Production preview must not expose a context lock that can diverge from tree selection",
);
assertMatches(
  "src/desktop-preview/bubbleComponentResolver.ts",
  /function bubbleAudioInputs[\s\S]*?showBadge:\s*false/,
  "Bubble must explicitly bind its embedded Audio badge visibility at the child boundary",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  'Timeline(definition)["extendsOwnerDuration"]?.GetValue<bool>() != false',
  "field metadata must decide whether a field advances serial owner duration",
);
assertMatches(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  /preDurationFieldIds[\s\S]*?delay[\s\S]*?postDurationFieldIds[\s\S]*?postWriteOnHold/,
  "Conversation runtime metadata must declare serial pre-delay and post-hold ownership explicitly",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  '["minimumEnabledKeyframes"] = 2',
  "Conversation must keep base write-on when text has only mandatory KF0",
);
assertContains(
  "src/desktop-preview/conversationModuleResolver.ts",
  'timeline.usesTrackCompletion("text", targetId)',
  "Conversation resolver must suppress built-in write-on when text owns animation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "Stopwatch.GetElapsedTime(_shotPlaybackStartedTimestamp).TotalSeconds",
  "Shot playback must derive frames from a monotonic elapsed clock",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_pendingPlaybackFramesOverride = ShotPlaybackFramePayloads",
  "Shot playback must prepare its frames through the shared HTML/raster playback route",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  '"0,0,0,Auto"',
  "production preview setup must collapse editor-owned Device, Theme and Mode selectors",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "ProductionPreviewContextStrip.Render",
  "production preview setup must expose inherited context through the shared read-only strip",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  'statusText: message.statusVisible ? message.statusText : ""',
  "hidden message delivery state must not reserve status text space",
);
assertContains(
  "src/desktop-preview/bubbleComponentResolver.ts",
  'optionalBoolean(preview, "typingIndicator") || state === "system"',
  "typing-indicator and system text must be centered by the Bubble resolver",
);
assertDoesNotContain(
  "src/desktop-preview/bubbleComponentRenderable.ts",
  "avatarIntrusion",
  "external Bubble avatars must not reserve internal content padding",
);
assertContains(
  "src/desktop-preview/bubbleComponentRenderable.ts",
  "minimumContentWidth",
  "Bubble layout must allow owned labels to define its minimum content width",
);
assertContains(
  "src/desktop-preview/bubbleComponentRenderable.ts",
  "inlineBubbleStatusWidth",
  "Bubble status must reuse the final text line when its resolved frame fits",
);
assertContains(
  "src/desktop-preview/bubbleComponentResolver.ts",
  'requiredString(status, "gapToken"',
  "Bubble status row spacing must use its declared spacing token",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  "[\"timelineFrameJsonKey\"] = \"conversationFrame\"",
  "modules with a local timeline must declare its runtime frame key",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  "[\"jsonKey\"] = \"conversationType\"",
  "Conversation must publish its individual or group type through the runtime contract",
);
assertMatches(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  /\["id"\] = "writeOn"[\s\S]*?\["valueKind"\] = ValueKind\.BehaviorTiming\.ToString\(\)[\s\S]*?\["baseFramesPerUnit"\] = 7/,
  "new Conversation messages must contribute a finite default write-on duration",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "candidate.EnabledWhenItemJsonKey.Equals(input.JsonKey",
  "runtime collection controls must rebuild after a dependency field changes",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/InstantEditorCard.cs",
  "public EditorSubcardLayout SubcardLayout { get; }",
  "subcard organization must be an explicit generic card property",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "EditorSubcardLayout.VerticalCards",
  "runtime collection cards must declare their subcard organization explicitly",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorGroupBlock.cs",
  "subcardLayout == EditorSubcardLayout.FlatStack",
  "flat-stack cards must inherit their parent surface through the shared card factory",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "CreateSeparatedInputContent(owner, preview, ownInputs)",
  "the General runtime category must use shared separated-section content",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  '"general",\n                "General",\n                "Runtime inputs"',
  "direct runtime fields must join the shared top-level category navigator",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "CreateTestValueCollectionContent(owner, preview, collection, actions, items)",
  "runtime collections must join the same top-level category navigator",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "_entries[section.Id] = entry;",
  "vertical-card rows must own full-width separators up to the navigation divider",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayout.cs",
  '[JsonPropertyName("groupLayout")]',
  "editor cards must declare reusable child-group organization through layout metadata",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayout.cs",
  '[JsonPropertyName("presentation")]',
  "individual editor groups must be able to declare a reusable presentation block",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  "EffectiveGroupLayout(group, groupLayout)",
  "mixed editor-card organization must remain metadata-driven and generic",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  '"verticalCards" => EditorSubcardLayout.VerticalCards',
  "layout cards must route vertical-card organization through the shared subcard host",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  '"separatedSections" => EditorSubcardLayout.SeparatedSections',
  "layout cards must route separated sections through the shared subcard host",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
  '"groupLayout": "verticalCards"',
  "component layouts must opt into vertical cards declaratively",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
  '"groupLayout": "separatedSections"',
  "component layouts must opt into separated sections declaratively",
);
assertMatches(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
  /"component\.status_bar"[\s\S]*?"groupLayout": "separatedSections"/,
  "Status Bar must use the same declarative separated-section organization as Atoms",
);
assertMatches(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
  /"component\.navigation_bar"[\s\S]*?"groupLayout": "separatedSections"/,
  "Navigation Bar must use the same declarative separated-section organization as Atoms",
);
assertMatches(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
  /"component\.keyboard"[\s\S]*?"groupLayout": "verticalCards"/,
  "Keyboard categories must use the same declarative vertical-card organization as Atoms",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  'EditorSubcardLayout.VerticalCards',
  "runtime input groups must use the shared vertical-card organization",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
  '"iconRow::preset::default"',
  "component input contracts must store concrete component preset references",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorTreeExpansionState.cs",
  "CollapseWorkspaceSectionPeers(node)",
  "workspace navigation cards must remain mutually exclusive",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorSessionUiState.cs",
  "public void SetExpanded(string key, bool value)",
  "nested card expansion must remain available within the current editor session",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "cards[0].IsExpanded = true",
  "nested cards must not reopen automatically in a new application session",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorShellStateService.cs",
  "ExpandedCards",
  "card expansion must never be written to persisted window state",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorSessionHistoryState.cs",
  "EditorViewState",
  "Preview and Variant history must never persist editor view state",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorCardHostController.cs",
  "card.IsExpanded = false;",
  "a new editor session must begin with every editor card closed",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorSessionViewStateStore.cs",
  "_statesByRecordClassId[RequiredRecordClassId(recordClassId)] = state;",
  "card expansion and scroll must remain available by exact layout class within the current session",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorViewStateController.cs",
  "EditorNodeSelectionState.EditorNodeForSelection(node).RecordClassId",
  "Variant editor state must resolve through the parent layout record class",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorViewStateController.cs",
  "node.Id",
  "editor view state must not be keyed by the selected node id",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorViewStateController.cs",
  "card.SessionStateId",
  "top-level editor card state must restore through explicit stable ids",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorViewState.cs",
  "bool[]",
  "top-level editor card expansion must not be stored by array position",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorContentController.cs",
  'var presentationKey = $"editor:{layoutNode.RecordClassId}:presentation";',
  "editor presentation mode must be shared by exact layout class within the session",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  'EditorNodeSelectionState.EditorNodeForSelection(node).RecordClassId',
  "Runtime Inputs tabs must retain their session selection by exact layout class",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  '"Interpolation",\n                    ValueKind.OptionToken',
  "keyframe interpolation must use the dictionary field route",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "EditorIcons.TimelineFirstFrame",
  "animation transport must reuse the standard timeline navigation icons",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "filled: hasCurrentKeyframe",
  "animation transport must expose exact-keyframe state at the active frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "playbackButton.Click += (_, _) => _togglePlayback()",
  "animation play-pause must delegate to the authoritative Preview playback owner",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  'var selectionKey = $"{node.Id}:animation-properties:{scopeKey}"',
  "animation property selection must remain isolated per declared animation scope in session state",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  /Children\s*=\s*\{[\s\S]*?CreateSeparator[\s\S]*?currentKeyframeButton,[\s\S]*?firstFrameButton,[\s\S]*?previousFrameButton,[\s\S]*?playbackButton,[\s\S]*?nextFrameButton,[\s\S]*?lastFrameButton/,
  "animation transport must keep diamond-first standard navigation order",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "DispatcherTimer",
  "animation editor must not create an independent playback clock",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "var resolvedTargets = document.Tracks",
  "animation property lists must originate from active persisted tracks only",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  ": EditorAnimationVisuals.OtherKeyframeBrush",
  "active animated properties that are not selected must remain visible in gray",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "_shotFrame() - screenStartFrame",
  "Screen animation panels must present the authoritative Shot playhead as Screen-local time",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(",
  "owner-relative keyframes must translate through the common timeline onto the containing Screen",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "ModuleInstanceTimeline.DurationFrames(_database, node.Id)",
  "animation authoring panels must use their containing Screen scale",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceTimeline.cs",
  "RuntimeDurationPolicy.Explicit",
  "explicit Screen duration must be resolved by the shared module-instance timeline",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "RuntimeDurationContract.Policy(contract) == RuntimeDurationPolicy.Explicit",
  "timeline synchronization must preserve explicitly authored Screen durations",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  '"durationPolicy":"explicit"',
  "Lock Screen must declare its explicit duration policy in its own module contract",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Common/RuntimeDurationContract.cs",
  "lockScreen",
  "the generic duration contract must not know concrete modules",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "screenStartFrame + RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame",
  "Screen-local keyframe markers must not persist or display absolute Shot offsets",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "_reloadAndSelect",
  "keyframe edits must refresh their local animation surface without rebuilding the editor",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  "firstMatchingValue",
  "entity-owned keyframes must support a generic stable first-appearance origin",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_playbackState.Changed += RefreshResolvedValue",
  "animated Runtime Values must follow the authoritative Preview playhead",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "control.IsEnabled = false",
  "an animated Runtime Value must be read-only outside its Animation editor",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs",
  "public void SetPresentedValue(string value)",
  "dictionary controls must support a resolved display value without committing it as runtime payload",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationValueResolver.cs",
  "destination.Interpolation is \"linear\" or \"easeInOut\"",
  "Runtime Values must resolve numeric keyframes with the generic interpolation contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
  '["animationTimeline"] = new JsonObject { ["sequenceItems"] = false }',
  "Component Stack slots must share a parallel Screen-time origin",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  /"stackStates"[\s\S]*?"animationPresentation":"collectionFooter"[\s\S]*?"animationTimeline":\{"sequenceItems":false\}/,
  "forwarded Lock Screen slots must preserve the parallel Component Stack timeline",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "ModuleInstanceAnimationValueResolver.ResolveDisplayValue(",
  "Animation keyframe controls and Runtime Values must share one resolved-value presentation path",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "timelineDuration += 10",
  "the provisional authoring horizon must grow in session-only ten-frame steps",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "toggle.IsChecked == true ? naturalDuration : null",
  "Retime off must remove the persisted target-duration override",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  "FieldReferenceDurationFrames",
  "reference-duration lanes must resolve through the common owner timeline",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  '_animationEditor.CreateTargetContent(owner.Node, "")',
  "Screen-owned animation must live inside the General runtime category",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_animationEditor.CreateTargetContent(owner.Node, itemId)",
  "collection-item animation must live inside its owning runtime item",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorCollectionCardFactory.cs",
  "animationEditor.Create(node)",
  "animation must not return as a detached module-level editor card",
);
assertContains(
  "src/desktop-preview/conversationModuleResolver.ts",
  'new RuntimeOwnerTimeline(preview, preview, animation, themeTokens)',
  "Conversation must resolve generic owner timing in its owning frame resolver",
);
assertContains(
  "src/desktop-preview/runtimeOwnerTimeline.ts",
  "extendsOwnerDuration !== false",
  "the generic preview owner timeline must support late fields that do not advance collection sequencing",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "CreateAnimationActivationGlyph(",
  "Runtime fields must derive the sequencing/non-sequencing activation glyph from animation metadata",
);
assertDoesNotContain(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "resolveParameterAnimation",
  "Conversation renderable must not evaluate parameter tracks",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"subtitle": "Theme color behavior"[\s\S]*?"groupLayout": "verticalCards"/,
  "Theme Colors groups must use the shared vertical-card organization",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"id": "icons"[\s\S]*?"groupLayout": "verticalCards"/,
  "Theme Icons groups must use the shared vertical-card organization",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  '"pairLayout": "sharedHeader"',
  "palette-pair groups must opt into the generic shared Light/Dark header through layout metadata",
);
for (const semanticIcon of [
  "Neutral tint",
  "App colors",
  "Content colors",
  "Action and input colors",
  "Navigation and feedback colors",
  "Border colors",
  "Icon colors",
  "Icon sizes",
  "Font families",
  "Text sizes",
  "Style and line heights",
]) {
  assertSourceContains(
    "data/desktop-editor-spike.sqlite",
    editorLayoutSource,
    `"icon": "navigation-asset:${semanticIcon}"`,
    `Theme group '${semanticIcon}' must use its dedicated reusable system icon`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryPalettePairControl.cs",
  'ColumnDefinitions = new ColumnDefinitions("*,*");',
  "compact palette pairs must preserve two equal columns at narrow widths",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryPalettePairControl.cs",
  "firstParent.Children.Remove(_firstControl);",
  "compact palette pairs must detach existing controls before reusing them in shared-header composition",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  "var controlLabels = control.UseSharedPairHeader();",
  "shared palette-pair presentation must be applied to every control in the group",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"id": "typography"[\s\S]*?"groupLayout": "verticalCards"[\s\S]*?"id": "fontFamilies"[\s\S]*?"id": "typographySizes"[\s\S]*?"id": "typographyStyle"/,
  "Theme Typography must remain split into semantic vertical cards",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Common/ThemeNumericTokenCatalog.cs",
  'Token("theme.typography.size",',
  "the retired singular typography size token must not remain in the numeric token catalog",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "ClearTransientValues(scopeKey);",
  "runtime contract changes must invalidate stale session-only input values generically",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "ShouldUse(",
  "subcard organization must not be inferred from count or hierarchy depth",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  '"flatStack" => EditorSubcardLayout.FlatStack',
  "the documented flat-stack organization must remain available through generic layout metadata",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "_content.Content = null;",
  "internal navigation must detach reusable editor content before selecting another card",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  '"Messages"',
  "shared internal navigation must not know a concrete runtime collection",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "actorIdentityVisible = conversationType === \"group\"",
  "Individual Conversation must override Bubble actor identity presentation off",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "let y = top + gap - scrollOffset",
  "Conversation messages must stack from Header with messageGap before applying overflow scroll",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  'transition: "slide"',
  "Conversation message overflow must use the shared Theme Slide motion",
);
assertContains(
  "src/desktop-preview/bubbleComponentResolver.ts",
  "actorIdentityVisible",
  "Bubble must consume its parent-owned actor identity visibility override",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  "[\"source\"] = \"calculated\"",
  "parent-owned timeline frame inputs must be declared calculated",
);
for (const placeholderPlural of ["input(s)", "collection(s)", "instance(s)"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
    placeholderPlural,
    `runtime-input UI must use grammatical counts instead of ${placeholderPlural}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "Payload key:",
  "runtime API diagnostics must label persisted payload keys explicitly",
);
for (const dictionaryControl of [
  "spikes/desktop-editor-shell/EditorShell/DictionaryEmbeddedComponentControl.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryComponentPresetControl.cs",
]) {
  assertDoesNotContain(
    dictionaryControl,
    "#D6A638",
    "dictionary embedded action chrome must use the shared override visuals",
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "$\"Slot: {slot.Label}\"",
  "embedded breadcrumbs must identify the owning slot without duplicating component identity",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "new(\"Component\", component)",
  "embedded context metadata must identify the concrete component",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "new EditorContextIdentity(\"Variant\", activePresetName)",
  "embedded context metadata must identify the concrete variant",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "mockupsPreviewImagePreloadResult",
  "WebView image preload must return a serializable request id and expose a synchronous result poll",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "asset-missing",
  "WebView patches must reject unresolved interned asset references before DOM mutation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "mockupsMissingPreviewAssets",
  "the host must reconcile asset keys with the active WebView document before every patch",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "[\\\\s\\\"'<>)]",
  "data-URI compaction must not terminate generated SVG assets at literal parentheses",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "PreviewAssetRegistry.Compact(originalHtml)",
  "rendered frames must intern large assets before entering the shared frame cache",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "PreviewAssetRegistry.Keys(bodyContent)",
  "WebView patches must consume already compacted frame bodies",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "imageSourcesChanged",
  "resident registered assets must not force a decode-gated layer replacement on every animated image source change",
);
for (const forbiddenFontFallback of [
  "system-ui",
  "Apple Color Emoji",
  "Segoe UI Emoji",
  "Noto Color Emoji",
  "fontId === \"system\"",
]) {
  assertDoesNotContain(
    "src/desktop-preview/previewFontHelpers.ts",
    forbiddenFontFallback,
    `desktop render typography must not use host-system fallback '${forbiddenFontFallback}'`,
  );
}
assertContains(
  "src/desktop-preview/previewFontHelpers.ts",
  "Required production emoji font is unavailable",
  "missing contract emoji fonts must fail visibly instead of falling back to the host system",
);
assertContains(
  "src/desktop-preview/previewAssetResolver.ts",
  "Required production font file is missing",
  "missing production font files must fail before web rendering",
);
for (const requiredAssetIdentityTerm of ["stats.size", "stats.mtimeMs", "stats.ctimeMs"]) {
  assertContains(
    "src/desktop-preview/previewAssetResolver.ts",
    requiredAssetIdentityTerm,
    `preview video cache identity must include ${requiredAssetIdentityTerm}`,
  );
}
for (const requiredReferenceIdentityTerm of ["info.Length", "info.LastWriteTimeUtc.Ticks", "info.CreationTimeUtc.Ticks"]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/PreviewReferenceOverlay.cs",
    requiredReferenceIdentityTerm,
    `reference overlay cache identity must include ${requiredReferenceIdentityTerm}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  "ValueKind.TypographySystemStyle",
  "system-component typography must use its registered dictionary control route",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
  "TypographyStyleValue.CreateDefault(\"theme.typography.sizes.s\", \"theme.system\")",
  "new Keyboard variants must use the Theme system-font role",
);
assertContains(
  "src/desktop-preview/textInputBarComponentResolver.ts",
  "fontFamilyId: \"theme.system\"",
  "Text Input Bar must bind its owned text surface to the Theme system-font role",
);
assertContains(
  "src/desktop-preview/statusBarComponentResolver.ts",
  "fontFamilyId: \"theme.system\"",
  "Status Bar text must use the Theme system-font role",
);
assertContains(
  "src/desktop-preview/audioComponentResolver.ts",
  "resolveLabelComponentFromRecords",
  "Audio calculated text must delegate its final visual presentation to Label",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "ActionDurationInputValue(action, 1)",
  "declarative playback clocks must read the action duration input instead of a private fallback",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "Math.Max(0, delayMs) + durationMs",
  "motion action duration must include both declared delay and transition duration",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryMotionTimingControl.cs",
  "Unit: \"ms\"",
  "Motion Timing duration and delay subfields must declare millisecond units",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryMotionTimingControl.cs",
  "definition.DisplayLabel",
  "compound Motion Timing labels must use shared FieldDefinition unit formatting",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_workspace == EditorWorkspace.Design",
  "preview input processing must explicitly separate Design from Production",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_productionRuntimeResolver.Resolve",
  "Production preview must use the reference-only runtime resolver",
);
assertNoTerms(
  "spikes/desktop-editor-shell/EditorShell/ProductionPreviewRuntimeResolver.cs",
  ["ApplyTransientTestValues", "ComponentPreviewActions", "PlaybackTimeValue"],
);

function assertDesktopSystemTypographyData() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const fonts = new Map(
      (database.prepare("SELECT id, category FROM production_fonts").all() as { id: string; category: string }[])
        .map((font) => [font.id, font.category]),
    );
    const themes = database.prepare("SELECT id, tokens_json FROM themes").all() as {
      id: string;
      tokens_json: string;
    }[];
    for (const theme of themes) {
      const typography = jsonRecord(jsonRecord(jsonParse(theme.tokens_json)).typography);
      const systemFontId = typeof typography.systemFontFamilyId === "string"
        ? typography.systemFontFamilyId
        : "";
      if (!systemFontId || fonts.get(systemFontId) !== "text") {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `theme ${theme.id} systemFontFamilyId must reference a text production font`,
        );
      }
    }

    const keyboards = database
      .prepare("SELECT id, config_json, metadata_json FROM component_classes WHERE component_type = 'keyboard'")
      .all() as { id: string; config_json: string; metadata_json: string }[];
    for (const keyboard of keyboards) {
      const configFont = jsonRecord(jsonRecord(jsonParse(keyboard.config_json)).keyboard).typography;
      if (jsonRecord(configFont).fontFamilyId !== "theme.system") {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `Keyboard ${keyboard.id} config must use theme.system`,
        );
      }
      const presets = jsonArray(jsonRecord(jsonParse(keyboard.metadata_json)).presets).map(jsonRecord);
      for (const [index, preset] of presets.entries()) {
        const presetFont = jsonRecord(
          jsonRecord(jsonRecord(jsonRecord(preset.config).keyboard).typography),
        ).fontFamilyId;
        if (presetFont !== "theme.system") {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `Keyboard ${keyboard.id} variant ${index} must use theme.system`,
          );
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopDatabaseHasNoRetiredTimeFields() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const tables = database.prepare(
      "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'",
    ).all() as { name: string }[];
    for (const { name: table } of tables) {
      const columns = (database.prepare(`PRAGMA table_info("${table}")`).all() as { name: string; type: string }[])
        .filter((column) => column.type.toUpperCase().includes("TEXT"));
      if (columns.length === 0) continue;
      const quoted = columns.map((column) => `"${column.name}"`).join(", ");
      for (const row of database.prepare(`SELECT ${quoted} FROM "${table}"`).all() as Record<string, unknown>[]) {
        for (const column of columns) {
          const value = String(row[column.name] ?? "");
          for (const retired of retiredTimeFields) {
            if (value.includes(retired)) {
              addViolation("data/desktop-editor-spike.sqlite", `${table}.${column.name} contains retired time field ${retired}`);
            }
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopDatabaseHasNoRetiredEditorLayouts() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const retired = ["status_bar", "navigation_bar", "navigation.status_bars", "navigation.navigation_bars"];
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const placeholders = retired.map(() => "?").join(",");
    const rows = database.prepare(
      `SELECT record_class_id FROM editor_layouts WHERE record_class_id IN (${placeholders})`,
    ).all(...retired) as { record_class_id: string }[];
    for (const row of rows) {
      addViolation("data/desktop-editor-spike.sqlite", `contains retired editor layout ${row.record_class_id}`);
    }
  } finally {
    database.close();
  }
}

function assertDesktopDatabaseHasCurrentDefinitionLifecycle() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const genericApps = database.prepare(
      "SELECT id FROM apps WHERE record_class_id = 'app.generic'",
    ).all() as { id: string }[];
    for (const app of genericApps) {
      addViolation("data/desktop-editor-spike.sqlite", `contains retired generic App ${app.id}`);
    }
    const genericLayouts = database.prepare(
      "SELECT record_class_id FROM editor_layouts WHERE record_class_id IN ('app.generic', 'module.generic')",
    ).all() as { record_class_id: string }[];
    for (const layout of genericLayouts) {
      addViolation("data/desktop-editor-spike.sqlite", `contains retired generic editor layout ${layout.record_class_id}`);
    }
  } finally {
    database.close();
  }
}

function assertModuleInstanceRuntimePayloadsMatchContracts() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const instances = database.prepare(
      "SELECT mi.id, mi.content_json, m.design_preview_json FROM module_instances mi JOIN modules m ON m.id = mi.module_id",
    ).all() as { id: string; content_json: string; design_preview_json: string }[];
    for (const instance of instances) {
      const content = jsonRecord(jsonParse(instance.content_json));
      const contract = jsonRecord(jsonParse(instance.design_preview_json));
      for (const input of jsonArray(contract.inputs).map(jsonRecord)) {
        const key = typeof input.jsonKey === "string" ? input.jsonKey : "";
        const source = typeof input.source === "string" ? input.source : "runtime";
        if (!key) continue;
        if (source === "runtime" && !(key in content)) {
          addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} is missing runtime input ${key}`);
        }
        if (source !== "runtime" && key in content) {
          addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} persists parent-owned input ${key}`);
        }
      }
      for (const collection of jsonArray(contract.collections).map(jsonRecord)) {
        const key = typeof collection.sourceCollectionJsonKey === "string"
          ? collection.sourceCollectionJsonKey
          : typeof collection.jsonKey === "string" ? collection.jsonKey : "";
        if (!key || !Array.isArray(content[key])) {
          addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} is missing runtime collection ${key}`);
          continue;
        }
        const fields = jsonArray(collection.fields).map(jsonRecord);
        for (const item of jsonArray(content[key]).map(jsonRecord)) {
          if (typeof item.id !== "string" || !item.id) {
            addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} collection ${key} contains an item without stable id`);
          }
          for (const field of fields) {
            const fieldKey = typeof field.jsonKey === "string" ? field.jsonKey : "";
            const source = typeof field.source === "string" ? field.source : "runtime";
            if (fieldKey && source === "runtime" && !(fieldKey in item)) {
              addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} collection ${key} item is missing ${fieldKey}`);
            }
          }
        }
      }
    }

    const shots = database.prepare(
      "SELECT s.id, s.duration_frames, COALESCE(SUM(mi.duration_frames), 0) AS expected FROM shots s LEFT JOIN module_instances mi ON mi.shot_id = s.id GROUP BY s.id",
    ).all() as { id: string; duration_frames: number; expected: number }[];
    for (const shot of shots) {
      if (shot.duration_frames !== Math.max(1, shot.expected)) {
        addViolation("data/desktop-editor-spike.sqlite", `Shot ${shot.id} duration does not equal its cut-slot duration sum`);
      }
    }
  } finally {
    database.close();
  }
}


assertDesktopSystemTypographyData();
assertSharedEditorSurfacesHaveNoConcreteEditors();
assertDesktopDatabaseHasNoRetiredTimeFields();
assertDesktopDatabaseHasNoRetiredEditorLayouts();
assertDesktopDatabaseHasCurrentDefinitionLifecycle();
assertDesktopRuntimeInputValueKindsAreCanonical();
assertModuleInstanceRuntimePayloadsMatchContracts();

if (violations.length > 0) {
  console.error("Desktop preview architecture check failed:");
  for (const violation of violations) {
    console.error(`- ${violation}`);
  }
  process.exit(1);
}

console.log("Desktop preview architecture boundaries validated.");
