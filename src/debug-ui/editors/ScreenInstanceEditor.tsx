import type { ReactNode } from "react";
import type { AppRecord, AppTableDefinition } from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import type { ScreenInstanceTab } from "./editorTabs.js";

interface ScreenInstanceEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: ScreenInstanceTab;
  transformFieldExists: boolean;
  deviceStateFieldExists: boolean;
  renderGeneralFields: () => ReactNode;
  renderTransformFields: () => ReactNode;
  renderTransitionFields: () => ReactNode;
  renderDeviceStateFields: () => ReactNode;
  setActiveTab: (tab: ScreenInstanceTab) => void;
}

export function ScreenInstanceEditor({
  table,
  record,
  activeTab,
  transformFieldExists,
  deviceStateFieldExists,
  renderGeneralFields,
  renderTransformFields,
  renderTransitionFields,
  renderDeviceStateFields,
  setActiveTab,
}: ScreenInstanceEditorProps) {
  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Screen instance editor"
        title={String(record[table.titleColumn] ?? record.id)}
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "general"}
            onClick={() => setActiveTab(activeTab === "general" ? "" : "general")}
          >
            Generales
          </EditorSectionButton>
          {activeTab === "general" ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {renderGeneralFields()}
            </div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "transform"}
            onClick={() =>
              setActiveTab(activeTab === "transform" ? "" : "transform")
            }
          >
            Transform
          </EditorSectionButton>
          {activeTab === "transform" && transformFieldExists ? (
            <div className="editor-section-body record-editor-field-stack record-editor-single-column record-editor-json-stack">
              {renderTransformFields()}
            </div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "transition"}
            onClick={() =>
              setActiveTab(activeTab === "transition" ? "" : "transition")
            }
          >
            Transition
          </EditorSectionButton>
          {activeTab === "transition" ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {renderTransitionFields()}
            </div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "deviceState"}
            onClick={() =>
              setActiveTab(activeTab === "deviceState" ? "" : "deviceState")
            }
          >
            Device State
          </EditorSectionButton>
          {activeTab === "deviceState" && deviceStateFieldExists ? (
            <div className="editor-section-body record-editor-field-stack record-editor-single-column record-editor-json-stack">
              {renderDeviceStateFields()}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
