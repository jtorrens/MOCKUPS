import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "../../components/inspector/InspectorFieldRow.js";
import type { JsonValue } from "../../components/json-editor/jsonEditorUtils.js";

interface ChatHeaderFieldsEditorProps {
  header: Record<string, JsonValue>;
  inheritedTitle: string;
  actorOptions: Array<{ value: string; label: string }>;
  onChange: (
    key: "title" | "subtitle" | "actorId" | "useContactColor",
    value: string | boolean,
  ) => void;
}

export function ChatHeaderFieldsEditor({
  header,
  inheritedTitle,
  actorOptions,
  onChange,
}: ChatHeaderFieldsEditorProps) {
  const title = String(header.title ?? inheritedTitle ?? "");
  const hasTitleOverride = Boolean(inheritedTitle) && title !== inheritedTitle;

  return (
    <div className="record-editor-content-fields">
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Actor</span>}
        control={
          <select
            className="json-value-control"
            value={String(header.actorId ?? "")}
            onChange={(event) =>
              onChange("actorId", event.target.value)
            }
          >
            <option value="">None</option>
            {!actorOptions.some((option) => option.value === String(header.actorId ?? "")) &&
            String(header.actorId ?? "") ? (
              <option value={String(header.actorId)}>Current actor</option>
            ) : null}
            {actorOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        }
      />
      <InspectorFieldRow
        className={`record-editor-content-field-row ${
          hasTitleOverride ? "json-override" : ""
        }`}
        state={hasTitleOverride ? "override" : "default"}
        label={<span>Title</span>}
        control={
          <DeferredTextInput
            className="json-value-control"
            value={title}
            onCommit={(nextValue) => onChange("title", nextValue)}
          />
        }
        restore={
          hasTitleOverride ? (
            <InspectorRestoreButton
              label="Restore user title"
              onClick={() => onChange("title", inheritedTitle)}
            />
          ) : null
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Subtitle</span>}
        control={
          <DeferredTextInput
            className="json-value-control"
            value={String(header.subtitle ?? "")}
            onCommit={(nextValue) => onChange("subtitle", nextValue)}
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={
          <span>
            <span>Use actor color</span>
            <div className="record-editor-field-note">
              Header background uses selected actor color
            </div>
          </span>
        }
        control={
          <input
            type="checkbox"
            checked={header.useContactColor === true}
            onChange={(event) =>
              onChange("useContactColor", event.target.checked)
            }
          />
        }
      />
    </div>
  );
}
