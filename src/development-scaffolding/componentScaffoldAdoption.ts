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
  parseComponentScaffoldSpec,
  type ComponentScaffoldField,
  type ComponentScaffoldSpec,
  type JsonObject,
  type JsonValue,
} from "./componentScaffold.js";
import { integratedComponentSpecRoot } from "./componentScaffoldWorkspace.js";

export type ComponentScaffoldIntent = ComponentScaffoldSpec["intent"];

export interface AdoptedComponentScaffold {
  schemaVersion: 1;
  status: "existing-component-adopted";
  componentType: string;
  specPath: string;
}

export function adoptExistingComponentScaffold(
  componentType: string,
  intent: ComponentScaffoldIntent,
  repositoryRoot: string,
  databasePath = path.join(repositoryRoot, "data", "desktop-editor-spike.sqlite"),
): AdoptedComponentScaffold {
  const specPath = `${integratedComponentSpecRoot}/${componentType}.json`;
  const resolvedSpecPath = repositoryPath(repositoryRoot, specPath);
  if (existsSync(resolvedSpecPath)) {
    throw new ComponentScaffoldValidationError([
      `Existing Component adoption will not overwrite '${specPath}'.`,
    ]);
  }

  const manifest = jsonObject(
    JSON.parse(readFileSync(repositoryPath(
      repositoryRoot,
      "src/desktop-preview/desktopPreviewManifest.json",
    ), "utf8")),
    "Desktop Preview manifest",
  );
  const manifestComponents = jsonObject(
    manifest.components,
    "Desktop Preview manifest components",
  );
  const manifestEntry = jsonObject(
    manifestComponents[componentType],
    `Desktop Preview manifest Component '${componentType}'`,
  );
  const contractRoute = stringValue(manifestEntry.contract, "manifest contract");
  const resolverRoute = stringValue(manifestEntry.resolver, "manifest resolver");
  const renderableRoute = stringValue(manifestEntry.renderable, "manifest renderable");

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
      WHERE component_type = ?
      ORDER BY id
    `).all(componentType) as Array<{
      id: string;
      project_id: string;
      component_type: string;
      record_class_id: string;
      name: string;
      notes: string;
      config_json: string;
      design_preview_json: string;
      metadata_json: string;
    }>;
    if (row.length !== 1) {
      throw new ComponentScaffoldValidationError([
        `Existing Component adoption requires exactly one '${componentType}' class; found ${row.length}.`,
      ]);
    }
    const component = row[0]!;
    const config = jsonObject(JSON.parse(component.config_json), "Component config");
    const preview = jsonObject(
      JSON.parse(component.design_preview_json),
      "Component Design Preview",
    );
    const metadataDocument = jsonObject(
      JSON.parse(component.metadata_json),
      "Component metadata",
    );
    if (!Array.isArray(metadataDocument.variants)) {
      throw new Error("Component metadata variants must be an array.");
    }
    const variants = metadataDocument.variants.map((value, index) =>
      jsonObject(value, `Component Variant ${index}`));
    const defaultVariant = variants.find((variant) => variant.id === "default");
    if (!defaultVariant) {
      throw new Error("Existing Component adoption requires the Default Variant.");
    }
    const additionalVariants = variants.filter((variant) => variant.id !== "default");
    const metadata = structuredClone(metadataDocument);
    delete metadata.variants;

    const layoutRow = database.prepare(`
      SELECT layout_json
      FROM editor_layouts
      WHERE record_class_id = ?
    `).get(component.record_class_id) as { layout_json: string } | undefined;
    if (!layoutRow) {
      throw new Error(`Missing editor layout '${component.record_class_id}'.`);
    }

    const contractSource = ownerSource(repositoryRoot, contractRoute);
    const resolverSource = ownerSource(repositoryRoot, resolverRoute);
    const renderableSource = ownerSource(repositoryRoot, renderableRoute);
    const focusedTest = `tests/animation/${componentType}Component.test.ts`;
    if (!existsSync(repositoryPath(repositoryRoot, focusedTest))) {
      throw new Error(`Missing focused Component test '${focusedTest}'.`);
    }

    const spec = parseComponentScaffoldSpec({
      schemaVersion: 1,
      intent,
      component: {
        componentType,
        category: stringValue(manifestEntry.category, "manifest category"),
        componentClassId: component.id,
        projectId: component.project_id,
        recordClassId: component.record_class_id,
        name: component.name,
        notes: component.notes,
      },
      manifest: {
        contract: contractRoute,
        resolver: resolverRoute,
        renderable: renderableRoute,
        embeds: stringArray(manifestEntry.embeds, "manifest embeds"),
      },
      owners: {
        contractExport: requiredExport(
          contractSource,
          /export (?:interface|type) ([A-Za-z_$][A-Za-z0-9_$]*DesignContract)\b/,
          "contract",
        ),
        resolverExport: requiredExport(
          resolverSource,
          /export function ([A-Za-z_$][A-Za-z0-9_$]*)\s*\(/,
          "resolver",
        ),
        renderableExport: requiredExport(
          renderableSource,
          /export function ([A-Za-z_$][A-Za-z0-9_$]*)\s*\(/,
          "renderable",
        ),
        registryMode: registryMode(repositoryRoot, componentType),
        focusedTest,
      },
      config,
      defaultVariant,
      additionalVariants,
      designPreview: preview,
      metadata,
      dictionaryFields: adoptedDictionaryFields(
        component.record_class_id,
        config,
        repositoryRoot,
      ),
      editorLayout: jsonObject(
        JSON.parse(layoutRow.layout_json),
        "Component editor layout",
      ),
      assets: [],
    });

    mkdirSync(path.dirname(resolvedSpecPath), { recursive: true });
    writeFileSync(resolvedSpecPath, `${JSON.stringify(spec, null, 2)}\n`, {
      encoding: "utf8",
      flag: "wx",
    });
    return {
      schemaVersion: 1,
      status: "existing-component-adopted",
      componentType,
      specPath,
    };
  } finally {
    database.close();
  }
}

function adoptedDictionaryFields(
  recordClassId: string,
  config: JsonObject,
  repositoryRoot: string,
) {
  const source = readFileSync(repositoryPath(
    repositoryRoot,
    "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
  ), "utf8");
  const prefix = `${recordClassId}.`;
  const fields: ComponentScaffoldField[] = [];
  for (const line of source.split(/\r?\n/)) {
    const match = line.match(/^\s*\["([^"]+)"\]\s*=\s*new\((.*)\),\s*$/);
    if (!match || !match[1]!.startsWith(prefix)) continue;
    const args = splitArguments(match[2]!);
    if (args.length < 5) {
      throw new Error(`Cannot adopt dictionary field '${match[1]}' from its current source.`);
    }
    const id = csharpString(args[0]!);
    const label = csharpString(args[1]!);
    const valueKind = /^ValueKind\.([A-Za-z0-9_]+)$/.exec(args[2]!)?.[1];
    if (!valueKind) throw new Error(`Cannot adopt ValueKind for '${id}'.`);
    const jsonPath = [...args[3]!.matchAll(/"([^"]+)"/g)].map((item) => item[1]!);
    const currentValue = jsonPathValue(config, jsonPath);
    const named = new Map<string, string>();
    for (const argument of args.slice(5)) {
      const namedMatch = /^([A-Za-z][A-Za-z0-9]*):\s*(.*)$/.exec(argument);
      if (namedMatch) named.set(namedMatch[1]!, namedMatch[2]!);
    }
    const optionsValue = named.get("Options") ?? "";
    const inlineOptions = optionsValue.startsWith("[")
      ? [...optionsValue.matchAll(/new\("([^"]*)",\s*"([^"]*)"\)/g)].map((option) => ({
          value: option[1]!,
          label: option[2]!,
        }))
      : [];
    const pair = /new\("([^"]*)",\s*"([^"]*)"\)/.exec(named.get("PairLabels") ?? "");
    const number = /new NumberDefinition\((.*)\)/.exec(named.get("Number") ?? "");
    const numberArgs = number ? splitArguments(number[1]!) : [];
    fields.push({
      id,
      label,
      valueKind,
      jsonPath,
      defaultValue: fieldStringValue(currentValue),
      isEditable: named.get("IsEditable") !== "false",
      options: inlineOptions,
      optionsSource: optionsValue && !optionsValue.startsWith("[") ? optionsValue : "",
      pairLabels: pair ? { first: pair[1]!, second: pair[2]! } : null,
      number: number
        ? {
            minimum: decimalValue(numberArgs[0]),
            maximum: decimalValue(numberArgs[1]),
            increment: decimalValue(numberArgs[2]) ?? 1,
            decimalPlaces: Math.trunc(decimalValue(numberArgs[3]) ?? 0),
            useSlider: (numberArgs[4] ?? "false") === "true",
          }
        : null,
      componentInputBindings: null,
      structuredCollection: null,
      componentVariantType: csharpNamedString(named.get("ComponentVariantType")),
      runtimeInputComponentVariantFieldId: csharpNamedString(
        named.get("RuntimeInputComponentVariantFieldId"),
      ),
      unit: csharpNamedString(named.get("Unit")),
    });
  }
  if (fields.length === 0) {
    throw new Error(`No dictionary fields were found for '${recordClassId}'.`);
  }
  return fields;
}

function registryMode(repositoryRoot: string, componentType: string) {
  const source = readFileSync(repositoryPath(
    repositoryRoot,
    "src/desktop-preview/componentClassRenderableRegistry.ts",
  ), "utf8");
  const match = new RegExp(
    `\\n\\s*${escapeRegex(componentType)}:\\s*\\(([^)]*)\\)\\s*=>`,
  ).exec(source);
  if (!match) throw new Error(`Missing registry route '${componentType}'.`);
  const args = match[1]!.split(",").map((value) => value.trim());
  const assignedBox = args.includes("assignedBox");
  const children = args.includes("renderChild");
  if (assignedBox && children) return "assignedBoxAndChildren";
  if (assignedBox) return "assignedBox";
  if (children) return "children";
  return "simple";
}

function ownerSource(repositoryRoot: string, route: string) {
  if (!route.startsWith("./")) throw new Error(`Invalid manifest owner route '${route}'.`);
  return readFileSync(repositoryPath(
    repositoryRoot,
    `src/desktop-preview/${route.slice(2)}.ts`,
  ), "utf8");
}

function requiredExport(source: string, pattern: RegExp, owner: string) {
  const value = pattern.exec(source)?.[1];
  if (!value) throw new Error(`Cannot derive ${owner} export from its current owner.`);
  return value;
}

function jsonPathValue(root: JsonObject, segments: string[]): JsonValue {
  let value: JsonValue = root;
  for (const segment of segments) {
    if (!value || typeof value !== "object" || Array.isArray(value)) {
      throw new Error(`Dictionary JSON path '${segments.join(".")}' is missing.`);
    }
    value = value[segment] as JsonValue;
  }
  return value;
}

function fieldStringValue(value: JsonValue) {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return JSON.stringify(value);
}

function splitArguments(source: string) {
  const values: string[] = [];
  let start = 0;
  let round = 0;
  let square = 0;
  let curly = 0;
  let quoted = false;
  let raw = false;
  for (let index = 0; index < source.length; index++) {
    if (source.startsWith('"""', index)) {
      raw = !raw;
      index += 2;
      continue;
    }
    const character = source[index]!;
    if (!raw && character === '"' && source[index - 1] !== "\\") {
      quoted = !quoted;
      continue;
    }
    if (quoted || raw) continue;
    if (character === "(") round++;
    else if (character === ")") round--;
    else if (character === "[") square++;
    else if (character === "]") square--;
    else if (character === "{") curly++;
    else if (character === "}") curly--;
    else if (character === "," && round === 0 && square === 0 && curly === 0) {
      values.push(source.slice(start, index).trim());
      start = index + 1;
    }
  }
  values.push(source.slice(start).trim());
  return values;
}

function csharpString(value: string) {
  if (value.startsWith('"""') && value.endsWith('"""')) return value.slice(3, -3);
  if (!value.startsWith('"') || !value.endsWith('"')) {
    throw new Error(`Expected a C# string literal, received '${value}'.`);
  }
  return JSON.parse(value) as string;
}

function csharpNamedString(value: string | undefined) {
  return value ? csharpString(value) : "";
}

function decimalValue(value: string | undefined): number | null {
  if (value === undefined || value === "null") return null;
  const parsed = Number(value.replace(/m$/, ""));
  if (!Number.isFinite(parsed)) throw new Error(`Invalid C# decimal '${value}'.`);
  return parsed;
}

function repositoryPath(repositoryRoot: string, relativePath: string) {
  const normalized = relativePath.replaceAll("\\", "/");
  if (path.isAbsolute(relativePath)
      || normalized !== path.posix.normalize(normalized)
      || normalized === ".."
      || normalized.startsWith("../")) {
    throw new Error(`Scaffold path escapes are prohibited: ${relativePath}`);
  }
  if (normalized === "docs/old" || normalized.startsWith("docs/old/")) {
    throw new Error("Historical archive scaffold paths are prohibited.");
  }
  return path.resolve(repositoryRoot, normalized);
}

function jsonObject(value: unknown, owner: string): JsonObject {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`${owner} must be an object.`);
  }
  return value as JsonObject;
}

function stringArray(value: JsonValue | undefined, owner: string) {
  if (!Array.isArray(value) || value.some((item) => typeof item !== "string")) {
    throw new Error(`${owner} must be a string array.`);
  }
  return value as string[];
}

function stringValue(value: JsonValue | undefined, owner: string) {
  if (typeof value !== "string" || !value) throw new Error(`${owner} must be a string.`);
  return value;
}

function escapeRegex(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
