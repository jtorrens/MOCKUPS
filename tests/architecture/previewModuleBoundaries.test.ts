import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import ts from "typescript";

import { componentRenderableFactories } from "../../src/desktop-preview/componentClassRenderableRegistry.js";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentManifestEntry,
} from "../../src/desktop-preview/desktopPreviewComponents.js";
import {
  desktopPreviewModules,
  type DesktopPreviewModuleManifestEntry,
} from "../../src/desktop-preview/desktopPreviewModules.js";
import { moduleRenderableFactories } from "../../src/desktop-preview/moduleRenderableRegistry.js";

const repositoryRoot = process.cwd();
const previewDirectory = path.join(repositoryRoot, "src", "desktop-preview");
const manifestPath = path.join(
  previewDirectory,
  "desktopPreviewManifest.json",
);
const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as {
  schemaVersion: unknown;
  components: Record<string, DesktopPreviewComponentManifestEntry>;
  modules: Record<string, DesktopPreviewModuleManifestEntry>;
};

type OwnerKind = "contract" | "resolver" | "renderable";

type ConcreteOwnerImport = {
  owner: string;
  ownerKind: "component" | "module";
  kind: OwnerKind;
};

function repositoryPath(fullPath: string): string {
  return path.relative(repositoryRoot, fullPath).split(path.sep).join("/");
}

function ownerFile(
  route: string,
): string {
  return path.join(
    previewDirectory,
    `${route.replace(/^\.\//, "")}.ts`,
  );
}

function ownerImport(route: string): string {
  return `${route}.js`;
}

function previewSourceFiles(): string[] {
  return readdirSync(previewDirectory, {
    withFileTypes: true,
  })
    .filter((entry) =>
      entry.isFile()
      && (entry.name.endsWith(".ts")
        || entry.name.endsWith(".tsx")))
    .map((entry) => path.join(previewDirectory, entry.name))
    .sort();
}

function sourceFile(fullPath: string): ts.SourceFile {
  return ts.createSourceFile(
    fullPath,
    readFileSync(fullPath, "utf8"),
    ts.ScriptTarget.Latest,
    true,
    fullPath.endsWith(".tsx")
      ? ts.ScriptKind.TSX
      : ts.ScriptKind.TS,
  );
}

function moduleImports(
  fullPath: string,
): string[] {
  const imports: string[] = [];
  const visit = (node: ts.Node) => {
    if ((ts.isImportDeclaration(node)
        || ts.isExportDeclaration(node))
      && node.moduleSpecifier
      && ts.isStringLiteral(node.moduleSpecifier)) {
      imports.push(node.moduleSpecifier.text);
    }
    if (ts.isCallExpression(node)
      && node.expression.kind === ts.SyntaxKind.ImportKeyword
      && node.arguments.length === 1
      && ts.isStringLiteral(node.arguments[0]!)) {
      imports.push(node.arguments[0].text);
    }
    ts.forEachChild(node, visit);
  };
  visit(sourceFile(fullPath));
  return imports;
}

function exactKeys(
  value: object,
): string[] {
  return Object.keys(value).sort();
}

function validateManifestEntry(
  owner: string,
  entry: DesktopPreviewComponentManifestEntry
    | DesktopPreviewModuleManifestEntry,
) {
  assert.deepEqual(
    exactKeys(entry),
    "category" in entry
      ? ["category", "contract", "embeds", "renderable", "resolver"]
      : ["contract", "embeds", "label", "renderable", "resolver"],
    `${owner} has fields outside the current manifest contract`,
  );
  assert.equal(
    new Set(entry.embeds).size,
    entry.embeds.length,
    `${owner} repeats an embedded dependency`,
  );
  for (const child of entry.embeds) {
    assert.ok(
      Object.hasOwn(desktopPreviewComponents, child),
      `${owner} embeds unknown Component ${child}`,
    );
  }
  for (const kind of [
    "contract",
    "resolver",
    "renderable",
  ] as const) {
    const route = entry[kind];
    assert.match(
      route,
      /^\.\/[A-Za-z][A-Za-z0-9]*$/,
      `${owner} has an unsafe ${kind} route`,
    );
    assert.ok(
      existsSync(ownerFile(route)),
      `${owner} points to missing ${kind} owner ${route}`,
    );
  }
}

function concreteOwnerImports(): Map<
  string,
  ConcreteOwnerImport
> {
  const imports = new Map<
    string,
    ConcreteOwnerImport
  >();
  for (const [owner, entry] of
    Object.entries(desktopPreviewComponents)) {
    for (const kind of [
      "contract",
      "resolver",
      "renderable",
    ] as const) {
      imports.set(ownerImport(entry[kind]), {
        owner,
        ownerKind: "component",
        kind,
      });
    }
  }
  for (const [owner, entry] of
    Object.entries(desktopPreviewModules)) {
    for (const kind of [
      "contract",
      "resolver",
      "renderable",
    ] as const) {
      imports.set(ownerImport(entry[kind]), {
        owner,
        ownerKind: "module",
        kind,
      });
    }
  }
  return imports;
}

function ownerFiles(): Map<
  string,
  {
    owner: string;
    ownerKind: "component" | "module";
    entry: DesktopPreviewComponentManifestEntry
      | DesktopPreviewModuleManifestEntry;
  }
> {
  const files = new Map<
    string,
    {
      owner: string;
      ownerKind: "component" | "module";
      entry: DesktopPreviewComponentManifestEntry
        | DesktopPreviewModuleManifestEntry;
    }
  >();
  for (const [owner, entry] of
    Object.entries(desktopPreviewComponents)) {
    for (const kind of [
      "contract",
      "resolver",
      "renderable",
    ] as const) {
      files.set(repositoryPath(ownerFile(entry[kind])), {
        owner,
        ownerKind: "component",
        entry,
      });
    }
  }
  for (const [owner, entry] of
    Object.entries(desktopPreviewModules)) {
    for (const kind of [
      "contract",
      "resolver",
      "renderable",
    ] as const) {
      files.set(repositoryPath(ownerFile(entry[kind])), {
        owner,
        ownerKind: "module",
        entry,
      });
    }
  }
  return files;
}

const registryPaths = new Set([
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "src/desktop-preview/generatedComponentScaffoldRegistry.ts",
  "src/desktop-preview/moduleRenderableRegistry.ts",
  "src/desktop-preview/generatedModuleScaffoldRegistry.ts",
]);

function assertFactoryObjectsContainRoutesOnly(
  relativePath: string,
) {
  const parsed = sourceFile(
    path.join(repositoryRoot, relativePath),
  );
  const violations: string[] = [];
  for (const statement of parsed.statements) {
    if (!ts.isVariableStatement(statement)) continue;
    for (const declaration of
      statement.declarationList.declarations) {
      if (!ts.isIdentifier(declaration.name)
        || !declaration.name.text.endsWith("Factories")
        || !declaration.initializer
        || !ts.isObjectLiteralExpression(
          declaration.initializer)) {
        continue;
      }
      for (const property of
        declaration.initializer.properties) {
        if (ts.isSpreadAssignment(property)) continue;
        if (!ts.isPropertyAssignment(property)
          || !ts.isArrowFunction(property.initializer)) {
          violations.push(
            `${property.getText(parsed)} is not a direct route`,
          );
          continue;
        }
        const body = property.initializer.body;
        if (!ts.isCallExpression(body)) {
          violations.push(
            `${property.name.getText(parsed)} contains logic instead of one owner call`,
          );
          continue;
        }
        const inspect = (node: ts.Node) => {
          if (ts.isConditionalExpression(node)
            || ts.isBinaryExpression(node)
            || ts.isObjectLiteralExpression(node)
            || ts.isElementAccessExpression(node)
            || ts.isAwaitExpression(node)) {
            violations.push(
              `${property.name.getText(parsed)} contains ${ts.SyntaxKind[node.kind]}`,
            );
          }
          ts.forEachChild(node, inspect);
        };
        inspect(body);
      }
    }
  }
  assert.deepEqual(
    violations,
    [],
    `${relativePath} must contain routing only`,
  );
}

test("the Preview manifest is the exact executable owner catalog", () => {
  assert.equal(manifest.schemaVersion, 2);
  assert.deepEqual(
    exactKeys(manifest.components),
    exactKeys(desktopPreviewComponents),
  );
  assert.deepEqual(
    exactKeys(manifest.modules),
    exactKeys(desktopPreviewModules),
  );
  for (const [owner, entry] of
    Object.entries(desktopPreviewComponents)) {
    assert.ok(
      ["atom", "component", "system"]
        .includes(entry.category),
      `${owner} has unsupported category ${entry.category}`,
    );
    validateManifestEntry(owner, entry);
  }
  for (const [owner, entry] of
    Object.entries(desktopPreviewModules)) {
    assert.ok(entry.label.trim(), `${owner} has no label`);
    validateManifestEntry(owner, entry);
  }
  assert.deepEqual(
    exactKeys(componentRenderableFactories),
    exactKeys(desktopPreviewComponents),
    "Component registry and manifest differ",
  );
  assert.deepEqual(
    exactKeys(moduleRenderableFactories),
    exactKeys(desktopPreviewModules),
    "Module registry and manifest differ",
  );
});

test("the TypeScript import graph permits only declared concrete owners", () => {
  const concreteImports = concreteOwnerImports();
  const concreteFiles = ownerFiles();
  const violations: string[] = [];
  for (const fullPath of previewSourceFiles()) {
    const relativePath = repositoryPath(fullPath);
    const importingOwner = concreteFiles.get(relativePath);
    for (const imported of moduleImports(fullPath)) {
      const concrete = concreteImports.get(imported);
      if (!concrete) continue;
      if (registryPaths.has(relativePath)) continue;

      if (!importingOwner) {
        violations.push(
          `${relativePath} imports concrete ${concrete.ownerKind} ${concrete.owner}`,
        );
        continue;
      }
      if (concrete.ownerKind === importingOwner.ownerKind
        && concrete.owner === importingOwner.owner) {
        continue;
      }
      if (concrete.ownerKind !== "component"
        || !importingOwner.entry.embeds.includes(
          concrete.owner)) {
        violations.push(
          `${relativePath} imports undeclared ${concrete.ownerKind} ${concrete.owner}`,
        );
      }
    }
  }
  assert.deepEqual(violations, []);
});

test("generic renderers and helpers cannot depend on Preview owners", () => {
  const concreteImports = concreteOwnerImports();
  const violations: string[] = [];
  const visualRoot = path.join(repositoryRoot, "src", "visual");
  const walk = (directory: string): string[] =>
    readdirSync(directory, {
      withFileTypes: true,
    }).flatMap((entry) => {
      const fullPath = path.join(directory, entry.name);
      if (entry.isDirectory()) return walk(fullPath);
      return entry.name.endsWith(".ts")
        || entry.name.endsWith(".tsx")
        ? [fullPath]
        : [];
    });
  for (const fullPath of walk(visualRoot)) {
    for (const imported of moduleImports(fullPath)) {
      if (concreteImports.has(imported)
        || imported.includes("desktop-preview")) {
        violations.push(
          `${repositoryPath(fullPath)} imports ${imported}`,
        );
      }
    }
  }
  assert.deepEqual(violations, []);
});

test("Preview filesystem imports stay at explicit request and asset boundaries", () => {
  const allowed = new Set([
    "src/desktop-preview/previewAssetResolver.ts",
    "src/desktop-preview/renderDesignPreviewHtml.tsx",
    "src/desktop-preview/renderPreviewRasterServer.ts",
  ]);
  const violations: string[] = [];
  for (const fullPath of previewSourceFiles()) {
    const relativePath = repositoryPath(fullPath);
    for (const imported of moduleImports(fullPath)) {
      if ((imported === "node:fs"
          || imported === "node:fs/promises")
        && !allowed.has(relativePath)) {
        violations.push(
          `${relativePath} imports ${imported}`,
        );
      }
    }
  }
  assert.deepEqual(violations, []);
});

test("Preview registries contain owner routing without business decisions", () => {
  for (const relativePath of registryPaths) {
    assertFactoryObjectsContainRoutesOnly(relativePath);
  }
});
