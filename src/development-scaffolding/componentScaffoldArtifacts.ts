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
  type JsonObject,
} from "./componentScaffold.js";

export const draftComponentSpecRoot = "scaffolding/drafts";
export const integratedComponentSpecRoot = "scaffolding/components";

export const generatedComponentRegistryPath =
  "src/desktop-preview/generatedComponentScaffoldRegistry.ts";
export const generatedDesktopFieldCatalogPath =
  "spikes/desktop-editor-shell/EditorShell/GeneratedComponentScaffoldFieldCatalog.cs";
export const generatedDesktopConfigRegistryPath =
  "spikes/desktop-editor-shell/Data/GeneratedComponentScaffoldConfigRegistry.cs";

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
    + `internal static partial class ComponentClassFieldCatalog\n`
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
    const owner = `${pascalCase(spec.component.componentType)}ComponentConfigContract`;
    return `            case ${owner}.ComponentType:\n`
      + `                ${owner}.Validate(config, context);\n`
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

function renderField(field: ComponentScaffoldField) {
  const argumentsList = [
    csharpString(field.id),
    csharpString(field.label),
    `ValueKind.${field.valueKind}`,
    `[${field.jsonPath.map(csharpString).join(", ")}]`,
    csharpString(field.defaultValue),
  ];
  if (!field.isEditable) argumentsList.push("IsEditable: false");
  if (field.optionsSource) {
    argumentsList.push(`Options: ${field.optionsSource}`);
  } else if (field.options.length > 0) {
    argumentsList.push(`Options: [${field.options.map(renderOption).join(", ")}]`);
  }
  if (field.pairLabels) {
    argumentsList.push(
      `PairLabels: new(${csharpString(field.pairLabels.first)}, `
      + `${csharpString(field.pairLabels.second)})`,
    );
  }
  if (field.number) {
    argumentsList.push(
      `Number: new NumberDefinition(${csharpDecimal(field.number.minimum)}, `
      + `${csharpDecimal(field.number.maximum)}, `
      + `${csharpDecimal(field.number.increment)}, `
      + `${field.number.decimalPlaces}, `
      + `${field.number.useSlider ? "true" : "false"})`,
    );
  }
  if (field.componentInputBindings !== null || field.structuredCollection !== null) {
    throw new Error(
      `Generated dictionary field '${field.id}' uses a complex owner that requires an explicit scaffold renderer.`,
    );
  }
  if (field.componentVariantType) {
    argumentsList.push(
      `ComponentVariantType: ${csharpString(field.componentVariantType)}`,
    );
  }
  if (field.runtimeInputComponentVariantFieldId) {
    argumentsList.push(
      `RuntimeInputComponentVariantFieldId: `
      + `${csharpString(field.runtimeInputComponentVariantFieldId)}`,
    );
  }
  if (field.unit) argumentsList.push(`Unit: ${csharpString(field.unit)}`);
  return `new(${argumentsList.join(", ")})`;
}

function renderOption(option: JsonObject) {
  const value = requiredOptionString(option, "value");
  const label = requiredOptionString(option, "label");
  const optional = [
    ["colorHex", "ColorHex"],
    ["groupValue", "GroupValue"],
    ["groupLabel", "GroupLabel"],
    ["localLabel", "LocalLabel"],
  ] as const;
  const args = [csharpString(value), csharpString(label)];
  if (option.isNeutral === true) args.push("IsNeutral: true");
  for (const [jsonKey, csharpName] of optional) {
    if (typeof option[jsonKey] === "string" && option[jsonKey]) {
      args.push(`${csharpName}: ${csharpString(option[jsonKey])}`);
    }
  }
  return `new(${args.join(", ")})`;
}

function requiredOptionString(option: JsonObject, key: string) {
  const value = option[key];
  if (typeof value !== "string") {
    throw new Error(`Generated dictionary option requires string '${key}'.`);
  }
  return value;
}

function csharpString(value: string) {
  return value.includes('"') || value.includes("\n")
    ? `"""${value.replaceAll('"""', '\\"\\"\\"')}"""`
    : `"${escapeCSharp(value)}"`;
}

function escapeCSharp(value: string) {
  return value.replaceAll("\\", "\\\\").replaceAll('"', '\\"');
}

function csharpDecimal(value: number | null) {
  if (value === null) return "null";
  return Number.isInteger(value) ? String(value) : `${value}m`;
}

function pascalCase(value: string) {
  return value.length === 0
    ? value
    : `${value[0]!.toUpperCase()}${value.slice(1)}`;
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
