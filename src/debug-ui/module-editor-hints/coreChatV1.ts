import type { ModuleEditorHintContract } from "./types.js";

export const coreChatV1EditorHints: ModuleEditorHintContract = {
  moduleId: "core.chat",
  schemaVersion: 1,
  fields: {
    module_data_json: {
      "participants.[]": {
        label: "Participant",
        summaryKeys: ["displayName", "role", "id"],
      },
      "participants.[].id": { label: "Participant ID" },
      "participants.[].displayName": { label: "Display name" },
      "participants.[].actorId": { label: "Linked actor" },
      "participants.[].role": {
        label: "Role",
        widget: "select",
        options: ["owner", "participant", "system"],
      },
      "messages.[]": {
        label: "Message",
        summaryKeys: ["text", "mediaAssetId", "type", "id"],
      },
      "messages.[].id": { label: "Message ID" },
      "messages.[].type": {
        label: "Message kind",
        widget: "select",
        options: ["text", "media", "system"],
      },
      "messages.[].senderParticipantId": { label: "Sender" },
      "messages.[].text": { label: "Message text", widget: "textarea" },
      "messages.[].mediaAssetId": { label: "Attached media asset" },
      "messages.[].media": { label: "Attached media" },
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
      "messages.[].startFrame": { label: "Start frame" },
      "messages.[].durationFrames": { label: "Duration frames" },
      "messages.[].writeOnStartFrame": { label: "Write-on start" },
      "messages.[].writeOnDurationFrames": { label: "Write-on duration" },
      "messages.[].exitFrame": { label: "Exit frame" },
    },
    module_config_json: {
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
    },
    tokens_json: {
      "typography.message.fontFamily": {
        label: "Message font family",
        widget: "font",
      },
      "typography.message.fontSize": {
        label: "Message font size",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.message.lineHeight": {
        label: "Message line height",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.message.fontWeight": {
        label: "Message font weight",
        widget: "select",
      },
      "typography.headerTitle.fontFamily": {
        label: "Header title font family",
        widget: "font",
      },
      "typography.headerTitle.fontSize": {
        label: "Header title font size",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.headerTitle.lineHeight": {
        label: "Header title line height",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.headerTitle.fontWeight": {
        label: "Header title font weight",
        widget: "select",
      },
      "typography.headerSubtitle.fontFamily": {
        label: "Header subtitle font family",
        widget: "font",
      },
      "typography.headerSubtitle.fontSize": {
        label: "Header subtitle font size",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.headerSubtitle.lineHeight": {
        label: "Header subtitle line height",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.headerSubtitle.fontWeight": {
        label: "Header subtitle font weight",
        widget: "select",
      },
    },
    module_tokens_override_json: {
      "typography.message.fontFamily": {
        label: "Message font family",
        widget: "font",
      },
      "typography.message.fontSize": {
        label: "Message font size",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.message.lineHeight": {
        label: "Message line height",
        widget: "number",
        min: 1,
        step: 1,
      },
      "typography.message.fontWeight": {
        label: "Message font weight",
        widget: "select",
      },
    },
  },
};
