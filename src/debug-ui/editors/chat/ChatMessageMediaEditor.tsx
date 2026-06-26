import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type { JsonPath, JsonValue } from "../../components/json-editor/jsonEditorUtils.js";

interface MediaNumberField {
  label: string;
  path: JsonPath;
  value: number;
  fallback: number;
}

interface ChatMessageMediaEditorProps {
  mediaType: string;
  filePath: string;
  canBrowse: boolean;
  numberFields: MediaNumberField[];
  onMediaTypeChange: (mediaType: string) => void;
  onFilePathChange: (filePath: string) => void;
  onBrowseFile: () => void;
  onNumberFieldChange: (path: JsonPath, value: JsonValue) => void;
}

export function ChatMessageMediaEditor({
  mediaType,
  filePath,
  canBrowse,
  numberFields,
  onMediaTypeChange,
  onFilePathChange,
  onBrowseFile,
  onNumberFieldChange,
}: ChatMessageMediaEditorProps) {
  return (
    <details className="record-editor-content-nested-card" open>
      <summary>
        <span>Media</span>
        <small>{mediaType}</small>
      </summary>
      <div className="record-editor-content-fields">
        <InspectorFieldRow
          className="record-editor-content-field-row"
          label={<span>Type</span>}
          control={
            <select
              className="json-value-control"
              value={mediaType}
              onChange={(event) => onMediaTypeChange(event.target.value)}
            >
              <option value="none">None</option>
              <option value="image">Image</option>
              <option value="video">Video</option>
            </select>
          }
        />
        {mediaType === "image" || mediaType === "video" ? (
          <>
            <InspectorFieldRow
              className="record-editor-content-field-row"
              label={<span>File path</span>}
              control={
                <div className="media-file-control">
                  <DeferredTextInput
                    value={filePath}
                    onCommit={onFilePathChange}
                  />
                  <button
                    type="button"
                    className="record-editor-compact-button"
                    disabled={!canBrowse}
                    onClick={onBrowseFile}
                  >
                    Browse…
                  </button>
                </div>
              }
            />
            {numberFields.map(({ label, path, value, fallback }) => (
              <InspectorFieldRow
                key={String(label)}
                className="record-editor-content-field-row"
                label={<span>{String(label)}</span>}
                control={
                  <input
                    className="json-value-control"
                    type="number"
                    step={String(label).includes("scale") ? "0.05" : "1"}
                    value={Number(value ?? fallback)}
                    onChange={(event) =>
                      onNumberFieldChange(path, Number(event.target.value))
                    }
                  />
                }
              />
            ))}
          </>
        ) : null}
      </div>
    </details>
  );
}
