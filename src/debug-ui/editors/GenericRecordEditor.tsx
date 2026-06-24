import type { ReactNode } from "react";
import type { AppFieldDefinition, AppRecord, AppTableDefinition } from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";

interface GenericRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: "" | "general";
  renderGenericField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: "" | "general") => void;
  showGeneralWarning?: boolean;
}

export function GenericRecordEditor({
  table,
  record,
  activeTab,
  renderGenericField,
  setActiveTab,
  showGeneralWarning,
}: GenericRecordEditorProps) {
  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow={table.id === "productions" ? "Production Editor" : "Record editor"}
        title={String(record[table.titleColumn] ?? record.id)}
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "general"}
            warning={showGeneralWarning}
            onClick={() => setActiveTab(activeTab === "general" ? "" : "general")}
          >
            General
          </EditorSectionButton>
          {activeTab === "general" ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {table.fields.map((field) => renderGenericField(field))}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
