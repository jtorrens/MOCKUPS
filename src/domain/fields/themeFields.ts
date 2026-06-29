import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

const GROUPS = {
  neutralTint: { id: "neutralTint", label: "Neutral tint" },
  appColors: { id: "appColors", label: "App colors" },
  iconColors: { id: "iconColors", label: "Icon colors" },
  borderColors: { id: "borderColors", label: "Border colors" },
  cursor: { id: "cursor", label: "Cursor" },
  statusBar: { id: "statusBar", label: "Status bar" },
  navigationBar: { id: "navigationBar", label: "Navigation bar" },
  keyboard: { id: "keyboard", label: "Keyboard" },
  notifications: { id: "notifications", label: "Notifications" },
  surfaceRelief: { id: "surfaceRelief", label: "Surface relief" },
  typography: { id: "typography", label: "Typography" },
} as const;

export const THEME_FIELDS = defineFields({
  id: {
    id: "theme.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "theme.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "theme.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  family: {
    id: "theme.family",
    kind: "enum",
    defaultValue: "ios",
    ui: {
      label: "Family",
      options: ["ios", "android"],
    },
  },
  iconThemeId: {
    id: "theme.iconThemeId",
    kind: "recordReference",
    ui: {
      label: "Icon theme",
      tableId: "icon_themes",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  statusBarId: {
    id: "theme.statusBarId",
    kind: "recordReference",
    ui: {
      label: "Status bar",
      tableId: "status_bars",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  navigationBarId: {
    id: "theme.navigationBarId",
    kind: "recordReference",
    ui: {
      label: "Navigation bar",
      tableId: "navigation_bars",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  version: {
    id: "theme.version",
    kind: "text",
    defaultValue: "1.0.0",
    ui: {
      label: "Version",
    },
  },
  tokens: {
    id: "theme.tokens",
    kind: "jsonObject",
    ui: {
      label: "Theme tokens",
    },
  },
  defaultMode: {
    id: "theme.defaultMode",
    kind: "enum",
    defaultValue: "light",
    ui: {
      label: "Default mode",
      options: ["light", "dark"],
    },
  },
  neutralTintHueDeg: {
    id: "theme.neutralTint.hueDeg",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Hue",
      group: GROUPS.neutralTint,
      numericControl: "hueDegrees",
      min: 0,
      max: 360,
      step: 1,
    },
  },
  neutralTintSaturation: {
    id: "theme.neutralTint.saturation",
    kind: "alpha",
    defaultValue: 0,
    ui: {
      label: "Saturation",
      group: GROUPS.neutralTint,
      min: 0,
      max: 1,
      step: 0.01,
    },
  },
  cursorWidth: {
    id: "theme.cursor.width",
    kind: "integer",
    defaultValue: 2,
    ui: {
      label: "Width",
      group: GROUPS.cursor,
      min: 1,
      step: 1,
    },
  },
  cursorBlinkFrames: {
    id: "theme.cursor.blinkFrames",
    kind: "integer",
    defaultValue: 15,
    ui: {
      label: "Blink frames",
      group: GROUPS.cursor,
      min: 1,
      step: 1,
    },
  },
  surfaceReliefAngleDeg: {
    id: "theme.surfaceRelief.default.angleDeg",
    kind: "integer",
    defaultValue: -45,
    ui: {
      label: "Angle",
      group: GROUPS.surfaceRelief,
      step: 1,
    },
  },
  surfaceReliefExtension: {
    id: "theme.surfaceRelief.default.extension",
    kind: "decimal",
    defaultValue: 1,
    ui: {
      label: "Extension",
      group: GROUPS.surfaceRelief,
      step: 0.1,
    },
  },
  surfaceReliefSpread: {
    id: "theme.surfaceRelief.default.spread",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Spread",
      group: GROUPS.surfaceRelief,
      step: 0.1,
    },
  },
  surfaceReliefUpperIntensity: {
    id: "theme.surfaceRelief.default.upperIntensity",
    kind: "decimal",
    defaultValue: 0.1,
    ui: {
      label: "Upper intensity",
      group: GROUPS.surfaceRelief,
      step: 0.01,
    },
  },
  surfaceReliefLowerIntensity: {
    id: "theme.surfaceRelief.default.lowerIntensity",
    kind: "decimal",
    defaultValue: -0.08,
    ui: {
      label: "Lower intensity",
      group: GROUPS.surfaceRelief,
      step: 0.01,
    },
  },
  typographyFamily: {
    id: "theme.typography.family",
    kind: "fontFamily",
    ui: {
      label: "Font family",
      group: GROUPS.typography,
    },
  },
  typographyEmojiFamily: {
    id: "theme.typography.emojiFamily",
    kind: "fontFamily",
    ui: {
      label: "Emoji font",
      group: GROUPS.typography,
      allowEmpty: true,
    },
  },
  typographyBodySize: {
    id: "theme.typography.bodySize",
    kind: "decimal",
    ui: {
      label: "Body size",
      group: GROUPS.typography,
      min: 1,
      step: 1,
    },
  },
  typographyBodyLineHeight: {
    id: "theme.typography.bodyLineHeight",
    kind: "decimal",
    ui: {
      label: "Body line height",
      group: GROUPS.typography,
      min: 1,
      step: 1,
    },
  },
  typographyCaptionSize: {
    id: "theme.typography.captionSize",
    kind: "decimal",
    ui: {
      label: "Caption size",
      group: GROUPS.typography,
      min: 1,
      step: 1,
    },
  },
  typographyFontWeight: {
    id: "theme.typography.fontWeight",
    kind: "fontWeight",
    ui: {
      label: "Font weight",
      group: GROUPS.typography,
    },
  },
  typographyFontStyle: {
    id: "theme.typography.fontStyle",
    kind: "fontStyle",
    ui: {
      label: "Font style",
      group: GROUPS.typography,
    },
  },
  colorBackgroundLight: {
    id: "theme.colors.background.light",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Background light",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.background", label: "Background", role: "light" },
    },
  },
  colorBackgroundDark: {
    id: "theme.colors.background.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Background dark",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.background", label: "Background", role: "dark" },
    },
  },
  colorTextPrimaryLight: {
    id: "theme.colors.textPrimary.light",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Primary text light",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.textPrimary", label: "Primary text", role: "light" },
    },
  },
  colorTextPrimaryDark: {
    id: "theme.colors.textPrimary.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Primary text dark",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.textPrimary", label: "Primary text", role: "dark" },
    },
  },
  colorTextSecondaryLight: {
    id: "theme.colors.textSecondary.light",
    kind: "paletteColorToken",
    defaultValue: "gray_040",
    ui: {
      label: "Secondary text light",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.textSecondary", label: "Secondary text", role: "light" },
    },
  },
  colorTextSecondaryDark: {
    id: "theme.colors.textSecondary.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_060",
    ui: {
      label: "Secondary text dark",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.textSecondary", label: "Secondary text", role: "dark" },
    },
  },
  colorAccentLight: {
    id: "theme.colors.accent.light",
    kind: "paletteColorToken",
    defaultValue: "blue",
    ui: {
      label: "Accent light",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.accent", label: "Accent", role: "light" },
    },
  },
  colorAccentDark: {
    id: "theme.colors.accent.dark",
    kind: "paletteColorToken",
    defaultValue: "blue_bright",
    ui: {
      label: "Accent dark",
      group: GROUPS.appColors,
      pair: { id: "theme.colors.accent", label: "Accent", role: "dark" },
    },
  },
  iconPrimaryLight: {
    id: "theme.icons.primary.light",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Primary icon light",
      group: GROUPS.iconColors,
      pair: { id: "theme.icons.primary", label: "Primary", role: "light" },
    },
  },
  iconPrimaryDark: {
    id: "theme.icons.primary.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Primary icon dark",
      group: GROUPS.iconColors,
      pair: { id: "theme.icons.primary", label: "Primary", role: "dark" },
    },
  },
  iconSecondaryLight: {
    id: "theme.icons.secondary.light",
    kind: "paletteColorToken",
    defaultValue: "gray_040",
    ui: {
      label: "Secondary icon light",
      group: GROUPS.iconColors,
      pair: { id: "theme.icons.secondary", label: "Secondary", role: "light" },
    },
  },
  iconSecondaryDark: {
    id: "theme.icons.secondary.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_060",
    ui: {
      label: "Secondary icon dark",
      group: GROUPS.iconColors,
      pair: { id: "theme.icons.secondary", label: "Secondary", role: "dark" },
    },
  },
  iconAccentLight: {
    id: "theme.icons.accent.light",
    kind: "paletteColorToken",
    defaultValue: "blue",
    ui: {
      label: "Accent icon light",
      group: GROUPS.iconColors,
      pair: { id: "theme.icons.accent", label: "Accent", role: "light" },
    },
  },
  iconAccentDark: {
    id: "theme.icons.accent.dark",
    kind: "paletteColorToken",
    defaultValue: "blue_bright",
    ui: {
      label: "Accent icon dark",
      group: GROUPS.iconColors,
      pair: { id: "theme.icons.accent", label: "Accent", role: "dark" },
    },
  },
  borderPrimaryLight: {
    id: "theme.borders.primary.light",
    kind: "paletteColorToken",
    defaultValue: "gray_080",
    ui: {
      label: "Primary border light",
      group: GROUPS.borderColors,
      pair: { id: "theme.borders.primary", label: "Primary", role: "light" },
    },
  },
  borderPrimaryDark: {
    id: "theme.borders.primary.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_030",
    ui: {
      label: "Primary border dark",
      group: GROUPS.borderColors,
      pair: { id: "theme.borders.primary", label: "Primary", role: "dark" },
    },
  },
  borderSecondaryLight: {
    id: "theme.borders.secondary.light",
    kind: "paletteColorToken",
    defaultValue: "gray_070",
    ui: {
      label: "Secondary border light",
      group: GROUPS.borderColors,
      pair: { id: "theme.borders.secondary", label: "Secondary", role: "light" },
    },
  },
  borderSecondaryDark: {
    id: "theme.borders.secondary.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_040",
    ui: {
      label: "Secondary border dark",
      group: GROUPS.borderColors,
      pair: { id: "theme.borders.secondary", label: "Secondary", role: "dark" },
    },
  },
  borderAlternateLight: {
    id: "theme.borders.alternate.light",
    kind: "paletteColorToken",
    defaultValue: "gray_090",
    ui: {
      label: "Alternate border light",
      group: GROUPS.borderColors,
      pair: { id: "theme.borders.alternate", label: "Alternate", role: "light" },
    },
  },
  borderAlternateDark: {
    id: "theme.borders.alternate.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_020",
    ui: {
      label: "Alternate border dark",
      group: GROUPS.borderColors,
      pair: { id: "theme.borders.alternate", label: "Alternate", role: "dark" },
    },
  },
  cursorColorLight: {
    id: "theme.cursor.color.light",
    kind: "paletteColorToken",
    defaultValue: "blue",
    ui: {
      label: "Cursor color light",
      group: GROUPS.cursor,
      pair: { id: "theme.cursor.color", label: "Color", role: "light" },
    },
  },
  cursorColorDark: {
    id: "theme.cursor.color.dark",
    kind: "paletteColorToken",
    defaultValue: "blue_bright",
    ui: {
      label: "Cursor color dark",
      group: GROUPS.cursor,
      pair: { id: "theme.cursor.color", label: "Color", role: "dark" },
    },
  },
  statusBarType: {
    id: "theme.statusBar.type",
    kind: "enum",
    defaultValue: "ios-default",
    ui: {
      label: "Type",
      group: GROUPS.statusBar,
      options: ["dummy-status-bar", "ios-default", "android-default"],
    },
  },
  statusBarIconScale: {
    id: "theme.statusBar.iconScale",
    kind: "decimal",
    defaultValue: 1,
    ui: {
      label: "Icon scale",
      group: GROUPS.statusBar,
      min: 0.1,
      step: 0.05,
    },
  },
  statusBarForegroundLight: {
    id: "theme.statusBar.foreground.light",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Foreground light",
      group: GROUPS.statusBar,
      pair: { id: "theme.statusBar.foreground", label: "Foreground", role: "light" },
    },
  },
  statusBarForegroundDark: {
    id: "theme.statusBar.foreground.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Foreground dark",
      group: GROUPS.statusBar,
      pair: { id: "theme.statusBar.foreground", label: "Foreground", role: "dark" },
    },
  },
  statusBarBackgroundLight: {
    id: "theme.statusBar.background.light",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Background light",
      group: GROUPS.statusBar,
      pair: { id: "theme.statusBar.background", label: "Background", role: "light" },
    },
  },
  statusBarBackgroundDark: {
    id: "theme.statusBar.background.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Background dark",
      group: GROUPS.statusBar,
      pair: { id: "theme.statusBar.background", label: "Background", role: "dark" },
    },
  },
  statusBarBackgroundAlphaLight: {
    id: "theme.statusBar.backgroundAlpha.light",
    kind: "alpha",
    defaultValue: 0,
    ui: {
      label: "Background alpha light",
      group: GROUPS.statusBar,
      step: 0.01,
      pair: { id: "theme.statusBar.backgroundAlpha", label: "Background alpha", role: "light" },
    },
  },
  statusBarBackgroundAlphaDark: {
    id: "theme.statusBar.backgroundAlpha.dark",
    kind: "alpha",
    defaultValue: 0,
    ui: {
      label: "Background alpha dark",
      group: GROUPS.statusBar,
      step: 0.01,
      pair: { id: "theme.statusBar.backgroundAlpha", label: "Background alpha", role: "dark" },
    },
  },
  navigationBarType: {
    id: "theme.navigationBar.type",
    kind: "enum",
    defaultValue: "ios-home-indicator",
    ui: {
      label: "Type",
      group: GROUPS.navigationBar,
      options: [
        "dummy-navigation-bar",
        "ios-home-indicator",
        "android-gesture",
        "android-3-button",
      ],
    },
  },
  navigationBarIconScale: {
    id: "theme.navigationBar.iconScale",
    kind: "decimal",
    defaultValue: 1,
    ui: {
      label: "Icon scale",
      group: GROUPS.navigationBar,
      min: 0.1,
      step: 0.05,
    },
  },
  navigationBarForegroundLight: {
    id: "theme.navigationBar.foreground.light",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Foreground light",
      group: GROUPS.navigationBar,
      pair: { id: "theme.navigationBar.foreground", label: "Foreground", role: "light" },
    },
  },
  navigationBarForegroundDark: {
    id: "theme.navigationBar.foreground.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Foreground dark",
      group: GROUPS.navigationBar,
      pair: { id: "theme.navigationBar.foreground", label: "Foreground", role: "dark" },
    },
  },
  navigationBarBackgroundLight: {
    id: "theme.navigationBar.background.light",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Background light",
      group: GROUPS.navigationBar,
      pair: { id: "theme.navigationBar.background", label: "Background", role: "light" },
    },
  },
  navigationBarBackgroundDark: {
    id: "theme.navigationBar.background.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Background dark",
      group: GROUPS.navigationBar,
      pair: { id: "theme.navigationBar.background", label: "Background", role: "dark" },
    },
  },
  navigationBarBackgroundAlphaLight: {
    id: "theme.navigationBar.backgroundAlpha.light",
    kind: "alpha",
    defaultValue: 0,
    ui: {
      label: "Background alpha light",
      group: GROUPS.navigationBar,
      step: 0.01,
      pair: { id: "theme.navigationBar.backgroundAlpha", label: "Background alpha", role: "light" },
    },
  },
  navigationBarBackgroundAlphaDark: {
    id: "theme.navigationBar.backgroundAlpha.dark",
    kind: "alpha",
    defaultValue: 0,
    ui: {
      label: "Background alpha dark",
      group: GROUPS.navigationBar,
      step: 0.01,
      pair: { id: "theme.navigationBar.backgroundAlpha", label: "Background alpha", role: "dark" },
    },
  },
  keyboardBackgroundLight: {
    id: "theme.keyboard.background.light",
    kind: "paletteColorToken",
    defaultValue: "gray_080",
    ui: {
      label: "Background light",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.background", label: "Background", role: "light" },
    },
  },
  keyboardBackgroundDark: {
    id: "theme.keyboard.background.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_020",
    ui: {
      label: "Background dark",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.background", label: "Background", role: "dark" },
    },
  },
  keyboardKeyBackgroundLight: {
    id: "theme.keyboard.keyBackground.light",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Key light",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.keyBackground", label: "Key", role: "light" },
    },
  },
  keyboardKeyBackgroundDark: {
    id: "theme.keyboard.keyBackground.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_040",
    ui: {
      label: "Key dark",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.keyBackground", label: "Key", role: "dark" },
    },
  },
  keyboardSpecialKeyBackgroundLight: {
    id: "theme.keyboard.specialKeyBackground.light",
    kind: "paletteColorToken",
    defaultValue: "gray_070",
    ui: {
      label: "Special key light",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.specialKeyBackground", label: "Special key", role: "light" },
    },
  },
  keyboardSpecialKeyBackgroundDark: {
    id: "theme.keyboard.specialKeyBackground.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_020",
    ui: {
      label: "Special key dark",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.specialKeyBackground", label: "Special key", role: "dark" },
    },
  },
  keyboardPressedKeyBackgroundLight: {
    id: "theme.keyboard.pressedKeyBackground.light",
    kind: "paletteColorToken",
    defaultValue: "gray_060",
    ui: {
      label: "Pressed key light",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.pressedKeyBackground", label: "Pressed key", role: "light" },
    },
  },
  keyboardPressedKeyBackgroundDark: {
    id: "theme.keyboard.pressedKeyBackground.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_060",
    ui: {
      label: "Pressed key dark",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.pressedKeyBackground", label: "Pressed key", role: "dark" },
    },
  },
  keyboardPopoverBackgroundLight: {
    id: "theme.keyboard.popoverBackground.light",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Popover light",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.popoverBackground", label: "Popover", role: "light" },
    },
  },
  keyboardPopoverBackgroundDark: {
    id: "theme.keyboard.popoverBackground.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_040",
    ui: {
      label: "Popover dark",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.popoverBackground", label: "Popover", role: "dark" },
    },
  },
  keyboardTextLight: {
    id: "theme.keyboard.text.light",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Text light",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.text", label: "Text", role: "light" },
    },
  },
  keyboardTextDark: {
    id: "theme.keyboard.text.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Text dark",
      group: GROUPS.keyboard,
      pair: { id: "theme.keyboard.text", label: "Text", role: "dark" },
    },
  },
  notificationBackgroundLight: {
    id: "theme.notifications.background.light",
    kind: "paletteColorToken",
    defaultValue: "gray_090",
    ui: {
      label: "Background light",
      group: GROUPS.notifications,
      pair: { id: "theme.notifications.background", label: "Background", role: "light" },
    },
  },
  notificationBackgroundDark: {
    id: "theme.notifications.background.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_020",
    ui: {
      label: "Background dark",
      group: GROUPS.notifications,
      pair: { id: "theme.notifications.background", label: "Background", role: "dark" },
    },
  },
  notificationBackgroundAlphaLight: {
    id: "theme.notifications.backgroundAlpha.light",
    kind: "alpha",
    defaultValue: 0.92,
    ui: {
      label: "Background alpha light",
      group: GROUPS.notifications,
      step: 0.01,
      pair: { id: "theme.notifications.backgroundAlpha", label: "Background alpha", role: "light" },
    },
  },
  notificationBackgroundAlphaDark: {
    id: "theme.notifications.backgroundAlpha.dark",
    kind: "alpha",
    defaultValue: 0.92,
    ui: {
      label: "Background alpha dark",
      group: GROUPS.notifications,
      step: 0.01,
      pair: { id: "theme.notifications.backgroundAlpha", label: "Background alpha", role: "dark" },
    },
  },
  notificationTitleColorLight: {
    id: "theme.notifications.titleColor.light",
    kind: "paletteColorToken",
    defaultValue: "gray_000",
    ui: {
      label: "Title light",
      group: GROUPS.notifications,
      pair: { id: "theme.notifications.titleColor", label: "Title", role: "light" },
    },
  },
  notificationTitleColorDark: {
    id: "theme.notifications.titleColor.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_100",
    ui: {
      label: "Title dark",
      group: GROUPS.notifications,
      pair: { id: "theme.notifications.titleColor", label: "Title", role: "dark" },
    },
  },
  notificationBodyColorLight: {
    id: "theme.notifications.bodyColor.light",
    kind: "paletteColorToken",
    defaultValue: "gray_020",
    ui: {
      label: "Body light",
      group: GROUPS.notifications,
      pair: { id: "theme.notifications.bodyColor", label: "Body", role: "light" },
    },
  },
  notificationBodyColorDark: {
    id: "theme.notifications.bodyColor.dark",
    kind: "paletteColorToken",
    defaultValue: "gray_080",
    ui: {
      label: "Body dark",
      group: GROUPS.notifications,
      pair: { id: "theme.notifications.bodyColor", label: "Body", role: "dark" },
    },
  },
});

export const THEME_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: THEME_FIELDS.id },
  { outputPath: ["production_id"], field: THEME_FIELDS.productionId },
  { outputPath: ["name"], field: THEME_FIELDS.name },
  { outputPath: ["family"], field: THEME_FIELDS.family },
  { outputPath: ["icon_theme_id"], field: THEME_FIELDS.iconThemeId },
  { outputPath: ["status_bar_id"], field: THEME_FIELDS.statusBarId },
  { outputPath: ["navigation_bar_id"], field: THEME_FIELDS.navigationBarId },
  { outputPath: ["version"], field: THEME_FIELDS.version },
  { outputPath: ["tokens_json"], field: THEME_FIELDS.tokens },
] satisfies readonly JsonFieldBinding[];

export const THEME_TOKEN_BINDINGS = [
  { outputPath: ["defaultMode"], field: THEME_FIELDS.defaultMode },
  { outputPath: ["statusBar", "type"], field: THEME_FIELDS.statusBarType },
  {
    outputPath: ["statusBar", "iconScale"],
    field: THEME_FIELDS.statusBarIconScale,
  },
  {
    outputPath: ["navigationBar", "type"],
    field: THEME_FIELDS.navigationBarType,
  },
  {
    outputPath: ["navigationBar", "iconScale"],
    field: THEME_FIELDS.navigationBarIconScale,
  },
  {
    outputPath: ["modes", "light", "colors", "background"],
    field: THEME_FIELDS.colorBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "background"],
    field: THEME_FIELDS.colorBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "colors", "textPrimary"],
    field: THEME_FIELDS.colorTextPrimaryLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "textPrimary"],
    field: THEME_FIELDS.colorTextPrimaryDark,
  },
  {
    outputPath: ["modes", "light", "colors", "textSecondary"],
    field: THEME_FIELDS.colorTextSecondaryLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "textSecondary"],
    field: THEME_FIELDS.colorTextSecondaryDark,
  },
  {
    outputPath: ["modes", "light", "colors", "accent"],
    field: THEME_FIELDS.colorAccentLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "accent"],
    field: THEME_FIELDS.colorAccentDark,
  },
  {
    outputPath: ["modes", "light", "colors", "icons.primary"],
    field: THEME_FIELDS.iconPrimaryLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "icons.primary"],
    field: THEME_FIELDS.iconPrimaryDark,
  },
  {
    outputPath: ["modes", "light", "colors", "icons.secondary"],
    field: THEME_FIELDS.iconSecondaryLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "icons.secondary"],
    field: THEME_FIELDS.iconSecondaryDark,
  },
  {
    outputPath: ["modes", "light", "colors", "icons.accent"],
    field: THEME_FIELDS.iconAccentLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "icons.accent"],
    field: THEME_FIELDS.iconAccentDark,
  },
  {
    outputPath: ["modes", "light", "colors", "borders.primary"],
    field: THEME_FIELDS.borderPrimaryLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "borders.primary"],
    field: THEME_FIELDS.borderPrimaryDark,
  },
  {
    outputPath: ["modes", "light", "colors", "borders.secondary"],
    field: THEME_FIELDS.borderSecondaryLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "borders.secondary"],
    field: THEME_FIELDS.borderSecondaryDark,
  },
  {
    outputPath: ["modes", "light", "colors", "borders.alternate"],
    field: THEME_FIELDS.borderAlternateLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "borders.alternate"],
    field: THEME_FIELDS.borderAlternateDark,
  },
  {
    outputPath: ["modes", "light", "colors", "theme.cursor.color"],
    field: THEME_FIELDS.cursorColorLight,
  },
  {
    outputPath: ["modes", "dark", "colors", "theme.cursor.color"],
    field: THEME_FIELDS.cursorColorDark,
  },
  {
    outputPath: ["modes", "light", "statusBar", "foreground"],
    field: THEME_FIELDS.statusBarForegroundLight,
  },
  {
    outputPath: ["modes", "dark", "statusBar", "foreground"],
    field: THEME_FIELDS.statusBarForegroundDark,
  },
  {
    outputPath: ["modes", "light", "statusBar", "background", "color"],
    field: THEME_FIELDS.statusBarBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "statusBar", "background", "color"],
    field: THEME_FIELDS.statusBarBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "statusBar", "background", "alpha"],
    field: THEME_FIELDS.statusBarBackgroundAlphaLight,
  },
  {
    outputPath: ["modes", "dark", "statusBar", "background", "alpha"],
    field: THEME_FIELDS.statusBarBackgroundAlphaDark,
  },
  {
    outputPath: ["modes", "light", "navigationBar", "foreground"],
    field: THEME_FIELDS.navigationBarForegroundLight,
  },
  {
    outputPath: ["modes", "dark", "navigationBar", "foreground"],
    field: THEME_FIELDS.navigationBarForegroundDark,
  },
  {
    outputPath: ["modes", "light", "navigationBar", "background", "color"],
    field: THEME_FIELDS.navigationBarBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "navigationBar", "background", "color"],
    field: THEME_FIELDS.navigationBarBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "navigationBar", "background", "alpha"],
    field: THEME_FIELDS.navigationBarBackgroundAlphaLight,
  },
  {
    outputPath: ["modes", "dark", "navigationBar", "background", "alpha"],
    field: THEME_FIELDS.navigationBarBackgroundAlphaDark,
  },
  {
    outputPath: ["modes", "light", "keyboard", "background"],
    field: THEME_FIELDS.keyboardBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "keyboard", "background"],
    field: THEME_FIELDS.keyboardBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "keyboard", "keyBackground"],
    field: THEME_FIELDS.keyboardKeyBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "keyboard", "keyBackground"],
    field: THEME_FIELDS.keyboardKeyBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "keyboard", "specialKeyBackground"],
    field: THEME_FIELDS.keyboardSpecialKeyBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "keyboard", "specialKeyBackground"],
    field: THEME_FIELDS.keyboardSpecialKeyBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "keyboard", "pressedKeyBackground"],
    field: THEME_FIELDS.keyboardPressedKeyBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "keyboard", "pressedKeyBackground"],
    field: THEME_FIELDS.keyboardPressedKeyBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "keyboard", "popoverBackground"],
    field: THEME_FIELDS.keyboardPopoverBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "keyboard", "popoverBackground"],
    field: THEME_FIELDS.keyboardPopoverBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "keyboard", "text"],
    field: THEME_FIELDS.keyboardTextLight,
  },
  {
    outputPath: ["modes", "dark", "keyboard", "text"],
    field: THEME_FIELDS.keyboardTextDark,
  },
  {
    outputPath: ["modes", "light", "notifications", "background", "color"],
    field: THEME_FIELDS.notificationBackgroundLight,
  },
  {
    outputPath: ["modes", "dark", "notifications", "background", "color"],
    field: THEME_FIELDS.notificationBackgroundDark,
  },
  {
    outputPath: ["modes", "light", "notifications", "background", "alpha"],
    field: THEME_FIELDS.notificationBackgroundAlphaLight,
  },
  {
    outputPath: ["modes", "dark", "notifications", "background", "alpha"],
    field: THEME_FIELDS.notificationBackgroundAlphaDark,
  },
  {
    outputPath: ["modes", "light", "notifications", "titleColor"],
    field: THEME_FIELDS.notificationTitleColorLight,
  },
  {
    outputPath: ["modes", "dark", "notifications", "titleColor"],
    field: THEME_FIELDS.notificationTitleColorDark,
  },
  {
    outputPath: ["modes", "light", "notifications", "bodyColor"],
    field: THEME_FIELDS.notificationBodyColorLight,
  },
  {
    outputPath: ["modes", "dark", "notifications", "bodyColor"],
    field: THEME_FIELDS.notificationBodyColorDark,
  },
  {
    outputPath: ["neutralTint", "hueDeg"],
    field: THEME_FIELDS.neutralTintHueDeg,
  },
  {
    outputPath: ["neutralTint", "saturation"],
    field: THEME_FIELDS.neutralTintSaturation,
  },
  { outputPath: ["cursor", "width"], field: THEME_FIELDS.cursorWidth },
  {
    outputPath: ["cursor", "blinkFrames"],
    field: THEME_FIELDS.cursorBlinkFrames,
  },
  {
    outputPath: ["surfaceRelief", "default", "angleDeg"],
    field: THEME_FIELDS.surfaceReliefAngleDeg,
  },
  {
    outputPath: ["surfaceRelief", "default", "extension"],
    field: THEME_FIELDS.surfaceReliefExtension,
  },
  {
    outputPath: ["surfaceRelief", "default", "spread"],
    field: THEME_FIELDS.surfaceReliefSpread,
  },
  {
    outputPath: ["surfaceRelief", "default", "upperIntensity"],
    field: THEME_FIELDS.surfaceReliefUpperIntensity,
  },
  {
    outputPath: ["surfaceRelief", "default", "lowerIntensity"],
    field: THEME_FIELDS.surfaceReliefLowerIntensity,
  },
  {
    outputPath: ["fonts", "family"],
    field: THEME_FIELDS.typographyFamily,
  },
  {
    outputPath: ["fonts", "emojiFamily"],
    field: THEME_FIELDS.typographyEmojiFamily,
  },
  {
    outputPath: ["fonts", "bodySize"],
    field: THEME_FIELDS.typographyBodySize,
  },
  {
    outputPath: ["fonts", "bodyLineHeight"],
    field: THEME_FIELDS.typographyBodyLineHeight,
  },
  {
    outputPath: ["fonts", "captionSize"],
    field: THEME_FIELDS.typographyCaptionSize,
  },
  {
    outputPath: ["fonts", "fontWeight"],
    field: THEME_FIELDS.typographyFontWeight,
  },
  {
    outputPath: ["fonts", "fontStyle"],
    field: THEME_FIELDS.typographyFontStyle,
  },
] satisfies readonly JsonFieldBinding[];
