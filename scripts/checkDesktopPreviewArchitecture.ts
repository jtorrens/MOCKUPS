import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentManifestEntry,
} from "../src/desktop-preview/desktopPreviewComponents.js";
import { componentRenderableFactoryKeys } from "../src/desktop-preview/componentClassRenderableRegistry.js";

const root = process.cwd();
const previewRoot = path.join(root, "src", "desktop-preview");

const violations: string[] = [];
const allowedComponentNodeTypes = new Set(["component_preview_unsupported"]);
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

if (existsSync(path.join(previewRoot, "webPreviewBridge.ts"))) {
  addViolation(
    "src/desktop-preview/webPreviewBridge.ts",
    "central web preview bridge must not exist",
  );
}

assertNoTerms("src/desktop-preview/renderDesignPreviewHtml.tsx", [
  "label",
  "avatar",
  "buttonIcon",
  "audio",
  "statusBar",
  "navigationBar",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
  "status_bar",
  "navigation_bar",
]);

assertNoTerms("src/desktop-preview/componentRenderableCommon.ts", [
  "label",
  "avatar",
  "buttonIcon",
  "audio",
  "statusBar",
  "navigationBar",
  "waveform",
  "badge",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
]);

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

const routedComponentClasses = new Set(componentRenderableFactoryKeys);
for (const [componentClass, entry] of manifestEntries) {
  if (entry.category === "system") continue;
  if (!routedComponentClasses.has(componentClass as (typeof componentRenderableFactoryKeys)[number])) {
    addViolation(
      "src/desktop-preview/componentClassRenderableRegistry.ts",
      `component class "${componentClass}" is missing from component renderable registry`,
    );
  }
}

const allowedComponentImports: Record<string, Set<string>> = {};
for (const [, entry] of manifestEntries) {
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
    const isConcreteComponentImport =
      /(?:label|avatar|buttonIcon|audio)Component(?:Resolver|Renderable)\.js$/.test(target);
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

assertNoTerms("spikes/desktop-editor-shell/MainWindow.axaml.cs", [
  "Current class values",
]);

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
  "EndsWith(\"::preset::default\", StringComparison.Ordinal)",
  "first component class selection must prefer the protected Default preset",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanRenameDirectly => Kind is ProjectTreeNodeKind.ComponentPreset && !IsProtected",
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
