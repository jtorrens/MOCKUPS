import {
  defineFields,
  type JsonFieldBinding,
} from "../../value-system/index.js";

const GROUPS = {
  bubble: { id: "bubble", label: "Bubble" },
  avatar: { id: "avatar", label: "Avatar" },
  label: { id: "label", label: "Label" },
  media: { id: "media", label: "Media" },
  tail: { id: "tail", label: "Tail" },
  status: { id: "status", label: "Status" },
} as const;

export const CHAT_BUBBLE_FIELDS = defineFields({
  avatarSize: {
    id: "chat.bubbles.avatarSize",
    kind: "decimal",
    defaultValue: 48,
    ui: { label: "Message avatar size", group: GROUPS.avatar, min: 0, step: 1 },
  },
  avatarGap: {
    id: "chat.bubbles.avatarGap",
    kind: "decimal",
    defaultValue: 8,
    ui: { label: "Message avatar gap", group: GROUPS.avatar, min: 0, step: 1 },
  },
  shadowEnabled: {
    id: "chat.bubbles.shadowEnabled",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Bubble shadow", group: GROUPS.bubble },
  },
  surfaceReliefEnabled: {
    id: "chat.bubbles.surfaceReliefEnabled",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Bubble surface relief", group: GROUPS.bubble },
  },
  contentMetaGap: {
    id: "chat.bubbles.contentMetaGap",
    kind: "decimal",
    defaultValue: 4,
    ui: { label: "Content meta gap", group: GROUPS.bubble, min: 0, step: 1 },
  },
  messageLabelUseActorColor: {
    id: "chat.bubbles.messageLabel.useActorColor",
    kind: "boolean",
    defaultValue: true,
    ui: { label: "Use actor color", group: GROUPS.label },
  },
  messageLabelOffsetX: {
    id: "chat.bubbles.messageLabel.offsetX",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Offset X",
      group: GROUPS.label,
      step: 1,
      pair: {
        id: "chat.bubbles.messageLabel.offset",
        label: "Label offset",
        role: "X",
      },
    },
  },
  messageLabelOffsetY: {
    id: "chat.bubbles.messageLabel.offsetY",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Offset Y",
      group: GROUPS.label,
      step: 1,
      pair: {
        id: "chat.bubbles.messageLabel.offset",
        label: "Label offset",
        role: "Y",
      },
    },
  },
  mediaBorderWidth: {
    id: "chat.bubbles.media.borderWidth",
    kind: "decimal",
    defaultValue: 0,
    ui: { label: "Border width", group: GROUPS.media, min: 0, step: 1 },
  },
  mediaCornerRadius: {
    id: "chat.bubbles.media.cornerRadius",
    kind: "decimal",
    defaultValue: 0,
    ui: { label: "Corner radius", group: GROUPS.media, min: 0, step: 1 },
  },
  mediaShadowEnabled: {
    id: "chat.bubbles.media.shadowEnabled",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Media shadow", group: GROUPS.media },
  },
  tailStyle: {
    id: "chat.bubbles.tail.style",
    kind: "enum",
    defaultValue: "rounded_wedge",
    ui: {
      label: "Tail style",
      group: GROUPS.tail,
      options: ["rounded_wedge", "simple_triangle", "curved_hook", "cut_corner"],
    },
  },
  tailVerticalPosition: {
    id: "chat.bubbles.tail.verticalPosition",
    kind: "enum",
    defaultValue: "bottom",
    ui: {
      label: "Tail position",
      group: GROUPS.tail,
      options: ["bottom", "top"],
    },
  },
  tailWidth: {
    id: "chat.bubbles.tail.width",
    kind: "decimal",
    defaultValue: 16,
    ui: { label: "Tail width", group: GROUPS.tail, min: 0, step: 1 },
  },
  tailHeight: {
    id: "chat.bubbles.tail.height",
    kind: "decimal",
    defaultValue: 12,
    ui: { label: "Tail height", group: GROUPS.tail, min: 0, step: 1 },
  },
  tailScale: {
    id: "chat.bubbles.tail.scale",
    kind: "decimal",
    defaultValue: 1,
    ui: { label: "Tail scale", group: GROUPS.tail, min: 0.01, step: 0.05 },
  },
  statusShowText: {
    id: "chat.bubbles.status.showText",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Show status text", group: GROUPS.status },
  },
  statusShowTicks: {
    id: "chat.bubbles.status.showTicks",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Show ticks", group: GROUPS.status },
  },
  statusSize: {
    id: "chat.bubbles.status.size",
    kind: "decimal",
    defaultValue: 12,
    ui: { label: "Status size", group: GROUPS.status, min: 0, step: 1 },
  },
  statusGap: {
    id: "chat.bubbles.status.gap",
    kind: "decimal",
    defaultValue: 4,
    ui: { label: "Status gap", group: GROUPS.status, min: 0, step: 1 },
  },
  statusOffsetX: {
    id: "chat.bubbles.status.offsetX",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Status X offset",
      group: GROUPS.status,
      step: 1,
      pair: {
        id: "chat.bubbles.status.offset",
        label: "Status offset",
        role: "X",
      },
    },
  },
  statusOffsetY: {
    id: "chat.bubbles.status.offsetY",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Status Y offset",
      group: GROUPS.status,
      step: 1,
      pair: {
        id: "chat.bubbles.status.offset",
        label: "Status offset",
        role: "Y",
      },
    },
  },
  statusTickSingleIconToken: {
    id: "chat.bubbles.status.tickSingleIconToken",
    kind: "iconToken",
    defaultValue: "status_check",
    ui: { label: "Single tick token", group: GROUPS.status },
  },
  statusTickDoubleIconToken: {
    id: "chat.bubbles.status.tickDoubleIconToken",
    kind: "iconToken",
    defaultValue: "status_check_double",
    ui: { label: "Double tick token", group: GROUPS.status },
  },
  incomingBackgroundLight: {
    id: "chat.bubbles.incomingBackground.light",
    kind: "paletteColorToken",
    ui: {
      label: "Incoming background light",
      group: GROUPS.bubble,
      pair: {
        id: "chat.bubbles.incomingBackground",
        label: "Incoming background",
        role: "light",
      },
    },
  },
  incomingBackgroundDark: {
    id: "chat.bubbles.incomingBackground.dark",
    kind: "paletteColorToken",
    ui: {
      label: "Incoming background dark",
      group: GROUPS.bubble,
      pair: {
        id: "chat.bubbles.incomingBackground",
        label: "Incoming background",
        role: "dark",
      },
    },
  },
  systemBackgroundLight: {
    id: "chat.bubbles.systemBackground.light",
    kind: "paletteColorToken",
    ui: {
      label: "System background light",
      group: GROUPS.bubble,
      pair: {
        id: "chat.bubbles.systemBackground",
        label: "System background",
        role: "light",
      },
    },
  },
  systemBackgroundDark: {
    id: "chat.bubbles.systemBackground.dark",
    kind: "paletteColorToken",
    ui: {
      label: "System background dark",
      group: GROUPS.bubble,
      pair: {
        id: "chat.bubbles.systemBackground",
        label: "System background",
        role: "dark",
      },
    },
  },
  systemTextLight: {
    id: "chat.bubbles.systemText.light",
    kind: "paletteColorToken",
    ui: {
      label: "System text light",
      group: GROUPS.bubble,
      pair: {
        id: "chat.bubbles.systemText",
        label: "System text",
        role: "light",
      },
    },
  },
  systemTextDark: {
    id: "chat.bubbles.systemText.dark",
    kind: "paletteColorToken",
    ui: {
      label: "System text dark",
      group: GROUPS.bubble,
      pair: {
        id: "chat.bubbles.systemText",
        label: "System text",
        role: "dark",
      },
    },
  },
  mediaBorderColorLight: {
    id: "chat.bubbles.media.borderColor.light",
    kind: "paletteColorToken",
    ui: {
      label: "Media border light",
      group: GROUPS.media,
      pair: {
        id: "chat.bubbles.media.borderColor",
        label: "Media border",
        role: "light",
      },
    },
  },
  mediaBorderColorDark: {
    id: "chat.bubbles.media.borderColor.dark",
    kind: "paletteColorToken",
    ui: {
      label: "Media border dark",
      group: GROUPS.media,
      pair: {
        id: "chat.bubbles.media.borderColor",
        label: "Media border",
        role: "dark",
      },
    },
  },
});

export const CHAT_BUBBLE_TOKEN_BINDINGS = [
  { outputPath: ["chatBubbles", "avatarSize"], field: CHAT_BUBBLE_FIELDS.avatarSize },
  { outputPath: ["chatBubbles", "avatarGap"], field: CHAT_BUBBLE_FIELDS.avatarGap },
  {
    outputPath: ["chatBubbles", "shadowEnabled"],
    field: CHAT_BUBBLE_FIELDS.shadowEnabled,
  },
  {
    outputPath: ["chatBubbles", "surfaceReliefEnabled"],
    field: CHAT_BUBBLE_FIELDS.surfaceReliefEnabled,
  },
  {
    outputPath: ["chatBubbles", "contentMetaGap"],
    field: CHAT_BUBBLE_FIELDS.contentMetaGap,
  },
  {
    outputPath: ["chatBubbles", "messageLabelUseActorColor"],
    field: CHAT_BUBBLE_FIELDS.messageLabelUseActorColor,
  },
  {
    outputPath: ["chatBubbles", "messageLabelOffsetX"],
    field: CHAT_BUBBLE_FIELDS.messageLabelOffsetX,
  },
  {
    outputPath: ["chatBubbles", "messageLabelOffsetY"],
    field: CHAT_BUBBLE_FIELDS.messageLabelOffsetY,
  },
  {
    outputPath: ["chatBubbles", "media", "borderWidth"],
    field: CHAT_BUBBLE_FIELDS.mediaBorderWidth,
  },
  {
    outputPath: ["chatBubbles", "media", "cornerRadius"],
    field: CHAT_BUBBLE_FIELDS.mediaCornerRadius,
  },
  {
    outputPath: ["chatBubbles", "media", "shadowEnabled"],
    field: CHAT_BUBBLE_FIELDS.mediaShadowEnabled,
  },
  {
    outputPath: ["chatBubbles", "tail", "style"],
    field: CHAT_BUBBLE_FIELDS.tailStyle,
  },
  {
    outputPath: ["chatBubbles", "tail", "verticalPosition"],
    field: CHAT_BUBBLE_FIELDS.tailVerticalPosition,
  },
  {
    outputPath: ["chatBubbles", "tail", "width"],
    field: CHAT_BUBBLE_FIELDS.tailWidth,
  },
  {
    outputPath: ["chatBubbles", "tail", "height"],
    field: CHAT_BUBBLE_FIELDS.tailHeight,
  },
  {
    outputPath: ["chatBubbles", "tail", "scale"],
    field: CHAT_BUBBLE_FIELDS.tailScale,
  },
  {
    outputPath: ["chatBubbles", "status", "showText"],
    field: CHAT_BUBBLE_FIELDS.statusShowText,
  },
  {
    outputPath: ["chatBubbles", "status", "showTicks"],
    field: CHAT_BUBBLE_FIELDS.statusShowTicks,
  },
  {
    outputPath: ["chatBubbles", "status", "size"],
    field: CHAT_BUBBLE_FIELDS.statusSize,
  },
  {
    outputPath: ["chatBubbles", "status", "gap"],
    field: CHAT_BUBBLE_FIELDS.statusGap,
  },
  {
    outputPath: ["chatBubbles", "status", "offsetX"],
    field: CHAT_BUBBLE_FIELDS.statusOffsetX,
  },
  {
    outputPath: ["chatBubbles", "status", "offsetY"],
    field: CHAT_BUBBLE_FIELDS.statusOffsetY,
  },
  {
    outputPath: ["chatBubbles", "status", "tickSingleIconToken"],
    field: CHAT_BUBBLE_FIELDS.statusTickSingleIconToken,
  },
  {
    outputPath: ["chatBubbles", "status", "tickDoubleIconToken"],
    field: CHAT_BUBBLE_FIELDS.statusTickDoubleIconToken,
  },
  {
    outputPath: ["modes", "light", "chatBubbles", "incomingBackground"],
    field: CHAT_BUBBLE_FIELDS.incomingBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "chatBubbles", "incomingBackground"],
    field: CHAT_BUBBLE_FIELDS.incomingBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "chatBubbles", "systemBackground"],
    field: CHAT_BUBBLE_FIELDS.systemBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "chatBubbles", "systemBackground"],
    field: CHAT_BUBBLE_FIELDS.systemBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "chatBubbles", "systemText"],
    field: CHAT_BUBBLE_FIELDS.systemTextLight,
  },
  {
    outputPath: ["modes", "dark", "chatBubbles", "systemText"],
    field: CHAT_BUBBLE_FIELDS.systemTextDark,
  },
  {
    outputPath: ["modes", "light", "chatBubbles", "media", "borderColor"],
    field: CHAT_BUBBLE_FIELDS.mediaBorderColorLight,
  },
  {
    outputPath: ["modes", "dark", "chatBubbles", "media", "borderColor"],
    field: CHAT_BUBBLE_FIELDS.mediaBorderColorDark,
  },
] satisfies readonly JsonFieldBinding[];
