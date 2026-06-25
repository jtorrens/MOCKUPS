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
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
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
  const layout = isJsonObject(root.layout) ? root.layout : {};
  const items = navigationBarItems(root);
  const generalFields = table.fields.filter(
    (field) =>
      !["id", "production_id", "config_json", "metadata_json"].includes(
        field.column,
      ),
  );
  const update = (path: Array<string | number>, nextValue: JsonValue) =>
    setJsonDraft("config_json", setAtPath(root, path, nextValue));
  const updateItems = (nextItems: JsonValue) => update(["items"], nextItems);

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
              <div className="status-bar-layout-grid">
                <label>
                  <span>Height</span>
                  <input
                    type="number"
                    min={0}
                    value={numberValue(layout.height, 34)}
                    onChange={(event) =>
                      update(["layout", "height"], Number(event.target.value))
                    }
                  />
                </label>
                <label>
                  <span>Item size</span>
                  <input
                    type="number"
                    min={0}
                    value={numberValue(layout.itemSize, 18)}
                    onChange={(event) =>
                      update(["layout", "itemSize"], Number(event.target.value))
                    }
                  />
                </label>
                <label>
                  <span>Side padding</span>
                  <input
                    type="number"
                    min={0}
                    value={numberValue(layout.sidePadding, 40)}
                    onChange={(event) =>
                      update(["layout", "sidePadding"], Number(event.target.value))
                    }
                  />
                </label>
                <label>
                  <span>Stroke width</span>
                  <input
                    type="number"
                    min={0}
                    value={numberValue(layout.strokeWidth, 2)}
                    onChange={(event) =>
                      update(["layout", "strokeWidth"], Number(event.target.value))
                    }
                  />
                </label>
                <label>
                  <span>Corner radius</span>
                  <input
                    type="number"
                    min={0}
                    value={numberValue(layout.cornerRadius, 3)}
                    onChange={(event) =>
                      update(["layout", "cornerRadius"], Number(event.target.value))
                    }
                  />
                </label>
                <label>
                  <span>Filled</span>
                  <input
                    type="checkbox"
                    checked={layout.filled === true}
                    onChange={(event) =>
                      update(["layout", "filled"], event.target.checked)
                    }
                  />
                </label>
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
                    <select
                      value={item.zone}
                      onChange={(event) =>
                        updateItems(
                          updateItem(items, index, {
                            zone: event.target.value as NavigationBarZone,
                          }),
                        )
                      }
                    >
                      <option value="off">Off</option>
                      <option value="left">Left</option>
                      <option value="center">Center</option>
                      <option value="right">Right</option>
                    </select>
                    <input
                      type="number"
                      value={item.order}
                      onChange={(event) =>
                        updateItems(
                          updateItem(items, index, {
                            order: Number(event.target.value),
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
