import type { Dispatch, SetStateAction } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { GenericRecordEditor } from "./GenericRecordEditor.js";
import { ModuleInstanceRecordEditor } from "./ModuleInstanceRecordEditor.js";
import { ScreenInstanceRecordEditor } from "./ScreenInstanceRecordEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
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
  productionFontCatalog?: ProductionFontCatalog;
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
  productionFontCatalog,
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
    renderGenericField,
  } = renderServices;

  if (table.id !== "module_instances" && table.id !== "screen_instances") {
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
