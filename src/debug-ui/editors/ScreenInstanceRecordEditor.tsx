import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { ScreenInstanceEditor } from "./ScreenInstanceEditor.js";
import { ScreenTransitionFields } from "./ScreenInstanceFields.js";
import type { RawJsonFieldOverride } from "./RecordFieldRenderer.js";
import type { ScreenInstanceTab } from "./editorTabs.js";

interface ScreenInstanceRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
  fieldsByColumn: Map<string, AppFieldDefinition>;
  drafts: Record<string, string>;
  activeTab: ScreenInstanceTab;
  renderFields: (columns: string[]) => ReactNode;
  renderField: (
    field: AppFieldDefinition,
    rawOverride?: RawJsonFieldOverride,
  ) => ReactNode;
  setDrafts: (drafts: Record<string, string>) => void;
  setActiveTab: (tab: ScreenInstanceTab) => void;
}

export function ScreenInstanceRecordEditor({
  table,
  record,
  records,
  fieldsByColumn,
  drafts,
  activeTab,
  renderFields,
  renderField,
  setDrafts,
  setActiveTab,
}: ScreenInstanceRecordEditorProps) {
  const deviceStateField = fieldsByColumn.get("device_state_json");
  const transformField = fieldsByColumn.get("transform_json");

  function setJsonText(column: string, value: string) {
    setDrafts({
      ...drafts,
      [column]: value,
    });
  }

  function setDraftValue(column: string, value: string) {
    setDrafts({
      ...drafts,
      [column]: value,
    });
  }

  return (
    <ScreenInstanceEditor
      table={table}
      record={record}
      activeTab={activeTab}
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
                setJsonText("transform_json", nextRawText),
            })
          : null
      }
      renderTransitionFields={() => (
        <ScreenTransitionFields
          records={records}
          record={record}
          drafts={drafts}
          setDraftValue={setDraftValue}
        />
      )}
      renderDeviceStateFields={() =>
        deviceStateField
          ? renderField(deviceStateField, {
              hideLabel: true,
              rawText: drafts.device_state_json ?? "{}",
              groupContext: "deviceState",
              onRawTextChange: (nextRawText) =>
                setJsonText("device_state_json", nextRawText),
            })
          : null
      }
      setActiveTab={setActiveTab}
    />
  );
}
