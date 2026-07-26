import {
  expectedIntegratedComponentScaffoldArtifacts,
  loadIntegratedComponentScaffoldSpecs,
} from "../../src/development-scaffolding/componentScaffoldArtifacts.js";
import {
  expectedIntegratedModuleScaffoldArtifacts,
  loadIntegratedModuleScaffoldSpecs,
} from "../../src/development-scaffolding/moduleScaffoldArtifacts.js";

import type { ArchitectureValidationContext } from "./validationContext.js";

export function checkGeneratedArtifacts({
  root,
  readText,
  addViolation,
  assertContains,
  assertDoesNotContain,
}: ArchitectureValidationContext) {
  const packageScripts = (JSON.parse(readText("package.json")) as {
    scripts?: Record<string, string>;
  }).scripts ?? {};
  const repositoryTestScript = packageScripts["test:repository"] ?? "";

  if (packageScripts["scaffold:component"] !== "tsx scripts/scaffoldComponent.ts") {
    addViolation(
      "package.json",
      "Component development scaffolding must use the single scaffold command owner",
    );
  }
  if (packageScripts["test:scaffolding"] !== "tsx --test tests/scaffolding/*.test.ts"
      || !repositoryTestScript.includes("npm run test:scaffolding")) {
    addViolation(
      "package.json",
      "the complete repository gate must execute Component scaffolding contract tests",
    );
  }
  if (packageScripts["scaffold:verify"]
        !== "tsx scripts/verifyIntegratedComponentScaffolds.ts"
      || !repositoryTestScript.includes("npm run scaffold:verify")) {
    addViolation(
      "package.json",
      "the complete repository gate must verify every integrated Component scaffold spec",
    );
  }
  if (packageScripts["scaffold:generate"]
      !== "tsx scripts/generateIntegratedComponentScaffoldArtifacts.ts") {
    addViolation(
      "package.json",
      "integrated Component scaffold artifacts must use one deterministic generator",
    );
  }
  if (packageScripts["scaffold:module"] !== "tsx scripts/scaffoldModule.ts"
      || packageScripts["scaffold:module:generate"]
        !== "tsx scripts/generateIntegratedModuleScaffoldArtifacts.ts"
      || packageScripts["scaffold:module:verify"]
        !== "tsx scripts/verifyIntegratedModuleScaffolds.ts"
      || !repositoryTestScript.includes("npm run scaffold:module:verify")) {
    addViolation(
      "package.json",
      "Module development scaffolding must expose planning, generation and full-gate verification",
    );
  }
  for (const [scaffoldPath, requiredTerm] of [
    ["src/development-scaffolding/componentScaffold.ts", "readonly: true"],
    ["src/development-scaffolding/componentScaffold.ts", "contract-ready-for-owner-implementation"],
    ["src/development-scaffolding/componentScaffold.ts", "Default and additionalVariants are the Variant sources"],
    ["src/development-scaffolding/componentScaffold.ts", "resolveComponentScaffoldSpecPath"],
    ["scripts/scaffoldComponent.ts", '"dry-run": { type: "boolean"'],
    ["scripts/scaffoldComponent.ts", 'materialize: { type: "boolean"'],
    ["scripts/scaffoldComponent.ts", 'integrate: { type: "boolean"'],
    ["scripts/scaffoldComponent.ts", 'verify: { type: "boolean"'],
    ["scripts/scaffoldComponent.ts", '"adopt-existing": { type: "boolean"'],
    ["src/development-scaffolding/componentScaffoldWorkspace.ts", "SCAFFOLD_SEMANTICS_REQUIRED"],
    ["src/development-scaffolding/componentScaffoldWorkspace.ts", "will not overwrite existing target"],
    ["src/development-scaffolding/componentScaffoldWorkspace.ts", "still requires semantic implementation"],
    ["src/development-scaffolding/componentScaffoldArtifacts.ts", "Do not edit manually"],
    ["src/development-scaffolding/componentScaffoldAdoption.ts", "will not overwrite"],
    ["scripts/verifyIntegratedComponentScaffolds.ts", "verifyComponentScaffoldImplementation"],
    ["tests/scaffolding/componentScaffold.test.ts", "opens the database read-only"],
    ["docs/architecture/development_workflow.md", "materialization never edits the manifest, registry or database"],
    ["docs/architecture/development_workflow.md", "Integration rejects missing assets"],
  ] as const) {
    assertContains(
      scaffoldPath,
      requiredTerm,
      "Component scaffolding must keep planning read-only and materialization unregistered until semantic owners exist",
    );
  }
  const integratedScaffoldSpecs = loadIntegratedComponentScaffoldSpecs(root);
  for (const [generatedPath, expected] of
    expectedIntegratedComponentScaffoldArtifacts(integratedScaffoldSpecs)) {
    if (readText(generatedPath) !== expected) {
      addViolation(
        generatedPath,
        "generated Component scaffold artifact is stale or was edited manually",
      );
    }
  }
  for (const [scaffoldPath, requiredTerm] of [
    ["src/development-scaffolding/moduleScaffold.ts", "readonly: true"],
    ["src/development-scaffolding/moduleScaffold.ts", "contract-ready-for-owner-implementation"],
    ["src/development-scaffolding/moduleScaffold.ts", "resolveModuleScaffoldSpecPath"],
    ["src/development-scaffolding/moduleScaffoldWorkspace.ts", "MODULE_SCAFFOLD_SEMANTICS_REQUIRED"],
    ["src/development-scaffolding/moduleScaffoldWorkspace.ts", "materialization will not overwrite"],
    ["src/development-scaffolding/moduleScaffoldArtifacts.ts", "Do not edit manually"],
    ["scripts/scaffoldModule.ts", '"dry-run": { type: "boolean"'],
    ["scripts/scaffoldModule.ts", 'materialize: { type: "boolean"'],
    ["scripts/scaffoldModule.ts", 'integrate: { type: "boolean"'],
    ["scripts/scaffoldModule.ts", 'verify: { type: "boolean"'],
    ["scripts/verifyIntegratedModuleScaffolds.ts", "verifyModuleScaffoldImplementation"],
    ["tests/scaffolding/moduleScaffold.test.ts", "derives one exact child Runtime contract"],
    ["docs/architecture/development_workflow.md", "Module contract planning"],
  ] as const) {
    assertContains(
      scaffoldPath,
      requiredTerm,
      "Module scaffolding must keep planning read-only and semantic integration explicit",
    );
  }
  const integratedModuleScaffoldSpecs = loadIntegratedModuleScaffoldSpecs(root);
  for (const [generatedPath, expected] of
    expectedIntegratedModuleScaffoldArtifacts(integratedModuleScaffoldSpecs)) {
    if (readText(generatedPath) !== expected) {
      addViolation(
        generatedPath,
        "generated Module scaffold artifact is stale or was edited manually",
      );
    }
  }
  for (const prohibitedWriteTerm of [
    "INSERT INTO component_classes",
    "UPDATE component_classes",
    "DELETE FROM component_classes",
    "INSERT INTO editor_layouts",
    "UPDATE editor_layouts",
  ]) {
    assertDoesNotContain(
      "src/development-scaffolding/componentScaffold.ts",
      prohibitedWriteTerm,
      "Component scaffold planning must not mutate current persistence",
    );
  }
  for (const scaffoldSource of [
    "src/development-scaffolding/componentScaffold.ts",
    "scripts/scaffoldComponent.ts",
  ]) {
    for (const prohibitedMutationTerm of [
      "writeFile",
      "appendFile",
      "copyFile",
      "renameSync",
      "mkdirSync",
      "rmSync",
      "database.exec(",
    ]) {
      assertDoesNotContain(
        scaffoldSource,
        prohibitedMutationTerm,
        "Component scaffold planning must remain filesystem- and database-read-only",
      );
    }
  }
}
