import { CHAT_CONTENT_HEADER_FIELDS } from "../../../domain/fields/chatFields.js";
import type { JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import {
  AnimationFieldEditor,
  type AnimatableField,
} from "../../editor-ui/animation/AnimationFieldEditor.js";
import { ChatDictionaryFieldRow } from "./ChatDictionaryFieldRow.js";

interface ChatHeaderFieldsEditorProps {
  header: Record<string, JsonValue>;
  inheritedTitle: string;
  actorOptions: Array<{ value: string; label: string }>;
  animation: Record<string, JsonValue>;
  timelineDurationFrames: number;
  onChange: (
    key: "title" | "subtitle" | "actorId" | "useContactColor",
    value: string | boolean,
  ) => void;
  onAnimationChange: (animation: Record<string, JsonValue>) => void;
  onAnimationFrameChange?: (frame: number) => void;
}

export function ChatHeaderFieldsEditor({
  header,
  inheritedTitle,
  actorOptions,
  animation,
  timelineDurationFrames,
  onChange,
  onAnimationChange,
  onAnimationFrameChange,
}: ChatHeaderFieldsEditorProps) {
  const title = String(header.title ?? inheritedTitle ?? "");
  const subtitle = String(header.subtitle ?? "");
  const actorId = String(header.actorId ?? "");
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
  const animatableFields: AnimatableField[] = [
    {
      key: "subtitle",
      label: "Subtitle",
      valueType: "text",
      value: subtitle,
      field: CHAT_CONTENT_HEADER_FIELDS.subtitle,
      interpolationOptions: ["hold", "linear", "ease"],
    },
  ];

  return (
    <AnimationFieldEditor
      animation={animation}
      fields={animatableFields}
      timelineDurationFrames={timelineDurationFrames}
      onAnimationChange={onAnimationChange}
      onAnimationFrameChange={onAnimationFrameChange}
    >
      {({ animationCard, fieldLabel }) => (
        <div className="record-editor-field-stack record-editor-direct-fields">
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_HEADER_FIELDS.actorId}
        value={actorId}
        selectOptions={actorSelectOptions}
        onChange={(nextValue) => onChange("actorId", String(nextValue))}
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_HEADER_FIELDS.title}
        value={title}
        onChange={(nextValue) => onChange("title", String(nextValue))}
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_HEADER_FIELDS.subtitle}
        labelOverride={fieldLabel("subtitle")}
        value={subtitle}
        onChange={(nextValue) => onChange("subtitle", String(nextValue))}
      />
      <ChatDictionaryFieldRow
        field={CHAT_CONTENT_HEADER_FIELDS.useContactColor}
        value={header.useContactColor === true}
        onChange={(nextValue) => onChange("useContactColor", nextValue === true)}
      />
      {animationCard}
        </div>
      )}
    </AnimationFieldEditor>
  );
}
