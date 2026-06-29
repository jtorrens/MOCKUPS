import { useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";

interface AppModalDialogProps {
  title: string;
  eyebrow?: string;
  message?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  hideConfirm?: boolean;
  className?: string;
  prompt?: {
    label: string;
    initialValue: string;
    placeholder?: string;
  };
  onCancel: () => void;
  onConfirm: (value?: string) => void;
}

export function AppModalDialog({
  title,
  eyebrow,
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  destructive = false,
  hideConfirm = false,
  className = "",
  prompt,
  onCancel,
  onConfirm,
}: AppModalDialogProps) {
  const [value, setValue] = useState(prompt?.initialValue ?? "");
  const inputRef = useRef<HTMLInputElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    setValue(prompt?.initialValue ?? "");
  }, [prompt?.initialValue]);

  useEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      if (prompt) {
        inputRef.current?.focus();
        inputRef.current?.select();
        return;
      }
      cancelRef.current?.focus();
    });
    return () => window.cancelAnimationFrame(frame);
  }, [prompt]);

  function submit() {
    onConfirm(prompt ? value : undefined);
  }

  return createPortal(
    <div
      className="modal-backdrop"
      role="presentation"
      onMouseDown={onCancel}
    >
      <section
        className={`app-modal-card app-confirm-modal ${className}`.trim()}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="app-modal-heading">
          <div>
            {eyebrow ? <span className="eyebrow">{eyebrow}</span> : null}
            <h2>{title}</h2>
          </div>
        </div>
        {message ? <div className="modal-help">{message}</div> : null}
        {prompt ? (
          <label className="app-modal-form-field">
            <span>{prompt.label}</span>
            <input
              ref={inputRef}
              className="app-modal-input"
              value={value}
              placeholder={prompt.placeholder}
              onChange={(event) => setValue(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") submit();
                if (event.key === "Escape") onCancel();
              }}
            />
          </label>
        ) : null}
        <footer className="app-modal-actions">
          <button
            ref={cancelRef}
            type="button"
            className="app-modal-button"
            onClick={onCancel}
          >
            {cancelLabel}
          </button>
          {hideConfirm ? null : (
            <button
              type="button"
              className={`app-modal-button ${destructive ? "danger" : "primary"}`}
              onClick={submit}
            >
              {confirmLabel}
            </button>
          )}
        </footer>
      </section>
    </div>,
    document.body,
  );
}
