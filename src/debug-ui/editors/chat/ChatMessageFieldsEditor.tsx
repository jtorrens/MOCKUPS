import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type { JsonPath, JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import { ChatMessageMediaEditor } from "./ChatMessageMediaEditor.js";

interface ActorOption {
  value: string;
  label: string;
}

interface MediaNumberField {
  label: string;
  path: JsonPath;
  value: number;
  fallback: number;
}

interface ChatMessageFieldsEditorProps {
  direction: string;
  actorId: string;
  actorOptions: ActorOption[];
  startFrame: number;
  writeOnDurationFrames: number;
  showBubbleBackground: boolean;
  textScale: number;
  text: string;
  statusText: string;
  deliveryStatus: string;
  textRevealMode: string;
  mediaType: string;
  mediaFilePath: string;
  mediaPlayMode: string;
  mediaPlayStartFrame: number;
  mediaRoot: string;
  productionId: string;
  canBrowseMedia: boolean;
  mediaNumberFields: MediaNumberField[];
  onDirectionChange: (direction: string) => void;
  onActorChange: (actorId: string) => void;
  onStartFrameChange: (frame: number) => void;
  onWriteOnDurationFramesChange: (frameCount: number) => void;
  onShowBubbleBackgroundChange: (show: boolean) => void;
  onTextScaleChange: (scale: number) => void;
  onTextChange: (text: string) => void;
  onStatusTextChange: (text: string) => void;
  onDeliveryStatusChange: (status: string) => void;
  onTextRevealModeChange: (mode: string) => void;
  onMediaTypeChange: (mediaType: string) => void;
  onMediaFilePathChange: (filePath: string) => void;
  onMediaPlayModeChange: (playMode: string) => void;
  onMediaPlayStartFrameChange: (playStartFrame: number) => void;
  onBrowseMedia: () => void;
  onMediaNumberFieldChange: (path: JsonPath, value: JsonValue) => void;
}

export function ChatMessageFieldsEditor({
  direction,
  actorId,
  actorOptions,
  startFrame,
  writeOnDurationFrames,
  showBubbleBackground,
  textScale,
  text,
  statusText,
  deliveryStatus,
  textRevealMode,
  mediaType,
  mediaFilePath,
  mediaPlayMode,
  mediaPlayStartFrame,
  mediaRoot,
  productionId,
  canBrowseMedia,
  mediaNumberFields,
  onDirectionChange,
  onActorChange,
  onStartFrameChange,
  onWriteOnDurationFramesChange,
  onShowBubbleBackgroundChange,
  onTextScaleChange,
  onTextChange,
  onStatusTextChange,
  onDeliveryStatusChange,
  onTextRevealModeChange,
  onMediaTypeChange,
  onMediaFilePathChange,
  onMediaPlayModeChange,
  onMediaPlayStartFrameChange,
  onBrowseMedia,
  onMediaNumberFieldChange,
}: ChatMessageFieldsEditorProps) {
  return (
    <div className="record-editor-content-fields">
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Type</span>}
        control={
          <select
            className="json-value-control"
            value={direction}
            onChange={(event) => onDirectionChange(event.target.value)}
          >
            <option value="received">Recibido</option>
            <option value="sent">Enviado</option>
            <option value="system">Sistema</option>
          </select>
        }
      />
      {direction !== "system" ? (
        <div className="record-editor-content-nested-panel">
          <InspectorFieldRow
            className="record-editor-content-field-row"
            label={<span>Actor</span>}
            control={
              <select
                className="json-value-control"
                value={actorId}
                onChange={(event) => onActorChange(event.target.value)}
              >
                {!actorOptions.some((option) => option.value === actorId) &&
                actorId ? (
                  <option value={actorId}>Current actor</option>
                ) : null}
                {actorOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            }
          />
        </div>
      ) : null}
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Start frame</span>}
        control={
          <input
            className="json-value-control"
            type="number"
            min={0}
            step={1}
            value={startFrame}
            onChange={(event) => onStartFrameChange(Number(event.target.value))}
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Write-on duration</span>}
        control={
          <input
            className="json-value-control"
            type="number"
            min={0}
            step={1}
            value={writeOnDurationFrames}
            onChange={(event) =>
              onWriteOnDurationFramesChange(Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Show bubble background</span>}
        control={
          <input
            type="checkbox"
            checked={showBubbleBackground}
            onChange={(event) =>
              onShowBubbleBackgroundChange(event.target.checked)
            }
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Text scale</span>}
        control={
          <input
            className="json-value-control"
            type="number"
            step="0.05"
            value={textScale}
            onChange={(event) => onTextScaleChange(Number(event.target.value))}
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Message text</span>}
        control={
          <DeferredTextInput
            multiline
            value={text}
            onCommit={onTextChange}
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Status text</span>}
        control={
          <DeferredTextInput
            value={statusText}
            onCommit={onStatusTextChange}
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Delivery status</span>}
        control={
          <select
            className="json-value-control"
            value={deliveryStatus}
            onChange={(event) => onDeliveryStatusChange(event.target.value)}
          >
            <option value="none">None</option>
            <option value="sent">Sent</option>
            <option value="delivered">Delivered</option>
            <option value="read">Read</option>
            <option value="failed">Failed</option>
          </select>
        }
      />
      <ChatMessageMediaEditor
        mediaType={mediaType}
        filePath={mediaFilePath}
        mediaRoot={mediaRoot}
        productionId={productionId}
        playMode={mediaPlayMode}
        playStartFrame={mediaPlayStartFrame}
        canBrowse={canBrowseMedia}
        numberFields={mediaNumberFields}
        onMediaTypeChange={onMediaTypeChange}
        onFilePathChange={onMediaFilePathChange}
        onPlayModeChange={onMediaPlayModeChange}
        onPlayStartFrameChange={onMediaPlayStartFrameChange}
        onBrowseFile={onBrowseMedia}
        onNumberFieldChange={onMediaNumberFieldChange}
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Text reveal mode</span>}
        control={
          <select
            className="json-value-control"
            value={textRevealMode}
            onChange={(event) => onTextRevealModeChange(event.target.value)}
          >
            <option value="simple_write_on">Simple write down</option>
            <option value="natural_write_on">Write down natural</option>
            <option value="waiting_dots">Waiting dots animation</option>
          </select>
        }
      />
    </div>
  );
}
