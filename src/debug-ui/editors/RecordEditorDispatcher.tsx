import type { Dispatch, SetStateAction } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppState,
  AppTableDefinition,
} from "../api/client.js";
import { AppRecordEditor } from "./AppRecordEditor.js";
import { ComponentClassRecordEditor } from "./ComponentClassRecordEditor.js";
import { FlatJsonObjectEditor } from "./FlatJsonFieldEditors.js";
import { GenericRecordEditor } from "./GenericRecordEditor.js";
import { IconThemeRecordEditor } from "./IconThemeRecordEditor.js";
import { ModuleInstanceRecordEditor } from "./ModuleInstanceRecordEditor.js";
import { ModuleThemeConfigRecordEditor } from "./ModuleThemeConfigRecordEditor.js";
import { NavigationBarRecordEditor } from "./NavigationBarRecordEditor.js";
import { ProductionFontRecordEditor } from "./ProductionFontRecordEditor.js";
import { ScreenInstanceRecordEditor } from "./ScreenInstanceRecordEditor.js";
import { StatusBarRecordEditor } from "./StatusBarRecordEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import type { createJsonGroupDrafts } from "./jsonGroupDrafts.js";
import type { createRecordEditorRenderServices } from "./recordEditorRenderServices.js";
import { parsedObject } from "./recordJsonUtils.js";
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
  productionFontCatalog?: ProductionFontCatalog;
  productionId: string;
  nativeBridge: NativeBridge | undefined;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  renderServices: ReturnType<typeof createRecordEditorRenderServices>;
  setDrafts: Dispatch<SetStateAction<Record<string, string>>>;
  tabs: ReturnType<typeof useRecordEditorTabs>;
  onRecordsChanged: (records: AppRecord[]) => void;
  onRecordSaved: (record: AppRecord) => void;
  onAppStateChanged?: (state: AppState, tableId: string, record: AppRecord) => void;
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
  productionFontCatalog,
  productionId,
  nativeBridge,
  relativePathFromRoot,
  renderServices,
  setDrafts,
  tabs,
  onRecordsChanged,
  onRecordSaved,
  onAppStateChanged,
  onPreviewRelativeFrameChange,
}: RecordEditorDispatcherProps) {
  const {
    renderField,
    renderFields,
    renderGenericField,
    setJsonDraft,
  } = renderServices;
  const renderFlatJsonObjectEditor = (column: string, omitKeys?: string[]) => {
    const field = fieldsByColumn.get(column);
    if (!field) return null;
    return (
      <FlatJsonObjectEditor
        table={table}
        field={field}
        record={record}
        root={parsedObject(drafts[column] ?? "{}")}
        omitKeys={omitKeys}
        productionFontCatalog={productionFontCatalog}
        paletteCatalog={paletteCatalog}
        onRootChange={(nextRoot) => setJsonDraft(column, nextRoot)}
      />
    );
  };

  if (table.id !== "module_instances" && table.id !== "screen_instances") {
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
          renderFields={renderFields}
          renderField={renderField}
          renderFlatJsonObjectEditor={renderFlatJsonObjectEditor}
          setActiveTab={tabs.setAppTab}
          setActiveTokenGroup={tabs.setAppTokenGroup}
          setJsonDraft={setJsonDraft}
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
          rawForJsonGroupValue={jsonGroupDrafts.rawForJsonGroupValue}
          updateJsonGroupValue={jsonGroupDrafts.updateJsonGroupValue}
          setActiveTab={tabs.setModuleThemeTab}
          setActiveDesignGroup={tabs.setModuleDesignGroup}
          setJsonDraft={setJsonDraft}
        />
      );
    }
    if (table.id === "icon_themes") {
      return (
        <IconThemeRecordEditor
          table={table}
          record={record}
          activeTab={tabs.iconThemeTab}
          drafts={drafts}
          mediaRoot={mediaRoot}
          nativeBridge={nativeBridge}
          renderField={renderField}
          setActiveTab={tabs.setIconThemeTab}
          onAppStateChanged={onAppStateChanged}
        />
      );
    }
    if (table.id === "status_bars") {
      return (
        <StatusBarRecordEditor
          table={table}
          record={record}
          records={records}
          activeTab={tabs.statusBarTab}
          drafts={drafts}
          mediaRoot={mediaRoot}
          nativeBridge={nativeBridge}
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
          activeTab={tabs.navigationBarTab}
          drafts={drafts}
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
          renderGenericField={renderGenericField}
          setActiveTab={tabs.setGenericTab}
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
          mediaRoot={mediaRoot}
          nativeBridge={nativeBridge}
          productionFontCatalog={productionFontCatalog}
          iconThemeRecords={records.icon_themes}
          renderField={renderField}
          setActiveTab={tabs.setGenericTab}
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

  return (
    <GenericRecordEditor
      table={table}
      record={record}
      activeTab={tabs.genericTab}
      renderGenericField={renderGenericField}
      setActiveTab={tabs.setGenericTab}
    />
  );
}
