import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
  updateAppRecord,
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import { AppRecordEditor } from "../editors/AppRecordEditor.js";
import { renderGenericField as renderGenericFieldFromDispatcher } from "../editors/GenericFieldDispatcher.js";
import { GenericRecordEditor } from "../editors/GenericRecordEditor.js";
import { ModuleInstanceEditor } from "../editors/ModuleInstanceEditor.js";
import { ModuleThemeConfigRecordEditor } from "../editors/ModuleThemeConfigRecordEditor.js";
import { ScreenInstanceEditor } from "../editors/ScreenInstanceEditor.js";
import { ThemeRecordEditor } from "../editors/ThemeRecordEditor.js";
import type {
  AppEditorTab,
  ModuleThemeTab,
  ScreenInstanceTab,
  ThemeEditorTab,
} from "../editors/editorTabs.js";
import {
  chatContentGroupHasWarning,
  defaultGroupValue,
} from "../editors/chat/chatContentModel.js";
import { ChatContentGroupEditor } from "../editors/chat/ChatContentGroupEditor.js";
import {
  RecordFieldRenderer,
  titleForRecord,
  type RawJsonFieldOverride,
} from "../editors/RecordFieldRenderer.js";
import {
  DeviceMetricsField,
  FlatJsonObjectEditor,
} from "../editors/FlatJsonFieldEditors.js";
import {
  normalizeGroupValue,
  parsedJsonValue,
  parsedObject,
} from "../editors/recordJsonUtils.js";
import { shotHasFpsOverride } from "../editors/ShotFields.js";
import { ScreenTransitionFields } from "../editors/ScreenInstanceFields.js";
import { ModuleBehaviorFields } from "../editors/ModuleBehaviorFields.js";
import { productionMediaRootForRecord } from "../editors/recordProductionUtils.js";
import { useJsonGroupDrafts } from "../editors/useJsonGroupDrafts.js";
import {
  cloneJson,
  defaultJsonValue,
  type JsonPath,
  type JsonValue,
} from "./json-editor/jsonEditorUtils.js";
import { buildJsonUiHints } from "./json-editor/uiHints.js";

type SaveState = "saved" | "dirty" | "invalid" | "saving" | "failed";

interface MockupsNativeBridge {
  pickFile?: () => Promise<string[]>;
  pickDirectory?: () => Promise<string[]>;
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

function mockupsNative() {
  return (window as Window & { mockupsNative?: MockupsNativeBridge }).mockupsNative;
}

function normalizeFilesystemPath(value: string) {
  return value.replace(/\\/g, "/").replace(/\/+$/g, "");
}

function relativePathFromRoot(filePath: string, rootPath: string) {
  const normalizedFile = normalizeFilesystemPath(filePath);
  const normalizedRoot = normalizeFilesystemPath(rootPath);
  if (!normalizedRoot) return normalizedFile;
  if (normalizedFile === normalizedRoot) return "";
  if (normalizedFile.startsWith(`${normalizedRoot}/`)) {
    return normalizedFile.slice(normalizedRoot.length + 1);
  }
  return normalizedFile;
}

interface RecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
  inheritedFields?: Record<string, Record<string, unknown>>;
  onRecordsChanged: (records: AppRecord[]) => void;
  onRecordSaved: (record: AppRecord) => void;
}

function stringifyJson(value: unknown): string {
  return JSON.stringify(value ?? {}, null, 2);
}

function draftValue(record: AppRecord | undefined, field: AppFieldDefinition) {
  if (!record) return "";
  const value = record[field.column];
  return field.kind === "json" ? stringifyJson(value) : String(value ?? "");
}

function parseValue(
  field: AppFieldDefinition,
  raw: string,
): { ok: true; value: unknown } | { ok: false; error: string } {
  if (field.kind === "json") {
    try {
      const parsed: unknown = JSON.parse(raw);
      if (
        parsed === null ||
        Array.isArray(parsed) ||
        typeof parsed !== "object"
      ) {
        return { ok: false, error: "JSON value must be an object." };
      }
      return { ok: true, value: parsed };
    } catch (error) {
      return {
        ok: false,
        error: error instanceof Error ? error.message : String(error),
      };
    }
  }
  if (field.kind === "number") {
    const numberValue = Number(raw);
    if (!Number.isFinite(numberValue)) {
      return { ok: false, error: "Value must be numeric." };
    }
    return { ok: true, value: numberValue };
  }
  return {
    ok: true,
    value: raw === "" && field.nullable ? null : raw,
  };
}

export function RecordEditor({
  table,
  record,
  records,
  inheritedFields = {},
  onRecordsChanged,
  onRecordSaved,
}: RecordEditorProps) {
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [states, setStates] = useState<Record<string, SaveState>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [screenTab, setScreenTab] = useState<ScreenInstanceTab>("");
  const [contentTab, setContentTab] = useState("participants");
  const [appTab, setAppTab] = useState<AppEditorTab>("");
  const [appTokenGroup, setAppTokenGroup] = useState("");
  const [themeTab, setThemeTab] = useState<ThemeEditorTab>("");
  const [themeTokenGroup, setThemeTokenGroup] = useState("");
  const [moduleThemeTab, setModuleThemeTab] = useState<ModuleThemeTab>("");
  const [moduleDesignGroup, setModuleDesignGroup] = useState("");
  const [genericTab, setGenericTab] = useState<"" | "general">("general");
  const [openContentItems, setOpenContentItems] = useState<Record<string, boolean>>(
    {},
  );

  useEffect(() => {
    const nextDrafts = Object.fromEntries(
      table.fields.map((field) => [field.column, draftValue(record, field)]),
    );
    setDrafts(nextDrafts);
    setStates(
      Object.fromEntries(table.fields.map((field) => [field.column, "saved"])),
    );
    setErrors({});
    setScreenTab("");
    setContentTab("participants");
    setModuleThemeTab("");
    setAppTab("");
    setAppTokenGroup("");
    setThemeTab("");
    setThemeTokenGroup("");
    setModuleDesignGroup("");
    setGenericTab("general");
    setOpenContentItems({});
  }, [record?.id, table.id]);

  function toggleExclusiveContentItem(groupKey: string, openKey: string, isOpen: boolean) {
    const groupPrefix = `${record?.id ?? "record"}:${groupKey}:`;
    setOpenContentItems((current) => {
      const nextEntries = Object.entries(current).filter(
        ([key]) => !key.startsWith(groupPrefix),
      );
      return isOpen
        ? Object.fromEntries(nextEntries)
        : {
            ...Object.fromEntries(nextEntries),
            [openKey]: true,
          };
    });
  }

  const editableFields = useMemo(
    () => table.fields.filter((field) => !field.readonly),
    [table],
  );

  useEffect(() => {
    if (!record) return;
    const timers = editableFields.flatMap((field) => {
      const raw = drafts[field.column];
      if (raw === undefined || raw === draftValue(record, field)) {
        return [];
      }
      const parsed = parseValue(field, raw);
      if (!parsed.ok) {
        setStates((current) => ({
          ...current,
          [field.column]: "invalid",
        }));
        setErrors((current) => ({
          ...current,
          [field.column]: parsed.error,
        }));
        return [];
      }
      const timer = window.setTimeout(() => {
        setStates((current) => ({ ...current, [field.column]: "saving" }));
        setErrors((current) => ({ ...current, [field.column]: "" }));
        void updateAppRecord(table.id, record.id, {
          [field.column]: parsed.value,
        })
          .then((result) => {
            onRecordsChanged(result.records);
            onRecordSaved((result.saved ?? result.record) as AppRecord);
            setStates((current) => ({
              ...current,
              [field.column]: "saved",
            }));
          })
          .catch((error: Error) => {
            setStates((current) => ({
              ...current,
              [field.column]: "failed",
            }));
            setErrors((current) => ({
              ...current,
              [field.column]: error.message,
            }));
          });
      }, 650);
      setStates((current) => ({ ...current, [field.column]: "dirty" }));
      setErrors((current) => ({ ...current, [field.column]: "" }));
      return [timer];
    });
    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [drafts, editableFields, onRecordSaved, onRecordsChanged, record, table.id]);

  if (!record) {
    return (
      <section className="record-editor record-editor-empty">
        No records in this table yet.
      </section>
    );
  }

  const fieldsByColumn = new Map(table.fields.map((field) => [field.column, field]));

  function setJsonDraft(column: string, value: JsonValue) {
    setDrafts({
      ...drafts,
      [column]: stringifyJson(value),
    });
  }

  const { rawForJsonGroupValue, updateJsonGroupValue } = useJsonGroupDrafts({
    drafts,
    defaultGroupValue,
    setDrafts,
  });

  function productionMediaRoot() {
    return productionMediaRootForRecord({ table, record, records });
  }

  function renderContentGroupEditor(
    field: AppFieldDefinition,
    groupKey: string,
    column = "module_data_json",
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
        canBrowseMedia={Boolean(mockupsNative()?.pickFile)}
        groupKey={groupKey}
        groupValue={groupValue}
        hints={hints}
        normalizeMediaPath={(filePath) =>
          relativePathFromRoot(filePath, productionMediaRoot())
        }
        onBrowseMedia={async () => {
          const [filePath] =
            await (mockupsNative()?.pickFile?.() ?? Promise.resolve([]));
          return filePath
            ? relativePathFromRoot(filePath, productionMediaRoot())
            : undefined;
        }}
        onGroupValueChange={updateGroupValue}
        onToggleItem={toggleExclusiveContentItem}
        openItems={openContentItems}
        recordId={record?.id}
        root={root}
      />
    );
  }

  function renderField(field: AppFieldDefinition, rawOverride?: RawJsonFieldOverride) {
    return (
      <RecordFieldRenderer
        key={field.column}
        table={table}
        field={field}
        record={record}
        records={records}
        drafts={drafts}
        state={states[field.column] ?? "saved"}
        error={errors[field.column]}
        inheritedValue={inheritedFields[field.column]}
        rawOverride={rawOverride}
        onDraftChange={(column, value) =>
          setDrafts({
            ...drafts,
            [column]: value,
          })
        }
      />
    );
  }

  function renderFields(columns: string[]) {
    return columns
      .map((column) => fieldsByColumn.get(column))
      .filter((field): field is AppFieldDefinition => Boolean(field))
      .filter((field) => field.column !== "id")
      .map((field) => renderField(field));
  }

  function renderGenericField(field: AppFieldDefinition) {
    return renderGenericFieldFromDispatcher({
      table,
      field,
      record,
      records,
      drafts,
      states,
      errors,
      nativeBridge: mockupsNative(),
      productionMediaRoot: productionMediaRoot(),
      relativePathFromRoot,
      setDrafts,
      setJsonDraft,
      renderField,
      renderFlatJsonObjectEditor,
      renderDeviceMetricsField,
    });
  }

  function renderFlatJsonObjectEditor(
    column: string,
    omitKeys: string[] = [],
  ) {
    const field = fieldsByColumn.get(column);
    if (!field) return null;
    const root = parsedObject(drafts[column] ?? "{}");
    return (
      <FlatJsonObjectEditor
        table={table}
        field={field}
        record={record}
        root={root}
        omitKeys={omitKeys}
        onRootChange={(nextRoot) => setJsonDraft(column, nextRoot)}
      />
    );
  }

  function renderDeviceMetricsField(field: AppFieldDefinition) {
    const root = parsedJsonValue(drafts[field.column] ?? "{}", {}) as JsonValue;
    return (
      <DeviceMetricsField
        key={field.column}
        table={table}
        field={field}
        record={record}
        root={root}
        onRootChange={(nextRoot) => setJsonDraft(field.column, nextRoot)}
      />
    );
  }

  function configGroupHasContent(groupKey: string) {
    const config = parsedObject(drafts.config_json ?? "{}");
    const value = config[groupKey];
    return (
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      Object.keys(value as Record<string, unknown>).length > 0
    );
  }

  if (table.id === "apps") {
    return (
      <AppRecordEditor
        table={table}
        record={record}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        inheritedFields={inheritedFields}
        activeTab={appTab}
        activeTokenGroup={appTokenGroup}
        mediaRoot={productionMediaRoot()}
        nativeBridge={mockupsNative()}
        relativePathFromRoot={relativePathFromRoot}
        renderFields={renderFields}
        renderField={renderField}
        renderFlatJsonObjectEditor={renderFlatJsonObjectEditor}
        setActiveTab={setAppTab}
        setActiveTokenGroup={setAppTokenGroup}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  if (table.id === "themes") {
    return (
      <ThemeRecordEditor
        table={table}
        record={record}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        activeTab={themeTab}
        activeTokenGroup={themeTokenGroup}
        renderFields={renderFields}
        renderField={renderField}
        setActiveTab={setThemeTab}
        setActiveTokenGroup={setThemeTokenGroup}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  if (table.id === "module_instances") {
    const contentField = fieldsByColumn.get("content_json");
    const behaviorField = fieldsByColumn.get("behavior_json");
    const contentGroups = ["participants", "header", "messages"].filter(
      (group) => group in parsedObject(drafts.content_json ?? "{}"),
    );
    const safeContentGroups = contentGroups.length
      ? contentGroups
      : ["participants", "header", "messages"];
    const activeContentTab = safeContentGroups.includes(contentTab)
      ? contentTab
      : "";

    return (
      <ModuleInstanceEditor
        table={table}
        record={record}
        activeTab={screenTab}
        activeContentTab={activeContentTab}
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
          contentField
            ? renderContentGroupEditor(contentField, group, "content_json")
            : null
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
        setActiveTab={setScreenTab}
        setContentTab={setContentTab}
      />
    );
  }

  if (table.id === "screen_instances") {
    const deviceStateField = fieldsByColumn.get("device_state_json");
    const transformField = fieldsByColumn.get("transform_json");
    return (
      <ScreenInstanceEditor
        table={table}
        record={record}
        activeTab={screenTab}
        transformFieldExists={Boolean(transformField)}
        deviceStateFieldExists={Boolean(deviceStateField)}
        renderGeneralFields={() =>
          renderFields(["app_id", "theme_mode", "start_frame", "end_frame"])
        }
        renderTransformFields={() =>
          transformField
            ? renderField(transformField, {
                hideLabel: true,
                rawText: drafts.transform_json ?? "{}",
                groupContext: "transform",
                onRawTextChange: (nextRawText) =>
                  setDrafts({ ...drafts, transform_json: nextRawText }),
              })
            : null
        }
        renderTransitionFields={() => (
          <ScreenTransitionFields
            records={records}
            record={record}
            drafts={drafts}
            setDraftValue={(column, value) =>
              setDrafts({
                ...drafts,
                [column]: value,
              })
            }
          />
        )}
        renderDeviceStateFields={() =>
          deviceStateField
            ? renderField(deviceStateField, {
                hideLabel: true,
                rawText: drafts.device_state_json ?? "{}",
                groupContext: "deviceState",
                onRawTextChange: (nextRawText) =>
                  setDrafts({ ...drafts, device_state_json: nextRawText }),
              })
            : null
        }
        setActiveTab={setScreenTab}
      />
    );
  }

  if (table.id === "module_theme_configs") {
    return (
      <ModuleThemeConfigRecordEditor
        table={table}
        record={record}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        inheritedFields={inheritedFields}
        activeTab={moduleThemeTab}
        activeDesignGroup={moduleDesignGroup}
        renderFields={renderFields}
        renderField={renderField}
        renderFlatJsonObjectEditor={renderFlatJsonObjectEditor}
        rawForJsonGroupValue={rawForJsonGroupValue}
        updateJsonGroupValue={updateJsonGroupValue}
        setActiveTab={setModuleThemeTab}
        setActiveDesignGroup={setModuleDesignGroup}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  return (
    <GenericRecordEditor
      table={table}
      record={record}
      activeTab={genericTab}
      renderGenericField={renderGenericField}
      setActiveTab={setGenericTab}
      showGeneralWarning={
        table.id === "shots" &&
        shotHasFpsOverride({
          records,
          record,
          drafts,
        })
      }
    />
  );
}
