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
  const nodeTypePattern = /type:\s*["'](component_[^"']+)["']/g;
  let nodeTypeMatch: RegExpExecArray | null;
  while ((nodeTypeMatch = nodeTypePattern.exec(source)) !== null) {
    const nodeType = nodeTypeMatch[1] ?? "";
    if (!allowedComponentNodeTypes.has(nodeType)) {
      addViolation(
        relativePath,
        `component-specific renderable node type "${nodeType}" must be a generic primitive type`,
      );
    }
  }
  if (registryFiles.has(relativePath)) continue;

  const imports = importTargets(source);
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

if (violations.length > 0) {
  console.error("Desktop preview architecture check failed:");
  for (const violation of violations) {
    console.error(`- ${violation}`);
  }
  process.exit(1);
}

console.log("Desktop preview architecture boundaries validated.");
