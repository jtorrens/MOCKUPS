import { useState, type ReactNode } from "react";

interface ToggleInspectorLabelProps {
  label: ReactNode;
  technicalLabel?: string;
  title?: string;
}

export function ToggleInspectorLabel({
  label,
  technicalLabel,
  title,
}: ToggleInspectorLabelProps) {
  const [showTechnical, setShowTechnical] = useState(false);
  if (!technicalLabel) return <>{label}</>;
  return (
    <button
      type="button"
      className={`inspector-field-label-toggle ${
        showTechnical ? "is-technical" : ""
      }`}
      title={title ?? technicalLabel}
      onClick={() => setShowTechnical((current) => !current)}
    >
      {showTechnical ? technicalLabel : label}
    </button>
  );
}

interface InspectorFieldRowProps {
  label: ReactNode;
  meta?: ReactNode;
  control: ReactNode;
  restore?: ReactNode;
  className?: string;
  state?: "default" | "override" | "invalid";
  "data-field-id"?: string;
  "data-value-kind"?: string;
  "data-control-kind"?: string;
  "data-source-kind"?: string;
}

export function InspectorFieldRow({
  label,
  meta,
  control,
  restore,
  className = "",
  state = "default",
  "data-field-id": dataFieldId,
  "data-value-kind": dataValueKind,
  "data-control-kind": dataControlKind,
  "data-source-kind": dataSourceKind,
}: InspectorFieldRowProps) {
  return (
    <div
      className={`inspector-field-row state-${state} ${className}`.trim()}
      data-control-kind={dataControlKind}
      data-field-id={dataFieldId}
      data-source-kind={dataSourceKind}
      data-value-kind={dataValueKind}
    >
      <div className="inspector-field-label">{label}</div>
      {meta ? <div className="inspector-field-meta">{meta}</div> : null}
      <div className="inspector-field-control">{control}</div>
      {restore ? <div className="inspector-field-restore">{restore}</div> : null}
    </div>
  );
}

interface InspectorRestoreButtonProps {
  label: string;
  onClick: () => void;
}

export function InspectorRestoreButton({
  label,
  onClick,
}: InspectorRestoreButtonProps) {
  return (
    <button
      type="button"
      className="inspector-restore-button"
      aria-label={label}
      title="Restore"
      onClick={onClick}
    >
      ↺
    </button>
  );
}
