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
}: ArchitectureValidationContext) {
  const packageScripts = (JSON.parse(readText("package.json")) as {
    scripts?: Record<string, string>;
  }).scripts ?? {};

  if (packageScripts["scaffold:component"] !== "tsx scripts/scaffoldComponent.ts") {
    addViolation(
      "package.json",
      "Component development scaffolding must use the single scaffold command owner",
    );
  }
  if (packageScripts["test:scaffolding"] !== "tsx --test tests/scaffolding/*.test.ts") {
    addViolation(
      "package.json",
      "the complete repository gate must execute Component scaffolding contract tests",
    );
  }
  if (packageScripts["scaffold:verify"]
        !== "tsx scripts/verifyIntegratedComponentScaffolds.ts") {
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
        !== "tsx scripts/verifyIntegratedModuleScaffolds.ts") {
    addViolation(
      "package.json",
      "Module development scaffolding must expose planning, generation and full-gate verification",
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
}
