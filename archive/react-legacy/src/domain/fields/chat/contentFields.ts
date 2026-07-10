import {
  defineFields,
  type JsonFieldBinding,
} from "../../value-system/index.js";

export const CHAT_CONTENT_HEADER_FIELDS = defineFields({
  actorId: {
    id: "chat.content.header.actorId",
    kind: "recordReference",
    ui: {
      label: "Actor",
      tableId: "actors",
      labelColumn: "display_name",
      allowEmpty: true,
    },
  },
  title: {
    id: "chat.content.header.title",
    kind: "text",
    defaultValue: "",
    ui: { label: "Title" },
  },
  subtitle: {
    id: "chat.content.header.subtitle",
    kind: "text",
    defaultValue: "",
    ui: { label: "Subtitle" },
  },
  useContactColor: {
    id: "chat.content.header.useContactColor",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Use actor color" },
  },
});

export const CHAT_CONTENT_MESSAGE_FIELDS = defineFields({
  direction: {
    id: "chat.content.message.direction",
    kind: "enum",
    defaultValue: "received",
    ui: {
      label: "Type",
      options: ["received", "sent", "system"],
    },
  },
  actorId: {
    id: "chat.content.message.actorId",
    kind: "recordReference",
    ui: {
      label: "Actor",
      tableId: "actors",
      labelColumn: "display_name",
      allowEmpty: true,
    },
  },
  delayAfterPreviousFrames: {
    id: "chat.content.message.delayAfterPreviousFrames",
    kind: "integer",
    defaultValue: 0,
    ui: { label: "Delay after previous write-on", min: 0, step: 1 },
  },
  writeOnDurationFrames: {
    id: "chat.content.message.writeOnDurationFrames",
    kind: "integer",
    defaultValue: 30,
    ui: { label: "Write-on duration", min: 0, step: 1 },
  },
  showBubbleBackground: {
    id: "chat.content.message.showBubbleBackground",
    kind: "boolean",
    defaultValue: true,
    ui: { label: "Show bubble background" },
  },
  textScale: {
    id: "chat.content.message.textScale",
    kind: "decimal",
    defaultValue: 1,
    ui: { label: "Text scale", min: 0.01, step: 0.05 },
  },
  text: {
    id: "chat.content.message.text",
    kind: "text",
    defaultValue: "",
    ui: { label: "Message text", multiline: true, rows: 4 },
  },
  statusText: {
    id: "chat.content.message.status.text",
    kind: "text",
    defaultValue: "",
    ui: { label: "Status text" },
  },
  deliveryStatus: {
    id: "chat.content.message.status.deliveryStatus",
    kind: "enum",
    defaultValue: "none",
    ui: {
      label: "Delivery status",
      options: ["none", "sent", "delivered", "read", "failed"],
    },
  },
  textRevealMode: {
    id: "chat.content.message.textReveal.mode",
    kind: "enum",
    defaultValue: "simple_write_on",
    ui: {
      label: "Text reveal mode",
      options: ["simple_write_on", "natural_write_on", "waiting_dots"],
    },
  },
});

export const CHAT_CONTENT_MEDIA_FIELDS = defineFields({
  type: {
    id: "chat.content.message.media.type",
    kind: "enum",
    defaultValue: "none",
    ui: {
      label: "Type",
      options: ["none", "image", "video", "audio"],
    },
  },
  durationSeconds: {
    id: "chat.content.message.media.durationSeconds",
    kind: "decimal",
    defaultValue: 8,
    ui: { label: "Duration seconds", min: 0.1, step: 0.1 },
  },
  filePath: {
    id: "chat.content.message.media.filePath",
    kind: "relativeFilePath",
    ui: {
      label: "File path",
      fileKind: "file",
      accept: ["image/*", "video/*"],
    },
  },
  playMode: {
    id: "chat.content.message.media.playMode",
    kind: "enum",
    defaultValue: "once",
    ui: {
      label: "Play",
      options: ["once", "loop"],
    },
  },
  playStartFrame: {
    id: "chat.content.message.media.playStartFrame",
    kind: "integer",
    defaultValue: 0,
    ui: { label: "Play start frame", min: 0, step: 1 },
  },
  windowWidth: {
    id: "chat.content.message.media.window.width",
    kind: "integer",
    defaultValue: 360,
    ui: {
      label: "Width",
      min: 1,
      step: 1,
      pair: { id: "mediaWindowSize", label: "Media window", role: "width" },
    },
  },
  windowHeight: {
    id: "chat.content.message.media.window.height",
    kind: "integer",
    defaultValue: 240,
    ui: {
      label: "Height",
      min: 1,
      step: 1,
      pair: { id: "mediaWindowSize", label: "Media window", role: "height" },
    },
  },
  windowOffsetX: {
    id: "chat.content.message.media.window.offsetX",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Offset X",
      step: 1,
      pair: { id: "mediaWindowOffset", label: "Media offset", role: "x" },
    },
  },
  windowOffsetY: {
    id: "chat.content.message.media.window.offsetY",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Offset Y",
      step: 1,
      pair: { id: "mediaWindowOffset", label: "Media offset", role: "y" },
    },
  },
  transformScale: {
    id: "chat.content.message.media.transform.scale",
    kind: "decimal",
    defaultValue: 1,
    ui: { label: "Scale", min: 0.01, step: 0.05 },
  },
  transformTranslateX: {
    id: "chat.content.message.media.transform.translateX",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Translate X",
      step: 1,
      pair: { id: "mediaTransformTranslate", label: "Media translate", role: "x" },
    },
  },
  transformTranslateY: {
    id: "chat.content.message.media.transform.translateY",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Translate Y",
      step: 1,
      pair: { id: "mediaTransformTranslate", label: "Media translate", role: "y" },
    },
  },
  transformRotationDegrees: {
    id: "chat.content.message.media.transform.rotationDegrees",
    kind: "decimal",
    defaultValue: 0,
    ui: { label: "Rotation", step: 1 },
  },
});

export const CHAT_CONTENT_HEADER_BINDINGS = [
  { outputPath: ["header", "actorId"], field: CHAT_CONTENT_HEADER_FIELDS.actorId },
  { outputPath: ["header", "title"], field: CHAT_CONTENT_HEADER_FIELDS.title },
  { outputPath: ["header", "subtitle"], field: CHAT_CONTENT_HEADER_FIELDS.subtitle },
  {
    outputPath: ["header", "useContactColor"],
    field: CHAT_CONTENT_HEADER_FIELDS.useContactColor,
  },
] satisfies readonly JsonFieldBinding[];

export const CHAT_CONTENT_MESSAGE_BINDINGS = [
  { outputPath: ["messages", "[]", "direction"], field: CHAT_CONTENT_MESSAGE_FIELDS.direction },
  { outputPath: ["messages", "[]", "actorId"], field: CHAT_CONTENT_MESSAGE_FIELDS.actorId },
  {
    outputPath: ["messages", "[]", "delayAfterPreviousFrames"],
    field: CHAT_CONTENT_MESSAGE_FIELDS.delayAfterPreviousFrames,
  },
  {
    outputPath: ["messages", "[]", "textReveal", "durationFrames"],
    field: CHAT_CONTENT_MESSAGE_FIELDS.writeOnDurationFrames,
  },
  {
    outputPath: ["messages", "[]", "showBubbleBackground"],
    field: CHAT_CONTENT_MESSAGE_FIELDS.showBubbleBackground,
  },
  { outputPath: ["messages", "[]", "textScale"], field: CHAT_CONTENT_MESSAGE_FIELDS.textScale },
  { outputPath: ["messages", "[]", "text"], field: CHAT_CONTENT_MESSAGE_FIELDS.text },
  { outputPath: ["messages", "[]", "status", "text"], field: CHAT_CONTENT_MESSAGE_FIELDS.statusText },
  {
    outputPath: ["messages", "[]", "status", "deliveryStatus"],
    field: CHAT_CONTENT_MESSAGE_FIELDS.deliveryStatus,
  },
  {
    outputPath: ["messages", "[]", "textReveal", "mode"],
    field: CHAT_CONTENT_MESSAGE_FIELDS.textRevealMode,
  },
] satisfies readonly JsonFieldBinding[];

export const CHAT_CONTENT_MEDIA_BINDINGS = [
  { outputPath: ["messages", "[]", "media", "type"], field: CHAT_CONTENT_MEDIA_FIELDS.type },
  {
    outputPath: ["messages", "[]", "media", "durationSeconds"],
    field: CHAT_CONTENT_MEDIA_FIELDS.durationSeconds,
  },
  {
    outputPath: ["messages", "[]", "media", "filePath"],
    field: CHAT_CONTENT_MEDIA_FIELDS.filePath,
  },
  { outputPath: ["messages", "[]", "media", "playMode"], field: CHAT_CONTENT_MEDIA_FIELDS.playMode },
  {
    outputPath: ["messages", "[]", "media", "playStartFrame"],
    field: CHAT_CONTENT_MEDIA_FIELDS.playStartFrame,
  },
  {
    outputPath: ["messages", "[]", "media", "window", "width"],
    field: CHAT_CONTENT_MEDIA_FIELDS.windowWidth,
  },
  {
    outputPath: ["messages", "[]", "media", "window", "height"],
    field: CHAT_CONTENT_MEDIA_FIELDS.windowHeight,
  },
  {
    outputPath: ["messages", "[]", "media", "window", "offsetX"],
    field: CHAT_CONTENT_MEDIA_FIELDS.windowOffsetX,
  },
  {
    outputPath: ["messages", "[]", "media", "window", "offsetY"],
    field: CHAT_CONTENT_MEDIA_FIELDS.windowOffsetY,
  },
  {
    outputPath: ["messages", "[]", "media", "transform", "scale"],
    field: CHAT_CONTENT_MEDIA_FIELDS.transformScale,
  },
  {
    outputPath: ["messages", "[]", "media", "transform", "translateX"],
    field: CHAT_CONTENT_MEDIA_FIELDS.transformTranslateX,
  },
  {
    outputPath: ["messages", "[]", "media", "transform", "translateY"],
    field: CHAT_CONTENT_MEDIA_FIELDS.transformTranslateY,
  },
  {
    outputPath: ["messages", "[]", "media", "transform", "rotationDegrees"],
    field: CHAT_CONTENT_MEDIA_FIELDS.transformRotationDegrees,
  },
] satisfies readonly JsonFieldBinding[];
