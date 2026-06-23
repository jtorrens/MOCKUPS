import type { ReactNode } from "react";

interface InspectorFieldRowProps {
  label: ReactNode;
  meta?: ReactNode;
  control: ReactNode;
  restore?: ReactNode;
  className?: string;
  state?: "default" | "override" | "invalid";
}

export function InspectorFieldRow({
  label,
  meta,
  control,
  restore,
  className = "",
  state = "default",
}: InspectorFieldRowProps) {
  return (
    <div className={`inspector-field-row state-${state} ${className}`.trim()}>
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
