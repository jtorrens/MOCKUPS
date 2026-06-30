import { CHAT_CONTENT_MESSAGE_FIELDS } from "../../../domain/fields/chatFields.js";
import type { JsonPath, JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import {
  AnimationFieldEditor,
  type AnimatableField,
} from "../../editor-ui/animation/AnimationFieldEditor.js";
import { ChatDictionaryFieldRow } from "./ChatDictionaryFieldRow.js";
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
  delayAfterPreviousFrames: number;
  writeOnDurationFrames: number;
  showBubbleBackground: boolean;
  textScale: number;
  text: string;
  statusText: string;
  deliveryStatus: string;
  textRevealMode: string;
  animation: Record<string, JsonValue>;
  timelineDurationFrames: number;
  mediaType: string;
  mediaFilePath: string;
  mediaDurationSeconds: number;
  mediaPlayMode: string;
  mediaPlayStartFrame: number;
  mediaRoot: string;
  productionId: string;
  canBrowseMedia: boolean;
  mediaNumberFields: MediaNumberField[];
  onDirectionChange: (direction: string) => void;
  onActorChange: (actorId: string) => void;
  onDelayAfterPreviousFramesChange: (frame: number) => void;
  onWriteOnDurationFramesChange: (frameCount: number) => void;
  onShowBubbleBackgroundChange: (show: boolean) => void;
  onTextScaleChange: (scale: number) => void;
  onTextChange: (text: string) => void;
  onStatusTextChange: (text: string) => void;
  onDeliveryStatusChange: (status: string) => void;
  onTextRevealModeChange: (mode: string) => void;
  onAnimationChange: (animation: Record<string, JsonValue>) => void;
  onMediaTypeChange: (mediaType: string) => void;
  onMediaFilePathChange: (filePath: string) => void;
  onMediaDurationSecondsChange: (durationSeconds: number) => void;
  onMediaPlayModeChange: (playMode: string) => void;
  onMediaPlayStartFrameChange: (playStartFrame: number) => void;
  onBrowseMedia: () => Promise<string | undefined>;
  onMediaNumberFieldChange: (path: JsonPath, value: JsonValue) => void;
  onAnimationFrameChange?: (frame: number) => void;
}

export function ChatMessageFieldsEditor({
  direction,
  actorId,
  actorOptions,
  delayAfterPreviousFrames,
  writeOnDurationFrames,
  showBubbleBackground,
  textScale,
  text,
  statusText,
  deliveryStatus,
  textRevealMode,
  animation,
  timelineDurationFrames,
  mediaType,
  mediaFilePath,
  mediaDurationSeconds,
  mediaPlayMode,
  mediaPlayStartFrame,
  mediaRoot,
  productionId,
  canBrowseMedia,
  mediaNumberFields,
  onDirectionChange,
  onActorChange,
  onDelayAfterPreviousFramesChange,
  onWriteOnDurationFramesChange,
  onShowBubbleBackgroundChange,
  onTextScaleChange,
  onTextChange,
  onStatusTextChange,
  onDeliveryStatusChange,
  onTextRevealModeChange,
  onAnimationChange,
  onMediaTypeChange,
  onMediaFilePathChange,
  onMediaDurationSecondsChange,
  onMediaPlayModeChange,
  onMediaPlayStartFrameChange,
  onBrowseMedia,
  onMediaNumberFieldChange,
  onAnimationFrameChange,
}: ChatMessageFieldsEditorProps) {
  const animatableFields: AnimatableField[] = [
    {
      key: "text",
      label: "Message text",
      valueType: "text",
      value: text,
      field: CHAT_CONTENT_MESSAGE_FIELDS.text,
      interpolationOptions: ["hold", "linear", "ease"],
    },
    {
      key: "status.text",
      label: "Status text",
      valueType: "text",
      value: statusText,
      field: CHAT_CONTENT_MESSAGE_FIELDS.statusText,
      interpolationOptions: ["hold", "linear", "ease"],
    },
    {
      key: "status.deliveryStatus",
      label: "Delivery status",
      valueType: "select",
      value: deliveryStatus,
      field: CHAT_CONTENT_MESSAGE_FIELDS.deliveryStatus,
      interpolationOptions: ["hold"],
      selectOptions: [
        { value: "none", label: "None" },
        { value: "sent", label: "Sent" },
        { value: "delivered", label: "Delivered" },
        { value: "read", label: "Read" },
        { value: "failed", label: "Failed" },
      ],
    },
  ];
  const actorSelectOptions = {
    allowEmpty: true,
    emptyLabel: "None",
    options: [
      ...(!actorOptions.some((option) => option.value === actorId) && actorId
        ? [{ value: actorId, label: "Current actor" }]
        : []),
      ...actorOptions,
    ],
  };

  return (
    <AnimationFieldEditor
      animation={animation}
      fields={animatableFields}
      timelineDurationFrames={timelineDurationFrames}
      onAnimationChange={onAnimationChange}
      onAnimationFrameChange={onAnimationFrameChange}
    >
      {({ animationCard, fieldLabel }) => (
        <div className="record-editor-content-fields">
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.direction}
        value={direction}
        onChange={(nextValue) => onDirectionChange(String(nextValue))}
      />
      {direction !== "system" ? (
        <div className="record-editor-content-nested-panel">
          <ChatDictionaryFieldRow
            field={CHAT_CONTENT_MESSAGE_FIELDS.actorId}
            value={actorId}
            selectOptions={actorSelectOptions}
            onChange={(nextValue) => onActorChange(String(nextValue))}
          />
        </div>
      ) : null}
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.delayAfterPreviousFrames}
        value={delayAfterPreviousFrames}
        onChange={(nextValue) =>
          onDelayAfterPreviousFramesChange(Number(nextValue))
        }
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.writeOnDurationFrames}
        value={writeOnDurationFrames}
        onChange={(nextValue) =>
          onWriteOnDurationFramesChange(Number(nextValue))
        }
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.showBubbleBackground}
        value={showBubbleBackground}
        onChange={(nextValue) =>
          onShowBubbleBackgroundChange(nextValue === true)
        }
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.textScale}
        value={textScale}
        onChange={(nextValue) => onTextScaleChange(Number(nextValue))}
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.text}
        labelOverride={fieldLabel("text")}
        value={text}
        onChange={(nextValue) => onTextChange(String(nextValue))}
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.statusText}
        labelOverride={fieldLabel("status.text")}
        value={statusText}
        onChange={(nextValue) => onStatusTextChange(String(nextValue))}
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.deliveryStatus}
        labelOverride={fieldLabel("status.deliveryStatus")}
        value={deliveryStatus}
        onChange={(nextValue) => onDeliveryStatusChange(String(nextValue))}
      />
      <ChatMessageMediaEditor
        mediaType={mediaType}
        filePath={mediaFilePath}
        durationSeconds={mediaDurationSeconds}
        mediaRoot={mediaRoot}
        productionId={productionId}
        playMode={mediaPlayMode}
        playStartFrame={mediaPlayStartFrame}
        canBrowse={canBrowseMedia}
        numberFields={mediaNumberFields}
        onMediaTypeChange={onMediaTypeChange}
        onFilePathChange={onMediaFilePathChange}
        onDurationSecondsChange={onMediaDurationSecondsChange}
        onPlayModeChange={onMediaPlayModeChange}
        onPlayStartFrameChange={onMediaPlayStartFrameChange}
        onBrowseFile={onBrowseMedia}
        onNumberFieldChange={onMediaNumberFieldChange}
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_MESSAGE_FIELDS.textRevealMode}
        value={textRevealMode}
        onChange={(nextValue) => onTextRevealModeChange(String(nextValue))}
      />
      {animationCard}
        </div>
      )}
    </AnimationFieldEditor>
  );
}
