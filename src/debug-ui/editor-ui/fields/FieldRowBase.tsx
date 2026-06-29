import type { ReactNode } from "react";
import type { ValueKind } from "../../../domain/value-system/index.js";
import type { EditorControlKind } from "../ValueKindControlRegistry.js";
import type {
  EditorFieldSourceKind,
  EditorFieldState,
} from "./EditorFieldDescriptor.js";

export interface FieldRowBaseProps {
  readonly label: ReactNode;
  readonly children: ReactNode;
  readonly className?: string;
  readonly description?: ReactNode;
  readonly error?: ReactNode;
  readonly restore?: ReactNode;
  readonly state?: EditorFieldState;
  readonly readonly?: boolean;
  readonly fieldId?: string;
  readonly valueKind?: ValueKind;
  readonly controlKind?: EditorControlKind;
  readonly sourceKind?: EditorFieldSourceKind;
}

function inspectorStateForFieldState(
  state: EditorFieldState | undefined,
): "default" | "override" | "invalid" {
  if (state === "invalid") return "invalid";
  if (state === "local") return "override";
  return "default";
}

export function FieldRowBase({
  label,
  children,
  className = "",
  description,
  error,
  restore,
  state = "default",
  readonly = false,
  fieldId,
  valueKind,
  controlKind,
  sourceKind,
}: FieldRowBaseProps) {
  const inspectorState = inspectorStateForFieldState(state);
  return (
    <div
      className={`inspector-field-row editor-field-row state-${inspectorState} ${className}`.trim()}
      data-field-id={fieldId}
      data-value-kind={valueKind}
      data-control-kind={controlKind}
      data-source-kind={sourceKind}
      data-editor-field-state={state}
      data-readonly={readonly ? "true" : undefined}
    >
      <div className="inspector-field-label">
        {label}
        {description ? (
          <small className="editor-field-row-description">{description}</small>
        ) : null}
      </div>
      <div className="inspector-field-control">
        {children}
        {error ? <strong className="editor-field-row-error">{error}</strong> : null}
      </div>
      {restore ? <div className="inspector-field-restore">{restore}</div> : null}
    </div>
  );
}
