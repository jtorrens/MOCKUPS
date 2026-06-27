import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import {
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import {
  InspectorFieldRow,
} from "../components/inspector/InspectorFieldRow.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { parsedObject } from "./recordJsonUtils.js";

type ComponentClassTab = "" | "general";

interface ComponentClassRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: ComponentClassTab;
  drafts: Record<string, string>;
  paletteCatalog?: PaletteColorCatalog;
  renderField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: ComponentClassTab) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function booleanValue(value: unknown, fallback = false) {
  return typeof value === "boolean" ? value : fallback;
}

function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function borderColorValue(
  tokens: Record<string, JsonValue>,
  mode: "light" | "dark",
) {
  const borderColor = isJsonObject(tokens.borderColor)
    ? tokens.borderColor
    : {};
  return stringValue(
    borderColor[mode],
    mode === "dark" ? "gray_020" : "gray_100",
  );
}

function setTokenValue(
  tokens: Record<string, JsonValue>,
  key: string,
  value: JsonValue,
) {
  return {
    ...tokens,
    [key]: value,
  };
}

function setBorderColorValue(
  tokens: Record<string, JsonValue>,
  mode: "light" | "dark",
  value: string,
) {
  const borderColor = isJsonObject(tokens.borderColor)
    ? tokens.borderColor
    : {};
  return {
    ...tokens,
    borderColor: {
      ...borderColor,
      [mode]: value,
    },
  };
}

export function ComponentClassRecordEditor({
  table,
  record,
  activeTab,
  drafts,
  paletteCatalog,
  renderField,
  setActiveTab,
  setJsonDraft,
}: ComponentClassRecordEditorProps) {
  const tokens = parsedObject(drafts.tokens_json ?? "{}") as Record<
    string,
    JsonValue
  >;
  const nameField = table.fields.find((field) => field.column === "name");

  function updateTokens(nextTokens: Record<string, JsonValue>) {
    setJsonDraft("tokens_json", {
      ...tokens,
      ...nextTokens,
      schemaVersion: numberValue(nextTokens.schemaVersion, 1),
      componentType: stringValue(nextTokens.componentType, "avatar"),
    });
  }

  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Component Class Editor"
        title={String(record[table.titleColumn] ?? record.id)}
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "general"}
            onClick={() =>
              setActiveTab(activeTab === "general" ? "" : "general")
            }
          >
            General
          </EditorSectionButton>
          {activeTab === "general" ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {nameField ? renderField(nameField) : null}
              <InspectorFieldRow
                label="Corner radius"
                control={
                  <DeferredTextInput
                    ariaLabel="Corner radius"
                    value={String(numberValue(tokens.cornerRadius, 12))}
                    onCommit={(nextValue) => {
                      const parsed = Number(nextValue);
                      if (!Number.isFinite(parsed)) return;
                      updateTokens(setTokenValue(tokens, "cornerRadius", parsed));
                    }}
                  />
                }
              />
              <InspectorFieldRow
                label="Border width"
                control={
                  <DeferredTextInput
                    ariaLabel="Border width"
                    value={String(numberValue(tokens.borderWidth, 0))}
                    onCommit={(nextValue) => {
                      const parsed = Number(nextValue);
                      if (!Number.isFinite(parsed)) return;
                      updateTokens(setTokenValue(tokens, "borderWidth", parsed));
                    }}
                  />
                }
              />
              <InspectorFieldRow
                label="Border color light"
                control={
                  <ColorValueEditor
                    label="Border color light"
                    value={borderColorValue(tokens, "light")}
                    paletteCatalog={paletteCatalog}
                    onChange={(nextValue) =>
                      updateTokens(setBorderColorValue(tokens, "light", nextValue))
                    }
                  />
                }
              />
              <InspectorFieldRow
                label="Border color dark"
                control={
                  <ColorValueEditor
                    label="Border color dark"
                    value={borderColorValue(tokens, "dark")}
                    paletteCatalog={paletteCatalog}
                    onChange={(nextValue) =>
                      updateTokens(setBorderColorValue(tokens, "dark", nextValue))
                    }
                  />
                }
              />
              <InspectorFieldRow
                label="Shadow enabled"
                control={
                  <label className="json-checkbox">
                    <input
                      type="checkbox"
                      checked={booleanValue(tokens.shadowEnabled)}
                      onChange={(event) =>
                        updateTokens(
                          setTokenValue(
                            tokens,
                            "shadowEnabled",
                            event.currentTarget.checked,
                          ),
                        )
                      }
                    />
                    {booleanValue(tokens.shadowEnabled) ? "true" : "false"}
                  </label>
                }
              />
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
