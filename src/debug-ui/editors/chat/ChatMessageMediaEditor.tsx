import { CHAT_CONTENT_MEDIA_FIELDS } from "../../../domain/fields/chatFields.js";
import type { FieldDefinition } from "../../../domain/value-system/index.js";
import type { JsonPath, JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import { cssUrl, useMediaPreviewUrl } from "../MediaPreviews.js";
import { ChatDictionaryFieldRow } from "./ChatDictionaryFieldRow.js";

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
  durationSeconds: number;
  playMode: string;
  playStartFrame: number;
  canBrowse: boolean;
  numberFields: MediaNumberField[];
  onMediaTypeChange: (mediaType: string) => void;
  onFilePathChange: (filePath: string) => void;
  onDurationSecondsChange: (durationSeconds: number) => void;
  onPlayModeChange: (playMode: string) => void;
  onPlayStartFrameChange: (playStartFrame: number) => void;
  onBrowseFile: () => Promise<string | undefined>;
  onNumberFieldChange: (path: JsonPath, value: JsonValue) => void;
}

export function ChatMessageMediaEditor({
  mediaType,
  filePath,
  mediaRoot,
  productionId,
  durationSeconds,
  playMode,
  playStartFrame,
  canBrowse,
  numberFields,
  onMediaTypeChange,
  onFilePathChange,
  onDurationSecondsChange,
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
        <ChatDictionaryFieldRow
          field={CHAT_CONTENT_MEDIA_FIELDS.type}
          value={mediaType}
          onChange={(nextValue) => onMediaTypeChange(String(nextValue))}
        />
        {mediaType === "audio" || mediaType === "video" ? (
          <>
            <ChatDictionaryFieldRow
              field={CHAT_CONTENT_MEDIA_FIELDS.durationSeconds}
              value={Math.max(0.1, Number(durationSeconds) || 8)}
              onChange={(nextValue) => onDurationSecondsChange(Number(nextValue))}
            />
            <ChatDictionaryFieldRow
              field={CHAT_CONTENT_MEDIA_FIELDS.playStartFrame}
              value={Math.max(0, Number(playStartFrame) || 0)}
              onChange={(nextValue) => onPlayStartFrameChange(Number(nextValue))}
            />
          </>
        ) : null}
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
            <ChatDictionaryFieldRow
              field={CHAT_CONTENT_MEDIA_FIELDS.filePath}
              fileBrowser={
                canBrowse
                  ? {
                      pickFile: async () => {
                        const nextPath = await onBrowseFile();
                        return nextPath ? [nextPath] : [];
                      },
                    }
                  : undefined
              }
              mediaRoot={mediaRoot}
              value={filePath}
              onChange={(nextValue) => onFilePathChange(String(nextValue))}
            />
            {mediaType === "video" ? (
              <>
                <ChatDictionaryFieldRow
                  field={CHAT_CONTENT_MEDIA_FIELDS.playMode}
                  value={playMode === "loop" ? "loop" : "once"}
                  onChange={(nextValue) => onPlayModeChange(String(nextValue))}
                />
                <ChatDictionaryFieldRow
                  field={CHAT_CONTENT_MEDIA_FIELDS.playStartFrame}
                  value={Math.max(0, Number(playStartFrame) || 0)}
                  onChange={(nextValue) => onPlayStartFrameChange(Number(nextValue))}
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
                    <ChatDictionaryFieldRow
                      key={String(label)}
                      className="chat-media-number-field"
                      field={mediaFieldForPath(path, String(label))}
                      value={Number(value ?? fallback)}
                      onChange={(nextValue) =>
                        onNumberFieldChange(path, Number(nextValue))
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

function mediaFieldForPath(path: JsonPath, label: string): FieldDefinition {
  const key = pathKey(path);
  if (key === "media.window.width") return CHAT_CONTENT_MEDIA_FIELDS.windowWidth;
  if (key === "media.window.height") return CHAT_CONTENT_MEDIA_FIELDS.windowHeight;
  if (key === "media.window.offsetX") return CHAT_CONTENT_MEDIA_FIELDS.windowOffsetX;
  if (key === "media.window.offsetY") return CHAT_CONTENT_MEDIA_FIELDS.windowOffsetY;
  if (key === "media.transform.scale") return CHAT_CONTENT_MEDIA_FIELDS.transformScale;
  if (key === "media.transform.translateX") {
    return CHAT_CONTENT_MEDIA_FIELDS.transformTranslateX;
  }
  if (key === "media.transform.translateY") {
    return CHAT_CONTENT_MEDIA_FIELDS.transformTranslateY;
  }
  if (key === "media.transform.rotationDegrees") {
    return CHAT_CONTENT_MEDIA_FIELDS.transformRotationDegrees;
  }
  return {
    id: `chat.content.message.${key}`,
    kind: "decimal",
    defaultValue: 0,
    ui: { label, step: 1 },
  };
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
