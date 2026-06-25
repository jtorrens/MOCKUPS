import { useEffect, useState } from "react";

interface DeferredNumberInputProps {
  ariaLabel?: string;
  className?: string;
  max?: number;
  min?: number;
  placeholder?: string;
  step?: number | string;
  value: number | "";
  onEmptyCommit?: () => void;
  onCommit: (nextValue: number) => void;
}

export function DeferredNumberInput({
  ariaLabel,
  className = "json-value-control",
  max,
  min,
  placeholder,
  step = "any",
  value,
  onEmptyCommit,
  onCommit,
}: DeferredNumberInputProps) {
  const [draft, setDraft] = useState(String(value));

  useEffect(() => {
    setDraft(String(value));
  }, [value]);

  function commit() {
    if (draft.trim() === "") {
      onEmptyCommit?.();
      return;
    }
    const nextValue = Number(draft);
    if (!Number.isFinite(nextValue)) {
      setDraft(String(value));
      return;
    }
    if (nextValue !== value) {
      onCommit(nextValue);
    }
  }

  return (
    <input
      aria-label={ariaLabel}
      className={className}
      max={max}
      min={min}
      placeholder={placeholder}
      step={step}
      type="number"
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
