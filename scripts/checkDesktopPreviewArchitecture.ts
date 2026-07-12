import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentManifestEntry,
} from "../src/desktop-preview/desktopPreviewComponents.js";
import { componentRenderableFactories } from "../src/desktop-preview/componentClassRenderableRegistry.js";
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

function assertMatches(relativePath: string, pattern: RegExp, message: string) {
  const source = readText(relativePath);
  if (!pattern.test(source)) {
    addViolation(relativePath, message);
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
    for (const row of rows) {
      const metadata = JSON.parse(row.metadata_json || "{}") as { presets?: unknown };
      const presetIds = new Set(
        Array.isArray(metadata.presets)
          ? metadata.presets
              .filter((preset): preset is { id?: unknown } => typeof preset === "object" && preset !== null)
              .map((preset) => preset.id)
              .filter((id): id is string => typeof id === "string" && id.length > 0)
          : [],
      );
      variantsByClassId.set(row.id, presetIds);
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
      const classConfig = JSON.parse(row.config_json || "{}");
      validateValue(row, classConfig, "config");
      const metadata = JSON.parse(row.metadata_json || "{}") as { presets?: unknown };
      if (!Array.isArray(metadata.presets)) continue;
      metadata.presets.forEach((preset, index) => validateValue(row, preset, `metadata.presets[${index}]`));
      const defaultPreset = metadata.presets.find((preset) =>
        typeof preset === "object"
        && preset !== null
        && (preset as { id?: unknown }).id === "default") as { config?: unknown } | undefined;
      if (!defaultPreset || JSON.stringify(defaultPreset.config ?? {}) !== JSON.stringify(classConfig)) {
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
  try {
    return JSON.parse(value || "{}") as unknown;
  } catch {
    return {};
  }
}

function jsonRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function jsonArray(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
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
          || typeof action.durationCollectionJsonKey === "string"
          || typeof action.durationSeconds === "number"
          || typeof action.durationThemeToken === "string"
          || typeof action.durationMotionConfigPath === "string";
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
  ]);
  const requiredMessageTimingKeys = new Set([
    "writeOnDurationFrames",
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

function moduleFile(entry: DesktopPreviewComponentManifestEntry, kind: "resolver" | "renderable") {
  return `src/desktop-preview/${entry[kind].replace(/^\.\//, "")}.ts`;
}

function moduleImport(entry: DesktopPreviewComponentManifestEntry, kind: "resolver" | "renderable") {
  return `${entry[kind]}.js`;
}

for (const [componentClass, entry] of manifestEntries) {
  for (const kind of ["contract", "resolver", "renderable"] as const) {
    const filePath = path.join(previewRoot, `${entry[kind].replace(/^\.\//, "")}.ts`);
    if (!existsSync(filePath)) {
      addViolation(
        "src/desktop-preview/desktopPreviewComponents.ts",
        `manifest entry "${componentClass}" points to missing ${kind} file "${entry[kind]}"`,
      );
    }
  }

  for (const child of entry.embeds) {
    if (!desktopPreviewComponents[child]) {
      addViolation(
        "src/desktop-preview/desktopPreviewComponents.ts",
        `manifest entry "${componentClass}" embeds unknown component "${child}"`,
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

allowedComponentImports["src/desktop-preview/conversationModuleRenderable.ts"] = new Set([
  "./avatarComponentRenderable.js",
  "./avatarComponentResolver.js",
  "./bubbleComponentRenderable.js",
  "./bubbleComponentResolver.js",
  "./keyboardComponentRenderable.js",
  "./keyboardComponentResolver.js",
  "./iconRowComponentRenderable.js",
  "./iconRowComponentResolver.js",
  "./navigationBarComponentRenderable.js",
  "./navigationBarComponentResolver.js",
  "./statusBarComponentRenderable.js",
  "./statusBarComponentResolver.js",
  "./textInputBarComponentRenderable.js",
  "./textInputBarComponentResolver.js",
]);

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

const componentSeedSourceFiles = [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
];
const componentSeedSource = componentSeedSourceFiles
  .filter((relativePath) => existsSync(path.join(root, relativePath)))
  .map((relativePath) => readText(relativePath))
  .join("\n");
const spikeDatabaseDataPaths = readdirSync(path.join(root, "spikes/desktop-editor-shell/Data"))
  .filter((entry) => /^SpikeDatabase.*\.cs$/.test(entry))
  .map((entry) => `spikes/desktop-editor-shell/Data/${entry}`);
const editorLayoutSource = readText(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.EditorLayouts.cs",
);
const seededComponentClasses = new Set(
  [...componentSeedSource.matchAll(/NewComponentSeed\("([^"]+)"/g)]
    .map((match) => match[1])
    .filter((value): value is string => typeof value === "string" && value.length > 0),
);
for (const componentClass of seededComponentClasses) {
  if (!desktopPreviewComponents[componentClass]) {
    addViolation(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
      `seeded component class "${componentClass}" is missing from desktop preview manifest`,
    );
  }
  if (!routedComponentClasses.has(componentClass)) {
    addViolation(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs",
      `seeded component class "${componentClass}" is missing from desktop preview registry`,
    );
  }
}
if (!editorLayoutSource.includes("ComponentSeedRows.Select((seed) => seed.RecordClassId)")) {
  addViolation(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.EditorLayouts.cs",
    "component editor layouts must be seeded from ComponentSeedRows so new components get layouts automatically",
  );
}
for (const componentClass of Object.keys(desktopPreviewComponents)) {
  if (!seededComponentClasses.has(componentClass)) {
    addViolation(
      "src/desktop-preview/desktopPreviewComponents.ts",
      `manifest component class "${componentClass}" is not seeded in the desktop editor`,
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
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.EditorLayouts.cs",
    forbiddenLegacyLayoutTerm,
    `legacy system bar layout term ${forbiddenLegacyLayoutTerm} must not return; use component layouts`,
  );
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
  "src/desktop-preview/desktopPreviewComponents.ts",
  "./systemBar",
  "system bars must be declared as explicit status/navigation component modules, not shared manifest entrypoints",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "./systemBar",
  "component registry must route status/navigation through their explicit component modules",
);
assertContains(
  "src/desktop-preview/desktopPreviewComponents.ts",
  "audio: {",
  "desktop preview component manifest must use the current audio component type",
);
assertContains(
  "src/desktop-preview/desktopPreviewComponents.ts",
  "textBox: {",
  "desktop preview component manifest must route text box as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewComponents.ts",
  "textInputBar: {",
  "desktop preview component manifest must route text input bar as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewComponents.ts",
  "keyboard: {",
  "desktop preview component manifest must route keyboard as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewComponents.ts",
  "media: {",
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
  "src/desktop-preview/desktopPreviewComponents.ts",
  "audio_message",
  "legacy audio_message component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewComponents.ts",
  "button_icon",
  "legacy button_icon component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewComponents.ts",
  "text_input_bar",
  "legacy text_input_bar component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewComponents.ts",
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
  "componentPresetConfig(componentBaseConfigs, \"button\", badgeSlot.presetId)",
  "audio badge preview must resolve the selected Button preset",
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
  "component.audio.badge.presetId",
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
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "Kind == ProjectTreeNodeKind.ComponentClass",
  "parent component classes must expose direct rename",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "Kind == ProjectTreeNodeKind.ComponentPreset && !IsProtected",
  "protected component presets must not expose direct rename",
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
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsage.cs",
  "Component Variant: {row.Name} · {preset.Name}",
  "component variant usage must scan references stored inside other variants",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsage.cs",
  "ProjectTreeNodeKind.ComponentPreset, id",
  "theme system bar references must mark component presets, not parent classes",
);
assertAnyContains(
  spikeDatabaseDataPaths,
  "GetComponentPresetReferenceOptionsByType(projectId, \"status_bar\"",
  "theme status bar selector must list component presets",
);
assertAnyContains(
  spikeDatabaseDataPaths,
  "GetComponentPresetReferenceOptionsByType(projectId, \"navigation_bar\"",
  "theme navigation bar selector must list component presets",
);
assertAnyContains(
  spikeDatabaseDataPaths,
  "[\"id\"] = DefaultComponentPresetId",
  "component class normalization must create a Default preset",
);
assertAnyContains(
  spikeDatabaseDataPaths,
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
    "spikes/desktop-editor-shell/Data/SpikeDatabase.EditorLayouts.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorCollectionCardFactory.cs",
  ]) {
    assertDoesNotContain(
      file,
      retiredInstanceEditorTerm,
      `module instances must not restore the retired Conversation-specific editor route (${retiredInstanceEditorTerm})`,
    );
  }
}
assertDoesNotContain(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "parseObject(payload.instanceJson).behavior",
  "module renderables must consume the canonical runtime preview payload instead of a second instance behavior channel",
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
  "$\"{componentLabel} / {slotPresetName}\"",
  "embedded breadcrumbs must distinguish component class and concrete variant",
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

assertDesktopSystemTypographyData();
assertDesktopDatabaseHasNoRetiredTimeFields();
assertDesktopDatabaseHasNoRetiredEditorLayouts();

if (violations.length > 0) {
  console.error("Desktop preview architecture check failed:");
  for (const violation of violations) {
    console.error(`- ${violation}`);
  }
  process.exit(1);
}

console.log("Desktop preview architecture boundaries validated.");
