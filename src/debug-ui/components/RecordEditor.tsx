import { useEffect, useMemo, useState } from "react";
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
import { AppRecordEditor } from "../editors/AppRecordEditor.js";
import { GenericRecordEditor } from "../editors/GenericRecordEditor.js";
import { ModuleInstanceRecordEditor } from "../editors/ModuleInstanceRecordEditor.js";
import { ModuleThemeConfigRecordEditor } from "../editors/ModuleThemeConfigRecordEditor.js";
import { ScreenInstanceRecordEditor } from "../editors/ScreenInstanceRecordEditor.js";
import { ThemeRecordEditor } from "../editors/ThemeRecordEditor.js";
import type {
  AppEditorTab,
  ModuleThemeTab,
  ScreenInstanceTab,
  ThemeEditorTab,
} from "../editors/editorTabs.js";
import { defaultGroupValue } from "../editors/chat/chatContentModel.js";
import { shotHasFpsOverride } from "../editors/ShotFields.js";
import { productionMediaRootForRecord } from "../editors/recordProductionUtils.js";
import { useJsonGroupDrafts } from "../editors/useJsonGroupDrafts.js";
import { useRecordEditorRenderServices } from "../editors/useRecordEditorRenderServices.js";

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
  }, [record?.id, table.id]);

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
  const productionMediaRoot = productionMediaRootForRecord({
    table,
    record,
    records,
  });
  const {
    renderField,
    renderFields,
    renderFlatJsonObjectEditor,
    renderGenericField,
    setJsonDraft,
  } = useRecordEditorRenderServices({
    table,
    record,
    records,
    fieldsByColumn,
    drafts,
    states,
    errors,
    inheritedFields,
    nativeBridge: mockupsNative(),
    productionMediaRoot,
    relativePathFromRoot,
    setDrafts,
  });

  const { rawForJsonGroupValue, updateJsonGroupValue } = useJsonGroupDrafts({
    drafts,
    defaultGroupValue,
    setDrafts,
  });

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
        mediaRoot={productionMediaRoot}
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
    return (
      <ModuleInstanceRecordEditor
        table={table}
        record={record}
        records={records}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        activeTab={screenTab}
        activeContentTab={contentTab}
        mediaRoot={productionMediaRoot}
        nativeBridge={mockupsNative()}
        relativePathFromRoot={relativePathFromRoot}
        setDrafts={setDrafts}
        setActiveTab={setScreenTab}
        setActiveContentTab={setContentTab}
      />
    );
  }

  if (table.id === "screen_instances") {
    return (
      <ScreenInstanceRecordEditor
        table={table}
        record={record}
        records={records}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        activeTab={screenTab}
        renderFields={renderFields}
        renderField={renderField}
        setDrafts={setDrafts}
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
