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
import {
  ModuleBehaviorFields,
  type StatusBarBehaviorItem,
} from "./ModuleBehaviorFields.js";
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

function statusBarItemsForBehavior(
  records: Record<string, AppRecord[]>,
): StatusBarBehaviorItem[] {
  const statusBar = records.status_bars?.[0];
  const rawConfig = statusBar?.config_json;
  const config =
    typeof rawConfig === "string"
      ? parsedObject(rawConfig)
      : rawConfig && typeof rawConfig === "object" && !Array.isArray(rawConfig)
        ? (rawConfig as Record<string, unknown>)
        : {};
  const rawItems = Array.isArray(config.items) ? config.items : [];
  const items: StatusBarBehaviorItem[] = [];
  for (const rawItem of rawItems) {
    const item =
      rawItem && typeof rawItem === "object" && !Array.isArray(rawItem)
        ? (rawItem as Record<string, unknown>)
        : {};
    const id = typeof item.id === "string" ? item.id : "";
    const label = typeof item.label === "string" ? item.label : id;
    const kind = typeof item.kind === "string" ? item.kind : "iconToken";
    const zone = typeof item.zone === "string" ? item.zone : "off";
    if (!id || !label) continue;
    if (zone === "off") continue;
    items.push({
      id,
      label,
      kind,
      value:
        typeof item.value === "string" ||
        typeof item.value === "number" ||
        typeof item.value === "boolean"
          ? item.value
          : undefined,
      charging: item.charging === true,
      zone,
    });
  }
  return items;
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
      />
    );
  }

  const contentField = fieldsByColumn.get("content_json");
  const behaviorField = fieldsByColumn.get("behavior_json");
  const contentGroups = ["header", "messages"].filter(
    (group) => group in parsedObject(drafts.content_json ?? "{}"),
  );
  const safeContentGroups = contentGroups.length
    ? contentGroups
    : ["header", "messages"];
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
          statusBarItems={statusBarItemsForBehavior(records)}
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
