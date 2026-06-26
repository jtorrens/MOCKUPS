import { useEffect, useState } from "react";

interface DeferredTextInputProps {
  ariaLabel?: string;
  className?: string;
  disabled?: boolean;
  multiline?: boolean;
  placeholder?: string;
  value: string;
  onCommit: (nextValue: string) => void;
}

export function DeferredTextInput({
  ariaLabel,
  className = "json-value-control",
  disabled = false,
  multiline = false,
  placeholder,
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
        aria-label={ariaLabel}
        className={className}
        placeholder={placeholder}
        value={draft}
        rows={4}
        disabled={disabled}
        onBlur={commit}
        onChange={(event) => setDraft(event.target.value)}
      />
    );
  }

  return (
    <input
      aria-label={ariaLabel}
      className={className}
      placeholder={placeholder}
      value={draft}
      disabled={disabled}
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
