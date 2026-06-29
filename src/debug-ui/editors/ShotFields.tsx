import type { ReactNode } from "react";
import {
  type AppFieldDefinition,
  type AppRecord,
} from "../api/client.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
} from "../editor-ui/DictionaryFieldControl.js";
import { EditorFieldRow } from "../editor-ui/fields/EditorFieldRow.js";
import { createRecordFieldDescriptor } from "../editor-ui/fields/createRecordFieldDescriptor.js";
import { toDictionaryFieldControlProps } from "../editor-ui/fields/EditorFieldDescriptor.js";
import { SHOT_FIELDS } from "../../domain/fields/shotFields.js";
import type { FieldDefinition } from "../../domain/value-system/index.js";
import { titleForRecord } from "./RecordFieldRenderer.js";
import type { FieldSaveState } from "./RecordFieldRenderer.js";

export function isHiddenShotField(column: string) {
  return ["production_id", "sort_order", "render_preset_id"].includes(column);
}

function projectDefaultFps(
  records: Record<string, AppRecord[]>,
  record: AppRecord | undefined,
) {
  const production = records.productions?.find(
    (item) => item.id === record?.production_id,
  );
  const fps = Number(production?.default_fps);
  return Number.isFinite(fps) && fps > 0 ? fps : undefined;
}

export function shotHasFpsOverride({
  records,
  record,
  drafts,
}: {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
}) {
  const inheritedFps = projectDefaultFps(records, record);
  const currentFps = Number(drafts.fps ?? record?.fps);
  return (
    inheritedFps !== undefined &&
    Number.isFinite(currentFps) &&
    currentFps !== inheritedFps
  );
}

function shotCalculatedDurationFrames(
  records: Record<string, AppRecord[]>,
  record: AppRecord | undefined,
) {
  const screens = records.screen_instances?.filter(
    (item) => item.shot_id === record?.id,
  );
  if (!screens?.length) return undefined;
  const durationFrames = screens.reduce(
    (sum, item) => sum + Math.max(1, Number(item.duration_frames ?? 0)),
    0,
  );
  return Number.isFinite(durationFrames) && durationFrames > 0
    ? durationFrames
    : undefined;
}

function shotOwnerDeviceName({
  records,
  record,
  drafts,
}: {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
}) {
  const owner = records.actors?.find(
    (item) => item.id === (drafts.owner_actor_id || record?.owner_actor_id),
  );
  const device = records.devices?.find(
    (item) => item.id === owner?.default_device_id,
  );
  return device ? titleForRecord(device, "name") : "No default device";
}

function renderShotName({
  records,
  record,
  drafts,
}: {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
}) {
  const production = records.productions?.find(
    (item) => item.id === record?.production_id,
  );
  const episode = records.episodes?.find(
    (item) => item.id === record?.episode_id,
  );
  const productionSlug = String(production?.slug ?? production?.name ?? "production");
  const episodeSlug = String(episode?.slug ?? episode?.name ?? "episode");
  const shotSlug = String(drafts.slug ?? record?.slug ?? record?.name ?? "shot");
  const version = Number(drafts.version ?? record?.version ?? 1);
  const versionSlug = String(Number.isFinite(version) ? version : 1).padStart(2, "0");
  return `${productionSlug}_${episodeSlug}_${shotSlug}_v${versionSlug}`;
}

interface ShotSpecialFieldContext {
  field: AppFieldDefinition;
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
  states: Record<string, FieldSaveState>;
  errors: Record<string, string>;
  setDraftValue: (column: string, value: string) => void;
  renderField: (field: AppFieldDefinition) => ReactNode;
}

function ShotFpsField({
  field,
  records,
  record,
  drafts,
  states,
  errors,
  setDraftValue,
}: ShotSpecialFieldContext) {
  const inheritedFps = projectDefaultFps(records, record);
  const currentFps = Number(drafts[field.column] ?? record?.[field.column]);
  const hasOverride =
    inheritedFps !== undefined &&
    Number.isFinite(currentFps) &&
    currentFps !== inheritedFps;
  const state = states[field.column] ?? "saved";
  const error = errors[field.column];
  const descriptor = createRecordFieldDescriptor({
    field: SHOT_FIELDS.fps,
    column: field.column,
    value: drafts[field.column] ?? "",
    recordId: typeof record?.id === "string" ? record.id : undefined,
    recordType: "shots",
    error,
    state: error || state === "invalid" || state === "failed"
      ? "invalid"
      : hasOverride
        ? "local"
        : "default",
    placeholder:
      inheritedFps !== undefined ? String(inheritedFps) : undefined,
    onWrite: (nextValue) => setDraftValue(field.column, String(nextValue ?? "")),
  });
  const fpsDescriptor = {
    ...descriptor,
    parentValue: inheritedFps,
    defaultValue: inheritedFps ?? descriptor.defaultValue,
    resolvedValue: Number.isFinite(currentFps)
      ? currentFps
      : inheritedFps ?? descriptor.resolvedValue,
    canRestore: hasOverride && inheritedFps !== undefined,
    actions: {
      ...descriptor.actions,
      restore:
        hasOverride && inheritedFps !== undefined
          ? () => setDraftValue(field.column, String(inheritedFps))
          : undefined,
    },
  };
  return (
    <EditorFieldRow
      key={field.column}
      className={`record-editor-field record-editor-field-${field.kind} ${DICTIONARY_FIELD_CLASS}`}
      descriptor={fpsDescriptor}
    >
      <DictionaryFieldControl
        {...toDictionaryFieldControlProps(fpsDescriptor)}
      />
    </EditorFieldRow>
  );
}

function ReadonlyShotField({
  field,
  value,
  column,
  record,
}: {
  field: FieldDefinition;
  value: unknown;
  column: string;
  record: AppRecord | undefined;
}) {
  const descriptor = createRecordFieldDescriptor({
    field,
    column,
    value,
    recordId: typeof record?.id === "string" ? record.id : undefined,
    recordType: "shots",
    readonly: true,
    onWrite: () => undefined,
  });
  return (
    <EditorFieldRow
      key={column}
      className={`record-editor-field record-editor-field-string is-readonly ${DICTIONARY_FIELD_CLASS}`}
      descriptor={descriptor}
    >
      <DictionaryFieldControl {...toDictionaryFieldControlProps(descriptor)} />
    </EditorFieldRow>
  );
}

function ShotDurationField({
  records,
  record,
}: ShotSpecialFieldContext) {
  const calculatedDuration = shotCalculatedDurationFrames(records, record);
  return (
    <ReadonlyShotField
      field={SHOT_FIELDS.durationFrames}
      column="duration_frames"
      value={calculatedDuration ?? record?.duration_frames ?? ""}
      record={record}
    />
  );
}

function ShotOwnerDeviceField({
  records,
  record,
  drafts,
}: Pick<ShotSpecialFieldContext, "records" | "record" | "drafts">) {
  return (
    <ReadonlyShotField
      field={SHOT_FIELDS.ownerDevice}
      column="owner_device"
      value={shotOwnerDeviceName({ records, record, drafts }) ?? ""}
      record={record}
    />
  );
}

export function renderShotSpecialField(
  context: ShotSpecialFieldContext,
): ReactNode | undefined {
  const { field, records, record, drafts, renderField } = context;
  if (isHiddenShotField(field.column)) return null;
  if (field.column === "fps") {
    return <ShotFpsField {...context} />;
  }
  if (field.column === "duration_frames") {
    return <ShotDurationField {...context} />;
  }
  if (field.column === "owner_actor_id") {
    return (
      <>
        {renderField(field)}
        <ShotOwnerDeviceField records={records} record={record} drafts={drafts} />
      </>
    );
  }
  if (field.column === "version") {
    return (
      <>
        {renderField(field)}
        <ReadonlyShotField
          field={SHOT_FIELDS.renderName}
          column="render_name"
          value={renderShotName({ records, record, drafts })}
          record={record}
        />
      </>
    );
  }
  return undefined;
}
