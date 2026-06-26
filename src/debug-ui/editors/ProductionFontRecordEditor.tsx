import { useState } from "react";
import type { ReactNode } from "react";
import {
  importProductionFont,
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";

interface NativeBridge {
  pickFile?: () => Promise<string[]>;
}

interface ProductionFontRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: "" | "general";
  nativeBridge: NativeBridge | undefined;
  productionId: string;
  renderGenericField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: "" | "general") => void;
  onRecordsChanged: (records: AppRecord[]) => void;
  onRecordSaved: (record: AppRecord) => void;
}

export function ProductionFontRecordEditor({
  table,
  record,
  activeTab,
  nativeBridge,
  productionId,
  renderGenericField,
  setActiveTab,
  onRecordsChanged,
  onRecordSaved,
}: ProductionFontRecordEditorProps) {
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState("");
  const fields = table.fields.filter(
    (field) => !["id", "production_id", "metadata_json"].includes(field.column),
  );

  async function chooseFontFile() {
    setError("");
    const [sourcePath] = await (nativeBridge?.pickFile?.() ?? Promise.resolve([]));
    if (!sourcePath) return;
    setImporting(true);
    try {
      const result = await importProductionFont({
        productionId,
        recordId: record.id,
        sourcePath,
      });
      onRecordsChanged(result.state.records.production_fonts ?? []);
      onRecordSaved(result.record);
    } catch (importError) {
      setError(importError instanceof Error ? importError.message : String(importError));
    } finally {
      setImporting(false);
    }
  }

  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Production Font Editor"
        title={`${String(record.family ?? "Font")} ${String(record.style ?? "")}`.trim()}
        summary="Approved font copied into the production root"
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
              <button
                type="button"
                className="secondary-button"
                disabled={!nativeBridge?.pickFile || importing}
                onClick={chooseFontFile}
              >
                {importing ? "Importing…" : "Import font file…"}
              </button>
              {error ? <p className="record-editor-field-error">{error}</p> : null}
              {fields.map((field) => renderGenericField(field))}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
