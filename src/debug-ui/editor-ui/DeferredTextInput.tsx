import { useEffect, useState } from "react";

interface DeferredTextInputProps {
  className?: string;
  value: string;
  onCommit: (nextValue: string) => void;
}

export function DeferredTextInput({
  className = "json-value-control",
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
