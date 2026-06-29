import type { ReactNode } from "react";
import { type AppFieldDefinition } from "../api/client.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
} from "../editor-ui/DictionaryFieldControl.js";
import { EditorFieldRow } from "../editor-ui/fields/EditorFieldRow.js";
import type { EditorFieldDescriptor } from "../editor-ui/fields/EditorFieldDescriptor.js";
import { toDictionaryFieldControlProps } from "../editor-ui/fields/EditorFieldDescriptor.js";
import { ANIMATION_PRESET_FIELDS } from "../../domain/fields/animationPresetFields.js";
import { parsedObject } from "./recordJsonUtils.js";

interface AnimationPresetFieldContext {
  field: AppFieldDefinition;
  drafts: Record<string, string>;
  setJsonDraft: (column: string, value: JsonValue) => void;
  renderField: (field: AppFieldDefinition) => ReactNode;
}

function parametersDescriptor({
  value,
  onWrite,
}: {
  readonly value: unknown;
  readonly onWrite: (nextValue: unknown) => void;
}): EditorFieldDescriptor {
  return {
    kind: "field",
    field: ANIMATION_PRESET_FIELDS.parameters,
    displayValue: value,
    resolvedValue: value,
    localValue: value,
    state: "local",
    readonly: false,
    canInherit: false,
    canRestore: false,
    source: {
      kind: "custom",
      path: ["parameters_json"],
      recordType: "animation_presets",
    },
    actions: {
      write: onWrite,
    },
  };
}

export function renderAnimationPresetField({
  field,
  drafts,
  setJsonDraft,
  renderField,
}: AnimationPresetFieldContext) {
  if (field.column === "parameters_json") {
    const descriptor = parametersDescriptor({
      value: parsedObject(drafts.parameters_json ?? "{}"),
      onWrite: (nextValue) => setJsonDraft("parameters_json", nextValue as JsonValue),
    });
    return (
      <EditorFieldRow
        key={field.column}
        className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
        descriptor={descriptor}
      >
        <DictionaryFieldControl {...toDictionaryFieldControlProps(descriptor)} />
      </EditorFieldRow>
    );
  }
  return renderField(field);
}
