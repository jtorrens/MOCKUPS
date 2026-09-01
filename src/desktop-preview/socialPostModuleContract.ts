export interface SocialPostComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export type SocialPostRowSlotKind = "none" | "avatar" | "icon" | "label";
export type SocialPostVerticalAlignment = "top" | "center" | "bottom";

export interface SocialPostRowSlot {
  index: number;
  kind: SocialPostRowSlotKind;
  componentType?: "avatar" | "button" | "label";
  componentSlot?: SocialPostComponentSlot;
  inputs: Record<string, unknown>;
}

export interface SocialPostRow {
  id: "row1" | "row2";
  padding: string;
  verticalAlignment: SocialPostVerticalAlignment;
  showSeparator: boolean;
  slots: SocialPostRowSlot[];
}

export interface SocialPostMessageContract {
  bubbleSlot: SocialPostComponentSlot;
  textInputBarSlot: SocialPostComponentSlot;
  keyboardSlot: SocialPostComponentSlot;
  padding: string;
  bubbleInputs: Record<string, unknown>;
  text: string;
  visibleText: string;
  writeOnDurationFrames: number;
  writeOnFrame: number;
  writeOnTrigger: boolean;
  currentCharacter: number;
  textInputVisible: boolean;
  keyboardVisible: boolean;
  bubbleVisible: boolean;
  bubbleWriteOnTrigger: boolean;
  showSeparator: boolean;
}

export interface SocialPostModuleContract {
  id: "module.core.socialPost";
  useAppWallpaper: boolean;
  showHeader: boolean;
  headerHeight: number;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  headerSurfaceSlot: SocialPostComponentSlot;
  rowGapToken: string;
  rows: [SocialPostRow, SocialPostRow];
  showMedia: boolean;
  mediaSlot: SocialPostComponentSlot;
  mediaPadding: string;
  mediaInputs: Record<string, unknown>;
  mediaSource: string;
  mediaHeight: number;
  mediaScale: number;
  mediaOffset: string;
  showMediaSeparator: boolean;
  showGallery: boolean;
  gallerySlot: SocialPostComponentSlot;
  galleryMode: "carousel" | "gallery";
  galleryDirectory: string;
  gallerySelectedIndex: number;
  galleryScrollRow: number;
  messageMinHeight: number;
  message: SocialPostMessageContract;
  footerHeight: number;
  footerSurfaceSlot: SocialPostComponentSlot;
  footerRowGapToken: string;
  footerRows: [SocialPostRow, SocialPostRow];
}
