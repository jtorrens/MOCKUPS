import {
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
      "chatBubbles.avatarSize": {
        label: "Message avatar size",
        widget: "number",
        min: 0,
        step: 1,
      },
      "chatBubbles.avatarGap": {
        label: "Message avatar gap",
        widget: "number",
        min: 0,
        step: 1,
      },
      "chatBubbles.shadowEnabled": {
        label: "Bubble shadow",
        widget: "checkbox",
      },
      "chatBubbles.surfaceReliefEnabled": {
        label: "Bubble surface relief",
        widget: "checkbox",
      },
      "chatBubbles.contentMetaGap": {
        label: "Content meta gap",
        widget: "number",
        min: 0,
        step: 1,
      },
      "chatBubbles.messageLabelUseActorColor": {
        label: "Use actor color",
        widget: "checkbox",
      },
      "chatBubbles.messageLabelOffsetX": {
        label: "Offset X",
        widget: "number",
        step: 1,
      },
      "chatBubbles.messageLabelOffsetY": {
        label: "Offset Y",
        widget: "number",
        step: 1,
      },
      "chatBubbles.tail.style": {
        label: "Tail style",
        widget: "select",
        options: ["rounded_wedge", "simple_triangle", "curved_hook", "cut_corner"],
      },
      "chatBubbles.tail.verticalPosition": {
        label: "Tail position",
        widget: "select",
        options: ["bottom", "top"],
      },
      "chatBubbles.tail.width": {
        label: "Tail width",
        widget: "number",
        min: 0,
        step: 1,
      },
      "chatBubbles.tail.height": {
        label: "Tail height",
        widget: "number",
        min: 0,
        step: 1,
      },
      "chatBubbles.tail.scale": {
        label: "Tail scale",
        widget: "number",
        min: 0.01,
        step: 0.05,
      },
      "chatBubbles.status.showText": {
        label: "Show status text",
        widget: "checkbox",
      },
      "chatBubbles.status.showTicks": {
        label: "Show ticks",
        widget: "checkbox",
      },
      "chatBubbles.status.size": {
        label: "Status size",
        widget: "number",
        min: 0,
        step: 1,
      },
      "chatBubbles.status.gap": {
        label: "Status gap",
        widget: "number",
        min: 0,
        step: 1,
      },
      "chatBubbles.status.offsetX": {
        label: "Status X offset",
        widget: "number",
        step: 1,
      },
      "chatBubbles.status.offsetY": {
        label: "Status Y offset",
        widget: "number",
        step: 1,
      },
      "chatBubbles.status.tickSingleIconToken": {
        label: "Single tick token",
      },
      "chatBubbles.status.tickDoubleIconToken": {
        label: "Double tick token",
      },
    },
    module_tokens_override_json: {
      ...coreChatTypographyHints,
    },
  },
};
