import {
  defineFields,
  type JsonFieldBinding,
} from "../../value-system/index.js";

const GROUPS = {
  layout: { id: "layout", label: "Layout" },
  messages: { id: "messages", label: "Messages" },
} as const;

export const CHAT_LAYOUT_MESSAGE_FIELDS = defineFields({
  screenGutter: {
    id: "chat.layout.screenGutter",
    kind: "decimal",
    defaultValue: 24,
    ui: { label: "Screen gutter", group: GROUPS.layout, min: 0, step: 1 },
  },
  messageSpacing: {
    id: "chat.messages.spacing",
    kind: "decimal",
    defaultValue: 8,
    ui: { label: "Message spacing", group: GROUPS.messages, min: 0, step: 1 },
  },
  messageGroupSpacing: {
    id: "chat.messages.groupSpacing",
    kind: "decimal",
    defaultValue: 12,
    ui: { label: "Group spacing", group: GROUPS.messages, min: 0, step: 1 },
  },
});

export const CHAT_LAYOUT_MESSAGE_TOKEN_BINDINGS = [
  {
    outputPath: ["layout", "screenGutter"],
    field: CHAT_LAYOUT_MESSAGE_FIELDS.screenGutter,
  },
  {
    outputPath: ["messages", "spacing"],
    field: CHAT_LAYOUT_MESSAGE_FIELDS.messageSpacing,
  },
  {
    outputPath: ["messages", "groupSpacing"],
    field: CHAT_LAYOUT_MESSAGE_FIELDS.messageGroupSpacing,
  },
] satisfies readonly JsonFieldBinding[];
