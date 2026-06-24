import type { ReactNode } from "react";
import type { AppRecord, AppTableDefinition } from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";

type AppEditorTab = "" | "general" | "tokens" | "colors" | "notes";

interface AppEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: AppEditorTab;
  tokensFieldExists: boolean;
  notesFieldExists: boolean;
  tokensWarning: boolean;
  colorsWarning: boolean;
  renderGeneral: () => ReactNode;
  renderTokens: () => ReactNode;
  renderColors: () => ReactNode;
  renderNotes: () => ReactNode;
  setActiveTab: (tab: AppEditorTab) => void;
}

export function AppEditor({
  table,
  record,
  activeTab,
  tokensFieldExists,
  notesFieldExists,
  tokensWarning,
  colorsWarning,
  renderGeneral,
  renderTokens,
  renderColors,
  renderNotes,
  setActiveTab,
}: AppEditorProps) {
  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="App editor"
        title={String(record[table.titleColumn] ?? record.id)}
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
              {renderGeneral()}
            </div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "tokens"}
            warning={tokensWarning}
            onClick={() => setActiveTab(activeTab === "tokens" ? "" : "tokens")}
          >
            Tokens
          </EditorSectionButton>
          {activeTab === "tokens" && tokensFieldExists ? (
            <div className="editor-section-body record-editor-nested-stack">
              {renderTokens()}
            </div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "colors"}
            warning={colorsWarning}
            onClick={() => setActiveTab(activeTab === "colors" ? "" : "colors")}
          >
            Colors
          </EditorSectionButton>
          {activeTab === "colors" ? (
            <div className="editor-section-body">{renderColors()}</div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "notes"}
            onClick={() => setActiveTab(activeTab === "notes" ? "" : "notes")}
          >
            Notes
          </EditorSectionButton>
          {activeTab === "notes" && notesFieldExists ? (
            <div className="editor-section-body record-editor-field-stack record-editor-single-column">
              {renderNotes()}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
