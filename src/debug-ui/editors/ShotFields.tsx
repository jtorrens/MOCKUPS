import type { ReactNode } from "react";
import {
  type AppFieldDefinition,
  type AppRecord,
} from "../api/client.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "../components/inspector/InspectorFieldRow.js";
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
  return (
    <InspectorFieldRow
      key={field.column}
      className={`record-editor-field record-editor-field-${field.kind} state-${state} ${
        hasOverride ? "json-override" : ""
      }`}
      state={
        state === "invalid" || state === "failed"
          ? "invalid"
          : hasOverride
            ? "override"
            : "default"
      }
      label={<span>{field.label}</span>}
      meta={
        inheritedFps !== undefined ? (
          <code>{`Project default: ${inheritedFps}`}</code>
        ) : null
      }
      control={
        <>
          <input
            data-testid={`field-${field.column}`}
            type="number"
            value={drafts[field.column] ?? ""}
            onChange={(event) => setDraftValue(field.column, event.target.value)}
          />
          {error ? <strong>{error}</strong> : null}
        </>
      }
      restore={
        hasOverride && inheritedFps !== undefined ? (
          <InspectorRestoreButton
            label="Restore project FPS"
            onClick={() => setDraftValue(field.column, String(inheritedFps))}
          />
        ) : null
      }
    />
  );
}

function ShotDurationField({
  field,
  records,
  record,
}: ShotSpecialFieldContext) {
  const calculatedDuration = shotCalculatedDurationFrames(records, record);
  return (
    <InspectorFieldRow
      key={field.column}
      className="record-editor-field record-editor-field-number is-readonly"
      label={<span>{field.label}</span>}
      meta={
        calculatedDuration !== undefined ? (
          <code>Calculated from screens</code>
        ) : null
      }
      control={
        <input
          disabled
          value={String(calculatedDuration ?? record?.duration_frames ?? "")}
        />
      }
    />
  );
}

function ShotOwnerDeviceField({
  records,
  record,
  drafts,
}: Pick<ShotSpecialFieldContext, "records" | "record" | "drafts">) {
  return (
    <InspectorFieldRow
      key="owner_device"
      className="record-editor-field record-editor-field-string is-readonly"
      label={<span>Device</span>}
      control={<input disabled value={shotOwnerDeviceName({ records, record, drafts }) ?? ""} />}
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
        <InspectorFieldRow
          key="render_name"
          className="record-editor-field record-editor-field-string is-readonly"
          label={<span>Render name</span>}
          control={
            <input
              disabled
              value={renderShotName({ records, record, drafts })}
            />
          }
        />
      </>
    );
  }
  return undefined;
}
