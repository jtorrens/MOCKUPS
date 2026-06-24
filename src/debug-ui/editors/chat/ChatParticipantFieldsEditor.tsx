import { useEffect, useState } from "react";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "../../components/inspector/InspectorFieldRow.js";
import type { AppRecord } from "../../api/client.js";
import type { JsonValue } from "../../components/json-editor/jsonEditorUtils.js";

interface ChatParticipantFieldsEditorProps {
  participant: Record<string, JsonValue>;
  actorOptions: AppRecord[];
  actorId: string;
  displayName: string;
  inheritedDisplayName: string;
  actorTitleForRecord: (actor: AppRecord) => string;
  onActorChange: (actorId: string) => void;
  onDisplayNameChange: (displayName: string) => void;
  onRoleChange: (role: string) => void;
}

function ParticipantDisplayNameInput({
  value,
  inheritedValue,
  onCommit,
}: {
  value: string;
  inheritedValue: string;
  onCommit: (nextValue: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  const hasOverride = Boolean(inheritedValue) && draft !== inheritedValue;

  useEffect(() => {
    setDraft(value);
  }, [value]);

  function commit() {
    if (draft !== value) {
      onCommit(draft);
    }
  }

  return (
    <InspectorFieldRow
      className={`record-editor-content-field-row ${
        hasOverride ? "json-override" : ""
      }`}
      state={hasOverride ? "override" : "default"}
      label={<span>Display name</span>}
      meta={inheritedValue ? <code>{`User: ${inheritedValue}`}</code> : null}
      control={
        <input
          className="json-value-control"
          value={draft}
          onBlur={commit}
          onChange={(event) => {
            setDraft(event.target.value);
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.currentTarget.blur();
            }
          }}
        />
      }
      restore={
        hasOverride ? (
          <InspectorRestoreButton
            label="Restore user display name"
            onClick={() => {
              setDraft(inheritedValue);
              onCommit(inheritedValue);
            }}
          />
        ) : null
      }
    />
  );
}

export function ChatParticipantFieldsEditor({
  participant,
  actorOptions,
  actorId,
  displayName,
  inheritedDisplayName,
  actorTitleForRecord,
  onActorChange,
  onDisplayNameChange,
  onRoleChange,
}: ChatParticipantFieldsEditorProps) {
  return (
    <div className="record-editor-content-fields">
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>User</span>}
        control={
          <select
            className="json-value-control"
            value={actorId}
            onChange={(event) => onActorChange(event.target.value)}
          >
            <option value="">No linked user</option>
            {actorOptions.map((actor) => (
              <option key={String(actor.id)} value={String(actor.id)}>
                {actorTitleForRecord(actor)}
              </option>
            ))}
          </select>
        }
      />
      <ParticipantDisplayNameInput
        value={displayName}
        inheritedValue={inheritedDisplayName}
        onCommit={onDisplayNameChange}
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Role</span>}
        control={
          <select
            className="json-value-control"
            value={String(participant.role ?? "participant")}
            onChange={(event) => onRoleChange(event.target.value)}
          >
            <option value="owner">Owner</option>
            <option value="participant">Participant</option>
            <option value="system">System</option>
          </select>
        }
      />
    </div>
  );
}
