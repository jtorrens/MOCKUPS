import type { AppRecord } from "../api/client.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

function nextScreenInstance({
  records,
  record,
  drafts,
}: {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
}) {
  const startFrame = Number(drafts.start_frame ?? record?.start_frame ?? 0);
  return records.screen_instances
    ?.filter(
      (item) =>
        item.shot_id === record?.shot_id &&
        item.id !== record?.id &&
        Number(item.start_frame) >= startFrame,
    )
    .sort((left, right) => {
      const frameDelta = Number(left.start_frame) - Number(right.start_frame);
      return frameDelta || String(left.id).localeCompare(String(right.id));
    })[0];
}

function transitionOverlapFrames({
  records,
  record,
  drafts,
}: {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
}) {
  const next = nextScreenInstance({ records, record, drafts });
  if (!next) return 0;
  const currentEnd = Number(drafts.end_frame ?? record?.end_frame ?? 0);
  const nextStart = Number(next.start_frame ?? 0);
  const overlap = currentEnd - nextStart;
  return Number.isFinite(overlap) && overlap > 0 ? overlap : 0;
}

interface ScreenTransitionFieldsProps {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
  setDraftValue: (column: string, value: string) => void;
}

export function ScreenTransitionFields({
  records,
  record,
  drafts,
  setDraftValue,
}: ScreenTransitionFieldsProps) {
  const root = parsedObject(drafts.transition_out_json ?? "{}");
  const type =
    root.type === "dissolve" || root.type === "overlay"
      ? String(root.type)
      : "overlay";
  const overlap = transitionOverlapFrames({ records, record, drafts });
  return (
    <>
      <InspectorFieldRow
        key="transition_type"
        className="record-editor-field record-editor-field-string"
        label={<span>Transition</span>}
        control={
          <select
            value={type}
            onChange={(event) =>
              setDraftValue(
                "transition_out_json",
                stringifyJson({
                  ...root,
                  type: event.target.value,
                  duration_frames: overlap,
                }),
              )
            }
          >
            <option value="overlay">Overlay</option>
            <option value="dissolve">Dissolve</option>
          </select>
        }
      />
      <InspectorFieldRow
        key="transition_duration"
        className="record-editor-field record-editor-field-number is-readonly"
        label={<span>Duration frames</span>}
        meta={<code>Calculated from screen overlap</code>}
        control={<input disabled value={String(overlap)} />}
      />
    </>
  );
}
