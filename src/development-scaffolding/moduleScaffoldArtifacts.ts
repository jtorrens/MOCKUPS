import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import path from "node:path";

import {
  parseModuleScaffoldSpec,
  type ModuleScaffoldField,
  type ModuleScaffoldSpec,
} from "./moduleScaffold.js";
import type { JsonObject } from "./componentScaffold.js";

export const draftModuleSpecRoot = "scaffolding/module-drafts";
export const integratedModuleSpecRoot = "scaffolding/modules";

export const generatedModuleRegistryPath =
  "src/desktop-preview/generatedModuleScaffoldRegistry.ts";
export const generatedModuleFieldCatalogPath =
  "spikes/desktop-editor-shell/EditorShell/GeneratedModuleScaffoldFieldCatalog.cs";
export const generatedModuleConfigRegistryPath =
  "spikes/desktop-editor-shell/Data/GeneratedModuleScaffoldConfigRegistry.cs";
export const generatedModuleEmbeddedSlotsPath =
  "spikes/desktop-editor-shell/EditorShell/GeneratedModuleScaffoldEmbeddedSlots.cs";

export function loadIntegratedModuleScaffoldSpecs(
  repositoryRoot: string,
): ModuleScaffoldSpec[] {
  const root = repositoryPath(repositoryRoot, integratedModuleSpecRoot);
  if (!existsSync(root)) return [];
  return readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith(".json"))
    .sort((left, right) => left.name.localeCompare(right.name))
    .map((entry) => parseModuleScaffoldSpec(
      JSON.parse(readFileSync(path.join(root, entry.name), "utf8")) as unknown,
    ));
}

export function expectedIntegratedModuleScaffoldArtifacts(
  specs: readonly ModuleScaffoldSpec[],
) {
  const ordered = [...specs].sort((left, right) =>
    left.module.recordClassId.localeCompare(right.module.recordClassId));
  return new Map<string, string>([
    [generatedModuleRegistryPath, renderRegistry(ordered)],
    [generatedModuleFieldCatalogPath, renderFieldCatalog(ordered)],
    [generatedModuleConfigRegistryPath, renderConfigRegistry(ordered)],
    [generatedModuleEmbeddedSlotsPath, renderEmbeddedSlots(ordered)],
  ]);
}

export function regenerateIntegratedModuleScaffoldArtifacts(repositoryRoot: string) {
  const specs = loadIntegratedModuleScaffoldSpecs(repositoryRoot);
  const written: string[] = [];
  for (const [relativePath, content] of expectedIntegratedModuleScaffoldArtifacts(specs)) {
    const target = repositoryPath(repositoryRoot, relativePath);
    if (!existsSync(target) || readFileSync(target, "utf8") !== content) {
      writeFileSync(target, content, "utf8");
      written.push(relativePath);
    }
  }
  return {
    schemaVersion: 1,
    status: "integrated-module-artifacts-generated",
    moduleClasses: specs.map((spec) => spec.module.recordClassId).sort(),
    written,
  };
}

function renderRegistry(specs: readonly ModuleScaffoldSpec[]) {
  const imports = specs.map((spec) =>
    `import { ${spec.owners.renderableExport} } from "${spec.manifest.renderable}.js";`)
    .join("\n");
  const routes = specs.map((spec) =>
    `  "${spec.module.recordClassId}": ${spec.owners.renderableExport},`)
    .join("\n");
  return `// Generated from scaffolding/modules/*.json. Do not edit manually.\n`
    + `import type { ModuleRenderableFactory } from "./moduleRenderableRegistry.js";\n`
    + `${imports}${imports ? "\n\n" : ""}`
    + `export const generatedModuleScaffoldFactories = {\n`
    + `${routes}${routes ? "\n" : ""}`
    + `} satisfies Record<string, ModuleRenderableFactory>;\n`;
}

function renderFieldCatalog(specs: readonly ModuleScaffoldSpec[]) {
  const fields = specs.flatMap((spec) => spec.dictionaryFields)
    .sort((left, right) => left.id.localeCompare(right.id))
    .map((field) => `        fields.Add(${csharpString(field.id)}, ${renderField(field)});`)
    .join("\n");
  return `// Generated from scaffolding/modules/*.json. Do not edit manually.\n`
    + `using System.Collections.Generic;\n\n`
    + `namespace Mockups.DesktopEditorShell.EditorShell;\n\n`
    + `internal static class GeneratedModuleScaffoldFieldCatalog\n`
    + `{\n`
    + `    public static void AddFields(\n`
    + `        Dictionary<string, RecordClassFieldDescriptor> fields)\n`
    + `    {\n`
    + `${fields}${fields ? "\n" : ""}`
    + `    }\n`
    + `}\n`;
}

function renderConfigRegistry(specs: readonly ModuleScaffoldSpec[]) {
  const validationCases = specs.map((spec) => {
    const owner = `${moduleTypeName(spec)}ModuleConfigContract`;
    return `            case ${owner}.RecordClassId:\n`
      + `                ${owner}.Validate(config, context);\n`
      + `                return true;`;
  }).join("\n");
  const descriptors = specs.flatMap((spec) =>
    spec.dictionaryFields.map((field) => ({ spec, field })))
    .sort((left, right) => left.field.id.localeCompare(right.field.id))
    .map(({ spec, field }) =>
      `        [${csharpString(field.id)}] = new(\n`
      + `            ${csharpString(spec.module.recordClassId)},\n`
      + `            ${csharpString(field.id)},\n`
      + `            ValueKind.${field.valueKind},\n`
      + `            [${field.jsonPath.map(csharpString).join(", ")}],\n`
      + `            ${csharpString(field.componentVariantType)}),`)
    .join("\n");
  return `// Generated from scaffolding/modules/*.json. Do not edit manually.\n`
    + `using Mockups.DesktopEditorShell.EditorShell;\n`
    + `using System;\n`
    + `using System.Collections.Generic;\n`
    + `using System.Text.Json.Nodes;\n\n`
    + `namespace Mockups.DesktopEditorShell.Data;\n\n`
    + `internal sealed record GeneratedModuleConfigFieldDescriptor(\n`
    + `    string RecordClassId,\n`
    + `    string FieldId,\n`
    + `    ValueKind ValueKind,\n`
    + `    string[] JsonPath,\n`
    + `    string ComponentVariantType);\n\n`
    + `internal static class GeneratedModuleScaffoldConfigRegistry\n`
    + `{\n`
    + `    private static readonly Dictionary<string, GeneratedModuleConfigFieldDescriptor> Fields =\n`
    + `        new(StringComparer.Ordinal)\n`
    + `    {\n`
    + `${descriptors}${descriptors ? "\n" : ""}`
    + `    };\n\n`
    + `    public static bool TryValidate(\n`
    + `        string recordClassId,\n`
    + `        JsonObject config,\n`
    + `        string context)\n`
    + `    {\n`
    + `        switch (recordClassId)\n`
    + `        {\n`
    + `${validationCases}${validationCases ? "\n" : ""}`
    + `            default:\n`
    + `                return false;\n`
    + `        }\n`
    + `    }\n\n`
    + `    public static bool TryGetField(\n`
    + `        string recordClassId,\n`
    + `        string fieldId,\n`
    + `        out GeneratedModuleConfigFieldDescriptor descriptor)\n`
    + `    {\n`
    + `        if (Fields.TryGetValue(fieldId, out var candidate)\n`
    + `            && candidate.RecordClassId.Equals(recordClassId, StringComparison.Ordinal))\n`
    + `        {\n`
    + `            descriptor = candidate;\n`
    + `            return true;\n`
    + `        }\n`
    + `        descriptor = new(\"\", \"\", ValueKind.StringSingleLine, [], \"\");\n`
    + `        return false;\n`
    + `    }\n`
    + `}\n`;
}

function renderEmbeddedSlots(specs: readonly ModuleScaffoldSpec[]) {
  const slots = specs.flatMap((spec) => spec.dictionaryFields)
    .filter((field) => field.embeddedSlot !== null)
    .sort((left, right) => left.id.localeCompare(right.id))
    .map((field) => {
      const embedded = field.embeddedSlot!;
      return `        new(\n`
        + `            ${csharpString(field.id)},\n`
        + `            ${csharpString(embedded.componentType)},\n`
        + `            ${csharpString(embedded.label)},\n`
        + `            ${csharpString(embedded.recordClassId)},\n`
        + `            [${field.jsonPath.map(csharpString).join(", ")}]),`;
    }).join("\n");
  return `// Generated from scaffolding/modules/*.json. Do not edit manually.\n`
    + `namespace Mockups.DesktopEditorShell.EditorShell;\n\n`
    + `internal static class GeneratedModuleScaffoldEmbeddedSlots\n`
    + `{\n`
    + `    public static EmbeddedComponentSlotDefinition[] All { get; } =\n`
    + `    [\n`
    + `${slots}${slots ? "\n" : ""}`
    + `    ];\n`
    + `}\n`;
}

function renderField(field: ModuleScaffoldField) {
  const args = [
    csharpString(field.id),
    csharpString(field.label),
    `ValueKind.${field.valueKind}`,
  ];
  if (!field.isEditable) args.push("IsEditable: false");
  if (field.optionsSource) {
    args.push(`Options: ${field.optionsSource}`);
  } else if (field.options.length > 0) {
    args.push(`Options: [${field.options.map(renderOption).join(", ")}]`);
  }
  if (field.pairLabels) {
    args.push(
      `PairLabels: new(${csharpString(field.pairLabels.first)}, `
      + `${csharpString(field.pairLabels.second)})`,
    );
  }
  if (field.number) {
    args.push(
      `Number: new(${csharpDecimal(field.number.minimum)}, `
      + `${csharpDecimal(field.number.maximum)}, `
      + `${csharpDecimal(field.number.increment)}, `
      + `${field.number.decimalPlaces}, `
      + `${field.number.useSlider ? "true" : "false"})`,
    );
  }
  if (field.componentVariantType) {
    args.push(`ComponentVariantType: ${csharpString(field.componentVariantType)}`);
  }
  if (field.unit) args.push(`Unit: ${csharpString(field.unit)}`);
  if (field.componentInputBindingsSource) {
    args.push(`ComponentInputBindings: ${field.componentInputBindingsSource}`);
  }
  if (field.runtimeInputComponentVariantFieldId) {
    args.push(
      `RuntimeInputComponentVariantFieldId: `
      + `${csharpString(field.runtimeInputComponentVariantFieldId)}`,
    );
  }
  if (field.runtimeCollectionComponentVariantFieldId) {
    args.push(
      `RuntimeCollectionComponentVariantFieldId: `
      + `${csharpString(field.runtimeCollectionComponentVariantFieldId)}`,
    );
  }
  return `new(${args.join(", ")})`;
}

function renderOption(option: JsonObject) {
  const value = requiredOptionString(option, "value");
  const label = requiredOptionString(option, "label");
  const args = [csharpString(value), csharpString(label)];
  if (option.isNeutral === true) args.push("IsNeutral: true");
  return `new(${args.join(", ")})`;
}

function requiredOptionString(option: JsonObject, key: string) {
  const value = option[key];
  if (typeof value !== "string") {
    throw new Error(`Generated Module dictionary option requires string '${key}'.`);
  }
  return value;
}

function moduleTypeName(spec: ModuleScaffoldSpec) {
  return pascalCase(spec.module.recordClassId.split(".").at(-1) ?? "");
}

function pascalCase(value: string) {
  return value.length === 0 ? value : `${value[0]!.toUpperCase()}${value.slice(1)}`;
}

function csharpString(value: string) {
  return value.includes('"') || value.includes("\n")
    ? `"""${value.replaceAll('"""', '\\"\\"\\"')}"""`
    : `"${value.replaceAll("\\", "\\\\").replaceAll('"', '\\"')}"`;
}

function csharpDecimal(value: number | null) {
  if (value === null) return "null";
  return Number.isInteger(value) ? String(value) : `${value}m`;
}

function repositoryPath(repositoryRoot: string, relativePath: string) {
  const normalized = relativePath.replaceAll("\\", "/");
  if (path.isAbsolute(relativePath)
      || path.win32.isAbsolute(relativePath)
      || normalized !== path.posix.normalize(normalized)
      || normalized === ".."
      || normalized.startsWith("../")) {
    throw new Error(`Generated Module scaffold path escapes are prohibited: ${relativePath}`);
  }
  if (normalized === "docs/old" || normalized.startsWith("docs/old/")) {
    throw new Error("Historical archive Module scaffold paths are prohibited.");
  }
  return path.resolve(repositoryRoot, normalized);
}
