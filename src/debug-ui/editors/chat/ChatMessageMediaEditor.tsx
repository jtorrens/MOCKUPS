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
  productionId: string;
  playMode: string;
  playStartFrame: number;
  canBrowse: boolean;
  numberFields: MediaNumberField[];
  onMediaTypeChange: (mediaType: string) => void;
  onFilePathChange: (filePath: string) => void;
  onPlayModeChange: (playMode: string) => void;
  onPlayStartFrameChange: (playStartFrame: number) => void;
  onBrowseFile: () => void;
  onNumberFieldChange: (path: JsonPath, value: JsonValue) => void;
}

export function ChatMessageMediaEditor({
  mediaType,
  filePath,
  mediaRoot,
  productionId,
  playMode,
  playStartFrame,
  canBrowse,
  numberFields,
  onMediaTypeChange,
  onFilePathChange,
  onPlayModeChange,
  onPlayStartFrameChange,
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
  const numberFieldRows = pairLogicalNumberFields(numberFields);

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
              productionId={productionId}
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
            {mediaType === "video" ? (
              <>
                <InspectorFieldRow
                  className="record-editor-content-field-row"
                  label={<span>Play</span>}
                  control={
                    <select
                      className="json-value-control"
                      value={playMode === "loop" ? "loop" : "once"}
                      onChange={(event) => onPlayModeChange(event.target.value)}
                    >
                      <option value="once">Once</option>
                      <option value="loop">Loop</option>
                    </select>
                  }
                />
                <InspectorFieldRow
                  className="record-editor-content-field-row"
                  label={<span>Play start frame</span>}
                  control={
                    <DeferredNumberInput
                      ariaLabel="Play start frame"
                      min={0}
                      step={1}
                      value={Math.max(0, Number(playStartFrame) || 0)}
                      onCommit={onPlayStartFrameChange}
                    />
                  }
                />
              </>
            ) : null}
            <div className="chat-media-number-grid">
              {numberFieldRows.map((row) => (
                <div
                  key={row.map((field) => pathKey(field.path)).join("|")}
                  className={
                    row.length > 1
                      ? "chat-media-number-pair"
                      : "chat-media-number-pair chat-media-number-pair-single"
                  }
                >
                  {row.map(({ label, path, value, fallback }) => (
                    <InspectorFieldRow
                      key={String(label)}
                      className="record-editor-content-field-row chat-media-number-field"
                      label={<span>{String(label)}</span>}
                      control={
                        <DeferredNumberInput
                          ariaLabel={String(label)}
                          min={String(label).toLowerCase().includes("scale") ? 0.01 : undefined}
                          step={String(label).toLowerCase().includes("scale") ? 0.05 : 1}
                          value={Number(value ?? fallback)}
                          onCommit={(nextValue) =>
                            onNumberFieldChange(path, nextValue)
                          }
                        />
                      }
                    />
                  ))}
                </div>
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

function pairLogicalNumberFields(fields: MediaNumberField[]) {
  const rows: MediaNumberField[][] = [];
  const used = new Set<string>();
  const pairs = [
    [
      ["media", "window", "width"],
      ["media", "window", "height"],
    ],
    [
      ["media", "window", "offsetX"],
      ["media", "window", "offsetY"],
    ],
    [["media", "transform", "scale"]],
    [
      ["media", "transform", "translateX"],
      ["media", "transform", "translateY"],
    ],
  ].map((row) => row.map((path) => pathKey(path)));

  for (const pair of pairs) {
    const row = pair
      .map((key) => fields.find((field) => pathKey(field.path) === key))
      .filter((field): field is MediaNumberField => Boolean(field));
    if (row.length > 0) {
      rows.push(row);
      row.forEach((field) => used.add(pathKey(field.path)));
    }
  }

  for (const field of fields) {
    if (!used.has(pathKey(field.path))) {
      rows.push([field]);
    }
  }

  return rows;
}

function ChatBubbleMediaPreview({
  containerHeight,
  containerWidth,
  filePath,
  mediaRoot,
  mediaType,
  productionId,
  scale,
  translateX,
  translateY,
}: {
  containerHeight: number;
  containerWidth: number;
  filePath: string;
  mediaRoot: string;
  mediaType: string;
  productionId: string;
  scale: number;
  translateX: number;
  translateY: number;
}) {
  const previewUrl = useMediaPreviewUrl({
    enabled: mediaType === "image",
    filePath,
    mediaRoot,
  });
  const videoPosterUrl =
    mediaType === "video" && productionId && filePath
      ? `/api/media-frame?productionId=${encodeURIComponent(productionId)}&path=${encodeURIComponent(filePath)}&frame=0&fps=30`
      : "";

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
        {videoPosterUrl ? (
          <div
            className="chat-media-preview-image"
            style={{
              backgroundImage: cssUrl(videoPosterUrl),
              backgroundPosition: `calc(50% + ${translateX}px) calc(50% + ${translateY}px)`,
              backgroundSize: `${Math.max(0.01, scale) * 100}%`,
            }}
          />
        ) : previewUrl ? (
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
