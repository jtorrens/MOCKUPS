import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type { JsonPath, JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import { ChatMessageMediaEditor } from "./ChatMessageMediaEditor.js";

interface ParticipantOption {
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
  senderId: string;
  receivedOptions: ParticipantOption[];
  showBubbleBackground: boolean;
  textScale: number;
  text: string;
  textRevealMode: string;
  mediaType: string;
  mediaFilePath: string;
  canBrowseMedia: boolean;
  mediaNumberFields: MediaNumberField[];
  onDirectionChange: (direction: string) => void;
  onSenderChange: (senderId: string) => void;
  onShowBubbleBackgroundChange: (show: boolean) => void;
  onTextScaleChange: (scale: number) => void;
  onTextChange: (text: string) => void;
  onTextRevealModeChange: (mode: string) => void;
  onMediaTypeChange: (mediaType: string) => void;
  onMediaFilePathChange: (filePath: string) => void;
  onBrowseMedia: () => void;
  onMediaNumberFieldChange: (path: JsonPath, value: JsonValue) => void;
}

export function ChatMessageFieldsEditor({
  direction,
  senderId,
  receivedOptions,
  showBubbleBackground,
  textScale,
  text,
  textRevealMode,
  mediaType,
  mediaFilePath,
  canBrowseMedia,
  mediaNumberFields,
  onDirectionChange,
  onSenderChange,
  onShowBubbleBackgroundChange,
  onTextScaleChange,
  onTextChange,
  onTextRevealModeChange,
  onMediaTypeChange,
  onMediaFilePathChange,
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
      {direction === "received" ? (
        <div className="record-editor-content-nested-panel">
          <InspectorFieldRow
            className="record-editor-content-field-row"
            label={<span>Participant</span>}
            control={
              <select
                className="json-value-control"
                value={senderId}
                onChange={(event) => onSenderChange(event.target.value)}
              >
                {receivedOptions.map((option) => (
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
        control={<DeferredTextInput value={text} onCommit={onTextChange} />}
      />
      <ChatMessageMediaEditor
        mediaType={mediaType}
        filePath={mediaFilePath}
        canBrowse={canBrowseMedia}
        numberFields={mediaNumberFields}
        onMediaTypeChange={onMediaTypeChange}
        onFilePathChange={onMediaFilePathChange}
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
