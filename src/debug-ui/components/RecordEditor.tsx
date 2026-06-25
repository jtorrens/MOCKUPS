import { useMemo } from "react";
import {
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
import { defaultGroupValue } from "../editors/chat/chatContentModel.js";
import { shotHasFpsOverride } from "../editors/ShotFields.js";
import { productionMediaRootForRecord } from "../editors/recordProductionUtils.js";
import { createJsonGroupDrafts } from "../editors/jsonGroupDrafts.js";
import { useRecordDraftAutosave } from "../editors/useRecordDraftAutosave.js";
import { useRecordEditorTabs } from "../editors/useRecordEditorTabs.js";
import { createRecordEditorRenderServices } from "../editors/recordEditorRenderServices.js";

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

export function RecordEditor({
  table,
  record,
  records,
  inheritedFields = {},
  onRecordsChanged,
  onRecordSaved,
}: RecordEditorProps) {
  const { drafts, errors, setDrafts, states } = useRecordDraftAutosave({
    table,
    record,
    onRecordsChanged,
    onRecordSaved,
  });
  const {
    appTab,
    appTokenGroup,
    contentTab,
    genericTab,
    moduleDesignGroup,
    moduleThemeTab,
    screenTab,
    setAppTab,
    setAppTokenGroup,
    setContentTab,
    setGenericTab,
    setModuleDesignGroup,
    setModuleThemeTab,
    setScreenTab,
    setThemeTab,
    setThemeTokenGroup,
    themeTab,
    themeTokenGroup,
  } = useRecordEditorTabs({
    recordId: record?.id,
    tableId: table.id,
  });

  const fieldsByColumn = useMemo(
    () => new Map(table.fields.map((field) => [field.column, field])),
    [table.fields],
  );

  if (!record) {
    return (
      <section className="record-editor record-editor-empty">
        No records in this table yet.
      </section>
    );
  }

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
  } = createRecordEditorRenderServices({
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

  const { rawForJsonGroupValue, updateJsonGroupValue } = createJsonGroupDrafts({
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
