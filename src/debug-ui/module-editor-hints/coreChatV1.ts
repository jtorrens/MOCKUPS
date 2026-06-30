import {
  CHAT_BUBBLE_TOKEN_BINDINGS,
  CHAT_CONTENT_HEADER_BINDINGS,
  CHAT_CONTENT_MEDIA_BINDINGS,
  CHAT_CONTENT_MESSAGE_BINDINGS,
  CHAT_HEADER_TOKEN_BINDINGS,
  CHAT_TYPOGRAPHY_TOKEN_BINDINGS,
} from "../../domain/fields/chatFields.js";
import { jsonUiHintsFromFieldBindings } from "../components/json-editor/fieldDefinitionHints.js";
import type { JsonUiHints } from "../components/json-editor/uiHints.js";
import type { ModuleEditorHintContract } from "./types.js";

const coreChatHeaderHints = jsonUiHintsFromFieldBindings(
  CHAT_HEADER_TOKEN_BINDINGS,
);
const coreChatBubbleHints = jsonUiHintsFromFieldBindings(
  CHAT_BUBBLE_TOKEN_BINDINGS,
);
const coreChatTypographyHints = jsonUiHintsFromFieldBindings(
  CHAT_TYPOGRAPHY_TOKEN_BINDINGS,
);
const coreChatContentHints = jsonUiHintsFromFieldBindings([
  ...CHAT_CONTENT_HEADER_BINDINGS,
  ...CHAT_CONTENT_MESSAGE_BINDINGS,
  ...CHAT_CONTENT_MEDIA_BINDINGS,
]);

const coreChatContentJsonHints: JsonUiHints = {
  "messages.[]": {
    label: "Message",
    summaryKeys: ["text", "media.filePath", "type", "id"],
  },
  "messages.[].id": { label: "Message ID" },
  "messages.[].type": {
    label: "Message kind",
    widget: "select",
    options: ["text", "media", "system"],
  },
  "messages.[].actorId": { label: "Actor" },
  "messages.[].text": { label: "Message text", widget: "textarea" },
  "messages.[].status": { label: "Bubble status" },
  "messages.[].status.text": { label: "Status text" },
  "messages.[].status.deliveryStatus": {
    label: "Delivery status",
    widget: "select",
    options: ["none", "sent", "delivered", "read", "failed"],
  },
  "messages.[].media": { label: "Attached media" },
  "messages.[].media.type": {
    label: "Media type",
    widget: "select",
    options: ["none", "image", "video", "audio"],
  },
  "messages.[].media.durationSeconds": { label: "Audio duration" },
  "messages.[].media.filePath": { label: "Media file path" },
  "messages.[].media.window": { label: "Media crop/window" },
  "messages.[].media.window.width": { label: "Media window width" },
  "messages.[].media.window.height": { label: "Media window height" },
  "messages.[].media.window.offsetX": { label: "Media window X offset" },
  "messages.[].media.window.offsetY": { label: "Media window Y offset" },
  "messages.[].media.transform": { label: "Media transform" },
  "messages.[].media.transform.scale": { label: "Media scale" },
  "messages.[].media.transform.translateX": { label: "Media X position" },
  "messages.[].media.transform.translateY": { label: "Media Y position" },
  "messages.[].media.transform.rotationDegrees": {
    label: "Media rotation",
  },
  "messages.[].delayAfterPreviousFrames": {
    label: "Delay after previous write-on",
  },
  "messages.[].durationFrames": { label: "Duration frames" },
  "messages.[].writeOnDurationFrames": { label: "Write-on duration" },
  "messages.[].exitFrame": { label: "Exit frame" },
  ...coreChatContentHints,
};

const coreChatBehaviorJsonHints: JsonUiHints = {
  showHeader: { label: "Show header", widget: "checkbox" },
  showStatusBar: { label: "Show status bar", widget: "checkbox" },
  showKeyboard: { label: "Show keyboard", widget: "checkbox" },
  debugShowBounds: { label: "Show debug bounds", widget: "checkbox" },
  initialScroll: {
    label: "Initial scroll",
    widget: "select",
    options: ["top", "bottom", "keep_latest_visible"],
  },
  messageGrouping: {
    label: "Message grouping",
    widget: "select",
    options: ["none", "bySender"],
  },
};

export const coreChatV1EditorHints: ModuleEditorHintContract = {
  moduleId: "core.chat",
  schemaVersion: 1,
  fields: {
    content_json: coreChatContentJsonHints,
    behavior_json: coreChatBehaviorJsonHints,
    module_data_json: coreChatContentJsonHints,
    module_config_json: coreChatBehaviorJsonHints,
    tokens_json: {
      ...coreChatTypographyHints,
      ...coreChatHeaderHints,
      ...coreChatBubbleHints,
    },
    module_tokens_override_json: {
      ...coreChatTypographyHints,
    },
  },
};
