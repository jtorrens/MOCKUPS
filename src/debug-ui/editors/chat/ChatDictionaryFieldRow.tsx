import type { ReactNode } from "react";
import type { FieldDefinition } from "../../../domain/value-system/index.js";
import {
  DICTIONARY_FIELD_CLASS,
  DictionaryFieldControl,
} from "../../editor-ui/DictionaryFieldControl.js";
import { EditorFieldRow } from "../../editor-ui/fields/EditorFieldRow.js";
import {
  toDictionaryFieldControlProps,
  type EditorFieldDescriptor,
} from "../../editor-ui/fields/EditorFieldDescriptor.js";
import type {
  DictionaryFileBrowser,
  DictionarySelectOptions,
} from "../../editor-ui/DictionaryFieldControl.js";
import type { IconThemeLikeRecord } from "../../editor-ui/IconGlyphPreview.js";

interface ChatDictionaryFieldRowProps {
  readonly field: FieldDefinition;
  readonly value: unknown;
  readonly className?: string;
  readonly selectOptions?: DictionarySelectOptions;
  readonly fileBrowser?: DictionaryFileBrowser;
  readonly mediaRoot?: string;
  readonly iconThemeRecords?: readonly IconThemeLikeRecord[];
  readonly labelOverride?: ReactNode;
  readonly onChange: (nextValue: unknown) => void;
}

export function ChatDictionaryFieldRow({
  field,
  value,
  className = "",
  selectOptions,
  fileBrowser,
  mediaRoot,
  iconThemeRecords,
  labelOverride,
  onChange,
}: ChatDictionaryFieldRowProps) {
  const resolvedSelectOptions =
    selectOptions ??
    (field.kind === "enum" && field.ui?.options?.length
      ? enumSelectOptions(field.ui.options)
      : undefined);
  const descriptor: EditorFieldDescriptor = {
    kind: "field",
    field,
    localValue: value,
    displayValue: value,
    resolvedValue: value,
    state: "default",
    readonly: false,
    canInherit: false,
    canRestore: false,
    source: {
      kind: "module-instance-content",
      path: field.id.split("."),
    },
    actions: {
      write: onChange,
    },
    selectOptions: resolvedSelectOptions,
  };

  return (
    <EditorFieldRow
      className={`record-editor-content-field-row ${DICTIONARY_FIELD_CLASS} ${className}`.trim()}
      descriptor={descriptor}
      labelOverride={labelOverride}
    >
      <DictionaryFieldControl
        {...toDictionaryFieldControlProps(descriptor, {
          fileBrowser,
          mediaRoot,
          iconThemeRecords,
        })}
      />
    </EditorFieldRow>
  );
}

export function enumSelectOptions(
  options: readonly string[],
): DictionarySelectOptions {
  return {
    options: options.map((option) => ({
      value: option,
      label: option,
    })),
  };
}
