import {
  defineFields,
  type JsonFieldBinding,
} from "../../value-system/index.js";

export const CHAT_HEADER_FIELDS = defineFields({
  height: {
    id: "chat.header.height",
    kind: "decimal",
    defaultValue: 96,
    ui: { label: "Height", min: 0, step: 1 },
  },
  background: {
    id: "chat.header.background",
    kind: "themeColorToken",
    defaultValue: "colors.background",
    ui: {
      label: "Background",
      semanticTokenGroup: "colors",
      options: [
        "colors.background",
        "colors.surface",
        "colors.surfaceElevated",
        "icons.primary",
        "icons.secondary",
        "icons.accent",
      ],
    },
  },
  separatorWidth: {
    id: "chat.header.separatorWidth",
    kind: "decimal",
    defaultValue: 1,
    ui: { label: "Separator width", min: 0, step: 1 },
  },
  separatorColor: {
    id: "chat.header.separatorColor",
    kind: "themeColorToken",
    defaultValue: "borders.primary",
    ui: {
      label: "Separator color",
      semanticTokenGroup: "borders",
      options: ["borders.primary", "borders.secondary", "borders.alternate"],
    },
  },
  elementGap: {
    id: "chat.header.elementGap",
    kind: "decimal",
    defaultValue: 8,
    ui: { label: "Element gap", min: 0, step: 1 },
  },
  sidePadding: {
    id: "chat.header.sidePadding",
    kind: "decimal",
    defaultValue: 8,
    ui: { label: "Side padding", min: 0, step: 1 },
  },
  iconSize: {
    id: "chat.header.iconSize",
    kind: "decimal",
    defaultValue: 24,
    ui: { label: "Icon size", min: 0, step: 1 },
  },
  avatarSize: {
    id: "chat.header.avatarSize",
    kind: "decimal",
    defaultValue: 56,
    ui: { label: "Avatar size", min: 0, step: 1 },
  },
  subtitleBottomPadding: {
    id: "chat.header.subtitleBottomPadding",
    kind: "decimal",
    defaultValue: 10,
    ui: { label: "Subtitle bottom padding", min: 0, step: 1 },
  },
  leftIconTokens: {
    id: "chat.header.leftIconTokens",
    kind: "iconToken",
    defaultValue: "nav_chevron_left",
    ui: { label: "Left icon tokens", allowMultiple: true },
  },
  rightIconTokens: {
    id: "chat.header.rightIconTokens",
    kind: "iconToken",
    defaultValue: "media_camera, phone_call",
    ui: { label: "Right icon tokens", allowMultiple: true },
  },
});

export const CHAT_HEADER_DEFAULTS = {
  height: CHAT_HEADER_FIELDS.height.defaultValue,
  background: CHAT_HEADER_FIELDS.background.defaultValue,
  separatorWidth: CHAT_HEADER_FIELDS.separatorWidth.defaultValue,
  separatorColor: CHAT_HEADER_FIELDS.separatorColor.defaultValue,
  elementGap: CHAT_HEADER_FIELDS.elementGap.defaultValue,
  sidePadding: CHAT_HEADER_FIELDS.sidePadding.defaultValue,
  iconSize: CHAT_HEADER_FIELDS.iconSize.defaultValue,
  avatarSize: CHAT_HEADER_FIELDS.avatarSize.defaultValue,
  subtitleBottomPadding:
    CHAT_HEADER_FIELDS.subtitleBottomPadding.defaultValue,
  leftIconTokens: CHAT_HEADER_FIELDS.leftIconTokens.defaultValue,
  rightIconTokens: CHAT_HEADER_FIELDS.rightIconTokens.defaultValue,
} as const;

export const CHAT_HEADER_TOKEN_BINDINGS = [
  { outputPath: ["height"], field: CHAT_HEADER_FIELDS.height },
  { outputPath: ["background"], field: CHAT_HEADER_FIELDS.background },
  { outputPath: ["separatorWidth"], field: CHAT_HEADER_FIELDS.separatorWidth },
  { outputPath: ["separatorColor"], field: CHAT_HEADER_FIELDS.separatorColor },
  { outputPath: ["elementGap"], field: CHAT_HEADER_FIELDS.elementGap },
  { outputPath: ["sidePadding"], field: CHAT_HEADER_FIELDS.sidePadding },
  { outputPath: ["iconSize"], field: CHAT_HEADER_FIELDS.iconSize },
  { outputPath: ["avatarSize"], field: CHAT_HEADER_FIELDS.avatarSize },
  {
    outputPath: ["subtitleBottomPadding"],
    field: CHAT_HEADER_FIELDS.subtitleBottomPadding,
  },
  { outputPath: ["leftIconTokens"], field: CHAT_HEADER_FIELDS.leftIconTokens },
  { outputPath: ["rightIconTokens"], field: CHAT_HEADER_FIELDS.rightIconTokens },
] satisfies readonly JsonFieldBinding[];

export const CHAT_HEADER_DESIGN_UNIT_PATHS = [
  ["header", "height"],
  ["header", "separatorWidth"],
  ["header", "elementGap"],
  ["header", "sidePadding"],
  ["header", "iconSize"],
  ["header", "avatarSize"],
  ["header", "subtitleBottomPadding"],
] as const;
