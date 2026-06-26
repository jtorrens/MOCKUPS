import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import { DeferredNumberInput } from "../../editor-ui/DeferredNumberInput.js";
import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type { JsonPath, JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import { cssUrl, useMediaPreviewUrl } from "../MediaPreviews.js";

interface MediaNumberField {
  label: string;
  path: JsonPath;
  value: number;
  fallback: number;
}

interface ChatMessageMediaEditorProps {
  mediaType: string;
  filePath: string;
  mediaRoot: string;
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
  mediaRoot,
  canBrowse,
  numberFields,
  onMediaTypeChange,
  onFilePathChange,
  onBrowseFile,
  onNumberFieldChange,
}: ChatMessageMediaEditorProps) {
  function numberFieldValue(path: JsonPath, fallback: number) {
    return (
      numberFields.find((field) => pathKey(field.path) === pathKey(path))?.value ??
      fallback
    );
  }

  const containerWidth = numberFieldValue(["media", "window", "width"], 360);
  const containerHeight = numberFieldValue(["media", "window", "height"], 240);
  const mediaScale = numberFieldValue(["media", "transform", "scale"], 1);
  const mediaX = numberFieldValue(["media", "transform", "translateX"], 0);
  const mediaY = numberFieldValue(["media", "transform", "translateY"], 0);

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
            <ChatBubbleMediaPreview
              containerHeight={containerHeight}
              containerWidth={containerWidth}
              filePath={filePath}
              mediaRoot={mediaRoot}
              mediaType={mediaType}
              scale={mediaScale}
              translateX={mediaX}
              translateY={mediaY}
            />
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
            <div className="chat-media-number-grid">
              {numberFields.map(({ label, path, value, fallback }) => (
                <InspectorFieldRow
                  key={String(label)}
                  className="record-editor-content-field-row chat-media-number-field"
                  label={<span>{String(label)}</span>}
                  control={
                    <DeferredNumberInput
                      ariaLabel={String(label)}
                      min={String(label).includes("scale") ? 0.01 : undefined}
                      step={String(label).includes("scale") ? 0.05 : 1}
                      value={Number(value ?? fallback)}
                      onCommit={(nextValue) =>
                        onNumberFieldChange(path, nextValue)
                      }
                    />
                  }
                />
              ))}
            </div>
          </>
        ) : null}
      </div>
    </details>
  );
}

function pathKey(path: JsonPath) {
  return path.map(String).join(".");
}

function ChatBubbleMediaPreview({
  containerHeight,
  containerWidth,
  filePath,
  mediaRoot,
  mediaType,
  scale,
  translateX,
  translateY,
}: {
  containerHeight: number;
  containerWidth: number;
  filePath: string;
  mediaRoot: string;
  mediaType: string;
  scale: number;
  translateX: number;
  translateY: number;
}) {
  const previewUrl = useMediaPreviewUrl({
    enabled: mediaType === "image",
    filePath,
    mediaRoot,
  });

  const safeWidth = Math.max(1, containerWidth);
  const safeHeight = Math.max(1, containerHeight);
  const previewWidth = Math.min(260, safeWidth);
  const previewHeight = Math.max(70, Math.round((previewWidth * safeHeight) / safeWidth));

  return (
    <div className="chat-media-preview-wrap">
      <div
        className="chat-media-preview-frame"
        style={{
          width: previewWidth,
          height: previewHeight,
        }}
      >
        {previewUrl ? (
          <div
            className="chat-media-preview-image"
            style={{
              backgroundImage: cssUrl(previewUrl),
              backgroundPosition: `calc(50% + ${translateX}px) calc(50% + ${translateY}px)`,
              backgroundSize: `${Math.max(0.01, scale) * 100}%`,
            }}
          />
        ) : (
          <span>{mediaType === "video" ? "Video container" : "Image preview"}</span>
        )}
      </div>
      <small>
        Container {Math.round(safeWidth)}×{Math.round(safeHeight)} · scale{" "}
        {Number(scale).toFixed(2)}
      </small>
    </div>
  );
}
