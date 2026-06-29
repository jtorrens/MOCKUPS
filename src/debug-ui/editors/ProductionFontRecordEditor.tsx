import type { ReactNode } from "react";
import {
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { parsedObject } from "./recordJsonUtils.js";

interface ProductionFontRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: "" | "general";
  renderGenericField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: "" | "general") => void;
}

export function ProductionFontRecordEditor({
  table,
  record,
  activeTab,
  renderGenericField,
  setActiveTab,
}: ProductionFontRecordEditorProps) {
  const fields = table.fields.filter(
    (field) =>
      ![
        "id",
        "production_id",
        "files_json",
        "source_path",
        "metadata_json",
      ].includes(field.column),
  );
  const filesRoot = parsedObject(
    typeof record.files_json === "string"
      ? record.files_json
      : JSON.stringify(record.files_json ?? {}),
  );
  const fontFiles = Array.isArray(filesRoot.files)
    ? filesRoot.files
        .filter((file): file is Record<string, unknown> =>
          typeof file === "object" && file !== null && !Array.isArray(file),
        )
        .map((file) => ({
          style: String(file.style ?? "Regular"),
          filePath: String(file.filePath ?? ""),
        }))
    : [];

  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Production Font Editor"
        title={String(record.family ?? "Font family")}
        summary="Approved font family copied into the production root"
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "general"}
            onClick={() => setActiveTab(activeTab === "general" ? "" : "general")}
          >
            General
          </EditorSectionButton>
          {activeTab === "general" ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {fields.map((field) => renderGenericField(field))}
              <div className="production-font-file-list" aria-label="Font files">
                <span className="production-font-file-list-title">Font files</span>
                {fontFiles.length ? (
                  fontFiles.map((file) => (
                    <div className="production-font-file-row" key={`${file.style}:${file.filePath}`}>
                      <strong>{file.style}</strong>
                      <span>{file.filePath}</span>
                    </div>
                  ))
                ) : (
                  <div className="production-font-file-row">
                    <strong>No files</strong>
                    <span>Import a font family to populate this list.</span>
                  </div>
                )}
              </div>
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
