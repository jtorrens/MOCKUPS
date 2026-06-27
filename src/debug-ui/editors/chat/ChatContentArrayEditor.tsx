import { useState, type ReactNode } from "react";
import { AppModalDialog } from "../../components/AppModalDialog.js";
import { isJsonObject, type JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import { friendlyGroupLabel } from "../../components/json-editor/labels.js";
import { contentSummary } from "./chatContentModel.js";

interface ChatContentArrayEditorProps {
  groupKey: string;
  recordId?: unknown;
  value: JsonValue[];
  openItems: Record<string, boolean>;
  onToggleItem: (groupKey: string, openKey: string, isOpen: boolean) => void;
  onMoveItem: (index: number, direction: -1 | 1) => void;
  onDuplicateItem: (index: number) => void;
  onDeleteItem: (index: number) => void;
  onAddItem: () => void;
  itemHasAnimation?: (entryValue: JsonValue) => boolean;
  renderItemContent: (
    entryValue: JsonValue,
    index: number,
    isOpen: boolean,
  ) => ReactNode;
}

export function ChatContentArrayEditor({
  groupKey,
  recordId,
  value,
  openItems,
  onToggleItem,
  onMoveItem,
  onDuplicateItem,
  onDeleteItem,
  onAddItem,
  itemHasAnimation,
  renderItemContent,
}: ChatContentArrayEditorProps) {
  const [pendingDeleteIndex, setPendingDeleteIndex] = useState<number | null>(null);
  const singularLabel = friendlyGroupLabel(groupKey).replace(/s$/i, "");

  return (
    <div className="record-editor-content-array-editor">
      {pendingDeleteIndex !== null ? (
        <AppModalDialog
          eyebrow={friendlyGroupLabel(groupKey)}
          title={`Delete ${singularLabel} [${pendingDeleteIndex}]?`}
          message="This removes the content row and its nested values."
          confirmLabel="Delete"
          destructive
          onCancel={() => setPendingDeleteIndex(null)}
          onConfirm={() => {
            const index = pendingDeleteIndex;
            setPendingDeleteIndex(null);
            onDeleteItem(index);
          }}
        />
      ) : null}
      {value.map((entryValue, index) => {
        const stableId =
          isJsonObject(entryValue) && typeof entryValue.id === "string"
            ? entryValue.id
            : String(index);
        const openKey = `${recordId ?? "record"}:${groupKey}:${stableId}`;
        const isOpen = Boolean(openItems[openKey]);
        const hasAnimation = itemHasAnimation?.(entryValue) === true;
        return (
          <section
            className={`record-editor-content-item-card ${isOpen ? "open" : ""}`}
            key={stableId}
          >
            <div className="record-editor-content-item-topbar">
              <button
                type="button"
                className="record-editor-content-item-header"
                aria-expanded={isOpen}
                onClick={() => onToggleItem(groupKey, openKey, isOpen)}
              >
                <span className="record-editor-content-item-summary">
                  {itemHasAnimation ? (
                    <span
                      className={`record-editor-animation-indicator is-inline ${
                        hasAnimation ? "is-active" : ""
                      }`}
                      title={
                        hasAnimation
                          ? "This item has active animation"
                          : "This item supports animation"
                      }
                      aria-hidden="true"
                    >
                      {hasAnimation ? "◆" : "◇"}
                    </span>
                  ) : null}
                  [{index}] {contentSummary(entryValue, groupKey)}
                </span>
              </button>
              <div className="record-editor-content-actions">
                <button
                  type="button"
                  className="record-editor-content-action ui-icon-button"
                  disabled={index === 0}
                  onClick={() => onMoveItem(index, -1)}
                >
                  ↑
                </button>
                <button
                  type="button"
                  className="record-editor-content-action ui-icon-button"
                  disabled={index === value.length - 1}
                  onClick={() => onMoveItem(index, 1)}
                >
                  ↓
                </button>
                <button
                  type="button"
                  className="record-editor-content-action ui-icon-button"
                  onClick={() => onDuplicateItem(index)}
                >
                  ⧉
                </button>
                <button
                  type="button"
                  className="record-editor-content-action ui-icon-button"
                  onClick={() => setPendingDeleteIndex(index)}
                >
                  ⌫
                </button>
              </div>
              <button
                type="button"
                className="record-editor-content-chevron-button"
                aria-label={isOpen ? "Collapse item" : "Expand item"}
                aria-expanded={isOpen}
                onClick={() => onToggleItem(groupKey, openKey, isOpen)}
              >
                <span
                  className={`record-editor-content-chevron ${
                    isOpen ? "is-open" : ""
                  }`}
                  aria-hidden="true"
                >
                  ›
                </span>
              </button>
            </div>
            {renderItemContent(entryValue, index, isOpen)}
          </section>
        );
      })}
    <button
        type="button"
        className="record-editor-content-add-button"
        onClick={onAddItem}
      >
        Add {singularLabel}
      </button>
    </div>
  );
}
