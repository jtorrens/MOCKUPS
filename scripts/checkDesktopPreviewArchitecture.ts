import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentManifestEntry,
} from "../src/desktop-preview/desktopPreviewComponents.js";
import { componentRenderableFactories } from "../src/desktop-preview/componentClassRenderableRegistry.js";
import { renderableNodeTypes } from "../src/visual/renderable/types.js";

const root = process.cwd();
const previewRoot = path.join(root, "src", "desktop-preview");

const violations: string[] = [];
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
  "src/desktop-preview/systemBarComponentContract.ts",
  "src/desktop-preview/systemBarPreviewResolver.ts",
  "src/desktop-preview/systemBarRenderables.ts",
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
  "\"navigation.status_bars\"",
  "\"navigation.navigation_bars\"",
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
  "src/domain/fields/themeFields.ts",
  /statusBarId:[\s\S]*?tableId:\s*"component_presets"/,
  "theme status bar field must reference component presets, not legacy status_bars",
);
assertMatches(
  "src/domain/fields/themeFields.ts",
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
  "buttonIcon: {",
  "desktop preview component manifest must use the current button icon component type",
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
  "buttonIcon: (payload)",
  "component renderable registry must route the current button icon component type",
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
  "src/domain/schemas/componentClass.ts",
  "src/domain/fields/componentClassFields.ts",
  "src/domain/repository/fixtures/exampleDataset.ts",
  "src/domain/resolvers/resolveChatScreen.ts",
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
    "src/persistence/sqlite/createDatabase.ts",
    `componentType: "${legacySeededComponentType}"`,
    `legacy component type ${legacySeededComponentType} must not be seeded as componentType`,
  );
  assertDoesNotContain(
    "src/persistence/sqlite/createDatabase.ts",
    `type: "${legacySeededComponentType}"`,
    `legacy component type ${legacySeededComponentType} must not be seeded as component_type`,
  );
}
for (const componentType of Object.keys(desktopPreviewComponents)) {
  assertContains(
    "src/domain/schemas/componentClass.ts",
    `"${componentType}"`,
    `component class schema must include manifest component type ${componentType}`,
  );
  assertContains(
    "src/domain/fields/componentClassFields.ts",
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
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
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
  "componentPresetConfig(componentBaseConfigs, \"buttonIcon\", badgeSlot.presetId)",
  "audio badge preview must resolve the selected button icon preset, not the default button icon config",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "NormalizeComponentConfigJsonForPreview",
  "design preview payloads must normalize embedded preset references before web rendering",
);
for (const embeddedPresetField of [
  "component.avatar.label.presetId",
  "component.buttonIcon.label.presetId",
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
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
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

if (violations.length > 0) {
  console.error("Desktop preview architecture check failed:");
  for (const violation of violations) {
    console.error(`- ${violation}`);
  }
  process.exit(1);
}

console.log("Desktop preview architecture boundaries validated.");
