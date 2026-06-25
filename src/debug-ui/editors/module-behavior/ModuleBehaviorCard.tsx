import type { ReactNode } from "react";

interface ModuleBehaviorCardProps {
  title: string;
  summary?: string;
  icon?: string;
  open: boolean;
  onToggle: () => void;
  children: ReactNode;
}

export function ModuleBehaviorCard({
  title,
  summary,
  icon = "⚙",
  open,
  onToggle,
  children,
}: ModuleBehaviorCardProps) {
  return (
    <section className="module-behavior-card">
      <button
        type="button"
        className="module-behavior-card-trigger"
        aria-expanded={open}
        onClick={onToggle}
      >
        <span className="module-behavior-card-icon ui-glyph" aria-hidden="true">
          {icon}
        </span>
        <span className="module-behavior-card-copy">
          <strong>{title}</strong>
          {summary ? <small>{summary}</small> : null}
        </span>
      </button>
      {open ? <div className="module-behavior-card-body">{children}</div> : null}
    </section>
  );
}
