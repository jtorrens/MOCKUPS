interface JsonEditorPanelProps {
  id: string;
  title: string;
  hint: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
}

export function JsonEditorPanel({
  id,
  title,
  hint,
  value,
  onChange,
  error,
}: JsonEditorPanelProps) {
  return (
    <section className={`panel json-panel ${error ? "has-error" : ""}`}>
      <div className="panel-heading compact">
        <div>
          <span className="eyebrow">{hint}</span>
          <h3>{title}</h3>
        </div>
        <span className={`validation-chip ${error ? "invalid" : ""}`}>
          {error ? "Invalid" : "JSON"}
        </span>
      </div>
      <textarea
        aria-label={title}
        data-testid={`editor-${id}`}
        spellCheck={false}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      {error ? <pre className="field-error">{error}</pre> : null}
    </section>
  );
}
