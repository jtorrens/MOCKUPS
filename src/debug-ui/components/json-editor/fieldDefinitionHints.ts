import type {
  FieldDefinition,
  JsonFieldBinding,
} from "../../../domain/value-system/index.js";
import {
  controlDefinitionForField,
  editorMetadataForField,
} from "../../editor-ui/ValueKindControlRegistry.js";
import type { JsonUiHint, JsonUiHints, JsonWidget } from "./uiHints.js";

function widgetForField(field: FieldDefinition): JsonWidget | undefined {
  const control = controlDefinitionForField(field).control;
  switch (control) {
    case "number":
    case "alpha":
      return "number";
    case "checkbox":
      return "checkbox";
    case "select":
    case "themeColorToken":
    case "paletteColorToken":
    case "iconToken":
    case "recordSelect":
      return "select";
    case "typography":
      return field.kind === "fontFamily" ? "font" : undefined;
    case "jsonObject":
    case "jsonArray":
    case "text":
    case "filePath":
    case "relativeFilePath":
      return "text";
  }
}

function hintForField(field: FieldDefinition): JsonUiHint {
  const metadata = editorMetadataForField(field);
  return {
    field,
    dictionaryDerived: true,
    label: metadata.label,
    widget: widgetForField(field),
    options: metadata.options ? [...metadata.options] : undefined,
    min: metadata.min,
    max: metadata.max,
    step: metadata.step,
    lockFontFamily: metadata.lockFontFamily,
    allowMultiple: metadata.allowMultiple,
    allowEmpty: metadata.allowEmpty,
    semanticTokenGroup: metadata.semanticTokenGroup,
    tableId: metadata.tableId,
    labelColumn: metadata.labelColumn,
    fileKind: metadata.fileKind,
    accept: metadata.accept ? [...metadata.accept] : undefined,
    multiline: metadata.multiline,
    rows: metadata.rows,
  };
}

export function jsonUiHintsFromFieldBindings(
  bindings: readonly JsonFieldBinding[],
): JsonUiHints {
  return Object.fromEntries(
    bindings
      .filter((binding) => binding.field && binding.outputPath.length > 0)
      .map((binding) => [
        binding.outputPath.join("."),
        hintForField(binding.field as FieldDefinition),
      ]),
  );
}
