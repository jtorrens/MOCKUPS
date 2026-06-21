interface RawJsonEditorProps {
  rawText: string;
  testId?: string;
  disabled?: boolean;
  error?: string;
  onChange: (nextRawText: string) => void;
}

export function RawJsonEditor({
  rawText,
  testId,
  disabled,
  error,
  onChange,
}: RawJsonEditorProps) {
  return (
    <div className="raw-json-editor">
      <textarea
        data-testid={testId}
        disabled={disabled}
        spellCheck={false}
        value={rawText}
        onChange={(event) => onChange(event.target.value)}
      />
      {error ? <strong className="json-editor-error">{error}</strong> : null}
    </div>
  );
}
