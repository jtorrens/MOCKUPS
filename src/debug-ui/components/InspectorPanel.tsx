interface InspectorPanelProps {
  title: string;
  eyebrow: string;
  value: unknown;
  testId: string;
}

export function InspectorPanel({
  title,
  eyebrow,
  value,
  testId,
}: InspectorPanelProps) {
  return (
    <section className="panel inspector-panel">
      <div className="panel-heading compact">
        <div>
          <span className="eyebrow">{eyebrow}</span>
          <h3>{title}</h3>
        </div>
        <span className="read-only-chip">Read only</span>
      </div>
      <pre data-testid={testId}>
        {JSON.stringify(value ?? null, null, 2)}
      </pre>
    </section>
  );
}
