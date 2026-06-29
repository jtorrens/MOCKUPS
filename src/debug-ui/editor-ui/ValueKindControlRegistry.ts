import type {
  FieldDefinition,
  FieldEditorMetadata,
} from "../../domain/value-system/index.js";
import { ValueRegistry as DomainValueRegistry } from "../../domain/value-system/index.js";
import type { ValueKind } from "../../domain/value-system/index.js";

export type EditorControlKind =
  | "number"
  | "text"
  | "checkbox"
  | "select"
  | "typography"
  | "paletteColorToken"
  | "themeColorToken"
  | "alpha"
  | "iconToken"
  | "recordSelect"
  | "filePath"
  | "relativeFilePath"
  | "jsonObject"
  | "jsonArray";

export interface ValueKindControlDefinition {
  readonly kind: ValueKind;
  readonly control: EditorControlKind;
  readonly label: string;
  readonly defaultStep?: number | "any";
  readonly requiresOptions?: boolean;
}

export interface FieldControlSpec {
  readonly field: FieldDefinition;
  readonly valueKind: ReturnType<typeof DomainValueRegistry.definition>;
  readonly controlDefinition: ValueKindControlDefinition;
  readonly metadata: FieldEditorMetadata;
  readonly acceptsInherited: boolean;
}

const VALUE_KIND_CONTROL_DEFINITIONS = [
  {
    kind: "integer",
    control: "number",
    label: "Integer",
    defaultStep: 1,
  },
  {
    kind: "decimal",
    control: "number",
    label: "Decimal",
    defaultStep: "any",
  },
  { kind: "text", control: "text", label: "Text" },
  { kind: "boolean", control: "checkbox", label: "Boolean" },
  {
    kind: "enum",
    control: "select",
    label: "Enum",
    requiresOptions: true,
  },
  {
    kind: "fontFamily",
    control: "typography",
    label: "Font family",
  },
  {
    kind: "fontWeight",
    control: "typography",
    label: "Font weight",
  },
  {
    kind: "fontStyle",
    control: "typography",
    label: "Font style",
  },
  {
    kind: "paletteColorToken",
    control: "paletteColorToken",
    label: "Palette color token",
  },
  {
    kind: "themeColorToken",
    control: "themeColorToken",
    label: "Theme color token",
  },
  { kind: "alpha", control: "alpha", label: "Alpha", defaultStep: 0.01 },
  { kind: "iconToken", control: "iconToken", label: "Icon token" },
  {
    kind: "recordReference",
    control: "recordSelect",
    label: "Record reference",
  },
  { kind: "filePath", control: "filePath", label: "File path" },
  {
    kind: "relativeFilePath",
    control: "relativeFilePath",
    label: "Relative file path",
  },
  { kind: "jsonObject", control: "jsonObject", label: "JSON object" },
  { kind: "jsonArray", control: "jsonArray", label: "JSON array" },
] satisfies readonly ValueKindControlDefinition[];

const controlDefinitions = new Map<ValueKind, ValueKindControlDefinition>(
  VALUE_KIND_CONTROL_DEFINITIONS.map((definition) => [
    definition.kind,
    definition,
  ]),
);

export function controlDefinitionForValueKind(
  kind: ValueKind,
): ValueKindControlDefinition {
  const definition = controlDefinitions.get(kind);
  if (!definition) {
    throw new Error(`No editor control registered for value kind "${kind}"`);
  }
  return definition;
}

export function controlDefinitionForField(
  field: FieldDefinition,
): ValueKindControlDefinition {
  return controlDefinitionForValueKind(field.kind);
}

export function editorMetadataForField(
  field: FieldDefinition,
): FieldEditorMetadata {
  const definition = controlDefinitionForField(field);
  return {
    step: definition.defaultStep,
    ...field.ui,
  };
}

export function fieldControlSpecForField(field: FieldDefinition): FieldControlSpec {
  const valueKind = DomainValueRegistry.definition(field.kind);
  return {
    field,
    valueKind,
    controlDefinition: controlDefinitionForField(field),
    metadata: editorMetadataForField(field),
    acceptsInherited: valueKind.acceptsInherited !== false,
  };
}

export function allValueKindControlDefinitions() {
  return [...VALUE_KIND_CONTROL_DEFINITIONS];
}
