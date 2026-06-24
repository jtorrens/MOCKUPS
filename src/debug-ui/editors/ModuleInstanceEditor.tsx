import type { ReactNode } from "react";
import type { AppRecord, AppTableDefinition } from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import type { ScreenInstanceTab } from "./editorTabs.js";

interface ModuleInstanceEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: ScreenInstanceTab;
  activeContentTab: string;
  contentFieldExists: boolean;
  behaviorFieldExists: boolean;
  contentGroups: string[];
  contentGroupHasWarning: (group: string) => boolean;
  renderContentGroup: (group: string) => ReactNode;
  renderBehaviorFields: () => ReactNode;
  renderSubgroupAccordion: (
    group: string,
    activeGroup: string,
    warning: boolean,
    onToggle: (group: string) => void,
    children: ReactNode,
  ) => ReactNode;
  setActiveTab: (tab: ScreenInstanceTab) => void;
  setContentTab: (tab: string) => void;
}

export function ModuleInstanceEditor({
  table,
  record,
  activeTab,
  activeContentTab,
  contentFieldExists,
  behaviorFieldExists,
  contentGroups,
  contentGroupHasWarning,
  renderContentGroup,
  renderBehaviorFields,
  renderSubgroupAccordion,
  setActiveTab,
  setContentTab,
}: ModuleInstanceEditorProps) {
  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Module instance editor"
        title={String(record[table.titleColumn] ?? record.id)}
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "content"}
            onClick={() => setActiveTab(activeTab === "content" ? "" : "content")}
          >
            Module Content
          </EditorSectionButton>
          {activeTab === "content" && contentFieldExists ? (
            <div className="editor-section-body record-editor-nested-stack">
              {contentGroups.map((group) =>
                renderSubgroupAccordion(
                  group,
                  activeContentTab,
                  contentGroupHasWarning(group),
                  setContentTab,
                  renderContentGroup(group),
                ),
              )}
            </div>
          ) : null}
        </EditorSectionCard>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "behavior"}
            onClick={() => setActiveTab(activeTab === "behavior" ? "" : "behavior")}
          >
            Behavior
          </EditorSectionButton>
          {activeTab === "behavior" && behaviorFieldExists ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {renderBehaviorFields()}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
