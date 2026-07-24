import {
  existsSync,
  mkdirSync,
  readFileSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";

import {
  createModuleScaffoldPlan,
  loadModuleScaffoldInventory,
  moduleOwnerTargets,
  ModuleScaffoldValidationError,
  resolveModuleScaffoldContract,
  type ModuleScaffoldPlan,
  type ModuleScaffoldSpec,
} from "./moduleScaffold.js";
import {
  draftModuleSpecRoot,
  generatedModuleConfigRegistryPath,
  generatedModuleEmbeddedSlotsPath,
  generatedModuleFieldCatalogPath,
  generatedModuleRegistryPath,
  integratedModuleSpecRoot,
  regenerateIntegratedModuleScaffoldArtifacts,
} from "./moduleScaffoldArtifacts.js";
import type { JsonObject } from "./componentScaffold.js";

export const moduleScaffoldSemanticMarker = "MODULE_SCAFFOLD_SEMANTICS_REQUIRED";

export function materializeModuleScaffold(
  spec: ModuleScaffoldSpec,
  plan: ModuleScaffoldPlan,
  repositoryRoot: string,
) {
  const specPath = `${draftModuleSpecRoot}/${moduleFileName(spec)}.json`;
  const targets = [specPath, ...plan.creates.map((owner) => owner.path)];
  for (const target of targets) {
    if (existsSync(scaffoldTarget(repositoryRoot, target))) {
      throw new ModuleScaffoldValidationError([
        `Module scaffold materialization will not overwrite '${target}'.`,
      ]);
    }
  }
  const content = new Map<string, string>([
    [specPath, `${JSON.stringify(spec, null, 2)}\n`],
  ]);
  for (const owner of plan.creates) {
    content.set(owner.path, ownerSkeleton(spec, owner.role));
  }
  for (const target of targets) {
    const resolved = scaffoldTarget(repositoryRoot, target);
    mkdirSync(path.dirname(resolved), { recursive: true });
    writeFileSync(resolved, content.get(target)!, { encoding: "utf8", flag: "wx" });
  }
  return {
    schemaVersion: 1,
    status: "materialized-unregistered",
    moduleClass: spec.module.recordClassId,
    specPath,
    created: targets,
  };
}

export function integrateModuleScaffold(
  spec: ModuleScaffoldSpec,
  repositoryRoot: string,
  databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite"),
) {
  const fileName = moduleFileName(spec);
  const draftSpecPath = `${draftModuleSpecRoot}/${fileName}.json`;
  const integratedSpecPath = `${integratedModuleSpecRoot}/${fileName}.json`;
  const draft = scaffoldTarget(repositoryRoot, draftSpecPath);
  const integrated = scaffoldTarget(repositoryRoot, integratedSpecPath);
  if (!existsSync(draft)) {
    throw new ModuleScaffoldValidationError([`Draft Module spec '${draftSpecPath}' is missing.`]);
  }
  if (canonicalJson(JSON.parse(readFileSync(draft, "utf8"))) !== canonicalJson(spec)) {
    throw new ModuleScaffoldValidationError([`Draft Module spec '${draftSpecPath}' differs.`]);
  }
  if (existsSync(integrated)) {
    throw new ModuleScaffoldValidationError([
      `Integrated Module spec '${integratedSpecPath}' already exists.`,
    ]);
  }
  const inventory = loadModuleScaffoldInventory(repositoryRoot, databasePath);
  const plan = createModuleScaffoldPlan(spec, inventory, repositoryRoot, "mustExist");
  const violations: string[] = [];
  for (const owner of moduleOwnerTargets(spec)) {
    const source = readFileSync(scaffoldTarget(repositoryRoot, owner.path), "utf8");
    if (!source.includes(owner.requiredTerm)) {
      violations.push(`${owner.label} owner '${owner.path}' lacks '${owner.requiredTerm}'.`);
    }
    if (source.includes(moduleScaffoldSemanticMarker)) {
      violations.push(`${owner.label} owner '${owner.path}' still requires semantics.`);
    }
  }
  for (const asset of plan.assets) {
    if (asset.status !== "existing") violations.push(`Required asset '${asset.path}' is missing.`);
  }
  if (violations.length > 0) throw new ModuleScaffoldValidationError(violations);

  const manifestPath = "src/desktop-preview/desktopPreviewManifest.json";
  const manifestTarget = scaffoldTarget(repositoryRoot, manifestPath);
  const manifest = jsonObject(JSON.parse(readFileSync(manifestTarget, "utf8")), "Preview manifest");
  const modules = jsonObject(manifest.modules, "Preview manifest modules");
  modules[spec.module.recordClassId] = { ...spec.manifest };

  const generatedPaths = [
    generatedModuleRegistryPath,
    generatedModuleFieldCatalogPath,
    generatedModuleConfigRegistryPath,
    generatedModuleEmbeddedSlotsPath,
  ];
  const sourcePaths = [
    manifestPath,
    draftSpecPath,
    integratedSpecPath,
    ...generatedPaths,
  ];
  const before = new Map(sourcePaths.map((relativePath) => {
    const target = scaffoldTarget(repositoryRoot, relativePath);
    return [relativePath, existsSync(target) ? readFileSync(target, "utf8") : null] as const;
  }));

  const database = new Database(databasePath, { fileMustExist: true });
  try {
    database.transaction(() => {
      database.prepare(`
        INSERT INTO modules (
          id, app_id, record_class_id, name, notes, sort_order,
          config_json, design_preview_json, metadata_json
        )
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
      `).run(
        plan.persistedDefinition.row.id,
        plan.persistedDefinition.row.app_id,
        plan.persistedDefinition.row.record_class_id,
        plan.persistedDefinition.row.name,
        plan.persistedDefinition.row.notes,
        plan.persistedDefinition.row.sort_order,
        JSON.stringify(plan.persistedDefinition.row.config_json),
        JSON.stringify(plan.persistedDefinition.row.design_preview_json),
        JSON.stringify(plan.persistedDefinition.row.metadata_json),
      );
      database.prepare(`
        INSERT INTO editor_layouts (record_class_id, layout_json)
        VALUES (?, ?)
      `).run(
        plan.editorLayout.row.record_class_id,
        JSON.stringify(plan.editorLayout.row.layout_json),
      );
      writeFileSync(manifestTarget, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
      mkdirSync(path.dirname(integrated), { recursive: true });
      writeFileSync(integrated, `${JSON.stringify(spec, null, 2)}\n`, {
        encoding: "utf8",
        flag: "wx",
      });
      unlinkSync(draft);
      regenerateIntegratedModuleScaffoldArtifacts(repositoryRoot);
    })();
  } catch (error) {
    restoreFiles(repositoryRoot, before);
    throw error;
  } finally {
    database.close();
  }
  return {
    schemaVersion: 1,
    status: "integrated",
    moduleClass: spec.module.recordClassId,
    specPath: integratedSpecPath,
    generated: generatedPaths.filter((relativePath) =>
      before.get(relativePath)
        !== readFileSync(scaffoldTarget(repositoryRoot, relativePath), "utf8")),
    persistence: {
      moduleId: spec.module.moduleId,
      recordClassId: spec.module.recordClassId,
    },
  };
}

export function verifyModuleScaffoldImplementation(
  spec: ModuleScaffoldSpec,
  repositoryRoot: string,
  databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite"),
) {
  const violations: string[] = [];
  const checked: string[] = [];
  const specPath = `${integratedModuleSpecRoot}/${moduleFileName(spec)}.json`;
  const persistedSpec = scaffoldTarget(repositoryRoot, specPath);
  if (!existsSync(persistedSpec)) {
    violations.push(`Integrated Module spec '${specPath}' is missing.`);
  } else if (canonicalJson(JSON.parse(readFileSync(persistedSpec, "utf8")))
      !== canonicalJson(spec)) {
    violations.push(`Integrated Module spec '${specPath}' differs.`);
  }
  checked.push(specPath);

  const inventory = loadModuleScaffoldInventory(repositoryRoot, databasePath);
  const resolved = resolveModuleScaffoldContract(spec, inventory);
  const manifestPath = "src/desktop-preview/desktopPreviewManifest.json";
  const manifest = jsonObject(
    JSON.parse(readFileSync(scaffoldTarget(repositoryRoot, manifestPath), "utf8")),
    "Preview manifest",
  );
  const modules = jsonObject(manifest.modules, "Preview manifest modules");
  if (canonicalJson(modules[spec.module.recordClassId]) !== canonicalJson(spec.manifest)) {
    violations.push(`Manifest route '${spec.module.recordClassId}' differs.`);
  }
  checked.push(manifestPath);

  for (const owner of moduleOwnerTargets(spec)) {
    const target = scaffoldTarget(repositoryRoot, owner.path);
    if (!existsSync(target)) {
      violations.push(`${owner.label} owner '${owner.path}' is missing.`);
      continue;
    }
    const source = readFileSync(target, "utf8");
    if (!source.includes(owner.requiredTerm)) {
      violations.push(`${owner.label} owner '${owner.path}' lacks '${owner.requiredTerm}'.`);
    }
    if (source.includes(moduleScaffoldSemanticMarker)) {
      violations.push(`${owner.label} owner '${owner.path}' still requires semantics.`);
    }
    checked.push(owner.path);
  }
  for (const generatedPath of [
    generatedModuleRegistryPath,
    generatedModuleFieldCatalogPath,
    generatedModuleConfigRegistryPath,
    generatedModuleEmbeddedSlotsPath,
  ]) {
    const source = readFileSync(scaffoldTarget(repositoryRoot, generatedPath), "utf8");
    if (!source.includes(spec.module.recordClassId)) {
      violations.push(`Generated Module artifact '${generatedPath}' lacks route identity.`);
    }
    checked.push(generatedPath);
  }

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const row = database.prepare(`
      SELECT m.id,
             m.app_id,
             a.project_id,
             m.record_class_id,
             m.name,
             m.notes,
             m.sort_order,
             m.config_json,
             m.design_preview_json,
             m.metadata_json
      FROM modules m
      JOIN apps a ON a.id = m.app_id
      WHERE m.id = ?
    `).get(spec.module.moduleId) as {
      id: string;
      app_id: string;
      project_id: string;
      record_class_id: string;
      name: string;
      notes: string;
      sort_order: number;
      config_json: string;
      design_preview_json: string;
      metadata_json: string;
    } | undefined;
    if (!row) {
      violations.push(`Module '${spec.module.moduleId}' is missing from persistence.`);
    } else {
      const identity = {
        id: row.id,
        appId: row.app_id,
        projectId: row.project_id,
        recordClassId: row.record_class_id,
        name: row.name,
        notes: row.notes,
        sortOrder: row.sort_order,
      };
      if (canonicalJson(identity) !== canonicalJson({
        id: spec.module.moduleId,
        appId: spec.module.appId,
        projectId: spec.module.projectId,
        recordClassId: spec.module.recordClassId,
        name: spec.module.name,
        notes: spec.module.notes,
        sortOrder: spec.module.sortOrder,
      })) {
        violations.push(`Module '${spec.module.moduleId}' identity differs.`);
      }
      if (canonicalJson(JSON.parse(row.config_json)) !== canonicalJson(spec.config)) {
        violations.push(`Module '${spec.module.moduleId}' config differs.`);
      }
      if (canonicalJson(JSON.parse(row.design_preview_json))
          !== canonicalJson(resolved.designPreview)) {
        violations.push(`Module '${spec.module.moduleId}' Runtime fixture differs.`);
      }
      if (canonicalJson(JSON.parse(row.metadata_json)) !== canonicalJson(resolved.metadata)) {
        violations.push(`Module '${spec.module.moduleId}' Variants differ.`);
      }
    }
    const layout = database.prepare(`
      SELECT layout_json FROM editor_layouts WHERE record_class_id = ?
    `).get(spec.module.recordClassId) as { layout_json: string } | undefined;
    if (!layout
        || canonicalJson(JSON.parse(layout.layout_json)) !== canonicalJson(spec.editorLayout)) {
      violations.push(`Editor layout '${spec.module.recordClassId}' differs.`);
    }
  } finally {
    database.close();
  }
  checked.push("data/desktop-editor-spike.sqlite");
  for (const asset of spec.assets) {
    if (!existsSync(scaffoldTarget(repositoryRoot, asset))) {
      violations.push(`Required Module asset '${asset}' is missing.`);
    }
  }
  if (violations.length > 0) throw new ModuleScaffoldValidationError(violations);
  return {
    schemaVersion: 1,
    status: "integrated-contract-verified",
    moduleClass: spec.module.recordClassId,
    checked: [...new Set(checked)],
  };
}

function ownerSkeleton(
  spec: ModuleScaffoldSpec,
  role: ModuleScaffoldPlan["creates"][number]["role"],
) {
  const moduleClass = spec.module.recordClassId;
  const typeName = pascalCase(moduleClass.split(".").at(-1) ?? "");
  switch (role) {
    case "contract":
      return `// ${moduleScaffoldSemanticMarker}\n`
        + `export interface ${spec.owners.contractExport} {\n`
        + `  id: "${moduleClass}";\n`
        + `}\n`;
    case "resolver":
      return `// ${moduleScaffoldSemanticMarker}\n`
        + `import type { DesignPreviewPayload } from "./designPreviewPayload.js";\n`
        + `import type { ${spec.owners.contractExport} } from "${spec.manifest.contract}.js";\n\n`
        + `export function ${spec.owners.resolverExport}(\n`
        + `  _payload: DesignPreviewPayload,\n`
        + `): ${spec.owners.contractExport} {\n`
        + `  throw new Error("${moduleClass} semantic resolver is not implemented");\n`
        + `}\n`;
    case "renderable":
      return `// ${moduleScaffoldSemanticMarker}\n`
        + `import type { RenderableNode } from "../visual/renderable/types.js";\n`
        + `import type { DesignPreviewPayload } from "./designPreviewPayload.js";\n\n`
        + `export function ${spec.owners.renderableExport}(\n`
        + `  _payload: DesignPreviewPayload,\n`
        + `): RenderableNode {\n`
        + `  throw new Error("${moduleClass} semantic renderable is not implemented");\n`
        + `}\n`;
    case "desktopConfigContract":
      return `// ${moduleScaffoldSemanticMarker}\n`
        + `using System;\n`
        + `using System.Text.Json.Nodes;\n\n`
        + `namespace Mockups.DesktopEditorShell.Data;\n\n`
        + `internal static class ${typeName}ModuleConfigContract\n`
        + `{\n`
        + `    public const string RecordClassId = "${moduleClass}";\n\n`
        + `    public static void Validate(JsonObject config, string context)\n`
        + `    {\n`
        + `        _ = config;\n`
        + `        throw new InvalidOperationException(\n`
        + `            $"{context} ${moduleClass} semantic config is not implemented.");\n`
        + `    }\n`
        + `}\n`;
    case "focusedTest":
      return `// ${moduleScaffoldSemanticMarker}\n`
        + `import assert from "node:assert/strict";\n`
        + `import test from "node:test";\n\n`
        + `import { ${spec.owners.resolverExport} } from `
        + `"../../src/desktop-preview/${path.basename(spec.manifest.resolver)}.js";\n\n`
        + `test("${moduleClass} scaffold requires concrete semantics", () => {\n`
        + `  assert.equal(typeof ${spec.owners.resolverExport}, "function");\n`
        + `});\n`;
  }
}

function moduleFileName(spec: ModuleScaffoldSpec) {
  return spec.module.recordClassId.replaceAll(".", "_");
}

function scaffoldTarget(repositoryRoot: string, relativePath: string) {
  if (path.isAbsolute(relativePath) || path.win32.isAbsolute(relativePath)) {
    throw new Error(`Absolute Module scaffold paths are prohibited: ${relativePath}`);
  }
  const normalized = relativePath.replaceAll("\\", "/");
  if (normalized !== path.posix.normalize(normalized)
      || normalized === ".."
      || normalized.startsWith("../")) {
    throw new Error(`Module scaffold path escapes are prohibited: ${relativePath}`);
  }
  if (normalized === "docs/old" || normalized.startsWith("docs/old/")) {
    throw new Error("Historical archive Module scaffold paths are prohibited.");
  }
  const target = path.resolve(repositoryRoot, normalized);
  const root = path.resolve(repositoryRoot);
  if (target !== root && !target.startsWith(`${root}${path.sep}`)) {
    throw new Error(`Module scaffold path escapes are prohibited: ${relativePath}`);
  }
  return target;
}

function restoreFiles(repositoryRoot: string, before: ReadonlyMap<string, string | null>) {
  for (const [relativePath, content] of before) {
    const target = scaffoldTarget(repositoryRoot, relativePath);
    if (content === null) {
      if (existsSync(target)) unlinkSync(target);
      continue;
    }
    mkdirSync(path.dirname(target), { recursive: true });
    writeFileSync(target, content, "utf8");
  }
}

function jsonObject(value: unknown, owner: string): JsonObject {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`${owner} must be an object.`);
  }
  return value as JsonObject;
}

function canonicalJson(value: unknown): string {
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(",")}]`;
  const record = value as Record<string, unknown>;
  return `{${Object.keys(record).sort().map((key) =>
    `${JSON.stringify(key)}:${canonicalJson(record[key])}`).join(",")}}`;
}

function pascalCase(value: string) {
  return value.length === 0 ? value : `${value[0]!.toUpperCase()}${value.slice(1)}`;
}
