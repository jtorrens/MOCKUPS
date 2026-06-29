import type { FieldDefinition } from "../../domain/value-system/index.js";
import { DeferredNumberInput } from "./DeferredNumberInput.js";
import { DeferredTextInput } from "./DeferredTextInput.js";
import {
  controlDefinitionForField,
  fieldControlSpecForField,
} from "./ValueKindControlRegistry.js";

export const DICTIONARY_FIELD_CLASS = "dictionary-field";
export const DICTIONARY_CONTROL_CLASS = "dictionary-control";

export interface DictionarySelectOption {
  readonly value: string;
  readonly label: string;
}

export interface DictionarySelectOptions {
  readonly allowEmpty?: boolean;
  readonly emptyLabel?: string;
  readonly options: readonly DictionarySelectOption[];
}

export interface DictionaryFieldControlProps {
  field: FieldDefinition;
  value: unknown;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  selectOptions?: DictionarySelectOptions;
  validation?: {
    readonly valid: boolean;
    readonly message?: string;
  };
  onChange: (nextValue: unknown) => void;
}

function classNameForField(field: FieldDefinition) {
  return `${DICTIONARY_CONTROL_CLASS} ${DICTIONARY_CONTROL_CLASS}--${controlDefinitionForField(field).control}`;
}

function stringValue(value: unknown) {
  return value === undefined || value === null ? "" : String(value);
}

function booleanValue(value: unknown) {
  return value === true || value === "true" || value === "1";
}

export function DictionaryFieldControl({
  field,
  value,
  disabled = false,
  readOnly = false,
  placeholder,
  selectOptions,
  onChange,
}: DictionaryFieldControlProps) {
  const spec = fieldControlSpecForField(field);
  const control = spec.controlDefinition.control;
  const metadata = spec.metadata;
  const controlClassName = classNameForField(field);

  if (control === "checkbox") {
    return (
      <label className={`json-checkbox ${controlClassName}`}>
        <input
          disabled={disabled || readOnly}
          type="checkbox"
          checked={booleanValue(value)}
          onChange={(event) => onChange(event.currentTarget.checked)}
        />
        {booleanValue(value) ? "true" : "false"}
      </label>
    );
  }

  if (
    control === "select" ||
    control === "recordSelect" ||
    control === "themeColorToken" ||
    control === "paletteColorToken" ||
    control === "iconToken"
  ) {
    const options = selectOptions?.options ?? [];
    return (
      <select
        className={controlClassName}
        disabled={disabled || readOnly}
        value={stringValue(value)}
        onChange={(event) => onChange(event.currentTarget.value)}
      >
        {selectOptions?.allowEmpty ? (
          <option value="">{selectOptions.emptyLabel ?? "Inherited/default"}</option>
        ) : null}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  if (control === "number" || control === "alpha") {
    return (
      <DeferredNumberInput
        className={controlClassName}
        disabled={disabled || readOnly}
        max={metadata.max}
        min={metadata.min}
        placeholder={placeholder}
        step={metadata.step ?? "any"}
        value={typeof value === "number" && Number.isFinite(value) ? value : ""}
        onEmptyCommit={() => onChange("")}
        onCommit={onChange}
      />
    );
  }

  return (
    <DeferredTextInput
      className={controlClassName}
      disabled={disabled || readOnly}
      placeholder={placeholder}
      value={stringValue(value)}
      onCommit={onChange}
    />
  );
}
