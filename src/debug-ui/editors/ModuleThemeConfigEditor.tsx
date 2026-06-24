import type { ReactNode } from "react";
import type { AppRecord, AppTableDefinition } from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";

type ModuleThemeTab = "" | "design" | "colors" | "settings";

interface ModuleThemeConfigEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: ModuleThemeTab;
  designFieldExists: boolean;
  colorsFieldExists: boolean;
  designWarning: boolean;
  colorsWarning: boolean;
  renderDesign: () => ReactNode;
  renderColors: () => ReactNode;
  renderSettings: () => ReactNode;
  setActiveTab: (tab: ModuleThemeTab) => void;
}

export function ModuleThemeConfigEditor({
  table,
  record,
  activeTab,
  designFieldExists,
  colorsFieldExists,
  designWarning,
  colorsWarning,
  renderDesign,
  renderColors,
  renderSettings,
  setActiveTab,
}: ModuleThemeConfigEditorProps) {
  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Screen module editor"
        title={String(record[table.titleColumn] ?? record.id)}
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "design"}
            warning={designWarning}
            onClick={() => setActiveTab(activeTab === "design" ? "" : "design")}
          >
            Design
          </EditorSectionButton>
          {activeTab === "design" && designFieldExists ? (
            <div className="editor-section-body record-editor-nested-stack">
              {renderDesign()}
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
          {activeTab === "colors" && colorsFieldExists ? (
            <div className="editor-section-body">{renderColors()}</div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "settings"}
            onClick={() =>
              setActiveTab(activeTab === "settings" ? "" : "settings")
            }
          >
            Settings
          </EditorSectionButton>
          {activeTab === "settings" ? (
            <div className="editor-section-body record-editor-nested-stack">
              {renderSettings()}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
