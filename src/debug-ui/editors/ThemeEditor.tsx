import type { ReactNode } from "react";
import type { AppRecord, AppTableDefinition } from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";

type ThemeEditorTab = "" | "general" | "tokens" | "colors";

interface ThemeEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: ThemeEditorTab;
  tokensFieldExists: boolean;
  renderGeneral: () => ReactNode;
  renderTokens: () => ReactNode;
  renderColors: () => ReactNode;
  setActiveTab: (tab: ThemeEditorTab) => void;
}

export function ThemeEditor({
  table,
  record,
  activeTab,
  tokensFieldExists,
  renderGeneral,
  renderTokens,
  renderColors,
  setActiveTab,
}: ThemeEditorProps) {
  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Theme editor"
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
            onClick={() => setActiveTab(activeTab === "colors" ? "" : "colors")}
          >
            Colors
          </EditorSectionButton>
          {activeTab === "colors" && tokensFieldExists ? (
            <div className="editor-section-body">{renderColors()}</div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
