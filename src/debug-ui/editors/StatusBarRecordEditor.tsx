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
  STATUS_BAR_CONFIG_BINDINGS,
  STATUS_BAR_FIELDS,
} from "../../domain/fields/statusBarFields.js";
import { parsedObject } from "./recordJsonUtils.js";

type StatusBarTab = "" | "general" | "config";
type StatusBarItemKind = "text" | "iconToken" | "generatedBattery" | "generatedSignal";
type StatusBarZone = "off" | "left" | "right";

interface StatusBarRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
  activeTab: StatusBarTab;
  drafts: Record<string, string>;
  mediaRoot?: string;
  nativeBridge?: {
    mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
  };
  renderField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: StatusBarTab) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

interface StatusBarItem {
  id: string;
  label: string;
  kind: StatusBarItemKind;
  value?: string | number | boolean;
  token?: string;
  charging?: boolean;
  zone: StatusBarZone;
  order: number;
}

function defaultStatusBarItems(): StatusBarItem[] {
  return [
    {
      id: "time",
      label: "Time",
      kind: "text",
      value: "9:41",
      zone: "left",
      order: 10,
    },
    {
      id: "carrier",
      label: "Carrier",
      kind: "text",
      value: "",
      zone: "off",
      order: 20,
    },
    {
      id: "signal",
      label: "Signal",
      kind: "generatedSignal",
      value: 4,
      zone: "right",
      order: 10,
    },
    {
      id: "wifi",
      label: "Wi‑Fi",
      kind: "iconToken",
      token: "status_wifi",
      zone: "right",
      order: 20,
    },
    {
      id: "soundOff",
      label: "Sound Off",
      kind: "iconToken",
      token: "media_volume_off",
      zone: "off",
      order: 30,
    },
    {
      id: "bluetooth",
      label: "Bluetooth",
      kind: "iconToken",
      token: "status_bluetooth",
      zone: "off",
      order: 40,
    },
    {
      id: "battery",
      label: "Battery",
      kind: "generatedBattery",
      value: 85,
      charging: false,
      zone: "right",
      order: 50,
    },
  ];
}

function defaultStatusBarConfig(): Record<string, JsonValue> {
  return {
    schemaVersion: 2,
    layout: {
      height: 54,
      itemSize: 18,
      gap: 6,
      sidePadding: 24,
    },
    items: defaultStatusBarItems() as unknown as JsonValue,
  };
}

function configRoot(raw: string) {
  const parsed = parsedObject(raw) as Record<string, JsonValue>;
  const parsedLayout = isJsonObject(parsed.layout)
    ? (parsed.layout as Record<string, JsonValue>)
    : {};
  const merged = {
    ...defaultStatusBarConfig(),
    ...parsed,
    layout: {
      ...(defaultStatusBarConfig().layout as Record<string, JsonValue>),
      ...parsedLayout,
    },
  } as Record<string, JsonValue>;
  if (!Array.isArray(merged.items)) {
    merged.items = defaultStatusBarItems() as unknown as JsonValue;
  }
  return merged;
}

function numberValue(value: JsonValue | undefined, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringValue(value: JsonValue | undefined, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function statusBarItems(root: Record<string, JsonValue>) {
  const rawItems = Array.isArray(root.items) ? root.items : [];
  return rawItems.map((rawItem, index) => {
    const item = isJsonObject(rawItem) ? rawItem : {};
    const fallback = defaultStatusBarItems()[index] ?? defaultStatusBarItems()[0];
    const kind =
      item.kind === "text" ||
      item.kind === "iconToken" ||
      item.kind === "generatedBattery" ||
      item.kind === "generatedSignal"
        ? item.kind
        : fallback.kind;
    const zone =
      item.zone === "off" || item.zone === "left" || item.zone === "right"
        ? item.zone
        : fallback.zone;
    return {
      id: stringValue(item.id, fallback.id),
      label: stringValue(item.label, fallback.label),
      kind,
      value:
        typeof item.value === "string" || typeof item.value === "number"
          ? item.value
          : fallback.value,
      token: stringValue(item.token, fallback.token ?? ""),
      charging: item.charging === true,
      zone,
      order: numberValue(item.order, fallback.order),
    } satisfies StatusBarItem;
  });
}

function updateItem(
  items: StatusBarItem[],
  index: number,
  patch: Partial<StatusBarItem>,
) {
  return items.map((item, itemIndex) =>
    itemIndex === index ? { ...item, ...patch } : item,
  ) as unknown as JsonValue;
}

function iconThemeTokenOptions(records: Record<string, AppRecord[]>) {
  const firstIconTheme = records.icon_themes?.[0];
  const rawMapping = firstIconTheme?.mapping_json;
  const rawMappingValue = rawMapping as JsonValue;
  const mapping =
    typeof rawMapping === "string"
      ? (parsedObject(rawMapping) as Record<string, JsonValue>)
      : isJsonObject(rawMappingValue)
        ? (rawMappingValue as Record<string, JsonValue>)
        : {};
  const tokens = isJsonObject(mapping.tokens)
    ? (mapping.tokens as Record<string, JsonValue>)
    : {};
  return Object.keys(tokens).sort((left, right) => left.localeCompare(right));
}

function tokenSelectOptions(
  tokens: readonly string[],
  currentToken?: string,
): DictionarySelectOptions {
  const uniqueTokens = new Set(tokens);
  if (currentToken) uniqueTokens.add(currentToken);
  return {
    allowEmpty: true,
    emptyLabel: "Select token…",
    options: [...uniqueTokens]
      .sort((left, right) => left.localeCompare(right))
      .map((token) => ({ value: token, label: token })),
  };
}

export function StatusBarRecordEditor({
  table,
  record,
  records,
  activeTab,
  drafts,
  mediaRoot,
  nativeBridge,
  renderField,
  setActiveTab,
  setJsonDraft,
}: StatusBarRecordEditorProps) {
  const rawRoot = parsedObject(drafts.config_json ?? "{}") as Record<string, JsonValue>;
  const root = configRoot(drafts.config_json ?? "{}");
  const items = statusBarItems(root);
  const tokenOptions = iconThemeTokenOptions(records);
  const zoneSelectOptions: DictionarySelectOptions = {
    options: [
      { value: "off", label: "Off" },
      { value: "left", label: "Left" },
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
  const fallbackConfig = defaultStatusBarConfig() as JsonValue;

  function renderLayoutField(binding: (typeof STATUS_BAR_CONFIG_BINDINGS)[number]) {
    const descriptor = createJsonFieldDescriptor({
      binding,
      localRoot: rawRoot,
      parentRoot: {},
      fallbackRoot: fallbackConfig,
      sourceKind: "json-binding",
      recordId: typeof record.id === "string" ? record.id : undefined,
      recordType: "status_bars",
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
        eyebrow="Status Bar Editor"
        title={String(record[table.titleColumn] ?? record.id)}
        summary={`${items.length} status items`}
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
                {STATUS_BAR_CONFIG_BINDINGS.map(renderLayoutField)}
              </div>

              <div className="status-bar-items-table">
                <div className="status-bar-items-header">
                  <span>Item</span>
                  <span>Value / token</span>
                  <span>Zone</span>
                  <span>Order</span>
                </div>
                {items.map((item, index) => (
                  <div className="status-bar-item-row" key={item.id}>
                    <div className="status-bar-item-label">
                      <strong>{item.label}</strong>
                      <small>{item.kind}</small>
                    </div>
                    <div className="status-bar-item-value">
                      {item.kind === "iconToken" ? (
                        <DictionaryFieldControl
                          field={STATUS_BAR_FIELDS.itemIconToken}
                          iconThemeRecords={records.icon_themes}
                          mediaRoot={mediaRoot}
                          fileBrowser={nativeBridge}
                          selectOptions={tokenSelectOptions(
                            tokenOptions,
                            item.token,
                          )}
                          value={item.token ?? ""}
                          onChange={(nextValue) =>
                            updateItems(
                              updateItem(items, index, { token: String(nextValue) }),
                            )
                          }
                        />
                      ) : item.kind === "text" ? (
                        <DictionaryFieldControl
                          field={STATUS_BAR_FIELDS.itemTextValue}
                          value={String(item.value ?? "")}
                          onChange={(nextValue) =>
                            updateItems(
                              updateItem(items, index, { value: String(nextValue) }),
                            )
                          }
                        />
                      ) : (
                        <div className="status-bar-generated-controls">
                          <DictionaryFieldControl
                            field={
                              item.kind === "generatedBattery"
                                ? STATUS_BAR_FIELDS.itemBatteryValue
                                : STATUS_BAR_FIELDS.itemSignalValue
                            }
                            value={Number(item.value ?? 0)}
                            onChange={(nextValue) =>
                              updateItems(
                                updateItem(items, index, {
                                  value: Number(nextValue),
                                }),
                              )
                            }
                          />
                          {item.kind === "generatedBattery" ? (
                            <div className="status-bar-generated-flag">
                              <span>Charging</span>
                              <DictionaryFieldControl
                                field={STATUS_BAR_FIELDS.itemCharging}
                                value={item.charging === true}
                                onChange={(nextValue) =>
                                  updateItems(
                                    updateItem(items, index, {
                                      charging: nextValue === true,
                                    }),
                                  )
                                }
                              />
                            </div>
                          ) : null}
                        </div>
                      )}
                    </div>
                    <DictionaryFieldControl
                      field={STATUS_BAR_FIELDS.itemZone}
                      selectOptions={zoneSelectOptions}
                      value={item.zone}
                      onChange={(nextValue) =>
                        updateItems(
                          updateItem(items, index, {
                            zone: nextValue as StatusBarZone,
                          }),
                        )
                      }
                    />
                    <DictionaryFieldControl
                      field={STATUS_BAR_FIELDS.itemOrder}
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
