import { useMemo } from "react";
import {
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { defaultGroupValue } from "../editors/chat/chatContentModel.js";
import { RecordEditorDispatcher } from "../editors/RecordEditorDispatcher.js";
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
  const tabs = useRecordEditorTabs({
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
  const renderServices = createRecordEditorRenderServices({
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

  const jsonGroupDrafts = createJsonGroupDrafts({
    drafts,
    defaultGroupValue,
    setDrafts,
  });

  return (
    <RecordEditorDispatcher
      table={table}
      record={record}
      records={records}
      fieldsByColumn={fieldsByColumn}
      drafts={drafts}
      inheritedFields={inheritedFields}
      jsonGroupDrafts={jsonGroupDrafts}
      mediaRoot={productionMediaRoot}
      nativeBridge={mockupsNative()}
      relativePathFromRoot={relativePathFromRoot}
      renderServices={renderServices}
      setDrafts={setDrafts}
      tabs={tabs}
    />
  );
}
