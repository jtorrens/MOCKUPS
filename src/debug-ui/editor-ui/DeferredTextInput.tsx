import { useEffect, useState } from "react";

interface DeferredTextInputProps {
  className?: string;
  multiline?: boolean;
  value: string;
  onCommit: (nextValue: string) => void;
}

export function DeferredTextInput({
  className = "json-value-control",
  multiline = false,
  value,
  onCommit,
}: DeferredTextInputProps) {
  const [draft, setDraft] = useState(value);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  function commit() {
    if (draft !== value) {
      onCommit(draft);
    }
  }

  if (multiline) {
    return (
      <textarea
        className={className}
        value={draft}
        rows={4}
        onBlur={commit}
        onChange={(event) => setDraft(event.target.value)}
      />
    );
  }

  return (
    <input
      className={className}
      value={draft}
      onBlur={commit}
      onChange={(event) => setDraft(event.target.value)}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.currentTarget.blur();
        }
      }}
    />
  );
}
