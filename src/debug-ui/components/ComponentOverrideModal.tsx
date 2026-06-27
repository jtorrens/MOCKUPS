import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "./inspector/InspectorFieldRow.js";

export interface ComponentOverrideField {
  key: string;
  label: string;
  kind: "number" | "text" | "boolean" | "select";
  options?: Array<{ value: string; label: string }>;
}

interface ComponentOverrideModalProps {
  title: string;
  fields: ComponentOverrideField[];
  baseTokens: Record<string, unknown>;
  overrides: Record<string, unknown>;
  onClose: () => void;
  onSetOverride: (key: string, value: unknown) => void;
  onRestoreOverride: (key: string) => void;
}

function displayTokenValue(value: unknown) {
  if (typeof value === "boolean") return value ? "true" : "false";
  if (typeof value === "number" || typeof value === "string") return String(value);
  return "";
}

function hasOwnKey(value: Record<string, unknown>, key: string) {
  return Object.prototype.hasOwnProperty.call(value, key);
}

export function ComponentOverrideModal({
  title,
  fields,
  baseTokens,
  overrides,
  onClose,
  onSetOverride,
  onRestoreOverride,
}: ComponentOverrideModalProps) {
  function renderControl(field: ComponentOverrideField) {
    const baseValue = baseTokens[field.key];
    const hasOverride = hasOwnKey(overrides, field.key);
    const value = hasOverride ? overrides[field.key] : baseValue;

    if (field.kind === "boolean") {
      return (
        <input
          type="checkbox"
          checked={value === true}
          onChange={(event) =>
            onSetOverride(field.key, event.currentTarget.checked)
          }
        />
      );
    }

    if (field.kind === "select") {
      return (
        <select
          className="json-value-control"
          value={typeof value === "string" ? value : String(baseValue ?? "")}
          onChange={(event) => onSetOverride(field.key, event.currentTarget.value)}
        >
          {(field.options ?? []).map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      );
    }

    return (
      <DeferredTextInput
        ariaLabel={`${field.label} override`}
        value={displayTokenValue(value)}
        onCommit={(nextValue) => {
          if (field.kind === "number") {
            const parsed = Number(nextValue);
            if (!Number.isFinite(parsed)) return;
            onSetOverride(field.key, parsed);
            return;
          }
          onSetOverride(field.key, nextValue);
        }}
      />
    );
  }

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={onClose}>
      <section
        className="app-modal-card app-confirm-modal"
        role="dialog"
        aria-modal="true"
        aria-label={`${title} overrides`}
        style={{ maxWidth: 760, width: "min(760px, calc(100vw - 48px))" }}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="app-modal-heading">
          <div>
            <span className="eyebrow">Component override</span>
            <h2>{title}</h2>
          </div>
        </div>
        <p className="modal-help">
          Overrides stored in this module. Restore removes the local value and
          returns to the component default.
        </p>
        <div className="record-editor-field-stack record-editor-single-column">
          {fields.map((field) => {
            const hasOverride = hasOwnKey(overrides, field.key);
            const baseValue = baseTokens[field.key];
            return (
              <InspectorFieldRow
                key={field.key}
                label={
                  <span
                    style={{
                      color: hasOverride
                        ? "var(--editor-warning-color, #b45309)"
                        : undefined,
                    }}
                  >
                    {field.label}
                  </span>
                }
                control={
                  <div
                    style={{
                      alignItems: "center",
                      display: "grid",
                      gap: 8,
                      gridTemplateColumns: "minmax(0, 1fr) auto",
                    }}
                  >
                    <div>
                      {renderControl(field)}
                      <small
                        style={{
                          display: "block",
                          marginTop: 4,
                          opacity: 0.7,
                        }}
                      >
                        Default: {displayTokenValue(baseValue)}
                      </small>
                    </div>
                    {hasOverride ? (
                      <InspectorRestoreButton
                        label={`Restore ${field.label}`}
                        onClick={() => onRestoreOverride(field.key)}
                      />
                    ) : null}
                  </div>
                }
              />
            );
          })}
        </div>
        <footer className="app-modal-actions">
          <button type="button" className="app-modal-button" onClick={onClose}>
            Close
          </button>
        </footer>
      </section>
    </div>
  );
}
