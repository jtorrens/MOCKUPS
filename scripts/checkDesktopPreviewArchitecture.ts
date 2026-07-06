import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";

const root = process.cwd();
const previewRoot = path.join(root, "src", "desktop-preview");

const violations: string[] = [];

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

const allowedComponentImports: Record<string, Set<string>> = {
  "src/desktop-preview/avatarComponentResolver.ts": new Set([
    "./labelComponentResolver.js",
  ]),
  "src/desktop-preview/avatarComponentRenderable.ts": new Set([
    "./labelComponentRenderable.js",
  ]),
  "src/desktop-preview/buttonIconComponentResolver.ts": new Set([
    "./labelComponentResolver.js",
  ]),
  "src/desktop-preview/buttonIconComponentRenderable.ts": new Set([
    "./labelComponentRenderable.js",
  ]),
  "src/desktop-preview/audioComponentResolver.ts": new Set([
    "./avatarComponentResolver.js",
    "./buttonIconComponentResolver.js",
  ]),
  "src/desktop-preview/audioComponentRenderable.ts": new Set([
    "./avatarComponentRenderable.js",
    "./buttonIconComponentRenderable.js",
  ]),
};

for (const filePath of walkFiles(previewRoot)) {
  const relativePath = relative(filePath);
  if (registryFiles.has(relativePath)) continue;

  const imports = importTargets(readFileSync(filePath, "utf8"));
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
