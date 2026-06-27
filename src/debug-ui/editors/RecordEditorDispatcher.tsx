import type { Dispatch, SetStateAction } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { AppRecordEditor } from "./AppRecordEditor.js";
import { ComponentClassRecordEditor } from "./ComponentClassRecordEditor.js";
import { GenericRecordEditor } from "./GenericRecordEditor.js";
import { IconThemeRecordEditor } from "./IconThemeRecordEditor.js";
import { ModuleInstanceRecordEditor } from "./ModuleInstanceRecordEditor.js";
import { ModuleThemeConfigRecordEditor } from "./ModuleThemeConfigRecordEditor.js";
import { NavigationBarRecordEditor } from "./NavigationBarRecordEditor.js";
import { ProductionFontRecordEditor } from "./ProductionFontRecordEditor.js";
import { ScreenInstanceRecordEditor } from "./ScreenInstanceRecordEditor.js";
import { StatusBarRecordEditor } from "./StatusBarRecordEditor.js";
import { ThemeRecordEditor } from "./ThemeRecordEditor.js";
import { shotHasFpsOverride } from "./ShotFields.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { createJsonGroupDrafts } from "./jsonGroupDrafts.js";
import type { createRecordEditorRenderServices } from "./recordEditorRenderServices.js";
import type { useRecordEditorTabs } from "./useRecordEditorTabs.js";

interface NativeBridge {
  pickFile?: () => Promise<string[]>;
  pickDirectory?: () => Promise<string[]>;
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

interface RecordEditorDispatcherProps {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
  fieldsByColumn: Map<string, AppFieldDefinition>;
  drafts: Record<string, string>;
  inheritedFields: Record<string, Record<string, unknown>>;
  jsonGroupDrafts: ReturnType<typeof createJsonGroupDrafts>;
  mediaRoot: string;
  paletteCatalog?: PaletteColorCatalog;
  productionId: string;
  nativeBridge: NativeBridge | undefined;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  renderServices: ReturnType<typeof createRecordEditorRenderServices>;
  setDrafts: Dispatch<SetStateAction<Record<string, string>>>;
  tabs: ReturnType<typeof useRecordEditorTabs>;
  onRecordsChanged: (records: AppRecord[]) => void;
  onRecordSaved: (record: AppRecord) => void;
  onPreviewRelativeFrameChange?: (frame: number) => void;
}

export function RecordEditorDispatcher({
  table,
  record,
  records,
  fieldsByColumn,
  drafts,
  inheritedFields,
  jsonGroupDrafts,
  mediaRoot,
  paletteCatalog,
  productionId,
  nativeBridge,
  relativePathFromRoot,
  renderServices,
  setDrafts,
  tabs,
  onRecordsChanged,
  onRecordSaved,
  onPreviewRelativeFrameChange,
}: RecordEditorDispatcherProps) {
  const {
    renderField,
    renderFields,
    renderFlatJsonObjectEditor,
    renderGenericField,
    setJsonDraft,
  } = renderServices;
  const { rawForJsonGroupValue, updateJsonGroupValue } = jsonGroupDrafts;

  if (table.id === "apps") {
    return (
      <AppRecordEditor
        table={table}
        record={record}
        records={records}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        inheritedFields={inheritedFields}
        activeTab={tabs.appTab}
        activeTokenGroup={tabs.appTokenGroup}
        mediaRoot={mediaRoot}
        nativeBridge={nativeBridge}
        relativePathFromRoot={relativePathFromRoot}
        renderFields={renderFields}
        renderField={renderField}
        renderFlatJsonObjectEditor={renderFlatJsonObjectEditor}
        setActiveTab={tabs.setAppTab}
        setActiveTokenGroup={tabs.setAppTokenGroup}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  if (table.id === "themes") {
    return (
      <ThemeRecordEditor
        table={table}
        record={record}
        records={records}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        activeTab={tabs.themeTab}
        activeTokenGroup={tabs.themeTokenGroup}
        renderFields={renderFields}
        renderField={renderField}
        setActiveTab={tabs.setThemeTab}
        setActiveTokenGroup={tabs.setThemeTokenGroup}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  if (table.id === "icon_themes") {
    return (
      <IconThemeRecordEditor
        table={table}
        record={record}
        drafts={drafts}
        activeTab={tabs.iconThemeTab}
        mediaRoot={mediaRoot}
        nativeBridge={nativeBridge}
        renderField={renderField}
        setActiveTab={tabs.setIconThemeTab}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  if (table.id === "status_bars") {
    return (
      <StatusBarRecordEditor
        table={table}
        record={record}
        records={records}
        drafts={drafts}
        activeTab={tabs.statusBarTab}
        renderField={renderField}
        setActiveTab={tabs.setStatusBarTab}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  if (table.id === "navigation_bars") {
    return (
      <NavigationBarRecordEditor
        table={table}
        record={record}
        drafts={drafts}
        activeTab={tabs.navigationBarTab}
        renderField={renderField}
        setActiveTab={tabs.setNavigationBarTab}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  if (table.id === "production_fonts") {
    return (
      <ProductionFontRecordEditor
        table={table}
        record={record}
        activeTab={tabs.genericTab}
        nativeBridge={nativeBridge}
        productionId={productionId}
        renderGenericField={renderGenericField}
        setActiveTab={tabs.setGenericTab}
        onRecordsChanged={onRecordsChanged}
        onRecordSaved={onRecordSaved}
      />
    );
  }

  if (table.id === "component_classes") {
    return (
      <ComponentClassRecordEditor
        table={table}
        record={record}
        activeTab={tabs.genericTab}
        drafts={drafts}
        paletteCatalog={paletteCatalog}
        renderField={renderField}
        setActiveTab={tabs.setGenericTab}
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
        activeTab={tabs.screenTab}
        activeContentTab={tabs.contentTab}
        mediaRoot={mediaRoot}
        productionId={productionId}
        nativeBridge={nativeBridge}
        relativePathFromRoot={relativePathFromRoot}
        setDrafts={setDrafts}
        setActiveTab={tabs.setScreenTab}
        setActiveContentTab={tabs.setContentTab}
        onPreviewRelativeFrameChange={onPreviewRelativeFrameChange}
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
        activeTab={tabs.screenTab}
        renderFields={renderFields}
        renderField={renderField}
        setDrafts={setDrafts}
        setActiveTab={tabs.setScreenTab}
      />
    );
  }

  if (table.id === "module_theme_configs") {
    return (
      <ModuleThemeConfigRecordEditor
        table={table}
        record={record}
        records={records}
        fieldsByColumn={fieldsByColumn}
        drafts={drafts}
        inheritedFields={inheritedFields}
        activeTab={tabs.moduleThemeTab}
        activeDesignGroup={tabs.moduleDesignGroup}
        renderFields={renderFields}
        renderField={renderField}
        renderFlatJsonObjectEditor={renderFlatJsonObjectEditor}
        rawForJsonGroupValue={rawForJsonGroupValue}
        updateJsonGroupValue={updateJsonGroupValue}
        setActiveTab={tabs.setModuleThemeTab}
        setActiveDesignGroup={tabs.setModuleDesignGroup}
        setJsonDraft={setJsonDraft}
      />
    );
  }

  return (
    <GenericRecordEditor
      table={table}
      record={record}
      activeTab={tabs.genericTab}
      renderGenericField={renderGenericField}
      setActiveTab={tabs.setGenericTab}
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
