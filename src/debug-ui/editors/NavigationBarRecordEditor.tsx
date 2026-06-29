import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import {
  isJsonObject,
  setAtPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
  type DictionarySelectOptions,
} from "../editor-ui/DictionaryFieldControl.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { EditorFieldRow } from "../editor-ui/fields/EditorFieldRow.js";
import { createJsonFieldDescriptor } from "../editor-ui/fields/createJsonFieldDescriptor.js";
import { toDictionaryFieldControlProps } from "../editor-ui/fields/EditorFieldDescriptor.js";
import {
  NAVIGATION_BAR_CONFIG_BINDINGS,
  NAVIGATION_BAR_FIELDS,
} from "../../domain/fields/navigationBarFields.js";
import { parsedObject } from "./recordJsonUtils.js";

type NavigationBarTab = "" | "general" | "config";
type NavigationBarItemKind =
  | "generatedBack"
  | "generatedHome"
  | "generatedRecents";
type NavigationBarZone = "off" | "left" | "center" | "right";

interface NavigationBarRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: NavigationBarTab;
  drafts: Record<string, string>;
  renderField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: NavigationBarTab) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

interface NavigationBarItem {
  id: string;
  label: string;
  kind: NavigationBarItemKind;
  zone: NavigationBarZone;
  order: number;
}

function defaultNavigationBarItems(): NavigationBarItem[] {
  return [
    {
      id: "back",
      label: "Back",
      kind: "generatedBack",
      zone: "left",
      order: 10,
    },
    {
      id: "home",
      label: "Home",
      kind: "generatedHome",
      zone: "center",
      order: 10,
    },
    {
      id: "recents",
      label: "Recents",
      kind: "generatedRecents",
      zone: "right",
      order: 10,
    },
  ];
}

function defaultNavigationBarConfig(): Record<string, JsonValue> {
  return {
    schemaVersion: 1,
    layout: {
      height: 34,
      itemSize: 18,
      sidePadding: 40,
      strokeWidth: 2,
      cornerRadius: 3,
      filled: false,
    },
    items: defaultNavigationBarItems() as unknown as JsonValue,
  };
}

function numberValue(value: JsonValue | undefined, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringValue(value: JsonValue | undefined, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function configRoot(raw: string) {
  const parsed = parsedObject(raw) as Record<string, JsonValue>;
  const parsedLayout = isJsonObject(parsed.layout)
    ? (parsed.layout as Record<string, JsonValue>)
    : {};
  const merged = {
    ...defaultNavigationBarConfig(),
    ...parsed,
    layout: {
      ...(defaultNavigationBarConfig().layout as Record<string, JsonValue>),
      ...parsedLayout,
    },
  } as Record<string, JsonValue>;
  if (!Array.isArray(merged.items)) {
    merged.items = defaultNavigationBarItems() as unknown as JsonValue;
  }
  return merged;
}

function navigationBarItems(root: Record<string, JsonValue>) {
  const rawItems = Array.isArray(root.items) ? root.items : [];
  return rawItems.map((rawItem, index) => {
    const item = isJsonObject(rawItem) ? rawItem : {};
    const fallback =
      defaultNavigationBarItems()[index] ?? defaultNavigationBarItems()[0];
    const kind =
      item.kind === "generatedBack" ||
      item.kind === "generatedHome" ||
      item.kind === "generatedRecents"
        ? item.kind
        : fallback.kind;
    const zone =
      item.zone === "off" ||
      item.zone === "left" ||
      item.zone === "center" ||
      item.zone === "right"
        ? item.zone
        : fallback.zone;
    return {
      id: stringValue(item.id, fallback.id),
      label: stringValue(item.label, fallback.label),
      kind,
      zone,
      order: numberValue(item.order, fallback.order),
    } satisfies NavigationBarItem;
  });
}

function updateItem(
  items: NavigationBarItem[],
  index: number,
  patch: Partial<NavigationBarItem>,
) {
  return items.map((item, itemIndex) =>
    itemIndex === index ? { ...item, ...patch } : item,
  ) as unknown as JsonValue;
}

export function NavigationBarRecordEditor({
  table,
  record,
  activeTab,
  drafts,
  renderField,
  setActiveTab,
  setJsonDraft,
}: NavigationBarRecordEditorProps) {
  const root = configRoot(drafts.config_json ?? "{}");
  const rawRoot = parsedObject(drafts.config_json ?? "{}") as Record<string, JsonValue>;
  const layout = isJsonObject(root.layout) ? root.layout : {};
  const items = navigationBarItems(root);
  const zoneSelectOptions: DictionarySelectOptions = {
    options: [
      { value: "off", label: "Off" },
      { value: "left", label: "Left" },
      { value: "center", label: "Center" },
      { value: "right", label: "Right" },
    ],
  };
  const generalFields = table.fields.filter(
    (field) =>
      !["id", "production_id", "config_json", "metadata_json"].includes(
        field.column,
      ),
  );
  const update = (path: Array<string | number>, nextValue: JsonValue) =>
    setJsonDraft("config_json", setAtPath(rawRoot, path, nextValue));
  const updateItems = (nextItems: JsonValue) => update(["items"], nextItems);
  const fallbackConfig = defaultNavigationBarConfig() as JsonValue;

  function renderLayoutField(binding: (typeof NAVIGATION_BAR_CONFIG_BINDINGS)[number]) {
    const descriptor = createJsonFieldDescriptor({
      binding,
      localRoot: rawRoot,
      parentRoot: {},
      fallbackRoot: fallbackConfig,
      sourceKind: "json-binding",
      recordId: typeof record.id === "string" ? record.id : undefined,
      recordType: "navigation_bars",
      onRootChange: (nextRoot) => setJsonDraft("config_json", nextRoot),
    });
    if (!descriptor) return null;
    return (
      <EditorFieldRow
        key={binding.outputPath.join(".")}
        className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
        descriptor={descriptor}
      >
        <DictionaryFieldControl {...toDictionaryFieldControlProps(descriptor)} />
      </EditorFieldRow>
    );
  }

  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Navigation Bar Editor"
        title={String(record[table.titleColumn] ?? record.id)}
        summary={`${items.length} generated buttons`}
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
              {generalFields.map((field) => renderField(field))}
            </div>
          ) : null}
        </EditorSectionCard>

        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "config"}
            onClick={() => setActiveTab(activeTab === "config" ? "" : "config")}
          >
            Configuration
          </EditorSectionButton>
          {activeTab === "config" ? (
            <div className="editor-section-body status-bar-config-editor">
              <div className="record-editor-field-stack record-editor-direct-fields">
                {NAVIGATION_BAR_CONFIG_BINDINGS.map(renderLayoutField)}
              </div>

              <div className="status-bar-items-table">
                <div className="status-bar-items-header">
                  <span>Button</span>
                  <span>Kind</span>
                  <span>Zone</span>
                  <span>Order</span>
                </div>
                {items.map((item, index) => (
                  <div className="status-bar-item-row" key={item.id}>
                    <div className="status-bar-item-label">
                      <strong>{item.label}</strong>
                      <small>{item.id}</small>
                    </div>
                    <div className="status-bar-item-value">
                      <span>{item.kind}</span>
                    </div>
                    <DictionaryFieldControl
                      field={NAVIGATION_BAR_FIELDS.itemZone}
                      selectOptions={zoneSelectOptions}
                      value={item.zone}
                      onChange={(nextValue) =>
                        updateItems(
                          updateItem(items, index, {
                            zone: nextValue as NavigationBarZone,
                          }),
                        )
                      }
                    />
                    <DictionaryFieldControl
                      field={NAVIGATION_BAR_FIELDS.itemOrder}
                      value={item.order}
                      onChange={(nextValue) =>
                        updateItems(
                          updateItem(items, index, {
                            order: Number(nextValue),
                          }),
                        )
                      }
                    />
                  </div>
                ))}
              </div>
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
