import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentManifestEntry,
} from "../src/desktop-preview/desktopPreviewComponents.js";
import { componentRenderableFactories } from "../src/desktop-preview/componentClassRenderableRegistry.js";

const root = process.cwd();
const previewRoot = path.join(root, "src", "desktop-preview");

const violations: string[] = [];
const allowedComponentNodeTypes = new Set(["component_preview_unsupported"]);
const desktopPreviewPaintNodeTypes = new Set([
  "avatar",
  "component_preview_unsupported",
  "design_preview_surface",
  "group",
  "icon_token",
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
  "waveform_bar",
]);

function relative(filePath: string) {
  return path.relative(root, filePath);
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

function assertDoesNotContain(relativePath: string, term: string, message: string) {
  const source = readText(relativePath);
  if (source.includes(term)) {
    addViolation(relativePath, message);
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

if (existsSync(path.join(previewRoot, "webPreviewBridge.ts"))) {
  addViolation(
    "src/desktop-preview/webPreviewBridge.ts",
    "central web preview bridge must not exist",
  );
}

for (const removedLegacyPath of [
  "src/debug-ui",
  "src/debug-server",
  "src/electron",
  "src/remotion",
  "src/visual/adapters/react",
  "src/visual/layout",
  "src/visual/modules",
  "src/visual/validation",
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
  "textInputBar",
  "keyboard",
  "video",
  "statusBar",
  "navigationBar",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
  "component_text_input",
  "component_keyboard",
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
  "message_bubble",
  "audio_message",
  "button_icon",
  "status_bar_item",
  "navigation_bar_item",
  "keyboard_key",
  "text_input_bar_",
  "video_message",
  "status_indicators",
]);

assertNoTerms("src/desktop-preview/componentRenderableCommon.ts", [
  "label",
  "avatar",
  "buttonIcon",
  "audio",
  "textInputBar",
  "keyboard",
  "video",
  "statusBar",
  "navigationBar",
  "waveform",
  "badge",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
  "component_text_input",
  "component_keyboard",
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
assertDoesNotContain(
  "src/desktop-preview/systemBarRenderables.ts",
  "systemBarType:",
  "shared system bar renderables must not emit system-bar identity metadata into the final paint tree",
);
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

  const nodeTypePattern = /type:\s*["']([^"']+)["']/g;
  let nodeTypeMatch: RegExpExecArray | null;
  while ((nodeTypeMatch = nodeTypePattern.exec(source)) !== null) {
    const nodeType = nodeTypeMatch[1] ?? "";
    if (nodeType.startsWith("component_") && !allowedComponentNodeTypes.has(nodeType)) {
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
if (payloadSource.includes("device:")) {
  addViolation(
    "src/desktop-preview/designPreviewPayload.ts",
    "design preview payload must expose previewFrame, not device",
  );
}
if (payloadSource.includes('"statusBar"') || payloadSource.includes('"navigationBar"')) {
  addViolation(
    "src/desktop-preview/designPreviewPayload.ts",
    "status/navigation bars must route as componentClass, not top-level preview kinds",
  );
}

const componentSeedSource = readText(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
);
const spikeDatabaseSource = readText(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
);
const seededComponentClasses = new Set(
  [...componentSeedSource.matchAll(/NewComponentSeed\("([^"]+)"/g)]
    .map((match) => match[1])
    .filter((value): value is string => typeof value === "string" && value.length > 0),
);
for (const componentClass of seededComponentClasses) {
  if (!desktopPreviewComponents[componentClass]) {
    addViolation(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
      `seeded component class "${componentClass}" is missing from desktop preview manifest`,
    );
  }
  if (!routedComponentClasses.has(componentClass)) {
    addViolation(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
      `seeded component class "${componentClass}" is missing from desktop preview registry`,
    );
  }
}
if (!spikeDatabaseSource.includes("ComponentSeedRows.Select((seed) => seed.RecordClassId)")) {
  addViolation(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
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
]);
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
  "video: {",
  "desktop preview component manifest must route video as an owning component module",
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
  "video: (payload)",
  "component renderable registry must route the current video component type",
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
assertContains(
  "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs",
  "ComponentPreset",
  "embedded component preset selection must have a dedicated dictionary value kind",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryValueControlFactory.cs",
  "ValueKind.ComponentPreset",
  "component preset fields must use their dedicated dictionary control",
);
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
    "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
    `{ "id": "${embeddedPresetField}"`,
    `embedded preset field "${embeddedPresetField}" must not be shown as a separate layout row`,
  );
}

assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
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
  "BuildEditorCards(editorNode, node)",
  "component editor layout node and data node must stay separated so presets edit preset config",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "presetSourceNode.Kind != ProjectTreeNodeKind.ComponentPreset",
  "Save preset must only be offered for a concrete selected component preset",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "EndsWith(\"::preset::default\", StringComparison.Ordinal)",
  "first component class selection must prefer the protected Default preset",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "Component presets can only be saved from an active selected preset.",
  "component preset saving must reject ambiguous parent component class configs",
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
  "Component Preset: {row.Name} · {preset.Name}",
  "component preset usage must scan references stored inside other presets",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsage.cs",
  "ProjectTreeNodeKind.ComponentPreset, id",
  "theme system bar references must mark component presets, not parent classes",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "GetComponentPresetReferenceOptionsByType(projectId, \"status_bar\"",
  "theme status bar selector must list component presets",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "GetComponentPresetReferenceOptionsByType(projectId, \"navigation_bar\"",
  "theme navigation bar selector must list component presets",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "[\"id\"] = DefaultComponentPresetId",
  "component class normalization must create a Default preset",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "[\"protected\"] = true",
  "Default component preset must be protected in stored metadata",
);

if (violations.length > 0) {
  console.error("Desktop preview architecture check failed:");
  for (const violation of violations) {
    console.error(`- ${violation}`);
  }
  process.exit(1);
}

console.log("Desktop preview architecture boundaries validated.");
