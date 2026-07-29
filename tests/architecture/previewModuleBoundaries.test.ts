import assert from "node:assert/strict";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
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

function sourceFilesUnder(directory: string): string[] {
  return readdirSync(directory, {
    withFileTypes: true,
  })
    .flatMap((entry) => {
      const fullPath = path.join(directory, entry.name);
      if (entry.isDirectory()) {
        return sourceFilesUnder(fullPath);
      }
      return entry.name.endsWith(".ts")
        || entry.name.endsWith(".tsx")
        ? [fullPath]
        : [];
    })
    .sort();
}

function previewSourceFiles(): string[] {
  return sourceFilesUnder(previewDirectory);
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
): {
  imports: string[];
  invalid: string[];
} {
  const imports: string[] = [];
  const invalid: string[] = [];
  const parsed = sourceFile(fullPath);
  const literal = (node: ts.Node | undefined) =>
    node && (ts.isStringLiteral(node)
      || ts.isNoSubstitutionTemplateLiteral(node))
      ? node.text
      : null;
  const rejectComputed = (
    node: ts.Node,
    kind: string,
  ) => {
    const { line, character } =
      parsed.getLineAndCharacterOfPosition(
        node.getStart(parsed),
      );
    invalid.push(
      `${repositoryPath(fullPath)}:${line + 1}:${character + 1} uses non-literal ${kind}`,
    );
  };
  const visit = (node: ts.Node) => {
    if ((ts.isImportDeclaration(node)
        || ts.isExportDeclaration(node))
      && node.moduleSpecifier) {
      const specifier = literal(node.moduleSpecifier);
      if (specifier !== null) {
        imports.push(specifier);
      } else {
        rejectComputed(node, "module specifier");
      }
    }
    if (ts.isImportEqualsDeclaration(node)
      && ts.isExternalModuleReference(
        node.moduleReference)) {
      const specifier = literal(
        node.moduleReference.expression,
      );
      if (specifier !== null) {
        imports.push(specifier);
      } else {
        rejectComputed(node, "import assignment");
      }
    }
    if (ts.isCallExpression(node)) {
      const kind = node.expression.kind
        === ts.SyntaxKind.ImportKeyword
        ? "dynamic import"
        : ts.isIdentifier(node.expression)
          && node.expression.text === "require"
          ? "require"
          : null;
      if (kind) {
        const specifier = node.arguments.length === 1
          ? literal(node.arguments[0])
          : null;
        if (specifier !== null) {
          imports.push(specifier);
        } else {
          rejectComputed(node, kind);
        }
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(parsed);
  return {
    imports,
    invalid,
  };
}

const compilerOptions: ts.CompilerOptions = {
  module: ts.ModuleKind.NodeNext,
  moduleResolution: ts.ModuleResolutionKind.NodeNext,
  resolveJsonModule: true,
};

function resolvedModulePath(
  importingFile: string,
  specifier: string,
): string | null {
  const resolved = ts.resolveModuleName(
    specifier,
    importingFile,
    compilerOptions,
    ts.sys,
  ).resolvedModule;
  return resolved
    ? path.resolve(resolved.resolvedFileName)
    : null;
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
      ? ["category", "contract", "embeds", "recordClassId", "renderable", "resolver"]
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
    assert.equal(
      entry.recordClassId,
      `component.${owner}`,
      `${owner} has an unexpected record class`,
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
  const concreteFiles = ownerFiles();
  const violations: string[] = [];
  for (const fullPath of previewSourceFiles()) {
    const relativePath = repositoryPath(fullPath);
    const importingOwner = concreteFiles.get(relativePath);
    const moduleGraph = moduleImports(fullPath);
    violations.push(...moduleGraph.invalid);
    for (const imported of moduleGraph.imports) {
      const resolvedPath = resolvedModulePath(
        fullPath,
        imported,
      );
      if (imported.startsWith(".") && !resolvedPath) {
        violations.push(
          `${relativePath} has unresolved import ${imported}`,
        );
        continue;
      }
      const concrete = resolvedPath
        ? concreteFiles.get(repositoryPath(resolvedPath))
        : null;
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
  const concreteFiles = ownerFiles();
  const violations: string[] = [];
  const visualRoot = path.join(repositoryRoot, "src", "visual");
  for (const fullPath of sourceFilesUnder(visualRoot)) {
    const moduleGraph = moduleImports(fullPath);
    violations.push(...moduleGraph.invalid);
    for (const imported of moduleGraph.imports) {
      const resolvedPath = resolvedModulePath(
        fullPath,
        imported,
      );
      if (resolvedPath
        && (concreteFiles.has(repositoryPath(resolvedPath))
          || resolvedPath.startsWith(
            `${previewDirectory}${path.sep}`))) {
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
    const moduleGraph = moduleImports(fullPath);
    violations.push(...moduleGraph.invalid);
    for (const imported of moduleGraph.imports) {
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

test("the Preview graph walks nested sources and rejects computed module loads", () => {
  const fixtureRoot = mkdtempSync(
    path.join(tmpdir(), "mockups-preview-graph-"),
  );
  try {
    const nested = path.join(fixtureRoot, "nested");
    mkdirSync(nested);
    const fixturePath = path.join(nested, "owner.ts");
    writeFileSync(
      fixturePath,
      `
        import value from "./static.js";
        export { value as exported } from "./exported.js";
        import assigned = require("./assigned.cjs");
        const required = require("./required.cjs");
        const dynamic = import("./dynamic.js");
        const route = "./computed.js";
        void import(route);
        require(route);
        void assigned;
        void required;
        void dynamic;
      `,
      "utf8",
    );

    assert.deepEqual(
      sourceFilesUnder(fixtureRoot),
      [fixturePath],
    );
    const graph = moduleImports(fixturePath);
    assert.deepEqual(
      graph.imports.sort(),
      [
        "./assigned.cjs",
        "./dynamic.js",
        "./exported.js",
        "./required.cjs",
        "./static.js",
      ],
    );
    assert.equal(graph.invalid.length, 2);
    assert.ok(graph.invalid.some((entry) =>
      entry.endsWith("non-literal dynamic import")));
    assert.ok(graph.invalid.some((entry) =>
      entry.endsWith("non-literal require")));
  } finally {
    rmSync(fixtureRoot, {
      force: true,
      recursive: true,
    });
  }
});

test("Preview registries contain owner routing without business decisions", () => {
  for (const relativePath of registryPaths) {
    assertFactoryObjectsContainRoutesOnly(relativePath);
  }
});
