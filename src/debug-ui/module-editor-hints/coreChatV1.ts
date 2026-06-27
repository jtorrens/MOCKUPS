import type { ModuleEditorHintContract } from "./types.js";

export const coreChatV1EditorHints: ModuleEditorHintContract = {
  moduleId: "core.chat",
  schemaVersion: 1,
  fields: {
    module_data_json: {
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
        options: ["none", "image", "video"],
      },
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
      "header.elementGap": {
        label: "Element gap",
        widget: "number",
        min: 0,
        step: 1,
      },
      "header.sidePadding": {
        label: "Side padding",
        widget: "number",
        min: 0,
        step: 1,
      },
      "header.iconSize": {
        label: "Icon size",
        widget: "number",
        min: 0,
        step: 1,
      },
      "header.leftIconTokens": {
        label: "Left icon tokens",
        widget: "text",
      },
      "header.rightIconTokens": {
        label: "Right icon tokens",
        widget: "text",
      },
      "header.subtitleBottomPadding": {
        label: "Subtitle bottom padding",
        widget: "number",
        min: 0,
        step: 1,
      },
      "header.avatarSize": {
        label: "Avatar size",
        widget: "number",
        min: 0,
        step: 1,
      },
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
