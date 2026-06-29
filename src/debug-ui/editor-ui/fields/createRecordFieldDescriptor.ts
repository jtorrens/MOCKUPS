import {
  ValueRegistry,
  type FieldDefinition,
} from "../../../domain/value-system/index.js";
import type { DictionarySelectOptions } from "../DictionaryFieldControl.js";
import type {
  EditorFieldDescriptor,
  EditorFieldState,
  EditorValidation,
} from "./EditorFieldDescriptor.js";

export interface CreateRecordFieldDescriptorOptions {
  readonly field: FieldDefinition;
  readonly column: string;
  readonly value: unknown;
  readonly recordId?: string;
  readonly recordType?: string;
  readonly readonly?: boolean;
  readonly error?: string;
  readonly selectOptions?: DictionarySelectOptions;
  readonly placeholder?: string;
  readonly state?: EditorFieldState;
  readonly onWrite: (value: unknown) => void;
}

function stringValue(value: unknown): string {
  return value === undefined || value === null ? "" : String(value);
}

function booleanValue(value: unknown): boolean {
  return value === true || value === "true" || value === "1";
}

function numericValue(value: unknown): number | "" {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value !== "string" || !value.trim()) return "";
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : "";
}

function displayValueForField(field: FieldDefinition, value: unknown): unknown {
  switch (field.kind) {
    case "integer":
    case "decimal":
    case "alpha":
    case "fontWeight":
      return numericValue(value);
    case "boolean":
      return booleanValue(value);
    default:
      return stringValue(value);
  }
}

function isEmptyAllowed(field: FieldDefinition, value: unknown): boolean {
  return (
    value === "" &&
    (field.ui?.allowEmpty === true || field.defaultValue === undefined)
  );
}

function validationForField(
  field: FieldDefinition,
  displayValue: unknown,
  error: string | undefined,
): EditorValidation | undefined {
  if (error) {
    return {
      valid: false,
      message: error,
      severity: "error",
    };
  }
  if (isEmptyAllowed(field, displayValue)) return undefined;
  const result = ValueRegistry.validate(field.kind, displayValue);
  if (result.ok) return undefined;
  return {
    valid: false,
    message: result.issue.message,
    severity: "error",
  };
}

export function createRecordFieldDescriptor({
  field,
  column,
  value,
  recordId,
  recordType,
  readonly = false,
  error,
  selectOptions,
  placeholder,
  state,
  onWrite,
}: CreateRecordFieldDescriptorOptions): EditorFieldDescriptor {
  const displayValue = displayValueForField(field, value);
  const validation = validationForField(field, displayValue, error);
  const resolvedState: EditorFieldState =
    state ?? (validation && !validation.valid ? "invalid" : "default");

  return {
    kind: "field",
    field,
    localValue: displayValue,
    defaultValue: field.defaultValue,
    resolvedValue: displayValue,
    displayValue,
    state: resolvedState,
    readonly,
    canInherit: false,
    canRestore: false,
    validation,
    source: {
      kind: "record-column",
      path: [column],
      recordId,
      recordType,
    },
    actions: {
      write: onWrite,
    },
    selectOptions,
    placeholder,
  };
}
