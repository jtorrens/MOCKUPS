import { useEffect, useState } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import { buildJsonUiHints } from "../components/json-editor/uiHints.js";
import { ModuleInstanceEditor } from "./ModuleInstanceEditor.js";
import { ModuleBehaviorFields } from "./ModuleBehaviorFields.js";
import { titleForRecord } from "./RecordFieldRenderer.js";
import type { ScreenInstanceTab } from "./editorTabs.js";
import {
  chatContentGroupHasWarning,
  defaultGroupValue,
} from "./chat/chatContentModel.js";
import { ChatContentGroupEditor } from "./chat/ChatContentGroupEditor.js";
import {
  normalizeGroupValue,
  parsedObject,
} from "./recordJsonUtils.js";

interface ModuleNativeBridge {
  pickFile?: () => Promise<string[]>;
}

interface ModuleInstanceRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
  fieldsByColumn: Map<string, AppFieldDefinition>;
  drafts: Record<string, string>;
  activeTab: ScreenInstanceTab;
  activeContentTab: string;
  mediaRoot: string;
  nativeBridge: ModuleNativeBridge | undefined;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  setDrafts: (drafts: Record<string, string>) => void;
  setActiveTab: (tab: ScreenInstanceTab) => void;
  setActiveContentTab: (tab: string) => void;
}

function stringifyJson(value: unknown): string {
  return JSON.stringify(value ?? {}, null, 2);
}

export function ModuleInstanceRecordEditor({
  table,
  record,
  records,
  fieldsByColumn,
  drafts,
  activeTab,
  activeContentTab,
  mediaRoot,
  nativeBridge,
  relativePathFromRoot,
  setDrafts,
  setActiveTab,
  setActiveContentTab,
}: ModuleInstanceRecordEditorProps) {
  const [openContentItems, setOpenContentItems] = useState<Record<string, boolean>>(
    {},
  );

  useEffect(() => {
    setOpenContentItems({});
  }, [record.id]);

  function toggleExclusiveContentItem(
    groupKey: string,
    openKey: string,
    isOpen: boolean,
  ) {
    const scopedGroupPrefix = `${record.id}:${groupKey}:`;
    setOpenContentItems((current) => {
      const nextEntries = Object.entries(current).filter(
        ([key]) => !key.startsWith(scopedGroupPrefix),
      );
      return isOpen
        ? Object.fromEntries(nextEntries)
        : {
            ...Object.fromEntries(nextEntries),
            [openKey]: true,
          };
    });
  }

  function renderContentGroupEditor(
    field: AppFieldDefinition,
    groupKey: string,
    column = "content_json",
  ) {
    const root = parsedObject(drafts[column] ?? "{}");
    const groupValue = normalizeGroupValue(root[groupKey], defaultGroupValue(groupKey));
    const hints = buildJsonUiHints(table, field, record);

    function updateGroupValue(nextValue: JsonValue) {
      setDrafts({
        ...drafts,
        [column]: stringifyJson({
          ...root,
          [groupKey]: nextValue,
        }),
      });
    }

    return (
      <ChatContentGroupEditor
        actors={records.actors ?? []}
        actorTitleForRecord={(actor) => titleForRecord(actor, "display_name")}
        canBrowseMedia={Boolean(nativeBridge?.pickFile)}
        groupKey={groupKey}
        groupValue={groupValue}
        hints={hints}
        normalizeMediaPath={(filePath) => relativePathFromRoot(filePath, mediaRoot)}
        onBrowseMedia={async () => {
          const [filePath] =
            await (nativeBridge?.pickFile?.() ?? Promise.resolve([]));
          return filePath ? relativePathFromRoot(filePath, mediaRoot) : undefined;
        }}
        onGroupValueChange={updateGroupValue}
        onToggleItem={toggleExclusiveContentItem}
        openItems={openContentItems}
        recordId={record.id}
        root={root}
      />
    );
  }

  const contentField = fieldsByColumn.get("content_json");
  const behaviorField = fieldsByColumn.get("behavior_json");
  const contentGroups = ["participants", "header", "messages"].filter(
    (group) => group in parsedObject(drafts.content_json ?? "{}"),
  );
  const safeContentGroups = contentGroups.length
    ? contentGroups
    : ["participants", "header", "messages"];
  const resolvedActiveContentTab = safeContentGroups.includes(activeContentTab)
    ? activeContentTab
    : "";

  return (
    <ModuleInstanceEditor
      table={table}
      record={record}
      activeTab={activeTab}
      activeContentTab={resolvedActiveContentTab}
      contentFieldExists={Boolean(contentField)}
      behaviorFieldExists={Boolean(behaviorField)}
      contentGroups={safeContentGroups}
      contentGroupHasWarning={(group) =>
        chatContentGroupHasWarning({
          group,
          contentRoot: parsedObject(drafts.content_json ?? "{}"),
          actors: records.actors ?? [],
        })
      }
      renderContentGroup={(group) =>
        contentField ? renderContentGroupEditor(contentField, group) : null
      }
      renderBehaviorFields={() => (
        <ModuleBehaviorFields
          rawValue={drafts.behavior_json ?? "{}"}
          onRawChange={(nextRaw) =>
            setDrafts({
              ...drafts,
              behavior_json: nextRaw,
            })
          }
        />
      )}
      renderSubgroupAccordion={(group, activeGroup, warning, onToggle, children) => (
        <EditorSubsectionAccordion
          key={group}
          group={group}
          activeGroup={activeGroup}
          warning={warning}
          onToggle={onToggle}
        >
          {children}
        </EditorSubsectionAccordion>
      )}
      setActiveTab={setActiveTab}
      setContentTab={setActiveContentTab}
    />
  );
}
