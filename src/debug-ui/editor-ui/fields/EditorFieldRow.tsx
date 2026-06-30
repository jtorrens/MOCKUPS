import type { ReactNode } from "react";
import {
  InspectorRestoreButton,
  ToggleInspectorLabel,
} from "../../components/inspector/InspectorFieldRow.js";
import { controlDefinitionForField } from "../ValueKindControlRegistry.js";
import type { EditorFieldDescriptor } from "./EditorFieldDescriptor.js";
import { editorFieldLabel } from "./EditorFieldDescriptor.js";
import { FieldRowBase } from "./FieldRowBase.js";

export interface EditorFieldRowProps {
  readonly descriptor: EditorFieldDescriptor;
  readonly children: ReactNode;
  readonly className?: string;
  readonly labelOverride?: ReactNode;
}

export function EditorFieldRow({
  descriptor,
  children,
  className = "",
  labelOverride,
}: EditorFieldRowProps) {
  const label = editorFieldLabel(descriptor);
  const controlDefinition = controlDefinitionForField(descriptor.field);
  const restore =
    descriptor.canRestore && descriptor.actions.restore ? (
      <InspectorRestoreButton
        label={`Restore ${label}`}
        onClick={descriptor.actions.restore}
      />
    ) : undefined;

  return (
    <FieldRowBase
      className={`${className} ${
        descriptor.field.ui?.multiline === true ? "is-multiline" : ""
      }`.trim()}
      controlKind={controlDefinition.control}
      description={descriptor.field.ui?.description}
      error={descriptor.validation?.message}
      fieldId={descriptor.field.id}
      label={
        labelOverride ?? (
          <ToggleInspectorLabel label={label} technicalLabel={descriptor.field.id} />
        )
      }
      readonly={descriptor.readonly}
      restore={restore}
      sourceKind={descriptor.source.kind}
      state={descriptor.state}
      valueKind={descriptor.field.kind}
    >
      {children}
    </FieldRowBase>
  );
}
