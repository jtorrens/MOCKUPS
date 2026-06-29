import {
  ValueRegistry,
  type FieldDefinition,
  type JsonFieldBinding,
} from "../../../domain/value-system/index.js";
import {
  deleteAtPathAndPrune,
  deepEqualJson,
  getAtPath,
  hasAtPath,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";
import type { DictionarySelectOptions } from "../DictionaryFieldControl.js";
import type {
  EditorFieldDescriptor,
  EditorFieldState,
  EditorValidation,
} from "./EditorFieldDescriptor.js";

export interface CreateJsonFieldDescriptorOptions {
  readonly binding: JsonFieldBinding;
  readonly localRoot: JsonValue;
  readonly parentRoot: JsonValue;
  readonly fallbackRoot?: JsonValue;
  readonly sourceKind?: "json-binding" | "component-override" | "module-instance-content";
  readonly recordId?: string;
  readonly recordType?: string;
  readonly readonly?: boolean;
  readonly restoreMode?: "remove" | "set";
  readonly selectOptions?: DictionarySelectOptions;
  readonly placeholder?: string;
  readonly onRootChange: (nextRoot: JsonValue) => void;
}

function jsonPathFromBinding(path: readonly string[]): JsonPath {
  return [...path];
}

function fieldForBinding(binding: JsonFieldBinding): FieldDefinition | undefined {
  if (binding.field) return binding.field;
  if (!binding.kind) return undefined;
  return {
    id: binding.fieldId ?? binding.outputPath.join("."),
    kind: binding.kind,
    defaultValue: binding.fallback,
  };
}

function normalizeForDisplay(field: FieldDefinition, value: unknown): unknown {
  if (value === undefined) return "";
  if (ValueRegistry.isInherited(value)) return value;
  return ValueRegistry.normalize(field.kind, value);
}

function validationForField(
  field: FieldDefinition,
  value: unknown,
): EditorValidation | undefined {
  if (value === "" && field.ui?.allowEmpty === true) return undefined;
  const result = ValueRegistry.validate(field.kind, value);
  if (result.ok) return undefined;
  return {
    valid: false,
    message: result.issue.message,
    severity: "error",
  };
}

function stateForValues({
  field,
  localHasValue,
  localValue,
  parentHasValue,
  defaultHasValue,
  validation,
}: {
  readonly field: FieldDefinition;
  readonly localHasValue: boolean;
  readonly localValue: unknown;
  readonly parentHasValue: boolean;
  readonly defaultHasValue: boolean;
  readonly validation?: EditorValidation;
}): EditorFieldState {
  if (validation && !validation.valid) return "invalid";
  if (localHasValue && !ValueRegistry.isInherited(localValue)) return "local";
  if (parentHasValue) return "inherited";
  if (defaultHasValue || field.defaultValue !== undefined) return "default";
  return "default";
}

export function createJsonFieldDescriptor({
  binding,
  localRoot,
  parentRoot,
  fallbackRoot,
  sourceKind = "json-binding",
  recordId,
  recordType,
  readonly = false,
  restoreMode = "remove",
  selectOptions,
  placeholder,
  onRootChange,
}: CreateJsonFieldDescriptorOptions): EditorFieldDescriptor | undefined {
  const field = fieldForBinding(binding);
  if (!field || binding.outputPath.length === 0) return undefined;

  const path = jsonPathFromBinding(binding.outputPath);
  const localHasValue = hasAtPath(localRoot, path);
  const parentHasValue = hasAtPath(parentRoot, path);
  const fallbackHasValue =
    fallbackRoot !== undefined && hasAtPath(fallbackRoot, path);
  const localValue = localHasValue ? getAtPath(localRoot, path) : undefined;
  const parentValue = parentHasValue ? getAtPath(parentRoot, path) : undefined;
  const fallbackValue =
    fallbackHasValue && fallbackRoot !== undefined
      ? getAtPath(fallbackRoot, path)
      : undefined;
  const defaultValue = field.defaultValue ?? binding.fallback ?? fallbackValue;
  const displaySource =
    localHasValue && !ValueRegistry.isInherited(localValue)
      ? localValue
      : parentHasValue
        ? parentValue
        : defaultValue;
  const displayValue = normalizeForDisplay(field, displaySource);
  const validation = validationForField(field, displayValue);
  const baselineValue = parentHasValue ? parentValue : defaultValue;
  const state = stateForValues({
    field,
    localHasValue,
    localValue,
    parentHasValue,
    defaultHasValue: defaultValue !== undefined,
    validation,
  });
  const canRestore =
    localHasValue &&
    !deepEqualJson(
      (localValue ?? null) as JsonValue,
      (baselineValue ?? null) as JsonValue,
    );

  return {
    kind: "field",
    field,
    localValue,
    parentValue,
    defaultValue,
    resolvedValue: displayValue,
    displayValue,
    state,
    readonly,
    canInherit: true,
    canRestore,
    validation,
    source: {
      kind: sourceKind,
      path: binding.outputPath,
      recordId,
      recordType,
    },
    actions: {
      write: (nextValue) => {
        onRootChange(setAtPath(localRoot, path, nextValue as JsonValue));
      },
      restore: canRestore
        ? () => {
            onRootChange(
              restoreMode === "set"
                ? setAtPath(
                    localRoot,
                    path,
                    (parentHasValue ? parentValue : defaultValue ?? null) as JsonValue,
                  )
                : deleteAtPathAndPrune(localRoot, path),
            );
          }
        : undefined,
    },
    selectOptions,
    placeholder,
  };
}
