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
    case "surfaceStyle":
    case "componentOverride":
      return "text";
  }
}

function encodedHintPath(path: readonly (string | number)[]) {
  return JSON.stringify(path);
}

function hintForField(
  field: FieldDefinition,
  storagePath: readonly (string | number)[],
): JsonUiHint {
  const metadata = editorMetadataForField(field);
  return {
    field,
    dictionaryDerived: true,
    label: metadata.label,
    storagePath: [...storagePath],
    group: metadata.group?.id,
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
    numericControl: metadata.numericControl,
  };
}

export function jsonUiHintsFromFieldBindings(
  bindings: readonly JsonFieldBinding[],
): JsonUiHints {
  return Object.fromEntries(
    bindings
      .filter((binding) => binding.field && binding.outputPath.length > 0)
      .flatMap((binding) => {
        const hint = hintForField(
          binding.field as FieldDefinition,
          binding.outputPath,
        );
        return [
          [binding.outputPath.join("."), hint],
          [encodedHintPath(binding.outputPath), hint],
        ];
      }),
  );
}
