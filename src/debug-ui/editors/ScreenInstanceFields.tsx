import type { AppRecord } from "../api/client.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

interface ScreenTransitionFieldsProps {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
  setDraftValue: (column: string, value: string) => void;
}

export function ScreenTransitionFields({
  drafts,
  setDraftValue,
}: ScreenTransitionFieldsProps) {
  const root = parsedObject(drafts.transition_in_json ?? "{}");
  const type =
    root.type === "none" || root.type === "dissolve" || root.type === "overlay"
      ? String(root.type)
      : "none";
  const durationFrames = Math.max(
    0,
    Number(root.durationFrames ?? root.duration_frames ?? 0),
  );

  function updateTransition(patch: Record<string, unknown>) {
    const next = {
      ...root,
      ...patch,
    };
    if (next.type === "none" || Number(next.durationFrames ?? 0) <= 0) {
      setDraftValue("transition_in_json", stringifyJson({ type: "none", durationFrames: 0 }));
      return;
    }
    setDraftValue("transition_in_json", stringifyJson(next));
  }

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
              updateTransition({
                type: event.target.value,
                durationFrames,
              })
            }
          >
            <option value="none">None</option>
            <option value="overlay">Overlay</option>
            <option value="dissolve">Dissolve</option>
          </select>
        }
      />
      <InspectorFieldRow
        key="transition_duration"
        className="record-editor-field record-editor-field-number"
        label={<span>Duration frames</span>}
        meta={<code>Starts before this screen</code>}
        control={
          <input
            type="number"
            min={0}
            value={String(durationFrames)}
            onChange={(event) =>
              updateTransition({
                type,
                durationFrames: Math.max(0, Number(event.target.value)),
              })
            }
          />
        }
      />
    </>
  );
}
