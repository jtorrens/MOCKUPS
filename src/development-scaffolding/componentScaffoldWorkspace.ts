import {
  existsSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";

import {
  ComponentScaffoldValidationError,
  type ComponentScaffoldPlan,
  type ComponentScaffoldSpec,
  type JsonObject,
} from "./componentScaffold.js";

export const componentScaffoldSemanticMarker = "SCAFFOLD_SEMANTICS_REQUIRED";
export const integratedComponentSpecRoot = "scaffolding/components";

export interface ComponentScaffoldMaterialization {
  schemaVersion: 1;
  status: "materialized-unregistered";
  componentType: string;
  specPath: string;
  created: string[];
}

export interface ComponentScaffoldVerification {
  schemaVersion: 1;
  status: "integrated-contract-verified";
  componentType: string;
  checked: string[];
}

export function materializeComponentScaffold(
  spec: ComponentScaffoldSpec,
  plan: ComponentScaffoldPlan,
  repositoryRoot: string,
): ComponentScaffoldMaterialization {
  const specPath = `${integratedComponentSpecRoot}/${spec.component.componentType}.json`;
  const targets = [
    specPath,
    ...plan.creates.map((owner) => owner.path),
  ];
  for (const target of targets) {
    const resolved = scaffoldTarget(repositoryRoot, target);
    if (existsSync(resolved)) {
      throw new ComponentScaffoldValidationError([
        `Scaffold materialization will not overwrite existing target '${target}'.`,
      ]);
    }
  }

  const contentByPath = new Map<string, string>([
    [specPath, `${JSON.stringify(spec, null, 2)}\n`],
  ]);
  for (const owner of plan.creates) {
    contentByPath.set(
      owner.path,
      ownerSkeleton(spec, owner.role),
    );
  }

  const created: string[] = [];
  for (const target of targets) {
    const resolved = scaffoldTarget(repositoryRoot, target);
    mkdirSync(path.dirname(resolved), { recursive: true });
    writeFileSync(resolved, contentByPath.get(target)!, {
      encoding: "utf8",
      flag: "wx",
    });
    created.push(target);
  }

  return {
    schemaVersion: 1,
    status: "materialized-unregistered",
    componentType: spec.component.componentType,
    specPath,
    created,
  };
}

export function verifyComponentScaffoldImplementation(
  spec: ComponentScaffoldSpec,
  repositoryRoot: string,
  databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite"),
): ComponentScaffoldVerification {
  const violations: string[] = [];
  const checked: string[] = [];
  const componentType = spec.component.componentType;
  const specPath = `${integratedComponentSpecRoot}/${componentType}.json`;
  const resolvedSpecPath = scaffoldTarget(repositoryRoot, specPath);
  if (!existsSync(resolvedSpecPath)) {
    violations.push(`Integrated Component spec '${specPath}' is missing.`);
  } else {
    checked.push(specPath);
    const persistedSpec = JSON.parse(readFileSync(resolvedSpecPath, "utf8")) as unknown;
    if (canonicalJson(persistedSpec) !== canonicalJson(spec)) {
      violations.push(`Integrated Component spec '${specPath}' differs from the verified contract.`);
    }
  }

  const manifestPath = "src/desktop-preview/desktopPreviewManifest.json";
  const manifest = jsonObject(
    JSON.parse(readFileSync(scaffoldTarget(repositoryRoot, manifestPath), "utf8")),
    "Desktop Preview manifest",
  );
  const components = jsonObject(manifest.components, "Desktop Preview manifest components");
  const manifestEntry = components[componentType];
  const expectedManifestEntry = {
    category: spec.component.category,
    ...spec.manifest,
  };
  if (canonicalJson(manifestEntry) !== canonicalJson(expectedManifestEntry)) {
    violations.push(
      `Manifest route '${componentType}' does not match its integrated scaffold spec.`,
    );
  }
  checked.push(manifestPath);

  const owners = ownerTargets(spec);
  for (const owner of owners) {
    const resolved = scaffoldTarget(repositoryRoot, owner.path);
    if (!existsSync(resolved)) {
      violations.push(`${owner.label} owner '${owner.path}' is missing.`);
      continue;
    }
    const source = readFileSync(resolved, "utf8");
    if (!source.includes(owner.requiredTerm)) {
      violations.push(
        `${owner.label} owner '${owner.path}' does not expose '${owner.requiredTerm}'.`,
      );
    }
    if (source.includes(componentScaffoldSemanticMarker)) {
      violations.push(
        `${owner.label} owner '${owner.path}' still requires semantic implementation.`,
      );
    }
    checked.push(owner.path);
  }

  const registryPath = "src/desktop-preview/componentClassRenderableRegistry.ts";
  const registry = readFileSync(scaffoldTarget(repositoryRoot, registryPath), "utf8");
  for (const required of [
    spec.owners.resolverExport,
    spec.owners.renderableExport,
    `${componentType}:`,
  ]) {
    if (!registry.includes(required)) {
      violations.push(`Registry route '${componentType}' is missing '${required}'.`);
    }
  }
  checked.push(registryPath);

  const fieldSources = [
    "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
    "spikes/desktop-editor-shell/EditorShell/GeneratedComponentScaffoldFieldCatalog.cs",
  ]
    .map((candidate) => scaffoldTarget(repositoryRoot, candidate))
    .filter(existsSync)
    .map((candidate) => readFileSync(candidate, "utf8"))
    .join("\n");
  for (const field of spec.dictionaryFields) {
    if (!fieldSources.includes(`["${field.id}"]`)) {
      violations.push(`Dictionary field '${field.id}' is not registered.`);
      continue;
    }
    if (!fieldSources.includes(`ValueKind.${field.valueKind}`)) {
      violations.push(
        `Dictionary field '${field.id}' does not expose ValueKind '${field.valueKind}'.`,
      );
    }
    if (field.componentVariantType
        && !fieldSources.includes(`ComponentVariantType: "${field.componentVariantType}"`)) {
      violations.push(
        `Dictionary field '${field.id}' does not retain Component type '${field.componentVariantType}'.`,
      );
    }
  }
  checked.push("spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs");

  const database = new Database(databasePath, {
    readonly: true,
    fileMustExist: true,
  });
  try {
    const row = database.prepare(`
      SELECT id,
             project_id,
             component_type,
             record_class_id,
             name,
             notes,
             config_json,
             design_preview_json,
             metadata_json
      FROM component_classes
      WHERE id = ?
    `).get(spec.component.componentClassId) as {
      id: string;
      project_id: string;
      component_type: string;
      record_class_id: string;
      name: string;
      notes: string;
      config_json: string;
      design_preview_json: string;
      metadata_json: string;
    } | undefined;
    if (!row) {
      violations.push(
        `Component class '${spec.component.componentClassId}' is missing from current persistence.`,
      );
    } else {
      const expectedIdentity = {
        id: spec.component.componentClassId,
        project_id: spec.component.projectId,
        component_type: componentType,
        record_class_id: spec.component.recordClassId,
        name: spec.component.name,
        notes: spec.component.notes,
      };
      const actualIdentity = {
        id: row.id,
        project_id: row.project_id,
        component_type: row.component_type,
        record_class_id: row.record_class_id,
        name: row.name,
        notes: row.notes,
      };
      if (canonicalJson(actualIdentity) !== canonicalJson(expectedIdentity)) {
        violations.push(`Component class '${row.id}' identity differs from its scaffold spec.`);
      }
      if (canonicalJson(JSON.parse(row.config_json)) !== canonicalJson(spec.config)) {
        violations.push(`Component class '${row.id}' config differs from its scaffold spec.`);
      }
      if (canonicalJson(JSON.parse(row.design_preview_json))
          !== canonicalJson(spec.designPreview)) {
        violations.push(
          `Component class '${row.id}' Design Preview differs from its scaffold spec.`,
        );
      }
      const expectedMetadata = {
        ...spec.metadata,
        variants: [spec.defaultVariant, ...spec.additionalVariants],
      };
      if (canonicalJson(JSON.parse(row.metadata_json))
          !== canonicalJson(expectedMetadata)) {
        violations.push(`Component class '${row.id}' Variants differ from its scaffold spec.`);
      }
    }

    const layout = database.prepare(`
      SELECT layout_json
      FROM editor_layouts
      WHERE record_class_id = ?
    `).get(spec.component.recordClassId) as { layout_json: string } | undefined;
    if (!layout
        || canonicalJson(JSON.parse(layout.layout_json))
          !== canonicalJson(spec.editorLayout)) {
      violations.push(
        `Editor layout '${spec.component.recordClassId}' differs from its scaffold spec.`,
      );
    }
  } finally {
    database.close();
  }
  checked.push("data/desktop-editor-spike.sqlite");

  for (const asset of spec.assets) {
    if (!existsSync(scaffoldTarget(repositoryRoot, asset))) {
      violations.push(`Required scaffold asset '${asset}' is missing.`);
    }
  }

  if (violations.length > 0) {
    throw new ComponentScaffoldValidationError(violations);
  }
  return {
    schemaVersion: 1,
    status: "integrated-contract-verified",
    componentType,
    checked: [...new Set(checked)],
  };
}

function ownerTargets(spec: ComponentScaffoldSpec) {
  const typeName = pascalCase(spec.component.componentType);
  return [
    {
      label: "contract",
      path: manifestOwnerPath(spec.manifest.contract),
      requiredTerm: spec.owners.contractExport,
    },
    {
      label: "resolver",
      path: manifestOwnerPath(spec.manifest.resolver),
      requiredTerm: spec.owners.resolverExport,
    },
    {
      label: "renderable",
      path: manifestOwnerPath(spec.manifest.renderable),
      requiredTerm: spec.owners.renderableExport,
    },
    {
      label: "desktop config contract",
      path: `spikes/desktop-editor-shell/Data/${typeName}ComponentConfigContract.cs`,
      requiredTerm: `${typeName}ComponentConfigContract`,
    },
    {
      label: "focused test",
      path: spec.owners.focusedTest,
      requiredTerm: spec.owners.resolverExport,
    },
  ];
}

function ownerSkeleton(
  spec: ComponentScaffoldSpec,
  role: ComponentScaffoldPlan["creates"][number]["role"],
) {
  const componentType = spec.component.componentType;
  const typeName = pascalCase(componentType);
  switch (role) {
    case "contract":
      return `// ${componentScaffoldSemanticMarker}\n`
        + `export interface ${spec.owners.contractExport} {\n`
        + `  id: "component.${componentType}";\n`
        + `}\n`;
    case "resolver":
      return `// ${componentScaffoldSemanticMarker}\n`
        + `import type { DesignPreviewPayload } from "./designPreviewPayload.js";\n`
        + `import type { ${spec.owners.contractExport} } from "${spec.manifest.contract}.js";\n\n`
        + `export function ${spec.owners.resolverExport}(\n`
        + `  _payload: DesignPreviewPayload,\n`
        + `): ${spec.owners.contractExport} {\n`
        + `  throw new Error("${componentType} semantic resolver is not implemented");\n`
        + `}\n`;
    case "renderable":
      return `// ${componentScaffoldSemanticMarker}\n`
        + `import type { RenderableNode } from "../visual/renderable/types.js";\n`
        + `import type { DesignPreviewPayload } from "./designPreviewPayload.js";\n`
        + `import type { ${spec.owners.contractExport} } from "${spec.manifest.contract}.js";\n\n`
        + `export function ${spec.owners.renderableExport}(\n`
        + `  _payload: DesignPreviewPayload,\n`
        + `  _component: ${spec.owners.contractExport},\n`
        + `): RenderableNode {\n`
        + `  throw new Error("${componentType} semantic renderable is not implemented");\n`
        + `}\n`;
    case "desktopConfigContract":
      return `// ${componentScaffoldSemanticMarker}\n`
        + `using System;\n`
        + `using System.Text.Json.Nodes;\n\n`
        + `namespace Mockups.DesktopEditorShell.Data;\n\n`
        + `internal static class ${typeName}ComponentConfigContract\n`
        + `{\n`
        + `    public const string ComponentType = "${componentType}";\n\n`
        + `    public static void Validate(JsonObject config, string context)\n`
        + `    {\n`
        + `        _ = config;\n`
        + `        throw new InvalidOperationException(\n`
        + `            $"{context} ${componentType} semantic config contract is not implemented.");\n`
        + `    }\n`
        + `}\n`;
    case "focusedTest":
      return `// ${componentScaffoldSemanticMarker}\n`
        + `import assert from "node:assert/strict";\n`
        + `import test from "node:test";\n\n`
        + `import { ${spec.owners.resolverExport} } from `
        + `"../../src/desktop-preview/${path.basename(spec.manifest.resolver)}.js";\n\n`
        + `test("${componentType} scaffold remains explicitly unregistered until semantics exist", () => {\n`
        + `  assert.equal(typeof ${spec.owners.resolverExport}, "function");\n`
        + `});\n`;
  }
}

function manifestOwnerPath(route: string) {
  return `src/desktop-preview/${route.slice(2)}.ts`;
}

function scaffoldTarget(repositoryRoot: string, relativePath: string) {
  if (path.isAbsolute(relativePath)) {
    throw new Error(`Absolute scaffold target paths are prohibited: ${relativePath}`);
  }
  const normalized = relativePath.replaceAll("\\", "/");
  if (normalized !== path.posix.normalize(normalized)
      || normalized === ".."
      || normalized.startsWith("../")) {
    throw new Error(`Scaffold target path escapes are prohibited: ${relativePath}`);
  }
  if (normalized === "docs/old" || normalized.startsWith("docs/old/")) {
    throw new Error("Historical archive scaffold targets are prohibited.");
  }
  const resolved = path.resolve(repositoryRoot, normalized);
  const root = path.resolve(repositoryRoot);
  if (resolved !== root && !resolved.startsWith(`${root}${path.sep}`)) {
    throw new Error(`Scaffold target path escapes are prohibited: ${relativePath}`);
  }
  return resolved;
}

function pascalCase(value: string) {
  return value.length === 0
    ? value
    : `${value[0]!.toUpperCase()}${value.slice(1)}`;
}

function jsonObject(value: unknown, owner: string): JsonObject {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`${owner} must be an object.`);
  }
  return value as JsonObject;
}

function canonicalJson(value: unknown): string {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) {
    return `[${value.map(canonicalJson).join(",")}]`;
  }
  const record = value as Record<string, unknown>;
  return `{${Object.keys(record).sort().map((key) =>
    `${JSON.stringify(key)}:${canonicalJson(record[key])}`).join(",")}}`;
}
