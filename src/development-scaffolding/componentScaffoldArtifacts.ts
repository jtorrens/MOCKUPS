import {
  existsSync,
  readFileSync,
  readdirSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";

import {
  parseComponentScaffoldSpec,
  type ComponentScaffoldField,
  type ComponentScaffoldSpec,
} from "./componentScaffold.js";

export const draftComponentSpecRoot = "scaffolding/drafts";
export const integratedComponentSpecRoot = "scaffolding/components";

export const generatedComponentRegistryPath =
  "src/desktop-preview/generatedComponentScaffoldRegistry.ts";
export const generatedDesktopFieldCatalogPath =
  "src/Mockups.Application/GeneratedComponentScaffoldFieldCatalog.cs";
export const generatedDesktopConfigRegistryPath =
  "src/Mockups.Application/GeneratedComponentScaffoldConfigRegistry.cs";
export const generatedComponentEmbeddedSlotsPath =
  "src/Mockups.Application/GeneratedComponentScaffoldEmbeddedSlots.cs";

export interface GeneratedComponentScaffoldArtifacts {
  schemaVersion: 1;
  status: "integrated-artifacts-generated";
  componentTypes: string[];
  written: string[];
}

export function loadIntegratedComponentScaffoldSpecs(
  repositoryRoot: string,
): ComponentScaffoldSpec[] {
  const specRoot = repositoryPath(repositoryRoot, integratedComponentSpecRoot);
  return readdirSync(specRoot, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith(".json"))
    .sort((left, right) => left.name.localeCompare(right.name))
    .map((entry) => parseComponentScaffoldSpec(
      JSON.parse(readFileSync(path.join(specRoot, entry.name), "utf8")) as unknown,
    ));
}

export function expectedIntegratedComponentScaffoldArtifacts(
  specs: readonly ComponentScaffoldSpec[],
) {
  const ordered = [...specs].sort((left, right) =>
    left.component.componentType.localeCompare(right.component.componentType));
  return new Map<string, string>([
    [generatedComponentRegistryPath, renderRegistry(ordered)],
    [generatedDesktopFieldCatalogPath, renderFieldCatalog(ordered)],
    [generatedDesktopConfigRegistryPath, renderConfigRegistry(ordered)],
    [generatedComponentEmbeddedSlotsPath, renderEmbeddedSlots(ordered)],
  ]);
}

export function regenerateIntegratedComponentScaffoldArtifacts(
  repositoryRoot: string,
): GeneratedComponentScaffoldArtifacts {
  const specs = loadIntegratedComponentScaffoldSpecs(repositoryRoot);
  if (specs.length === 0) {
    throw new Error("At least one integrated Component scaffold spec is required.");
  }
  const artifacts = expectedIntegratedComponentScaffoldArtifacts(specs);
  const written: string[] = [];
  for (const [relativePath, content] of artifacts) {
    const target = repositoryPath(repositoryRoot, relativePath);
    if (!existsSync(target) || readFileSync(target, "utf8") !== content) {
      writeFileSync(target, content, "utf8");
      written.push(relativePath);
    }
  }
  return {
    schemaVersion: 1,
    status: "integrated-artifacts-generated",
    componentTypes: specs.map((spec) => spec.component.componentType).sort(),
    written,
  };
}

function renderRegistry(specs: readonly ComponentScaffoldSpec[]) {
  const imports = specs.flatMap((spec) => [
    `import { ${spec.owners.renderableExport} } from "${spec.manifest.renderable}.js";`,
    `import { ${spec.owners.resolverExport} } from "${spec.manifest.resolver}.js";`,
  ]).join("\n");
  const routes = specs.map((spec) => {
    const type = spec.component.componentType;
    const resolved = `${spec.owners.resolverExport}(payload)`;
    const rendered = (() => {
      switch (spec.owners.registryMode) {
        case "simple":
          return `${spec.owners.renderableExport}(payload, ${resolved})`;
        case "assignedBox":
          return `${spec.owners.renderableExport}(payload, ${resolved}, assignedBox)`;
        case "children":
          return `${spec.owners.renderableExport}(payload, ${resolved}, renderChild)`;
        case "assignedBoxAndChildren":
          return `${spec.owners.renderableExport}(payload, ${resolved}, assignedBox, renderChild)`;
      }
    })();
    const parameters = (() => {
      switch (spec.owners.registryMode) {
        case "simple":
          return "payload";
        case "assignedBox":
          return "payload, assignedBox";
        case "children":
          return "payload, _assignedBox, renderChild";
        case "assignedBoxAndChildren":
          return "payload, assignedBox, renderChild";
      }
    })();
    return `  ${type}: (${parameters}) =>\n    ${rendered},`;
  }).join("\n");
  return `// Generated from scaffolding/components/*.json. Do not edit manually.\n`
    + `import type { ComponentRenderableFactory } from "./componentClassRenderableRegistry.js";\n`
    + `${imports}\n\n`
    + `export const generatedComponentScaffoldFactories = {\n`
    + `${routes}\n`
    + `} satisfies Record<string, ComponentRenderableFactory>;\n`;
}

function renderFieldCatalog(specs: readonly ComponentScaffoldSpec[]) {
  const fields = specs.flatMap((spec) => spec.dictionaryFields)
    .sort((left, right) => left.id.localeCompare(right.id))
    .map((field) => `        fields.Add("${escapeCSharp(field.id)}", ${renderField(field)});`)
    .join("\n");
  return `// Generated from scaffolding/components/*.json. Do not edit manually.\n`
    + `using System.Collections.Generic;\n\n`
    + `namespace Mockups.DesktopEditorShell.EditorShell;\n\n`
    + `public static partial class ComponentClassFieldCatalog\n`
    + `{\n`
    + `    static partial void AddGeneratedFields(\n`
    + `        Dictionary<string, ComponentClassFieldDescriptor> fields)\n`
    + `    {\n`
    + `${fields}\n`
    + `    }\n`
    + `}\n`;
}

function renderConfigRegistry(specs: readonly ComponentScaffoldSpec[]) {
  const cases = specs.map((spec) => {
    const owner = spec.owners.configContractExport;
    return `            case "${escapeCSharp(spec.component.componentType)}":\n`
      + (owner ? `                ${owner}.Validate(config, context);\n` : "")
      + `                return true;`;
  }).join("\n");
  return `// Generated from scaffolding/components/*.json. Do not edit manually.\n`
    + `using System.Text.Json.Nodes;\n\n`
    + `namespace Mockups.DesktopEditorShell.Data;\n\n`
    + `internal static class GeneratedComponentScaffoldConfigRegistry\n`
    + `{\n`
    + `    public static bool TryValidate(\n`
    + `        string componentType,\n`
    + `        JsonObject config,\n`
    + `        string context)\n`
    + `    {\n`
    + `        switch (componentType)\n`
    + `        {\n`
    + `${cases}\n`
    + `            default:\n`
    + `                return false;\n`
    + `        }\n`
    + `    }\n`
    + `}\n`;
}

function renderEmbeddedSlots(specs: readonly ComponentScaffoldSpec[]) {
  const slots = specs.flatMap((spec) => spec.dictionaryFields)
    .filter((field) => field.embeddedSlot !== null)
    .sort((left, right) => left.id.localeCompare(right.id))
    .map((field) => {
      const embedded = field.embeddedSlot!;
      const slotPath = field.valueKind === "ComponentVariant"
        ? field.jsonPath.slice(0, -1)
        : field.jsonPath;
      return `        new(\n`
        + `            ${csharpString(field.id)},\n`
        + `            ${csharpString(embedded.componentType)},\n`
        + `            ${csharpString(embedded.label)},\n`
        + `            ${csharpString(embedded.recordClassId)},\n`
        + `            [${slotPath.map(csharpString).join(", ")}]),`;
    }).join("\n");
  return `// Generated from scaffolding/components/*.json. Do not edit manually.\n`
    + `namespace Mockups.DesktopEditorShell.EditorShell;\n\n`
    + `public static class GeneratedComponentScaffoldEmbeddedSlots\n`
    + `{\n`
    + `    public static EmbeddedComponentSlotDefinition[] All { get; } =\n`
    + `    [\n`
    + `${slots}${slots ? "\n" : ""}`
    + `    ];\n`
    + `}\n`;
}

function renderField(field: ComponentScaffoldField) {
  const descriptor = {
    id: field.id,
    label: field.label,
    valueKind: field.valueKind,
    jsonPath: field.jsonPath,
    defaultValue: field.defaultValue,
    isEditable: field.isEditable,
    options: field.options,
    pairLabels: field.pairLabels,
    number: field.number,
    componentInputBindings: field.componentInputBindings,
    structuredCollection: field.structuredCollection,
    componentVariantType: field.componentVariantType,
    runtimeInputComponentVariantFieldId: field.runtimeInputComponentVariantFieldId,
    unit: field.unit,
    helpText: field.helpText,
    valuePattern: field.valuePattern,
    valuePatternMessage: field.valuePatternMessage,
  };
  return `ScaffoldDictionaryFieldContract.Component(${csharpString(JSON.stringify(descriptor))})`;
}

function csharpString(value: string) {
  return value.includes('"') || value.includes("\n")
    ? `"""${value.replaceAll('"""', '\\"\\"\\"')}"""`
    : `"${escapeCSharp(value)}"`;
}

function escapeCSharp(value: string) {
  return value.replaceAll("\\", "\\\\").replaceAll('"', '\\"');
}

function repositoryPath(repositoryRoot: string, relativePath: string) {
  const normalized = relativePath.replaceAll("\\", "/");
  if (path.isAbsolute(relativePath)
      || normalized !== path.posix.normalize(normalized)
      || normalized === ".."
      || normalized.startsWith("../")) {
    throw new Error(`Generated scaffold path escapes are prohibited: ${relativePath}`);
  }
  if (normalized === "docs/old" || normalized.startsWith("docs/old/")) {
    throw new Error("Historical archive scaffold paths are prohibited.");
  }
  return path.resolve(repositoryRoot, normalized);
}
